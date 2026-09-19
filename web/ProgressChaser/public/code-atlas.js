'use strict';

// Cytoscape.js Canvas 渲染 + ELK 布局的代码关系画布。
// 数据仍来自 generated/manual/layout 三份 JSON；只改变渲染与交互实现。
const $ = (selector) => document.querySelector(selector);
const $$ = (selector) => Array.from(document.querySelectorAll(selector));
const clone = (value) => JSON.parse(JSON.stringify(value));

const KIND_COLORS = { assembly: '#5b8cff', class: '#82d9b0', call: '#c48cff', event: '#ff7c9b', data: '#5ed2e6' };
const KIND_LABELS = { assembly: '程序集依赖', class: '类引用', call: '方法调用', event: '事件', data: '数据 / 状态' };
const ALL_KINDS = Object.keys(KIND_COLORS);

const state = {
  bundle: null,
  view: 'assembly',
  filters: new Set(ALL_KINDS),
  external: true,
  history: [],
  cy: null,
  graph: null,
  selected: null,
  saveTimer: null,
  saving: false,
  dirty: false,
  blocked: false,
  revision: 0,
  lod: '',
  gestureOpen: false,
  suppress: false,
  request: 0,
};

const status = (text) => { $('#status').textContent = text; };

// —— 滚轮缩放力度（wheelSensitivity）——
// Cytoscape 只在初始化时读取该值，但会复制到 renderer，滚轮事件里读的是 renderer.wheelSensitivity，
// 因此运行时改 renderer 即可立即生效，无需重建实例。
const WHEEL_KEY = 'atlas.wheelSensitivity';
const WHEEL_DEFAULT = 0.18;
const WHEEL_MIN = 0.05;
const WHEEL_MAX = 1;

function readWheelSensitivity() {
  try {
    const stored = Number(localStorage.getItem(WHEEL_KEY));
    if (Number.isFinite(stored) && stored >= WHEEL_MIN && stored <= WHEEL_MAX) return stored;
  } catch { /* localStorage 不可用时用默认值 */ }
  return WHEEL_DEFAULT;
}

function applyWheelSensitivity(value) {
  const renderer = state.cy && typeof state.cy.renderer === 'function' ? state.cy.renderer() : null;
  if (renderer) renderer.wheelSensitivity = value;
  const label = $('#wheelValue');
  if (label) label.textContent = value.toFixed(2);
  const slider = $('#wheelSensitivity');
  if (slider && Number(slider.value) !== value) slider.value = String(value);
}

function setWheelSensitivity(value) {
  const clamped = Math.min(WHEEL_MAX, Math.max(WHEEL_MIN, value));
  try { localStorage.setItem(WHEEL_KEY, String(clamped)); } catch { /* 忽略持久化失败 */ }
  applyWheelSensitivity(clamped);
}

async function api(url, options = {}) {
  const response = await fetch(url, options);
  const data = await response.json();
  if (!response.ok) throw new Error(data.message || `HTTP ${response.status}`);
  return data;
}

function view() {
  const views = state.bundle.layout.views;
  views[state.view] ||= { zoom: 1, pan: { x: 0, y: 0 }, nodes: {} };
  return views[state.view];
}

function checkpoint() {
  state.history.push({ view: state.view, value: clone(view()) });
  if (state.history.length > 60) state.history.shift();
  $('#undoButton').disabled = false;
}

function changed() {
  state.dirty = true;
  status('布局待保存…');
  clearTimeout(state.saveTimer);
  state.saveTimer = setTimeout(() => { state.saveTimer = null; flush(); }, 700);
}

async function flush() {
  if (state.saving || !state.dirty || state.blocked) return;
  state.saving = true;
  try {
    while (state.dirty) {
      state.dirty = false;
      const saved = await api('/api/code-atlas/layout', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ revision: state.revision, token: state.bundle.layoutToken, layout: state.bundle.layout }),
      });
      state.revision = saved.layout.revision;
      state.bundle.layoutToken = saved.token;
    }
    status('布局已保存');
  } catch (error) {
    state.dirty = true;
    state.blocked = true;
    status('保存失败：' + error.message + '；可导出布局保留改动');
  } finally {
    state.saving = false;
  }
}

