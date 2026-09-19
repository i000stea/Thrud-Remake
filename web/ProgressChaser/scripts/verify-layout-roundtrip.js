// 校验布局读写闭环：读取 -> 保存 -> 再读取坐标一致；旧 token 保存必须被拒绝。
// 用法：node scripts/verify-layout-roundtrip.js
const { spawn } = require('node:child_process');
const path = require('node:path');

const ROOT = path.resolve(__dirname, '..');
const PORT = Number(process.env.PROGRESS_PORT || 3218);
const BASE = `http://127.0.0.1:${PORT}`;

async function waitForHealth(timeoutMs = 15000) {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    try {
      const response = await fetch(`${BASE}/api/health`);
      if (response.ok) return;
    } catch { /* 继续等 */ }
    await new Promise((resolve) => setTimeout(resolve, 200));
  }
  throw new Error('服务未就绪');
}

async function main() {
  const child = spawn(process.execPath, ['server.js', '--no-open'], {
    cwd: ROOT,
    env: { ...process.env, PROGRESS_PORT: String(PORT) },
    stdio: ['ignore', 'pipe', 'pipe'],
  });
  try {
    await waitForHealth();
    const first = await (await fetch(`${BASE}/api/code-atlas`)).json();
    const before = first.layout.views.assembly?.nodes || {};
    const sampleId = Object.keys(before)[0];
    const sample = sampleId ? before[sampleId] : null;
    console.log(`读取: schema=v${first.layout.schemaVersion} revision=${first.layout.revision} assemblyNodes=${Object.keys(before).length}`);

    const saved = await (await fetch(`${BASE}/api/code-atlas/layout`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ revision: first.layout.revision, token: first.layoutToken, layout: first.layout }),
    })).json();
    if (!saved.layout) throw new Error('保存失败：' + JSON.stringify(saved));
    console.log(`保存: schema=v${saved.layout.schemaVersion} revision=${saved.layout.revision}`);

    const second = await (await fetch(`${BASE}/api/code-atlas`)).json();
    const after = second.layout.views.assembly?.nodes || {};
    if (sampleId && JSON.stringify(after[sampleId]) !== JSON.stringify(sample)) {
      throw new Error(`坐标丢失：${sampleId} 保存前 ${JSON.stringify(sample)} 保存后 ${JSON.stringify(after[sampleId])}`);
    }
    console.log(`重读: revision=${second.layout.revision} 坐标一致 (${sampleId || '无节点'})`);

    // 用过期 token 再保存一次，必须被拒绝。
    const stale = await fetch(`${BASE}/api/code-atlas/layout`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ revision: first.layout.revision, token: first.layoutToken, layout: first.layout }),
    });
    if (stale.ok) throw new Error('过期 token 保存未被拒绝');
    const staleBody = await stale.json();
    console.log(`冲突拒绝: HTTP ${stale.status} → ${staleBody.message}`);

    console.log('ROUNDTRIP-OK');
  } finally {
    child.kill();
    await new Promise((resolve) => setTimeout(resolve, 400));
    if (child.exitCode === null) child.kill('SIGKILL');
  }
}

main().catch((error) => {
  console.error('ROUNDTRIP-FAIL:', error.message);
  process.exitCode = 1;
});
