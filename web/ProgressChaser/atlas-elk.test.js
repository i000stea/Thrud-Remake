// 锁定浏览器端实际使用的 ELK 调用方式：
// 官方 elk-api.js + engine worker（elk-worker.min.js）。
// Node 没有 Web Worker，这里用 elk-worker.min.js 导出的同线程 Worker 作替身，
// 走的是完全相同的 postMessage 协议，因此能挡住「workerFactory / 协议用错」这类回归。
const test = require('node:test');
const assert = require('node:assert/strict');
const ELK = require('elkjs/lib/elk-api.js');
const workerModule = require('elkjs/lib/elk-worker.min.js');
const view = require('./public/atlas-view');

test('elk-api + engine worker 能完成 layered 布局并返回坐标', async () => {
  assert.equal(typeof ELK, 'function', 'elk-api.js 应导出 ELK 构造函数');
  assert.equal(typeof workerModule.Worker, 'function', 'elk-worker.min.js 应导出 Worker');

  const elk = new ELK({ workerFactory: () => new workerModule.Worker() });
  const graph = view.buildElkGraph({
    nodes: [{ id: 'a', name: 'A' }, { id: 'b', name: 'B' }, { id: 'c', name: 'C' }],
    edges: [{ id: 'e1', source: 'a', target: 'b' }, { id: 'e2', source: 'b', target: 'c' }],
  });

  const result = await elk.layout(graph);
  const positions = view.elkPositions(result);
  assert.equal(Object.keys(positions).length, 3);
  assert.ok(Object.values(positions).every((p) => Number.isFinite(p.x) && Number.isFinite(p.y)));
  // layered + RIGHT 应把 a→b→c 排成递增的 x
  assert.ok(positions.a.x < positions.b.x);
  assert.ok(positions.b.x < positions.c.x);
});