// —— Cytoscape 样式 ——
const CY_STYLE = [
  {
    selector: 'node',
    style: {
      shape: 'round-rectangle',
      width: 190,
      height: 46,
      'background-color': '#161c26',
      'border-width': 1.6,
      'border-color': (ele) => KIND_COLORS[ele.data('kind')] || '#5a6472',
      label: 'data(label)',
      color: '#e6edf6',
      'font-size': 12,
      'text-valign': 'center',
      'text-halign': 'center',
      'text-max-width': 168,
      'text-wrap': 'ellipsis',
      'overlay-opacity': 0,
    },
  },
  { selector: 'node[external]', style: { 'border-style': 'dashed', 'border-color': '#f0b85b' } },
  { selector: 'node[orphan]', style: { 'border-style': 'dotted', 'border-color': '#ff7272', 'background-color': '#241a1a' } },
  { selector: 'node:selected', style: { 'border-width': 3, 'border-color': '#ffcf5b', 'background-color': '#1d2733' } },
  {
    selector: 'edge',
    style: {
      width: 1.4,
      'curve-style': 'bezier',
      'line-color': (ele) => KIND_COLORS[ele.data('primary')] || '#5a6472',
      'target-arrow-color': (ele) => KIND_COLORS[ele.data('primary')] || '#5a6472',
      'target-arrow-shape': 'triangle',
      'arrow-scale': 0.9,
      opacity: 0.55,
      label: 'data(label)',
      'font-size': 9,
      color: '#9fb0c3',
      'text-background-color': '#0d1117',
      'text-background-opacity': 0.85,
      'text-background-padding': 2,
      'text-rotation': 'autorotate',
      'overlay-opacity': 0,
    },
  },
  { selector: 'edge:selected', style: { width: 3, opacity: 1, 'line-color': '#ffcf5b', 'target-arrow-color': '#ffcf5b' } },
  { selector: '.faded', style: { opacity: 0.16 } },
  // LOD：只改表现，不改数据与选择。
  { selector: 'node.lod-far', style: { label: '', width: 70, height: 22, 'font-size': 0 } },
  { selector: 'edge.lod-far', style: { label: '', opacity: 0.3 } },
  { selector: 'edge.lod-mid', style: { label: '' } },
];

function lodClass(level) {
  return 'lod-' + level;
}

function buildCy(elements) {
  if (state.cy) { state.cy.destroy(); state.cy = null; }
  const cy = cytoscape({
    container: $('#cy'),
    elements,
    style: CY_STYLE,
    layout: { name: 'preset', fit: false, padding: 40 },
    wheelSensitivity: readWheelSensitivity(),
    minZoom: 0.1,
    maxZoom: 4,
    boxSelectionEnabled: false,
  });
  state.cy = cy;
  applyWheelSensitivity(readWheelSensitivity());
  bindCy(cy);
  return cy;
}

function bindCy(cy) {
  cy.on('tap', 'node', (evt) => { if (!state.suppress) activate(evt.target); });
  cy.on('tap', 'edge', (evt) => { if (!state.suppress) showEdge(evt.target); });
  cy.on('tap', (evt) => { if (evt.target === cy) clearSelection(); });

  cy.on('grab', 'node', () => { if (!state.gestureOpen) { state.gestureOpen = true; checkpoint(); } });
  cy.on('dragfree', 'node', (evt) => {
    state.gestureOpen = false;
    const node = evt.target;
    const v = view();
    v.nodes[node.id()] = { ...(v.nodes[node.id()] || {}), x: node.position('x'), y: node.position('y') };
    changed();
  });

  cy.on('panstart zoomstart', () => { if (!state.gestureOpen) { state.gestureOpen = true; checkpoint(); } });
  cy.on('panend zoomend', () => { state.gestureOpen = false; });
  cy.on('zoom', () => { updateZoomReadout(); applyLod(); });
  cy.on('pan zoom', () => {
    const v = view();
    v.pan = { ...cy.pan() };
    v.zoom = cy.zoom();
    changed();
  });
}

