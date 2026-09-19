/* ELK 布局 Worker。
 * 主线程每次发送 { requestId, graph }，这里返回 { requestId, type, result|error }。
 * ELK 不支持中途取消，取消策略由主线程 terminate 并重建 Worker 实现；
 * 过期结果由主线程按 requestId 丢弃，这里不做额外判断。 */
importScripts('/vendor/elk.bundled.js');

const ELKClass = self.ELK || (typeof ELK !== 'undefined' ? ELK : null);
const elk = ELKClass ? new ELKClass() : null;

function reply(message) {
  self.postMessage(message);
}

self.onmessage = async (event) => {
  const data = event.data || {};
  if (data.type !== 'layout') return;
  const { requestId, graph } = data;
  if (!elk) {
    reply({ requestId, type: 'error', error: 'ELK 未在 Worker 中加载成功' });
    return;
  }
  try {
    const result = await elk.layout(graph);
    reply({ requestId, type: 'layout', result });
  } catch (error) {
    reply({ requestId, type: 'error', error: String((error && error.message) || error) });
  }
};
