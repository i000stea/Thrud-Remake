// K0 基准：备份三份 JSON 并统计当前图规模。只读 generated/manual/layout，不改写它们。
// 用法：node scripts/baseline-stats.js
const fs = require('node:fs/promises');
const path = require('node:path');

const ROOT = path.resolve(__dirname, '..');
const DATA = path.join(ROOT, 'data');
const FILES = {
  generated: 'code-atlas.generated.json',
  layout: 'code-atlas.layout.json',
  manual: 'code-atlas.manual.json',
};

async function readJson(file, fallback) {
  try {
    return JSON.parse((await fs.readFile(file, 'utf8')).replace(/^\uFEFF/, ''));
  } catch (e) {
    if (e.code === 'ENOENT') return fallback;
    throw new Error(`${path.basename(file)} 无法读取：${e.message}`);
  }
}

function relationStats(relations) {
  const byKind = {};
  const pairs = new Set();
  for (const r of relations) {
    byKind[r.kind] = (byKind[r.kind] || 0) + 1;
    pairs.add(`${r.source} -> ${r.target}`);
  }
  return { total: relations.length, byKind, aggregatedPairs: pairs.size };
}

function viewStats(generated, layout, view) {
  const v = layout?.views?.[view];
  const assembly = view === 'assembly' ? null : view.slice(6);
  const orphans = generated.orphans || [];
  const own = new Set(
    (assembly ? [...(generated.classes || []), ...orphans.filter((n) => n.assembly)]
      : [...(generated.assemblies || []), ...orphans.filter((n) => !n.assembly)])
      .filter((n) => (assembly ? n.assembly === assembly : !n.external))
      .map((n) => n.id));
  const relations = (generated.relations || []).filter((r) =>
    (assembly ? r.kind !== 'assembly' : r.kind === 'assembly') && (own.has(r.source) || own.has(r.target)));
  const pairs = new Set(relations.map((r) => `${r.source} -> ${r.target}`));
  return {
    view,
    nodes: Object.keys(v?.nodes || {}).length,
    relationDetails: relations.length,
    aggregatedEdges: pairs.size,
    zoom: v?.zoom ?? null,
  };
}

async function main() {
  const generated = await readJson(path.join(DATA, FILES.generated), null);
  const layout = await readJson(path.join(DATA, FILES.layout), null);
  const manual = await readJson(path.join(DATA, FILES.manual), null);
  if (!generated) throw new Error('缺少 code-atlas.generated.json，无法建立基准。');

  const stamp = new Date().toISOString().replace(/[:.]/g, '-');
  const backupDir = path.join(DATA, 'backups', `baseline-${stamp}`);
  await fs.mkdir(backupDir, { recursive: true });
  const backedUp = [];
  for (const name of Object.values(FILES)) {
    try {
      await fs.copyFile(path.join(DATA, name), path.join(backupDir, name));
      backedUp.push(name);
    } catch (e) {
      if (e.code !== 'ENOENT') throw e;
    }
  }

  const baseline = {
    capturedAt: new Date().toISOString(),
    generatedAt: generated.generatedAt,
    scanStats: generated.stats,
    classCount: (generated.classes || []).length,
    orphanCount: (generated.orphans || []).length,
    relationStats: relationStats(generated.relations || []),
    views: Object.keys(layout?.views || {}).map((view) => viewStats(generated, layout, view)),
    manual: {
      relations: manual?.relations?.length ?? 0,
      notes: manual?.notes?.length ?? 0,
    },
    layoutRevision: layout?.revision ?? null,
    layoutSchemaVersion: layout?.schemaVersion ?? null,
  };

  await fs.writeFile(path.join(backupDir, 'baseline-stats.json'), JSON.stringify(baseline, null, 2) + '\n', 'utf8');
  console.log(JSON.stringify({ backupDir: path.relative(ROOT, backupDir), backedUp, ...baseline }, null, 2));
}

main().catch((e) => {
  console.error('[atlas-baseline]', e.message);
  process.exitCode = 1;
});
