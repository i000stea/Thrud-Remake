// 依赖检查与首次安装。供 start-progress-web.cmd 调用；也可单独运行：
//   node scripts/ensure-deps.js            # 需要时安装
//   node scripts/ensure-deps.js --check    # 只检查，不安装
// 判定依据：node_modules 中每个依赖存在 package.json，且安装戳等于 package-lock.json 的哈希。
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const { spawnSync } = require('node:child_process');

const ROOT = path.resolve(__dirname, '..');
const pkg = JSON.parse(fs.readFileSync(path.join(ROOT, 'package.json'), 'utf8'));
const dependencies = Object.keys(pkg.dependencies || {});
const lockFile = path.join(ROOT, 'package-lock.json');
const stampFile = path.join(ROOT, 'node_modules', '.thrud-install-stamp');
const checkOnly = process.argv.includes('--check');

function lockHash() {
  if (!fs.existsSync(lockFile)) return null;
  return crypto.createHash('sha256').update(fs.readFileSync(lockFile)).digest('hex');
}

function missingDependencies() {
  return dependencies.filter((name) =>
    !fs.existsSync(path.join(ROOT, 'node_modules', name, 'package.json')));
}

function stampMatches(hash) {
  if (!hash) return false;
  try {
    return fs.readFileSync(stampFile, 'utf8').trim() === hash;
  } catch {
    return false;
  }
}

function runNpm(args) {
  const command = process.platform === 'win32' ? 'npm.cmd' : 'npm';
  // 用 shell 执行单条命令字符串，避免 spawnSync(shell:true, args[]) 的 DEP0190 警告。
  const result = spawnSync(`${command} ${[...args, '--no-audit', '--no-fund'].join(' ')}`, {
    cwd: ROOT,
    stdio: 'inherit',
    shell: true,
  });
  if (result.error) throw result.error;
  if (result.status !== 0) throw new Error(`npm ${args.join(' ')} 退出码 ${result.status}`);
}

function main() {
  const hash = lockHash();
  const missing = missingDependencies();

  if (!missing.length && stampMatches(hash)) {
    console.log('[progress-web] 依赖已就绪，跳过安装。');
    return;
  }

  if (checkOnly) {
    throw new Error(`依赖未就绪（缺失 ${missing.length} 个${hash ? '' : '，且缺少 package-lock.json'}），请先运行 start-progress-web.cmd 安装。`);
  }

  const mode = hash ? 'ci' : 'install';
  console.log(`[progress-web] 依赖未就绪（缺失 ${missing.length || '无'}，锁文件 ${hash ? '存在' : '缺失'}），执行 npm ${mode}…`);
  runNpm([mode]);

  const stillMissing = missingDependencies();
  if (stillMissing.length) throw new Error(`安装后仍缺少：${stillMissing.join(', ')}`);

  if (hash) fs.writeFileSync(stampFile, hash + '\n', 'utf8');
  console.log('[progress-web] 依赖安装完成。');
}

try {
  main();
} catch (error) {
  console.error('[progress-web]', error.message);
  process.exitCode = 1;
}
