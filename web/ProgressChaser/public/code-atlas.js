'use strict';
const NS = 'http://www.w3.org/2000/svg';
const $ = s => document.querySelector(s);
const clone = value => JSON.parse(JSON.stringify(value));
const colors = { assembly:'#80a9ff', class:'#82d9b0', call:'#c48cff', event:'#ff7c9b', data:'#5ed2e6' };
const labels = { assembly:'程序集依赖', class:'类引用', call:'方法调用', event:'事件', data:'数据 / 状态' };
const dashes = { event:'6 4', data:'2 4' };
const state = { bundle:null, view:'assembly', filters:new Set(Object.keys(colors)), external:true, history:[], graph:null, drag:null, selected:null, saveTimer:null, saving:false, dirty:false, blocked:false, revision:0, suppressClick:false, request:0 };
const status = text => { $('#status').textContent = text; };
function el(tag, attrs={}, text='') { const e=document.createElement(tag); for (const [k,v] of Object.entries(attrs)) e.setAttribute(k,v); e.textContent=text; return e; }
function svg(tag, attrs={}, text='') { const e=document.createElementNS(NS,tag); for (const [k,v] of Object.entries(attrs)) e.setAttribute(k,v); e.textContent=text; return e; }
async function api(url, options={}) { const r=await fetch(url,options); const data=await r.json(); if(!r.ok) throw new Error(data.message||`HTTP ${r.status}`); return data; }
function view() {
  const views=state.bundle.layout.views;
  views[state.view] ||= { zoom:1, pan:{x:0,y:0}, nodes:{} };
  return views[state.view];
}
function checkpoint() { state.history.push({view:state.view, value:clone(view())}); if(state.history.length>60)state.history.shift(); $('#undoButton').disabled=false; }
function changed() { state.dirty=true; status('布局待保存…'); clearTimeout(state.saveTimer); state.saveTimer=setTimeout(()=>{state.saveTimer=null;flush();},600); }
async function flush() {
  if (state.saving || !state.dirty || state.blocked) return;
  state.saving=true;
  try {
    while(state.dirty) {
      state.dirty=false;
      const saved=await api('/api/code-atlas/layout',{method:'PUT',headers:{'Content-Type':'application/json'},body:JSON.stringify({revision:state.revision,token:state.bundle.layoutToken,layout:state.bundle.layout})});
      state.revision=saved.layout.revision; state.bundle.layoutToken=saved.token;
    }
    status('布局已保存');
  } catch(e) { state.dirty=true; state.blocked=true; status('保存失败：'+e.message+'；可导出布局保留改动'); }
  finally { state.saving=false; }
}
function setTransform() { const v=view(); $('#viewport').setAttribute('transform',`translate(${v.pan.x},${v.pan.y}) scale(${v.zoom})`); $('#zoomReadout').textContent=`${Math.round(v.zoom*100)}%`; }
function render() {
  if(!state.bundle)return;
  state.graph=AtlasModel.graph(state.bundle,state.view,state.filters,state.external);
  const {nodes,edges}=state.graph,v=view();
  const automatic=AtlasModel.autoLayout(nodes,edges);
  nodes.forEach(n=>{ v.nodes[n.id] ||= automatic[n.id]; });
  $('#edges').replaceChildren(); $('#nodes').replaceChildren();
  const selectedNode=state.selected?.node;
  const neighbors=new Set([selectedNode]); edges.filter(e=>e.source===selectedNode||e.target===selectedNode).forEach(e=>{neighbors.add(e.source);neighbors.add(e.target);});
  for(const e of edges) {
    const a=v.nodes[e.source],b=v.nodes[e.target]; if(!a||!b)continue;
    const group=svg('g',{'class':'edge-group','tabindex':'0','role':'button','aria-label':`${e.source} 指向 ${e.target}`});
    const ax=a.x+125,ay=a.y+30,bx=b.x+125,by=b.y+30;
    const angle=Math.atan2(by-ay,bx-ax);
    const radius=(dx,dy)=>Math.min(125/Math.max(Math.abs(dx),.001),30/Math.max(Math.abs(dy),.001));
    const ra=radius(Math.cos(angle),Math.sin(angle)),rb=radius(Math.cos(angle),Math.sin(angle));
    const x1=ax+Math.cos(angle)*ra,y1=ay+Math.sin(angle)*ra,x2=bx-Math.cos(angle)*rb,y2=by-Math.sin(angle)*rb;
    const path=`M ${x1} ${y1} Q ${(x1+x2)/2-Math.sin(angle)*25} ${(y1+y2)/2+Math.cos(angle)*25} ${x2} ${y2}`;
    const hit=svg('path',{d:path,fill:'none',stroke:'transparent','stroke-width':18}); group.append(hit);
    e.kinds.forEach((kind,i)=>group.append(svg('path',{d:path,class:'edge',stroke:colors[kind],'stroke-dasharray':dashes[kind]||'none','marker-end':`url(#arrow-${kind})`,transform:`translate(${i*2},${i*2})`})));
    if(e.relations.length>1)group.append(svg('text',{x:(x1+x2)/2,y:(y1+y2)/2-7,class:'edge-label'},`${e.relations.length}`));
    if(selectedNode&&!neighbors.has(e.source))group.style.opacity='.16';
    if(state.selected?.edge===e.id)group.classList.add('selected');
    group.addEventListener('click',()=>showEdge(e)); group.addEventListener('keydown',ev=>{if(ev.key==='Enter')showEdge(e);});
    $('#edges').append(group);
  }
  for(const n of nodes) {
    const p=v.nodes[n.id];
    const group=svg('g',{class:`node ${state.view==='assembly'?'assembly':''} ${n.external?'external':''} ${n.orphan?'orphan':''} ${selectedNode===n.id?'selected':''}`,transform:`translate(${p.x},${p.y})`,'data-id':n.id,tabindex:0,role:'button','aria-label':n.name});
    group.append(svg('rect',{width:250,height:60}));
    group.append(svg('title',{},n.id));
    const display=n.label.length>29?n.label.slice(0,27)+'…':n.label;
    group.append(svg('text',{x:12,y:24},display));
    group.append(svg('text',{x:12,y:46,class:'node-meta'},n.orphan?'孤立 · 保留记录':n.external?'外部 · '+(n.assembly||'程序集'):state.view==='assembly'?'点击进入类图':n.kind+' · '+n.methods?.length+' 方法'));
    if(selectedNode&&!neighbors.has(n.id))group.style.opacity='.26';
    if(p.color)group.firstChild.setAttribute('stroke',p.color);
    group.addEventListener('click',()=>{if(!state.suppressClick)activate(n);});
    group.addEventListener('keydown',e=>{if(e.key==='Enter')activate(n);});
    $('#nodes').append(group);
  }
  const crumb=$('#crumb'); crumb.replaceChildren();
  const back=el('button',{class:'secondary'},'全部程序集'); back.onclick=()=>switchView('assembly'); crumb.append(back);
  if(state.view!=='assembly')crumb.append(el('span',{},' / '+state.view.slice(6)));
  crumb.append(el('span',{class:'muted'},`　${nodes.length} 节点 · ${edges.length} 组关系 · 箭头：引用者 → 被引用者；事件：发布者 → 事件 → 订阅者`));
  setTransform();
}
function switchView(key) { state.view=key; state.selected=null; state.request++; const fresh=!state.bundle.layout.views[key]; render(); if(fresh){fit();changed();} showIntro(); }
function detailTitle(title) { const d=$('#detail'); d.replaceChildren(el('h2',{},title)); return d; }
function activate(n) {
  if(state.view==='assembly'&&!n.external&&!n.orphan){switchView('class:'+n.id);return;}
  state.selected={node:n.id}; render(); state.request++;
  const d=detailTitle(n.name); d.append(el('code',{},n.id));
  const copy=el('button',{class:'secondary'},'复制 ID'); copy.onclick=()=>navigator.clipboard.writeText(n.id).then(()=>status('节点 ID 已复制')).catch(()=>status('请选中上方 ID 复制')); d.append(copy);
  d.append(el('p',{class:'muted'},n.orphan?'代码已失效，保留位置与人工说明。':n.external?'外部引用节点。项目内外部类型可跳转；第三方程序集当前只展示边界。':'类级视图；点击关系查看方法、事件和状态证据。'));
  if(n.assembly&&state.bundle.generated.assemblies.some(a=>a.name===n.assembly&&!a.external)&&n.assembly!==state.view.slice(6)){const b=el('button',{},'进入 '+n.assembly); b.onclick=()=>switchView('class:'+n.assembly);d.append(b);}
  for(const f of n.files||[])addEvidence(d,{file:f.path,line:f.line});
  for(const note of state.bundle.manual.notes.filter(x=>x.node===n.id))d.append(el('p',{},note.text));
  d.append(el('h3',{},'方法声明'));
  for(const m of n.methods||[]) {d.append(el('p',{class:'method-name'},m.signature));addEvidence(d,m);}
  if(n.unresolved)d.append(el('p',{class:'muted'},'引用 GUID 未在 Assets/Packages 源码中解析，可能位于 PackageCache 或预编译 DLL。'));
}
function showIntro(){const d=detailTitle('阅读方式');d.append(el('p',{class:'muted'},'先点击程序集进入类图，再点击类高亮相邻关系，点击连线查看源码证据。外部类型以虚线边框标识。'));
  d.append(el('p',{class:'muted'},state.bundle.generated.diagnostics.note));
  d.append(el('p',{class:'muted'},'人工关系只编辑 manual.json；修改 JSON 后点“重新读取 JSON”。“重新扫描”只重建 generated.json。'));
  d.append(el('p',{class:'muted'},'扫描时间：'+new Date(state.bundle.generated.generatedAt).toLocaleString()));
}
function showEdge(edge){state.selected={edge:edge.id};render();state.request++;const d=detailTitle('关系明细');
  const nodeName=id=>state.graph.nodes.find(n=>n.id===id)?.name||id;
  d.append(el('p',{},nodeName(edge.source)+' → '+nodeName(edge.target)));
  for(const r of edge.relations){const section=el('section',{class:'relation-detail'}); section.append(el('span',{class:'relation-tag'},labels[r.kind]+' · '+(r.origin==='manual'?'人工说明':r.confidence==='declared'?'显式声明':'静态候选')));section.append(el('p',{},r.label||r.kind));if(r.method)section.append(el('p',{class:'muted'},'所在方法：'+r.method));if(r.note)section.append(el('p',{},r.note));addEvidence(section,r);d.append(section);}
}
function addEvidence(container,r){if(!r.file)return; const b=el('button',{class:'source-link'},r.file+':'+(r.line||1));b.onclick=async()=>{const token=state.request;try{const data=await api('/api/code-atlas/source?file='+encodeURIComponent(r.file));if(token!==state.request)return;const lines=data.source.split('\n'),start=Math.max(0,(r.line||1)-5),end=Math.min(lines.length,(r.line||1)+8);container.querySelector('pre')?.remove();container.append(el('pre',{class:'source-code'},lines.slice(start,end).map((s,i)=>`${start+i+1} ${s}`).join('\n')));}catch(e){status(e.message);}};container.append(b);}
function fit(){const v=view(),ps=state.graph.nodes.map(n=>v.nodes[n.id]);if(!ps.length)return;const rect=$('#canvas').getBoundingClientRect(),x=Math.min(...ps.map(p=>p.x)),y=Math.min(...ps.map(p=>p.y)),w=Math.max(...ps.map(p=>p.x+250))-x,h=Math.max(...ps.map(p=>p.y+60))-y;v.zoom=Math.max(.1,Math.min(1.4,(rect.width-60)/w,(rect.height-60)/h));v.pan={x:(rect.width-w*v.zoom)/2-x*v.zoom,y:(rect.height-h*v.zoom)/2-y*v.zoom};setTransform();}
function zoomAt(factor,clientX,clientY){const v=view(),r=$('#canvas').getBoundingClientRect(),x=clientX-r.left,y=clientY-r.top,next=Math.min(4,Math.max(.1,v.zoom*factor));v.pan={x:x-(x-v.pan.x)*next/v.zoom,y:y-(y-v.pan.y)*next/v.zoom};v.zoom=next;setTransform();}
const canvas=$('#canvas');
canvas.addEventListener('pointerdown',e=>{if(e.button!==0||!state.bundle||e.target.closest('.edge-group'))return;const node=e.target.closest('.node'),v=view();state.drag={id:node?.dataset.id,startX:e.clientX,startY:e.clientY,before:clone(v),moved:false};canvas.setPointerCapture(e.pointerId);});
canvas.addEventListener('pointermove',e=>{const d=state.drag;if(!d)return;const dx=e.clientX-d.startX,dy=e.clientY-d.startY;if(!d.moved&&Math.hypot(dx,dy)<4)return;if(!d.moved){checkpoint();d.moved=true;state.suppressClick=true;}const v=view();if(d.id){v.nodes[d.id].x=d.before.nodes[d.id].x+dx/v.zoom;v.nodes[d.id].y=d.before.nodes[d.id].y+dy/v.zoom;render();}else{v.pan={x:d.before.pan.x+dx,y:d.before.pan.y+dy};setTransform();}});
function finishDrag(e){if(!state.drag)return;const d=state.drag;state.drag=null;if(canvas.hasPointerCapture(e.pointerId))canvas.releasePointerCapture(e.pointerId);if(d.moved){changed();setTimeout(()=>state.suppressClick=false,0);}else if(d.id){const n=state.graph.nodes.find(n=>n.id===d.id);state.suppressClick=true;activate(n);setTimeout(()=>state.suppressClick=false,0);}}
canvas.addEventListener('pointerup',finishDrag);canvas.addEventListener('pointercancel',e=>{if(state.drag){state.bundle.layout.views[state.view]=state.drag.before;state.drag=null;render();}state.suppressClick=false;});
canvas.addEventListener('wheel',e=>{e.preventDefault();if(!state.bundle)return;if(!state.saveTimer)checkpoint();zoomAt(Math.exp(-e.deltaY*.0015),e.clientX,e.clientY);changed();},{passive:false});
$('#autoLayoutButton').onclick=()=>{checkpoint();const positions=AtlasModel.autoLayout(state.graph.nodes,state.graph.edges);for(const [id,p] of Object.entries(positions))view().nodes[id]={...view().nodes[id],...p};render();fit();changed();};
$('#resetButton').onclick=()=>{checkpoint();const fresh=AtlasModel.autoLayout(state.graph.nodes,state.graph.edges);for(const [id,p] of Object.entries(fresh))view().nodes[id]={...view().nodes[id],...p};view().zoom=1;view().pan={x:0,y:0};render();fit();changed();};
$('#undoButton').onclick=()=>{const old=state.history.pop();if(!old)return;state.bundle.layout.views[old.view]=old.value;state.view=old.view;state.selected=null;render();changed();$('#undoButton').disabled=!state.history.length;};
$('#fitButton').onclick=()=>{checkpoint();fit();changed();};
$('#exportButton').onclick=()=>{const blob=new Blob([JSON.stringify(state.bundle.layout,null,2)],{type:'application/json'}),url=URL.createObjectURL(blob),a=el('a',{href:url,download:'code-atlas.layout.json'});a.click();URL.revokeObjectURL(url);};
async function load(scan=false){clearTimeout(state.saveTimer);if(state.dirty||state.saving){await flush();if(state.dirty||state.saving){status('布局仍未保存；请先解决保存错误，或导出后刷新页面');return;}}status(scan?'扫描源码中…':'读取 JSON 中…');
  const bundle=await api(scan?'/api/code-atlas/scan':'/api/code-atlas',scan?{method:'POST'}:{});state.bundle=bundle;state.revision=bundle.layout.revision||0;state.blocked=false;state.history=[];$('#undoButton').disabled=true;const fresh=!Object.keys(bundle.layout.views[state.view]?.nodes||{}).length;render();if(fresh){fit();changed();}showIntro();status(`已读取：${bundle.generated.stats.files} 文件 · ${bundle.generated.stats.classes} 类型`);
}
$('#reloadButton').onclick=()=>load().catch(e=>status(e.message));$('#scanButton').onclick=()=>load(true).catch(e=>status(e.message));
document.querySelectorAll('[data-kind]').forEach(input=>input.onchange=()=>{if(input.dataset.kind==='external')state.external=input.checked;else input.checked?state.filters.add(input.dataset.kind):state.filters.delete(input.dataset.kind);render();});
window.addEventListener('beforeunload',e=>{if(state.dirty||state.saving){e.preventDefault();e.returnValue='';}});
document.addEventListener('visibilitychange',()=>{if(document.hidden)flush();});
document.addEventListener('keydown',e=>{if((e.ctrlKey||e.metaKey)&&e.key==='z'&&!/INPUT|TEXTAREA/.test(e.target.tagName)){e.preventDefault();$('#undoButton').click();}});
const defs=svg('defs');for(const [kind,color] of Object.entries(colors)){const marker=svg('marker',{id:'arrow-'+kind,viewBox:'0 0 10 10',refX:9,refY:5,markerWidth:7,markerHeight:7,orient:'auto-start-reverse'});marker.append(svg('path',{d:'M 0 0 L 10 5 L 0 10 z',fill:color}));defs.append(marker);}canvas.prepend(defs);
load().catch(e=>status('加载失败：'+e.message));