function currentPosition(id) {
  return view().nodes[id];
}

function renderGraph() {
  if (!state.bundle) return;
  const graph = AtlasModel.graph(state.bundle, state.view, state.filters, state.external);
  state.graph = graph;

  const v = view();
  const fallback = AtlasView.gridFallback(graph);
  for (const node of graph.nodes) {
    const existing = v.nodes[node.id];
    if (!existing || !Number.isFinite(existing.x) || !Number.isFinite(existing.y)) {
      v.nodes[node.id] = { ...(existing || {}), ...(fallback[node.id] || { x: 0, y: 0 }) };
    }
  }

  const elements = AtlasView.toElements(graph, currentPosition);
  const cy = buildCy(elements);
  if (Number.isFinite(v.zoom)) cy.zoom(v.zoom);
  if (v.pan) cy.pan(v.pan);
  applyLod();
  updateZoomReadout();
  updateCrumb();
  applySelection();
}

function updateCrumb() {
  const crumb = $('#crumb');
  crumb.replaceChildren();
  const back = document.createElement('button');
  back.className = 'secondary';
  back.textContent = '全部程序集';
  back.onclick = () => switchView('assembly');
  crumb.append(back);
  if (state.view !== 'assembly') {
    const span = document.createElement('span');
    span.textContent = ' / ' + state.view.slice(6);
    crumb.append(span);
  }
  const info = document.createElement('span');
  info.className = 'muted';
  const nodes = state.graph ? state.graph.nodes.length : 0;
  const edges = state.graph ? state.graph.edges.length : 0;
  info.textContent = `　${nodes} 节点 · ${edges} 组关系 · 箭头：引用者 → 被引用者；事件：发布者 → 事件 → 订阅者`;
  crumb.append(info);
}

function updateZoomReadout() {
  if (!state.cy) return;
  $('#zoomReadout').textContent = `${Math.round(state.cy.zoom() * 100)}%`;
}

function applyLod() {
  if (!state.cy) return;
  const level = AtlasView.lodOf(state.cy.zoom());
  if (level === state.lod) return;
  state.lod = level;
  const elements = state.cy.elements();
  for (const name of ['far', 'mid', 'near', 'close']) elements.removeClass(lodClass(name));
  elements.addClass(lodClass(level));
}

function clearSelection() {
  state.selected = null;
  applySelection();
  showIntro();
}

function neighborDepth() {
  const select = $('#neighborDepth');
  return select ? Number(select.value) || 0 : 0;
}

function applySelection() {
  const cy = state.cy;
  if (!cy) return;
  cy.batch(() => {
    cy.elements().removeClass('faded');
    cy.elements().unselect();
    if (!state.selected) return;

    if (state.selected.node) {
      const node = cy.getElementById(state.selected.node);
      if (node.empty()) return;
      const depth = neighborDepth();
      if (depth === 0) { node.select(); return; }
      let neighbors = node.closedNeighborhood();
      if (depth >= 2) neighbors = neighbors.closedNeighborhood();
      cy.elements().difference(neighbors).addClass('faded');
      node.select();
    } else if (state.selected.edge) {
      const edge = cy.getElementById(state.selected.edge);
      if (edge.empty()) return;
      const neighbors = edge.connectedNodes().union(edge);
      cy.elements().difference(neighbors).addClass('faded');
      edge.select();
    }
  });
}

