// 合成大图性能取证（headless）：500 节点 / 2000 聚合边。
// 只测量布局与元素构建的数据管线耗时，浏览器渲染帧率由用户按文档 §10.3 目测验收。
// 用法：node scripts/perf-atlas.js [nodeCount] [edgeCount]
const cytoscape = require('cytoscape');
const ELK = require('elkjs/lib/elk.bundled.js');
const AtlasView = require('../public/atlas-view');
const AtlasModel = require('../public/atlas-model');

const nodeCount = Number(process.argv[2] || 500);
const edgeCount = Number(process.argv[3] || 2000);

function syntheticGraph(nodes, edges) {
  const nodeList = [];
  for (let i = 0; i < nodes; i++) {
    nodeList.push({ id: 'N' + i, name: 'Node' + i, label: 'Node' + i, assembly: i % 3 === 0 ? 'Game' : 'Other', external: i % 5 === 0, orphan: false, methods: [] });
  }
  // 用 (a, a+d) 的定向对保证聚合边互不重复，覆盖不同跨度以模拟依赖分布。
  const groups = new Map();
  let made = 0;
  for (let d = 1; d < nodes && made < edges; d++) {
    for (let a = 0; a < nodes && made < edges; a++) {
      const b = (a + d) % nodes;
      if (b === a) continue;
      const key = 'N' + a + '->N' + b;
      groups.set(key, { id: key, source: 'N' + a, target: 'N' + b, kinds: ['class'], relations: [{ kind: 'class' }] });
      made++;
    }
  }
  return { nodes: nodeList, edges: [...groups.values()] };
}

async function main() {
  const graph = syntheticGraph(nodeCount, edgeCount);
  const report = { nodeCount: graph.nodes.length, aggregatedEdges: graph.edges.length, relationDetails: graph.edges.reduce((n, e) => n + e.relations.length, 0) };

  let t = performance.now();
  const elements = AtlasView.toElements(graph, null);
  report.toElementsMs = +(performance.now() - t).toFixed(1);

  t = performance.now();
  const cy = cytoscape({ headless: true, styleEnabled: false, elements: [...elements.nodes, ...elements.edges] });
  report.cytoscapeBuildMs = +(performance.now() - t).toFixed(1);
  report.cytoscapeNodes = cy.nodes().length;
  report.cytoscapeEdges = cy.edges().length;

  t = performance.now();
  const neighborhood = cy.getElementById('N0').closedNeighborhood();
  report.neighborhoodMs = +(performance.now() - t).toFixed(2);
  report.neighborhoodSize = neighborhood.length;

  t = performance.now();
  AtlasModel.autoLayout(graph.nodes, graph.edges);
  report.gridFallbackMs = +(performance.now() - t).toFixed(1);

  const elk = new ELK();
  const elkGraph = AtlasView.buildElkGraph(graph);
  t = performance.now();
  const result = await elk.layout(elkGraph);
  report.elkLayeredMs = +(performance.now() - t).toFixed(1);
  report.elkChildren = (result.children || []).length;

  cy.destroy();
  console.log(JSON.stringify(report, null, 2));
}

main().catch((error) => {
  console.error('[perf-atlas]', error && error.message ? error.message : error);
  process.exitCode = 1;
});
