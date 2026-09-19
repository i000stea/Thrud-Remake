// 图数据 -> Cytoscape 元素的纯函数模块。浏览器挂到 AtlasView，Node 里可 require 做测试。
(function (root, factory) {
  const api = factory();
  if (typeof module !== 'undefined' && module.exports) module.exports = api;
  else root.AtlasView = api;
})(typeof globalThis === 'undefined' ? this : globalThis, function () {
  const KIND_ORDER = ['assembly', 'class', 'call', 'event', 'data'];

  function primaryKind(kinds) {
    for (const kind of KIND_ORDER) if (kinds.includes(kind)) return kind;
    return kinds[0];
  }

  // graph: AtlasModel.graph() 的输出 { nodes, edges }
  // positionOf(id): 可选，返回 { x, y } 或 null
  function toElements(graph, positionOf) {
    const nodes = graph.nodes.map((node) => {
      const element = {
        data: {
          id: node.id,
          label: node.label || node.name || node.id,
          name: node.name || node.id,
          kind: node.kind || 'class',
          assembly: node.assembly || '',
          external: !!node.external,
          orphan: !!node.orphan,
          methodCount: (node.methods || []).length,
        },
      };
      const position = positionOf ? positionOf(node.id) : null;
      if (position && Number.isFinite(position.x) && Number.isFinite(position.y)) {
        element.position = { x: position.x, y: position.y };
      }
      return element;
    });

    const edges = graph.edges.map((edge, index) => ({
      data: {
        id: edge.id || `${edge.source}->${edge.target}#${index}`,
        source: edge.source,
        target: edge.target,
        kinds: [...edge.kinds],
        primary: primaryKind(edge.kinds),
        count: edge.relations.length,
        label: edge.relations.length > 1 ? String(edge.relations.length) : '',
      },
    }));

    return { nodes, edges };
  }

  // 分级显示（LOD）：只影响表现，不改变数据与选择状态。
  function lodOf(zoom) {
    if (zoom < 0.3) return 'far';
    if (zoom < 0.7) return 'mid';
    if (zoom < 1.5) return 'near';
    return 'close';
  }

  // 为没有坐标的节点生成网格兜底位置（按入度分列，环落到兜底列）。
  function gridFallback(graph) {
    const indegree = new Map(graph.nodes.map((n) => [n.id, 0]));
    for (const e of graph.edges) indegree.set(e.target, (indegree.get(e.target) || 0) + 1);
    const rank = new Map();
    const queue = graph.nodes.filter((n) => !indegree.get(n.id)).map((n) => n.id);
    queue.forEach((id) => rank.set(id, 0));
    for (let i = 0; i < queue.length; i++) {
      for (const e of graph.edges.filter((e) => e.source === queue[i])) {
        rank.set(e.target, Math.max(rank.get(e.target) || 0, (rank.get(e.source) || 0) + 1));
        indegree.set(e.target, indegree.get(e.target) - 1);
        if (!indegree.get(e.target)) queue.push(e.target);
      }
    }
    const columns = new Map();
    const result = {};
    for (const node of [...graph.nodes].sort((a, b) => (a.name || '').localeCompare(b.name || ''))) {
      const column = node.external ? 5 : Math.min(4, rank.get(node.id) || 0);
      const row = columns.get(column) || 0;
      columns.set(column, row + 1);
      result[node.id] = { x: 80 + column * 330, y: 70 + row * 100 };
    }
    return result;
  }

  // —— ELK 适配（纯函数，可在 Node 中测试）——

  const NODE_WIDTH = 190;
  const NODE_HEIGHT = 46;

  const ELK_LAYERED_OPTIONS = {
    'elk.algorithm': 'layered',
    'elk.direction': 'RIGHT',
    'elk.edgeRouting': 'ORTHOGONAL',
    'elk.layered.spacing.nodeNodeBetweenLayers': '90',
    'elk.spacing.nodeNode': '50',
    'elk.layered.crossingMinimization.strategy': 'LAYER_SWEEP',
    'elk.layered.nodePlacement.strategy': 'BRANDES_KOEPF',
  };

  // 把图装配结果转成 ELK 的 JSON 输入。外部节点与孤立节点同样参与布局。
  function buildElkGraph(graph, options) {
    return {
      id: 'root',
      layoutOptions: { ...ELK_LAYERED_OPTIONS, ...(options || {}) },
      children: graph.nodes.map((node) => ({ id: node.id, width: NODE_WIDTH, height: NODE_HEIGHT })),
      edges: graph.edges.map((edge, index) => ({
        id: edge.id || `${edge.source}->${edge.target}#${index}`,
        sources: [edge.source],
        targets: [edge.target],
      })),
    };
  }

  // 从 ELK 输出中取节点坐标。
  function elkPositions(result) {
    const positions = {};
    for (const child of result?.children || []) {
      positions[child.id] = { x: Number(child.x) || 0, y: Number(child.y) || 0 };
    }
    return positions;
  }

  return { toElements, lodOf, gridFallback, primaryKind, KIND_ORDER, buildElkGraph, elkPositions, ELK_LAYERED_OPTIONS, NODE_WIDTH, NODE_HEIGHT };
});
