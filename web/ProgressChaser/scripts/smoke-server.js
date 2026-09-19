// 冒烟测试：以 --no-open 启动本地服务，验证 /api/health、/vendor 依赖与 /api/code-atlas，然后关闭。
// 用法：node scripts/smoke-server.js
const { spawn } = require('node:child_process');
const path = require('node:path');

const ROOT = path.resolve(__dirname, '..');
const PORT = Number(process.env.PROGRESS_PORT || 3219);
const BASE = `http://127.0.0.1:${PORT}`;

async function waitForHealth(timeoutMs = 15000) {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    try {
      const response = await fetch(`${BASE}/api/health`);
      if (response.ok) return await response.json();
    } catch {
      // 服务还没起来，继续等
    }
    await new Promise((resolve) => setTimeout(resolve, 200));
  }
  throw new Error('服务未在超时时间内就绪');
}

async function main() {
  const child = spawn(process.execPath, ['server.js', '--no-open'], {
    cwd: ROOT,
    env: { ...process.env, PROGRESS_PORT: String(PORT) },
    stdio: ['ignore', 'pipe', 'pipe'],
  });
  let log = '';
  child.stdout.on('data', (d) => { log += d; });
  child.stderr.on('data', (d) => { log += d; });

  try {
    const health = await waitForHealth();
    if (!health.ok) throw new Error('/api/health 未返回 ok');
    console.log('health:', JSON.stringify(health));

    for (const file of ['cytoscape.min.js', 'cytoscape-elk.js', 'elk-api.js', 'elk.bundled.js', 'elk-worker.min.js']) {
      const response = await fetch(`${BASE}/vendor/${file}`);
      const body = await response.arrayBuffer();
      if (!response.ok) throw new Error(`/vendor/${file} -> ${response.status}`);
      console.log(`vendor ${file}: ${response.status} ${response.headers.get('content-type')} ${body.byteLength} bytes`);
    }

    for (const file of ['code-atlas.html', 'code-atlas.js', 'code-atlas.css', 'atlas-view.js', 'atlas-model.js']) {
      const response = await fetch(`${BASE}/${file}`);
      const body = await response.text();
      if (!response.ok) throw new Error(`/${file} -> ${response.status}`);
      console.log(`static ${file}: ${response.status} ${body.length} chars`);
    }

    const response = await fetch(`${BASE}/api/code-atlas`);
    const bundle = await response.json();
    if (!response.ok || !bundle.generated) throw new Error('/api/code-atlas 返回异常');
    console.log(`atlas: ${response.status} classes=${bundle.generated.stats?.classes} views=${Object.keys(bundle.layout?.views || {}).length} layoutSchema=v${bundle.layout?.schemaVersion} revision=${bundle.layout?.revision} token=${String(bundle.layoutToken).slice(0, 12)}…`);
    console.log('SMOKE-OK');
  } finally {
    child.kill();
    await new Promise((resolve) => setTimeout(resolve, 400));
    if (child.exitCode === null) child.kill('SIGKILL');
  }
}

main().catch((error) => {
  console.error('SMOKE-FAIL:', error.message);
  process.exitCode = 1;
});