// 搜索定位：按名称 / ID / 程序集匹配，选中并居中。
function searchAndFocus(query) {
  if (!state.cy || !state.graph) return false;
  const text = String(query || '').trim().toLowerCase();
  if (!text) return false;
  const match = state.graph.nodes.find((n) =>
    n.id.toLowerCase().includes(text) || (n.name || '').toLowerCase().includes(text) || (n.assembly || '').toLowerCase().includes(text));
  if (!match) { status('未找到匹配节点：' + query); return false; }
  state.selected = { node: match.id };
  applySelection();
  state.request += 1;
  const element = state.cy.getElementById(match.id);
  if (!element.empty()) {
    state.cy.animate({ center: { eles: element }, zoom: Math.max(0.7, state.cy.zoom()) }, { duration: 220 });
    activate(element);
  }
  return true;
}

function switchView(key, options = {}) {
  state.view = key;
  state.selected = null;
  state.request += 1;
  state.lod = '';
  const fresh = !state.bundle.layout.views[key] || !Object.keys(state.bundle.layout.views[key].nodes || {}).length;
  renderGraph();
  if (fresh && !options.keepViewport) { fit(); changed(); }
  if (!options.keepDetail) showIntro();
}

function detailTitle(title) {
  const detail = $('#detail');
  detail.replaceChildren();
  const h2 = document.createElement('h2');
  h2.textContent = title;
  detail.append(h2);
  return detail;
}

function activate(node) {
  const data = node.data();
  if (state.view === 'assembly' && !data.external && !data.orphan) { switchView('class:' + data.id); return; }
  state.selected = { node: data.id };
  applySelection();
  state.request += 1;

  const detail = detailTitle(data.name);
  const code = document.createElement('code');
  code.textContent = data.id;
  detail.append(code);
  const copy = document.createElement('button');
  copy.className = 'secondary';
  copy.textContent = '复制 ID';
  copy.onclick = () => navigator.clipboard.writeText(data.id).then(() => status('节点 ID 已复制')).catch(() => status('请选中上方 ID 复制'));
  detail.append(copy);

  const info = document.createElement('p');
  info.className = 'muted';
  info.textContent = data.orphan
    ? '代码已失效，保留位置与人工说明。'
    : data.external
      ? '外部引用节点。项目内外部类型可跳转；第三方程序集当前只展示边界。'
      : '类级视图；点击关系查看方法、事件和状态证据。';
  detail.append(info);

  const source = state.graph.nodes.find((n) => n.id === data.id);
  if (source && source.assembly && state.bundle.generated.assemblies.some((a) => a.name === source.assembly && !a.external) && source.assembly !== state.view.slice(6)) {
    const button = document.createElement('button');
    button.textContent = '进入 ' + source.assembly;
    button.onclick = () => switchView('class:' + source.assembly);
    detail.append(button);
  }

  if (source) {
    for (const file of source.files || []) addEvidence(detail, { file: file.path, line: file.line });
    for (const note of state.bundle.manual.notes.filter((n) => n.node === data.id)) {
      const p = document.createElement('p');
      p.textContent = note.text;
      detail.append(p);
    }
    if ((source.methods || []).length) {
      const h3 = document.createElement('h3');
      h3.textContent = '方法声明';
      detail.append(h3);
      for (const method of source.methods) {
        const p = document.createElement('p');
        p.className = 'method-name';
        p.textContent = method.signature;
        detail.append(p);
        addEvidence(detail, method);
      }
    }
    if (source.unresolved) {
      const p = document.createElement('p');
      p.className = 'muted';
      p.textContent = '引用 GUID 未在 Assets/Packages 源码中解析，可能位于 PackageCache 或预编译 DLL。';
      detail.append(p);
    }
  }
}

