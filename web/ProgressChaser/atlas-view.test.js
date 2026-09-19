// headless 测试：AtlasModel -> AtlasView.toElements -> Cytoscape（含 ELK 布局）。
// Cytoscape 在 Node 下以 headless 模式运行，不需要 DOM。
const test = require('node:test');
const assert = require('node:assert/strict');
const cytoscape = require('cytoscape');
const model = require('./public/atlas-model');
const view = require('./public/atlas-view');

function fakeBundle() {
  const classes = [
    { id: 'Game::A', name: 'A', assembly: 'Game', kind: 'class', methods: [{ name: 'Tick', signature: 'void Tick()' }] },
    { id: 'Game::B', name: 'B', assembly: 'Game', kind: 'class' },
    { id: 'Other::C', name: 'C', assembly: 'Other', kind: 'class' },
  ];
  return {
    generated: {
      assemblies: [{ id: 'Game', name: 'Game' }, { id: 'Other', name: 'Other', external: true }],
      classes,
      relations: [
        { id: 'r1', source: 'Game::A', target: 'Game::B', kind: 'class', label: '字段引用' },
        { id: 'r2', source: 'Game::A', target: 'Game::C', kind: 'call', label: '调用 C.Run', method: 'Tick' },
        { id: 'r3', source: 'Game::A', target: 'Game::C', kind: 'data', label: '写入 C.Value', access: 'write' },
        { id: 'r4', source: 'Game::B', target: 'Other::C', kind: 'class', label: '类型引用' },
      ],
      orphans: [],
    },
    manual: { relations: [{ id: 'm1', source: 'Game::A', target: 'Game::B', kind: 'data', label: '人工：同步状态' }], notes: [] },
    layout: { views: {} },
  };
}

test('toElements 把图装配结果转成 Cytoscape 元素，并保留聚合计数', () => {
  const graph = model.graph(fakeBundle(), 'class:Game', new Set(['class', 'call', 'data']), true);
  const elements = view.toElements(graph, () => ({ x: 12, y: 34 }));

  assert.equal(elements.nodes.length, graph.nodes.length);
  assert.equal(elements.edges.length, graph.edges.length);
  assert.ok(elements.nodes.every((n) => n.position.x === 12 && n.position.y === 34));

  const ab = elements.edges.find((e) => e.data.source === 'Game::A' && e.data.target === 'Game::B');
  assert.equal(ab.data.count, 2, 'A->B 应聚合 class + 人工 data 两条明细');
  assert.equal(ab.data.primary, 'class', '主类型应优先 class');

  const cy = cytoscape({ headless: true, styleEnabled: false, elements: [...elements.nodes, ...elements.edges] });
  assert.equal(cy.nodes().length, elements.nodes.length);
  assert.equal(cy.edges().length, elements.edges.length);
  const node = cy.getElementById('Game::A');
  assert.equal(node.data('name'), 'A');
  assert.equal(node.data('methodCount'), 1);
  assert.equal(node.closedNeighborhood().nodes().length, 3, 'A 的闭邻域应包含 A/B/C');
  cy.destroy();
});

test('LOD 分级边界与 gridFallback 有限坐标', () => {
  assert.equal(view.lodOf(0.1), 'far');
  assert.equal(view.lodOf(0.29), 'far');
  assert.equal(view.lodOf(0.3), 'mid');
  assert.equal(view.lodOf(0.69), 'mid');
  assert.equal(view.lodOf(0.7), 'near');
  assert.equal(view.lodOf(1.49), 'near');
  assert.equal(view.lodOf(1.5), 'close');

  const graph = model.graph(fakeBundle(), 'class:Game', new Set(['class', 'call']), true);
  const positions = view.gridFallback(graph);
  assert.equal(Object.keys(positions).length, graph.nodes.length);
  assert.ok(Object.values(positions).every((p) => Number.isFinite(p.x) && Number.isFinite(p.y)));
});

test('buildElkGraph / elkPositions 与 ELK 输出互逆', () => {
  const graph = model.graph(fakeBundle(), 'class:Game', new Set(['class', 'call', 'data']), true);
  const input = view.buildElkGraph(graph);
  assert.equal(input.layoutOptions['elk.algorithm'], 'layered');
  assert.equal(input.layoutOptions['elk.edgeRouting'], 'ORTHOGONAL');
  assert.equal(input.children.length, graph.nodes.length);
  assert.ok(input.children.every((c) => c.width === 190 && c.height === 46));
  assert.equal(input.edges.length, graph.edges.length);
  assert.deepEqual(input.edges[0].sources.length, 1);

  const positions = view.elkPositions({ children: [{ id: 'a', x: 1, y: 2 }, { id: 'b' }] });
  assert.deepEqual(positions.a, { x: 1, y: 2 });
  assert.deepEqual(positions.b, { x: 0, y: 0 });
});

test('ELK layered 布局在 headless 下产出有限坐标，并避开未开启的关系', async () => {
  // 浏览器里 cytoscape-elk 检测到全局 cytoscape 会自动注册；Node 下需显式 use。
  const register = require('cytoscape-elk');
  cytoscape.use(register);
  assert.ok(cytoscape('layout', 'elk'), 'cytoscape-elk 应注册 elk 布局');

  const graph = model.graph(fakeBundle(), 'class:Game', new Set(['class', 'call', 'data']), true);
  const elements = view.toElements(graph, null);
  const cy = cytoscape({ headless: true, styleEnabled: false, elements: [...elements.nodes, ...elements.edges] });

  await new Promise((resolve, reject) => {
    const layout = cy.layout({
      name: 'elk',
      elk: { 'elk.algorithm': 'layered', 'elk.direction': 'RIGHT', 'elk.edgeRouting': 'ORTHOGONAL' },
    });
    layout.one('layoutstop', resolve);
    layout.one('layoutstop error', reject);
    layout.run();
  });

  assert.ok(cy.nodes().length > 0);
  assert.ok(cy.nodes().every((n) => Number.isFinite(n.position('x')) && Number.isFinite(n.position('y'))));
  cy.destroy();
});
