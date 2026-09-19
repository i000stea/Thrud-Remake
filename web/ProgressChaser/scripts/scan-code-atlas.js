// 重建 code-atlas.generated.json。整体重写自动扫描结果，绝不触碰 manual / layout。
// 用法：node scripts/scan-code-atlas.js   （等价于 npm run scan）
const path = require('node:path');
const core = require('../atlas-core');

const PROJECT_DIR = path.resolve(__dirname, '..', '..', '..');
const ATLAS_DIR = path.resolve(__dirname, '..', 'data');

core.scanAndSave(PROJECT_DIR, ATLAS_DIR)
  .then((generated) => {
    const { files, classes, lines, assemblies } = generated.stats;
    console.log(`[scan] ${assemblies} 程序集 / ${files} 文件 / ${classes} 类型 / ${lines} 行`);
    console.log(`[scan] ${generated.relations.length} 条关系 -> data/code-atlas.generated.json`);
    if (generated.orphans?.length) console.log(`[scan] 保留 ${generated.orphans.length} 个孤立节点`);
  })
  .catch((error) => {
    console.error('[scan] 失败：', error.message);
    process.exitCode = 1;
  });
