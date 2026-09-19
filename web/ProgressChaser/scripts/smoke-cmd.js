// 验证一键启动脚本：真实调用 start-progress-web.cmd --no-open，
// 等 /api/health 就绪后关闭整棵进程树。用法：node scripts/smoke-cmd.js
const { spawn, spawnSync } = require('node:child_process');
const path = require('node:path');

const ROOT = path.resolve(__dirname, '..');
const PORT = Number(process.env.PROGRESS_PORT || 3217);

async function waitForHealth(timeoutMs = 25000) {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    try {
      const response = await fetch(`http://127.0.0.1:${PORT}/api/health`);
      if (response.ok) return await response.json();
    } catch { /* 继续等 */ }
    await new Promise((resolve) => setTimeout(resolve, 250));
  }
  throw new Error('一键启动后服务未在超时时间内就绪');
}

async function main() {
  const child = spawn('cmd.exe', ['/c', 'start-progress-web.cmd', '--no-open'], {
    cwd: ROOT,
    env: { ...process.env, PROGRESS_PORT: String(PORT) },
    stdio: ['ignore', 'pipe', 'pipe'],
    windowsHide: true,
  });
  let log = '';
  child.stdout.on('data', (d) => { log += d; });
  child.stderr.on('data', (d) => { log += d; });

  try {
    const health = await waitForHealth();
    console.log('cmd stdout/stderr:');
    console.log(log.trim());
    console.log('health:', JSON.stringify(health));
    console.log('CMD-SMOKE-OK');
  } finally {
    // 关闭 cmd 及其派生的 node，避免残留常驻服务。
    spawnSync('taskkill', ['/PID', String(child.pid), '/T', '/F'], { stdio: 'ignore' });
    await new Promise((resolve) => setTimeout(resolve, 400));
  }
}

main().catch((error) => {
  console.error('CMD-SMOKE-FAIL:', error.message);
  process.exitCode = 1;
});
