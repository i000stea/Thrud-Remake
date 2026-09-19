(function(root, factory) {
  const api = factory();
  if (typeof module !== 'undefined' && module.exports) module.exports = api;
  else root.AtlasModel = api;
})(typeof globalThis === 'undefined' ? this : globalThis, function() {
  function graph(bundle, view, kinds, external = true) {
    const g = bundle.generated;
    const assembly = view === 'assembly' ? null : view.slice(6);
    const sourceNodes = assembly ? [...g.classes, ...(g.orphans || []).filter(n => n.assembly)] : [...g.assemblies, ...(g.orphans || []).filter(n=>!n.assembly)];
    const nodesById = new Map(sourceNodes.map(n => [n.id, { ...n, label: n.name, orphan: (g.orphans || []).some(o=>o.id===n.id), external: assembly ? n.assembly !== assembly : !!n.external }]));
    const all = [...g.relations.map(r=>({...r,origin:'auto'})), ...bundle.manual.relations.map(r=>({...r,origin:'manual',confidence:'manual'}))];
    const own = new Set(sourceNodes.filter(n => assembly ? n.assembly === assembly : !n.external).map(n=>n.id));
    // 坐标和人工说明关联的失效节点也保留；不会自动丢弃阅读记录。
    for (const id of Object.keys(bundle.layout.views?.[view]?.nodes || {})) {
      if (!nodesById.has(id)) { nodesById.set(id,{id,name:id,label:id,orphan:true,external:false}); own.add(id); }
    }
    const selected = all.filter(r => (assembly ? r.kind !== 'assembly' : r.kind === 'assembly') && kinds.has(r.kind) && (own.has(r.source)||own.has(r.target)));
    const visible = new Set(own), grouped = new Map();
    for (const r of selected) {
      if (!external && (!own.has(r.source)||!own.has(r.target))) continue;
      for (const id of [r.source,r.target]) {
        visible.add(id);
        if (!nodesById.has(id)) nodesById.set(id,{id,label:id,name:id,orphan:true,external:true});
      }
      const key = JSON.stringify([r.source,r.target]);
      if (!grouped.has(key)) grouped.set(key,{id:key,source:r.source,target:r.target,relations:[],kinds:[]});
      const e = grouped.get(key); e.relations.push(r); if (!e.kinds.includes(r.kind)) e.kinds.push(r.kind);
    }
    return { nodes: [...visible].map(id=>nodesById.get(id)), edges:[...grouped.values()] };
  }
  function autoLayout(nodes, edges) {
    // 以入度选择入口，再沿依赖方向分列；环中的节点落入同一兜底列。
    const indegree = new Map(nodes.map(n=>[n.id,0])), rank = new Map();
    for (const e of edges) indegree.set(e.target,(indegree.get(e.target)||0)+1);
    const queue = nodes.filter(n=>!indegree.get(n.id)).map(n=>n.id);
    queue.forEach(id=>rank.set(id,0));
    for (let i=0;i<queue.length;i++) for (const e of edges.filter(e=>e.source===queue[i])) {
      rank.set(e.target,Math.max(rank.get(e.target)||0,(rank.get(e.source)||0)+1));
      indegree.set(e.target,indegree.get(e.target)-1); if (!indegree.get(e.target)) queue.push(e.target);
    }
    const columns = new Map(), result = {};
    for (const n of [...nodes].sort((a,b)=>a.name.localeCompare(b.name))) {
      const column = n.external ? 5 : Math.min(4,rank.get(n.id)||0);
      const row = columns.get(column)||0; columns.set(column,row+1);
      result[n.id] = { x:80+column*330, y:70+row*100 };
    }
    return result;
  }
  return { graph, autoLayout };
});
