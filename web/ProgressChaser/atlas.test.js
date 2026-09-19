const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs/promises');
const path = require('node:path');
const os = require('node:os');
const core = require('./atlas-core');
const model = require('./public/atlas-model');

test('类型作用域、嵌套类、partial 和注释不会混成同一个类', () => {
  const text = 'namespace N { partial class A { B b; void Tick() { b.Run(); } class Inner {} } class B { public void Run() {} } /* class Fake {} */ }';
  const {types}=core.parseSource(text,'Test.cs','Game');
  assert.deepEqual(types.map(t=>t.id),['Game::N.A','Game::N.A+Inner','Game::N.B']);
  assert.equal(types[0].methods[0].name,'Tick');
  assert.equal(types[2].methods[0].name,'Run');
  assert.ok(!types[0].body.includes('class Inner'));
});

test('扫描、partial 合并、嵌套 asmdef、关系证据、孤立保留、保存冲突',async t=>{
  const dir=await fs.mkdtemp(path.join(os.tmpdir(),'thrud-atlas-test-'));
  t.after(async()=>{assert.ok(path.resolve(dir).startsWith(path.resolve(os.tmpdir())+path.sep+'thrud-atlas-test-'));await fs.rm(dir,{recursive:true});});
  const write=async(p,content)=>{await fs.mkdir(path.dirname(path.join(dir,p)),{recursive:true});await fs.writeFile(path.join(dir,p),content);};
  await write('Assets/02_Scripts/Game.asmdef','{"name":"Game","references":["Other"]}');
  await write('Assets/02_Scripts/Nested/Other.asmdef','{"name":"Other"}');
  await write('Assets/02_Scripts/A.cs','namespace N { partial class A { B b; public void Tick() { b.Run(); b.Value = 2; this.SendEvent(new E()); } } class Empty { string s="B.Run()"; } struct E {} }');
  await write('Assets/02_Scripts/A.Designer.cs','namespace N { partial class A { int field; } }');
  await write('Assets/02_Scripts/Nested/B.cs','namespace N { class B { public int Value; public void Run() {} } }');
  const data=path.join(dir,'data');const g=await core.scanAndSave(dir,data);
  const a=g.classes.find(c=>c.id==='Game::N.A'),b=g.classes.find(c=>c.id==='Other::N.B');
  assert.equal(a.files.length,2);assert.ok(b);
  assert.ok(g.relations.some(r=>r.kind==='call'&&r.source===a.id&&r.target===b.id&&r.line===1));
  assert.ok(g.relations.some(r=>r.kind==='data'&&r.access==='write'));
  assert.ok(g.relations.some(r=>r.kind==='event'&&r.role==='publish'));
  assert.ok(!g.relations.some(r=>r.source==='Game::N.Empty'));
  const bundle=await core.readBundle(dir,data);
  // 显式创建初始布局，模拟交付文件。
  await write('data/code-atlas.layout.json',JSON.stringify(bundle.layout));
  const fresh=await core.readBundle(dir,data);
  const request={revision:0,token:fresh.layoutToken,layout:{schemaVersion:1,views:{assembly:{zoom:1,pan:{x:0,y:0},nodes:{Game:{x:10,y:20}}}}}};
  const saved=await core.saveLayout(data,request);assert.equal(saved.layout.revision,1);
  await assert.rejects(core.saveLayout(data,request),/另一页面/);
  await fs.unlink(path.join(dir,'Assets/02_Scripts/Nested/B.cs'));
  const next=await core.scanAndSave(dir,data);assert.ok(next.orphans.some(n=>n.id===b.id));
  await write('data/code-atlas.manual.json','{ invalid');
  await assert.rejects(core.readBundle(dir,data),/manual.json/);
});

test('类图包含向外引用及事件反向通道；人工关系独立筛选、同向聚合',()=>{
  const bundle={generated:{assemblies:[],classes:[{id:'a',name:'A',assembly:'Game'},{id:'b',name:'B',assembly:'Other'},{id:'e',name:'Event',assembly:'Other'}],relations:[{source:'a',target:'b',kind:'call'},{source:'a',target:'b',kind:'class'},{source:'e',target:'a',kind:'event'}],orphans:[]},manual:{relations:[{source:'a',target:'b',kind:'data',note:'写入'}]},layout:{views:{}}};
  const g=model.graph(bundle,'class:Game',new Set(['call','class','event','data']));
  assert.equal(g.nodes.length,3);assert.equal(g.edges.length,2);assert.equal(g.edges[0].relations.length,3);
  const filtered=model.graph(bundle,'class:Game',new Set(['data']));assert.equal(filtered.edges.length,1);assert.equal(filtered.edges[0].relations[0].origin,'manual');
  assert.equal(model.graph(bundle,'class:Game',new Set(['call']),false).nodes.length,1);
  const p=model.autoLayout(g.nodes,g.edges);assert.ok(Object.values(p).every(n=>Number.isFinite(n.x)&&Number.isFinite(n.y)));
});