function showIntro() {
  const detail = detailTitle('阅读方式');
  const lines = [
    '先点击程序集进入类图，再点击类高亮相邻关系，点击连线查看源码证据。外部类型以虚线边框标识。',
    state.bundle?.generated?.diagnostics?.note || '',
    '人工关系只编辑 manual.json；修改 JSON 后点“重新读取 JSON”。重新扫描只重建 generated.json。',
    state.bundle?.generated ? '扫描时间：' + new Date(state.bundle.generated.generatedAt).toLocaleString() : '',
  ].filter(Boolean);
  for (const line of lines) {
    const p = document.createElement('p');
    p.className = 'muted';
    p.textContent = line;
    detail.append(p);
  }
}

function showEdge(edge) {
  const data = edge.data();
  state.selected = { edge: data.id };
  applySelection();
  state.request += 1;

  const detail = detailTitle('关系明细');
  const nodeName = (id) => state.graph?.nodes.find((n) => n.id === id)?.name || id;
  const head = document.createElement('p');
  head.textContent = nodeName(data.source) + ' → ' + nodeName(data.target);
  detail.append(head);

  const record = state.graph.edges.find((e) => e.id === data.id);
  for (const relation of (record?.relations || [])) {
    const section = document.createElement('section');
    section.className = 'relation-detail';
    const tag = document.createElement('span');
    tag.className = 'relation-tag';
    tag.textContent = `${KIND_LABELS[relation.kind]} · ${relation.origin === 'manual' ? '人工说明' : relation.confidence === 'declared' ? '显式声明' : '静态候选'}`;
    section.append(tag);
    const label = document.createElement('p');
    label.textContent = relation.label || relation.kind;
    section.append(label);
    if (relation.method) {
      const method = document.createElement('p');
      method.className = 'muted';
      method.textContent = '所在方法：' + relation.method;
      section.append(method);
    }
    if (relation.note) {
      const note = document.createElement('p');
      note.textContent = relation.note;
      section.append(note);
    }
    addEvidence(section, relation);
    detail.append(section);
  }
}

function addEvidence(container, relation) {
  if (!relation.file) return;
  const button = document.createElement('button');
  button.className = 'source-link';
  button.textContent = relation.file + ':' + (relation.line || 1);
  button.onclick = async () => {
    const token = state.request;
    try {
      const data = await api('/api/code-atlas/source?file=' + encodeURIComponent(relation.file));
      if (token !== state.request) return;
      const lines = data.source.split('\n');
      const start = Math.max(0, (relation.line || 1) - 5);
      const end = Math.min(lines.length, (relation.line || 1) + 8);
      container.querySelector('pre')?.remove();
      const pre = document.createElement('pre');
      pre.className = 'source-code';
      pre.textContent = lines.slice(start, end).map((line, index) => `${start + index + 1} ${line}`).join('\n');
      container.append(pre);
    } catch (error) {
      status(error.message);
    }
  };
  container.append(button);
}

function fit() {
  if (!state.cy || !state.cy.elements().length) return;
  state.cy.fit(state.cy.elements(), 50);
  const v = view();
  v.zoom = state.cy.zoom();
  v.pan = { ...state.cy.pan() };
  updateZoomReadout();
  applyLod();
}

function hasElkExtension() {
  return typeof cytoscape === 'function' && !!cytoscape('layout', 'elk');
}

function syncPositionsFromCy() {
  const v = view();
  for (const node of state.cy.nodes()) {
    v.nodes[node.id()] = { ...(v.nodes[node.id()] || {}), x: node.position('x'), y: node.position('y') };
  }
}

// —— ELK Worker 客户端：支持取消、过期结果丢弃、异常回退 ——
let elkWorker = null;
let elkSeq = 0;
let elkPending = null;
let elkProgressTimer = null;

function getElkWorker() {
  if (!elkWorker) elkWorker = new Worker('./elk-worker.js');
  return elkWorker;
}

function disposeElkWorker() {
  if (elkWorker) { elkWorker.terminate(); elkWorker = null; }
}

