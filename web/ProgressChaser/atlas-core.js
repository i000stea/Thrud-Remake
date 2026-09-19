// 无编译器依赖的保守索引器。关系是有源码证据的静态候选，不是运行时追踪。
const fs = require('node:fs/promises');
const path = require('node:path');
const crypto = require('node:crypto');
const names = { generated: 'code-atlas.generated.json', layout: 'code-atlas.layout.json', manual: 'code-atlas.manual.json' };
const slash = p => p.replaceAll('\\', '/');
const blank = s => s.replace(/[^\r\n]/g, ' ');
function mask(source) {
  return source.replace(/\/\/[^\r\n]*|\/\*[\s\S]*?\*\/|@"(?:""|[^"])*"|"(?:\\.|[^"\\])*"|'(?:\\.|[^'\\])*'/g, blank);
}
function closing(text, start, open = '{', close = '}') {
  let depth = 0;
  for (let i = start; i < text.length; i++) { if (text[i] === open) depth++; if (text[i] === close && --depth === 0) return i; }
  return text.length - 1;
}
function parseSource(source, file, assembly) {
  const code = mask(source), types = [], scopes = [];
  const line = at => code.slice(0, at).split('\n').length;
  for (const m of code.matchAll(/\bnamespace\s+([\w.]+)\s*([;{])/g)) {
    const start = m.index + m[0].length - 1;
    scopes.push({ name: m[1], start, end: m[2] === ';' ? code.length : closing(code, start) });
  }
  for (const m of code.matchAll(/\b(class|struct|interface|enum|record)\s+(\w+)(\s*<[^>{}]+>)?[^;{}]*\{/g)) {
    const start = m.index + m[0].length - 1, end = closing(code, start);
    const parent = types.filter(t => t.start < start && t.end > end).at(-1);
    const ns = scopes.filter(s => s.start < start && s.end > end).map(s => s.name).join('.');
    const arity = m[3] ? '`' + (m[3].split(',').length) : '';
    const fullName = parent ? parent.fullName + '+' + m[2] + arity : [ns, m[2] + arity].filter(Boolean).join('.');
    types.push({ id: `${assembly}::${fullName}`, name: m[2], fullName, namespace: ns, kind: m[1], assembly, file, line: line(m.index), start, end, declaration: m.index, methods: [], fields: {} });
  }
  for (const t of types) {
    let body = code.slice(t.start + 1, t.end);
    for (const nested of types.filter(n => n.start > t.start && n.end < t.end)) {
      const a = nested.declaration - t.start - 1, b = nested.end - t.start;
      body = body.slice(0, a) + blank(body.slice(a, b)) + body.slice(b);
    }
    t.body = body;
    const base = t.start + 1;
    for (const m of body.matchAll(/\b([\w.]+)(?:\s*<[^;={}()]+>)?(?:\[\])?\s+(\w+)\s*(?=[;=,{)])/g)) t.fields[m[2]] = m[1];
    for (const m of body.matchAll(/\b(\w+)\s*(?:<[^;{}()]+>)?\s*\([^;{}]*\)\s*(?:where\s+[^{}]+)?\s*(\{|=>)/g)) {
      if (['if','for','foreach','while','switch','catch','using','lock'].includes(m[1])) continue;
      const before = body.slice(Math.max(0, m.index - 120), m.index);
      // 调用表达式、lambda 和局部函数不作为类级方法声明。
      if (/[.=]\s*$/.test(before) || !/(?:\b\w+[<>\[\],.? ]*\s+|\b(?:public|private|protected|internal)\s+)$/.test(before)) continue;
      const at = base + m.index;
      let depth = 0; for (const c of body.slice(0, m.index)) { if (c === '{') depth++; if (c === '}') depth--; }
      if (depth !== 0) continue;
      const end = m[2] === '{' ? closing(code, at + m[0].lastIndexOf('{')) : code.indexOf(';', at);
      t.methods.push({ name: m[1], signature: m[0].replace(/\s+/g, ' ').replace(/(\{|=>)$/, '').trim(), file, line: line(at), start: at, end });
    }
    t.lineAt = local => line(base + local);
  }
  return { code, types };
}
async function walk(dir, accept) {
  const result = [];
  async function visit(folder) {
    for (const e of await fs.readdir(folder, { withFileTypes: true })) {
      if (e.isSymbolicLink() || e.name.startsWith('.') || ['node_modules','Library','Temp','obj','bin'].includes(e.name)) continue;
      const p = path.join(folder, e.name);
      if (e.isDirectory()) await visit(p); else if (accept(p)) result.push(p);
    }
  }
  try { await visit(dir); } catch (e) { if (e.code !== 'ENOENT') throw e; }
  return result.sort();
}
async function scan(root) {
  const scripts = path.join(root, 'Assets/02_Scripts');
  const paths = await walk(scripts, p => /\.(cs|asmdef)$/.test(p));
  const assemblyPaths = [...await walk(path.join(root, 'Assets'), p => p.endsWith('.asmdef')), ...await walk(path.join(root, 'Packages'), p => p.endsWith('.asmdef'))];
  const assemblies = [], guids = new Map();
  for (const p of assemblyPaths) {
    const a = JSON.parse((await fs.readFile(p, 'utf8')).replace(/^\uFEFF/, ''));
    const meta = await fs.readFile(p + '.meta', 'utf8').catch(() => '');
    const guid = meta.match(/^guid:\s*(\S+)/m)?.[1]; if (guid) guids.set('GUID:' + guid, a.name);
    assemblies.push({ id: a.name, name: a.name, path: slash(path.relative(root, p)), references: a.references || [], includePlatforms: a.includePlatforms || [], external: !paths.includes(p) });
  }
  for (const a of assemblies) a.references = a.references.map(r => guids.get(r) || r);
  const projectAssemblies = assemblies.filter(a => !a.external);
  const refs = new Set(projectAssemblies.flatMap(a => a.references));
  const visibleAssemblies = assemblies.filter(a => !a.external || refs.has(a.name));
  for (const ref of refs) if (!visibleAssemblies.some(a => a.name === ref)) visibleAssemblies.push({ id: ref, name: ref, external: true, unresolved: true, references: [] });
  const allTypes = [], files = [];
  for (const p of paths.filter(p => p.endsWith('.cs'))) {
    const file = slash(path.relative(root, p)), source = await fs.readFile(p, 'utf8');
    const a = projectAssemblies.filter(a => file.startsWith(a.path.slice(0, a.path.lastIndexOf('/') + 1))).sort((x,y) => y.path.length - x.path.length)[0];
    const assembly = a?.name || 'Assembly-CSharp';
    const parsed = parseSource(source, file, assembly);
    allTypes.push(...parsed.types);
    files.push({ path: file, assembly, lineCount: source.split('\n').length, types: parsed.types.map(t => t.id), hash: crypto.createHash('sha256').update(source).digest('hex') });
  }
  const byId = new Map(), byName = new Map();
  for (const t of allTypes) {
    if (!byId.has(t.id)) byId.set(t.id, { id: t.id, name: t.name, fullName: t.fullName, namespace: t.namespace, assembly: t.assembly, kind: t.kind, files: [], methods: [], fields: {} });
    const merged = byId.get(t.id);
    merged.files.push({ path: t.file, line: t.line }); merged.methods.push(...t.methods.map(({start,end,...m}) => m)); Object.assign(merged.fields, t.fields);
  }
  for (const t of byId.values()) { if (!byName.has(t.name)) byName.set(t.name, []); byName.get(t.name).push(t); }
  let ambiguous = 0;
  function resolve(name, owner) {
    const candidates = byName.get(name?.split('.').at(-1)) || [];
    if (candidates.length === 1) return candidates[0];
    const local = candidates.filter(t => t.assembly === owner.assembly && t.namespace === owner.namespace);
    if (local.length === 1) return local[0];
    if (candidates.length > 1) ambiguous++;
    return null;
  }
  const relations = [], seen = new Set();
  function add(source, target, kind, label, evidence = {}) {
    if (source === target) return;
    const key = JSON.stringify([source,target,kind,label,evidence.file,evidence.line]); if (seen.has(key)) return; seen.add(key);
    relations.push({ id: crypto.createHash('sha1').update(key).digest('hex').slice(0,20), source, target, kind, label, confidence: kind === 'assembly' ? 'declared' : 'candidate', ...evidence });
  }
  for (const a of projectAssemblies) for (const ref of a.references) add(a.name, ref, 'assembly', 'asmdef 显式引用', { file: a.path, line: 1 });
  for (const t of allTypes) {
    const body = t.body, vars = { ...byId.get(t.id).fields };
    for (const m of body.matchAll(/\bvar\s+(\w+)\s*=\s*(?:new\s+(\w+)|(?:this\.)?Get(?:Model|System|Utility)\s*<\s*(\w+)\s*>)/g)) vars[m[1]] = m[2] || m[3];
    const evidence = (index, extra = {}) => ({ file: t.file, line: t.lineAt(index), method: t.methods.find(m => m.start <= t.start + 1 + index && m.end >= t.start + 1 + index)?.name || null, ...extra });
    for (const m of body.matchAll(/\b[A-Za-z_]\w*\b/g)) {
      const target = resolve(m[0], t);
      if (target && !seen.has('type:' + t.id + ':' + target.id + ':' + t.file)) {
        add(t.id, target.id, 'class', '类型名引用候选', evidence(m.index)); seen.add('type:' + t.id + ':' + target.id + ':' + t.file);
      }
    }
    for (const m of body.matchAll(/\b(SendEvent|PublishEvent|RegisterEvent|Subscribe)\s*<\s*(\w+)\s*>/g)) {
      const target = resolve(m[2], t); if (!target) continue;
      const publish = /Send|Publish/.test(m[1]);
      add(publish ? t.id : target.id, publish ? target.id : t.id, 'event', publish ? `发布 ${m[2]}` : `订阅 ${m[2]}`, evidence(m.index, { role: publish ? 'publish' : 'subscribe' }));
    }
    for (const m of body.matchAll(/\b(?:SendEvent|PublishEvent)\s*\(\s*new\s+(\w+)/g)) {
      const target = resolve(m[1], t);
      if (target) add(t.id, target.id, 'event', `发布 ${m[1]}`, evidence(m.index, { role:'publish' }));
    }
    for (const m of body.matchAll(/\bGet(?:Model|System|Utility)\s*<\s*(\w+)\s*>\s*\(\s*\)\.(\w+)\s*\(/g)) {
      const target = resolve(m[1],t);
      if(target) add(t.id,target.id,'call',`调用 ${m[1]}.${m[2]}`,evidence(m.index,{access:'call',member:m[2]}));
    }
    // 可解析的局部/字段链：controller.State.Hud.Health.Value。无法解析的链不猜目标。
    for (const m of body.matchAll(/\b(\w+)((?:\.\w+)+)\s*(?=\(|[=;+\-*/<>,)\]}\r\n])/g)) {
      const parts = [m[1], ...m[2].slice(1).split('.')];
      let owner = m[1] === 'this' ? byId.get(t.id) : resolve(vars[m[1]] || m[1], t);
      if (!owner) continue;
      let member = parts.at(-1);
      for (let i = 1; i < parts.length - 1; i++) {
        const next = resolve(owner.fields[parts[i]], t);
        if (!next) { if (parts[i + 1] !== 'Value' && !/^Register/.test(parts[i + 1])) owner = null; break; }
        owner = next;
      }
      if (!owner) continue;
      const tail = body.slice(m.index + m[0].trimEnd().length);
      const call = /^\s*\(/.test(tail);
      const binding = /^Register/.test(member);
      const write = /^\s*(?:=(?!=|>)|\+=|-=|\+\+|--)/.test(tail);
      const kind = binding || !call ? 'data' : 'call';
      add(t.id, owner.id, kind, `${binding ? '订阅状态' : call ? '调用' : write ? '写入' : '读取'} ${parts.join('.')}`, evidence(m.index, { access: binding ? 'subscribe' : call ? 'call' : write ? 'write' : 'read', member }));
    }
  }
  return { schemaVersion: 2, generatedAt: new Date().toISOString(), scriptsRoot: 'Assets/02_Scripts', stats: { assemblies: projectAssemblies.length, files: files.length, classes: byId.size, lines: files.reduce((n,f)=>n+f.lineCount,0) }, assemblies: visibleAssemblies, classes: [...byId.values()], files, relations, diagnostics: { ambiguousNamesSkipped: ambiguous, note: '程序集关系来自 asmdef；类、调用、事件、读写为词法静态候选。未解析重载/动态派发/反射/条件编译；不是完整语义调用图。' } };
}
async function read(file, fallback) {
  try { return JSON.parse((await fs.readFile(file, 'utf8')).replace(/^\uFEFF/, '')); }
  catch (e) { if (e.code === 'ENOENT') return fallback; throw new Error(`${path.basename(file)} 无法读取：${e.message}`); }
}
async function atomic(file, value) {
  const tmp = file + '.' + crypto.randomUUID() + '.tmp';
  await fs.writeFile(tmp, JSON.stringify(value, null, 2) + '\n', 'utf8');
  await fs.rename(tmp, file);
}
function validateLayout(layout) {
  if (layout?.schemaVersion !== 1 || !layout.views || Array.isArray(layout.views)) throw new Error('布局需要 schemaVersion:1 和 views 对象');
  for (const v of Object.values(layout.views)) {
    if (!v || !Number.isFinite(v.zoom) || v.zoom < .1 || v.zoom > 4 || !Number.isFinite(v.pan?.x) || !Number.isFinite(v.pan?.y) || !v.nodes) throw new Error('布局视口无效');
    for (const n of Object.values(v.nodes)) if (!Number.isFinite(n.x) || !Number.isFinite(n.y)) throw new Error('节点坐标必须是有限数值');
  }
}
const fingerprint = value => crypto.createHash('sha256').update(JSON.stringify(value)).digest('hex');
let queue = Promise.resolve();
function enqueue(fn) { const next = queue.then(fn); queue = next.catch(()=>{}); return next; }
async function saveLayout(dir, body) {
  return enqueue(async () => {
    validateLayout(body.layout);
    const file = path.join(dir, names.layout), current = await read(file, { revision: 0 });
    if (body.revision !== (current.revision || 0) || body.token !== fingerprint(current)) throw new Error('布局已在另一页面或 JSON 中更新，请重新读取后再保存');
    const result = { ...body.layout, revision: (current.revision || 0) + 1 };
    await atomic(file, result); return { layout: result, token:fingerprint(result) };
  });
}
async function scanAndSave(root, dir) {
  return enqueue(async () => {
    await fs.mkdir(dir, { recursive: true });
    const file = path.join(dir, names.generated), old = await read(file, null), next = await scan(root);
    const live = new Set([...next.assemblies.map(a=>a.id), ...next.classes.map(c=>c.id)]);
    next.orphans = [...(old?.orphans || []), ...(old?.classes || []), ...(old?.assemblies || [])].filter(n => !live.has(n.id));
    next.orphans = [...new Map(next.orphans.map(n=>[n.id,n])).values()];
    if (old) await atomic(file + '.bak', old);
    await atomic(file, next); return next;
  });
}
async function readBundle(root, dir) {
  await fs.mkdir(dir, { recursive: true });
  let generated = await read(path.join(dir, names.generated), null);
  if (!generated || generated.schemaVersion !== 2) generated = await scanAndSave(root, dir);
  const layout = await read(path.join(dir, names.layout), { schemaVersion: 1, revision: 0, views: {} });
  validateLayout(layout);
  const manual = await read(path.join(dir, names.manual), { schemaVersion: 1, relations: [], notes: [] });
  if (!Array.isArray(manual.relations) || !Array.isArray(manual.notes)) throw new Error('人工 JSON 的 relations 和 notes 必须是数组');
  for (const r of manual.relations) if (!r.id || !r.source || !r.target || !['assembly','class','call','event','data'].includes(r.kind)) throw new Error('人工关系需要 id/source/target 和有效 kind');
  return { generated, layout, manual, layoutToken:fingerprint(layout) };
}
module.exports = { scan, parseSource, mask, readBundle, scanAndSave, saveLayout, validateLayout };
