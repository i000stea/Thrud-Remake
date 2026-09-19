// 用真实 JSON 跑一遍 ELK 布局管线（与浏览器 Worker 中的计算完全一致），
// 用于定位「某个视图的图导致 ELK 报错」。用法：node scripts/check-elk.js
const fs = require('node:fs');
const path = require('node:path');
const ELK = require('elkjs/lib/elk.bundled.js');
const model = require('../public/atlas-model');
const view = require('../public/atlas-view');

const DATA = path.join(__dirname, '..', 'data');
const readJson = (name) => JSON.parse(fs.readFileSync(path.join(DATA, name), 'utf8'));

async function main() {
  const bundle = {
    generated: readJson('code-atlas.generated.json'),
    layout: readJson('code-atlas.layout.json'),
    manual: readJson('code-atlas.manual.json'),
  };
  const views = ['assembly', ...Object.keys(bundle.layout.views || {}).filter((v) => v.startsWith('class:'))];
  const kinds = new Set(['assembly', 'class', 'call', 'event', 'data']);
  const elk = new ELK();
  let failed = 0;

  for (const name of views) {
    const graph = model.graph(bundle, name, kinds, true);
    const input = view.buildElkGraph(graph);
    const ids = new Set(graph.nodes.map((n) => n.id));
    const dangling = graph.edges.filter((e) => !ids.has(e.source) || !ids.has(e.target));
    const edgeIds = graph.edges.map((e) => e.id);
    const duplicates = edgeIds.filter((id, i) => edgeIds.indexOf(id) !== i);
    const selfLoops = graph.edges.filter((e) => e.source === e.target);
    const undefinedNodes = graph.nodes.filter((n) => !n || !n.id).length;

    try {
      const started = Date.now();
      const result = await elk.layout(input);
      const positions = view.elkPositions(result);
      console.log(`OK   ${name}: nodes=${graph.nodes.length} edges=${graph.edges.length} -> ${Object.keys(positions).length} px (${Date.now() - started}ms)`);
    } catch (error) {
      failed++;
      console.log(`FAIL ${name}: nodes=${graph.nodes.length} edges=${graph.edges.length} -> ${error.message}`);
      console.log(`     dangling=${dangling.length} duplicateEdgeIds=${duplicates.length} selfLoops=${selfLoops.length} undefinedNodes=${undefinedNodes}`);
      for (const e of dangling.slice(0, 5)) console.log(`     dangling edge: ${e.source} -> ${e.target}`);
      for (const id of duplicates.slice(0, 5)) console.log(`     duplicate edge id: ${id}`);
    }
  }

  process.exitCode = failed ? 1 : 0;
}

main().catch((error) => {
  console.error('[check-elk] 失败：', error.message);
  process.exitCode = 1;
});