function settleElk(kind, payload, requestId) {
  const pending = elkPending;
  if (!pending || (requestId !== undefined && pending.requestId !== requestId)) return;
  elkPending = null;
  clearTimeout(elkProgressTimer);
  const button = $('#cancelLayoutButton');
  if (button) button.hidden = true;
  if (kind === 'error') pending.reject(payload); else pending.resolve(payload);
}

function cancelElkLayout(reason) {
  if (!elkPending) return;
  disposeElkWorker();
  settleElk('error', new Error(reason || '布局已取消'));
}

function requestElkLayout(graph) {
  // 新请求取代旧请求：终止旧 Worker 并让旧 Promise 以取消结束，避免回调误结算。
  if (elkPending) cancelElkLayout('被新的布局请求取代');
  return new Promise((resolve, reject) => {
    if (typeof Worker === 'undefined') { reject(new Error('当前浏览器不支持 Web Worker')); return; }
    const worker = getElkWorker();
    const requestId = ++elkSeq;
    const onMessage = (event) => {
      const data = event.data || {};
      if (data.requestId !== requestId) return; // 过期结果直接丢弃
      worker.removeEventListener('message', onMessage);
      worker.removeEventListener('error', onError);
      if (data.type === 'error') settleElk('error', new Error(data.error || 'ELK 布局失败'), requestId);
      else settleElk('resolve', data.result, requestId);
    };
    const onError = (event) => {
      worker.removeEventListener('message', onMessage);
      worker.removeEventListener('error', onError);
      disposeElkWorker();
      settleElk('error', new Error(event.message || 'ELK Worker 异常'), requestId);
    };
    worker.addEventListener('message', onMessage);
    worker.addEventListener('error', onError);
    elkPending = { requestId, resolve, reject };
    const button = $('#cancelLayoutButton');
    if (button) button.hidden = true;
    elkProgressTimer = setTimeout(() => {
      if (elkPending && elkPending.requestId === requestId && button) button.hidden = false;
    }, 3000);
    worker.postMessage({ type: 'layout', requestId, graph });
  });
}

function applyElkPositions(positions) {
  const v = view();
  for (const [id, position] of Object.entries(positions)) {
    v.nodes[id] = { ...(v.nodes[id] || {}), ...position };
    const node = state.cy.getElementById(id);
    if (node && !node.empty()) node.position(position);
  }
}

function fallbackGridLayout() {
  const fallback = AtlasView.gridFallback(state.graph);
  const v = view();
  for (const [id, position] of Object.entries(fallback)) v.nodes[id] = { ...(v.nodes[id] || {}), ...position };
  for (const node of state.cy.nodes()) {
    const position = v.nodes[node.id()];
    if (position) node.position(position);
  }
}

async function runAutoLayout() {
  if (!state.cy) return;
  checkpoint();
  status('ELK 布局中…');
  try {
    const graph = AtlasView.buildElkGraph(state.graph);
    const result = await requestElkLayout(graph);
    applyElkPositions(AtlasView.elkPositions(result));
    fit();
    changed();
    status('ELK 布局完成');
  } catch (error) {
    if (error.message === '布局已取消') { status('已取消布局，保留上次位置'); return; }
    console.warn('[code-atlas] ELK Worker 失败，回退布局：', error);
    if (hasElkExtension()) {
      await new Promise((resolve) => {
        const layout = state.cy.layout({ name: 'elk', fit: true, padding: 40, elk: AtlasView.ELK_LAYERED_OPTIONS });
        layout.one('layoutstop', resolve);
        layout.run();
      });
      syncPositionsFromCy();
    } else {
      fallbackGridLayout();
    }
    fit();
    changed();
    status('已回退布局：' + error.message);
  }
}

function resetLayout() {
  if (!state.cy) return;
  checkpoint();
  const fallback = AtlasView.gridFallback(state.graph);
  const v = view();
  for (const [id, position] of Object.entries(fallback)) v.nodes[id] = { ...(v.nodes[id] || {}), ...position };
  for (const node of state.cy.nodes()) {
    const position = v.nodes[node.id()];
    if (position) node.position(position);
  }
  v.zoom = 1;
  v.pan = { x: 0, y: 0 };
  fit();
  changed();
}

