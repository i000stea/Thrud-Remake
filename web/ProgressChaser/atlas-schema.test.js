// K5：JSON 契约校验与错误恢复的测试。
const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs/promises');
const path = require('node:path');
const os = require('node:os');
const core = require('./atlas-core');

test('manual 校验逐字段定位错误', () => {
  const base = { schemaVersion: 1, relations: [], notes: [] };
  assert.doesNotThrow(() => core.validateManual({ ...base }));

  assert.throws(() => core.validateManual({ schemaVersion: 2, relations: [], notes: [] }), /schemaVersion/);
  assert.throws(() => core.validateManual({ schemaVersion: 1, relations: {}, notes: [] }), /relations 必须是数组/);
  assert.throws(() => core.validateManual({ schemaVersion: 1, relations: [{ source: 'a', target: 'b', kind: 'class' }], notes: [] }), /缺少字段 id/);
  assert.throws(() => core.validateManual({ schemaVersion: 1, relations: [{ id: 'x', source: 'a', target: 'b', kind: 'wrong' }], notes: [] }), /kind：非法值/);
  assert.throws(() => core.validateManual({ schemaVersion: 1, relations: [{ id: 'x', source: 'a', target: 'b', kind: 'class' }, { id: 'x', source: 'a', target: 'b', kind: 'class' }], notes: [] }), /重复的 id/);
  assert.throws(() => core.validateManual({ schemaVersion: 1, relations: [], notes: [{ node: 'a' }] }), /text/);
});

test('layout 同时接受 schemaVersion 1/2，并统一提升到 2', () => {
  const v1 = { schemaVersion: 1, revision: 3, views: { assembly: { zoom: 1, pan: { x: 0, y: 0 }, nodes: { A: { x: 10, y: 20 } } } } };
  assert.doesNotThrow(() => core.validateLayout(v1));
  assert.equal(core.normalizeLayout(v1).schemaVersion, 2);

  const v2 = { ...v1, schemaVersion: 2 };
  assert.doesNotThrow(() => core.validateLayout(v2));

  const bad = { schemaVersion: 1, views: { assembly: { zoom: 99, pan: { x: 0, y: 0 }, nodes: {} } } };
  assert.throws(() => core.validateLayout(bad), /zoom/);
  const badCoord = { schemaVersion: 2, views: { assembly: { zoom: 1, pan: { x: 0, y: 0 }, nodes: { A: { x: 'nope', y: 0 } } } } };
  assert.throws(() => core.validateLayout(badCoord), /有限数值/);
});

test('readBundle 读入旧版 layout 并提升版本，扫描保留 orphan 标记', async (t) => {
  const dir = await fs.mkdtemp(path.join(os.tmpdir(), 'thrud-atlas-schema-'));
  t.after(async () => {
    assert.ok(path.resolve(dir).startsWith(path.resolve(os.tmpdir())));
    await fs.rm(dir, { recursive: true });
  });
  const write = async (rel, content) => {
    await fs.mkdir(path.dirname(path.join(dir, rel)), { recursive: true });
    await fs.writeFile(path.join(dir, rel), content);
  };
  await write('Assets/02_Scripts/Game.asmdef', '{"name":"Game","references":[]}');
  await write('Assets/02_Scripts/A.cs', 'namespace N { class A {} }');
  await write('Assets/02_Scripts/B.cs', 'namespace N { class B {} }');
  const data = path.join(dir, 'data');
  await core.scanAndSave(dir, data);

  // 写一份 schemaVersion:1 的旧布局 + 一份合法 manual。
  await write('data/code-atlas.layout.json', JSON.stringify({ schemaVersion: 1, revision: 5, views: { assembly: { zoom: 1, pan: { x: 1, y: 2 }, nodes: { Game: { x: 3, y: 4 } } } } }));
  await write('data/code-atlas.manual.json', JSON.stringify({ schemaVersion: 1, relations: [], notes: [{ node: 'Game', text: '先读 Game' }] }));

  const bundle = await core.readBundle(dir, data);
  assert.equal(bundle.layout.schemaVersion, 2, '旧 layout 应提升到 2');
  assert.equal(bundle.layout.revision, 5, 'revision 必须保留');

  // 删除一个类，重新扫描应保留为 orphan 且带 orphan:true。
  await fs.unlink(path.join(dir, 'Assets/02_Scripts/B.cs'));
  const next = await core.scanAndSave(dir, data);
  const orphanB = next.orphans.find((n) => n.id === 'Game::N.B');
  assert.ok(orphanB, '被删除的类应保留为 orphan');
  assert.equal(orphanB.orphan, true);
  assert.ok(next.orphans.every((n) => n.id && n.orphan === true));

  // 备份文件应存在，且 manual / layout 未被扫描覆盖。
  const bak = await fs.readFile(path.join(data, 'code-atlas.generated.json.bak'), 'utf8');
  assert.ok(bak.includes('schemaVersion'));
  const manualAfter = JSON.parse(await fs.readFile(path.join(data, 'code-atlas.manual.json'), 'utf8'));
  assert.equal(manualAfter.notes.length, 1);
});