function undo() {
  const previous = state.history.pop();
  if (!previous) return;
  state.bundle.layout.views[previous.view] = previous.value;
  state.view = previous.view;
  state.selected = null;
  state.lod = '';
  renderGraph();
  changed();
  $('#undoButton').disabled = !state.history.length;
}

async function load(scan = false) {
  clearTimeout(state.saveTimer);
  if (state.dirty || state.saving) {
    await flush();
    if (state.dirty || state.saving) { status('布局仍未保存；请先解决保存错误，或导出后刷新页面'); return; }
  }
  status(scan ? '扫描源码中…' : '读取 JSON 中…');
  const bundle = await api(scan ? '/api/code-atlas/scan' : '/api/code-atlas', scan ? { method: 'POST' } : {});
  state.bundle = bundle;
  state.revision = bundle.layout.revision || 0;
  state.blocked = false;
  state.history = [];
  state.lod = '';
  $('#undoButton').disabled = true;
  const freshView = !Object.keys(bundle.layout.views[state.view]?.nodes || {}).length;
  renderGraph();
  if (freshView) { fit(); changed(); }
  showIntro();
  status(`已读取：${bundle.generated.stats.files} 文件 · ${bundle.generated.stats.classes} 类型`);
}

function bindControls() {
  $('#reloadButton').onclick = () => load().catch((error) => status(error.message));
  $('#scanButton').onclick = () => load(true).catch((error) => status(error.message));
  $('#autoLayoutButton').onclick = () => runAutoLayout();
  $('#cancelLayoutButton').onclick = () => cancelElkLayout('布局已取消');
  const searchInput = $('#searchInput');
  if (searchInput) searchInput.addEventListener('keydown', (event) => {
    if (event.key === 'Enter') { event.preventDefault(); searchAndFocus(event.target.value); }
  });
  const depthSelect = $('#neighborDepth');
  if (depthSelect) depthSelect.onchange = () => applySelection();
  const wheelSlider = $('#wheelSensitivity');
  if (wheelSlider) {
    const initial = readWheelSensitivity();
    wheelSlider.value = String(initial);
    applyWheelSensitivity(initial);
    wheelSlider.oninput = () => setWheelSensitivity(Number(wheelSlider.value));
  }
  $('#resetButton').onclick = () => resetLayout();
  $('#fitButton').onclick = () => { checkpoint(); fit(); changed(); };
  $('#undoButton').onclick = () => undo();
  $('#exportButton').onclick = () => {
    const blob = new Blob([JSON.stringify(state.bundle.layout, null, 2)], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = 'code-atlas.layout.json';
    link.click();
    URL.revokeObjectURL(url);
  };
  $$('[data-kind]').forEach((input) => {
    if (input.checked) {
      if (input.dataset.kind === 'external') state.external = true; else state.filters.add(input.dataset.kind);
    }
    input.onchange = () => {
      if (input.dataset.kind === 'external') state.external = input.checked;
      else if (input.checked) state.filters.add(input.dataset.kind); else state.filters.delete(input.dataset.kind);
      state.selected = null;
      state.lod = '';
      renderGraph();
      showIntro();
    };
  });
  window.addEventListener('beforeunload', (event) => {
    if (state.dirty || state.saving) { event.preventDefault(); event.returnValue = ''; }
  });
  document.addEventListener('visibilitychange', () => { if (document.hidden) flush(); });
  document.addEventListener('keydown', (event) => {
    if ((event.ctrlKey || event.metaKey) && event.key === 'z' && !/INPUT|TEXTAREA/.test(event.target.tagName)) {
      event.preventDefault();
      $('#undoButton').click();
    }
  });
}

bindControls();
load().catch((error) => status('加载失败：' + error.message));
