const fs = require("node:fs/promises");
const path = require("node:path");
const { randomUUID } = require("node:crypto");

const ROOT_DIR = __dirname;
const DOC_TASK_FILE = path.join(ROOT_DIR, "..", "Docs", "99_下一步行动", "README.md");
const OUTPUT_FILE = path.join(ROOT_DIR, "data", "progress.json");

function stripMarkdown(text) {
  return String(text || "")
    .replace(/`([^`]+)`/g, "$1")
    .replace(/\*\*([^*]+)\*\*/g, "$1")
    .replace(/\*([^*]+)\*/g, "$1")
    .replace(/~~([^~]+)~~/g, "$1")
    .replace(/\[[ xX]\]\s*/g, "")
    .replace(/\([^)]*来源：[^)]*\)/g, "")
    .replace(/（[^）]*来源：[^）]*）/g, "")
    .replace(/\s+/g, " ")
    .trim();
}

function removeLeadingTicket(text) {
  return text
    .replace(/^[A-Z]\d(?:-\d+)?(?:[A-Z])?[·.、 ]*/u, "")
    .replace(/^T\d(?:\.\d+)?(?:[A-Z])?[·.、 ]*/u, "")
    .replace(/^[CP]\d(?:-\d+)?[·.、 ]*/u, "")
    .replace(/^⚠️\s*/u, "")
    .trim();
}

function toOralTitle(rawText) {
  const clean = stripMarkdown(rawText);
  const [rawHead, ...rest] = clean.split("：");
  const head = removeLeadingTicket(rawHead);
  const tail = rest.join("：").trim();

  const rules = [
    [/最小链路真机/u, "先把真机主流程跑通"],
    [/产出/u, "补一版构建说明和验收清单"],
    [/真机微调项/u, "把触屏细节再顺一遍"],
    [/触摸层真机回归/u, "在真机上把触摸层再验一遍"],
    [/TryEnsurePanelAvailable/u, "修掉结算和暂停面板的 AB 回收坑"],
    [/battle_runtime 挂 teamCatalog/u, "给 battle_runtime 挂上 teamCatalog"],
    [/可选清理/u, "顺手清掉旧的运行时适配器"],
    [/复用 level_01/u, "用 level_01 先搭出三段演示关"],
    [/数值按/u, "把战斗数值调回演示基线"],
    [/角色 SO/u, "把角色血量残留改回正式值"],
    [/失败\/全灭/u, "把失败和胜利收尾流程补顺"],
    [/编辑器执行一键构建出包/u, "在编辑器里完整跑一次出包"],
    [/真机回归清单/u, "补一版真机回归清单"],
    [/分发/u, "准备一版可直接外发的 APK"],
    [/切人键/u, "定一下切人按钮到底怎么做"],
    [/中央拖拽区/u, "把拖拽区大小和边缘热区定下来"],
    [/冲刺/u, "确定冲刺键到底单独放还是并到摇杆"],
    [/自动开火/u, "决定要不要加自动开火开关"],
    [/T3\.2/u, "开始做三种关卡模式和灰盒关卡"],
    [/T3\.3/u, "把正式战斗 HUD 补起来"],
    [/T3\.4/u, "把结算系统接起来"],
    [/Phase 4/u, "开始做手感和性能这轮收口"],
    [/Phase 5/u, "把后面的移动端和热更计划先挂上日程"],
    [/锁定手感/u, "把锁敌手感再拍板一次"],
    [/垂直切片角色/u, "把演示角色和武器最终定下来"],
    [/整场限时用途/u, "把整场限时的判定规则说死"],
    [/导弹被击落/u, "把导弹和地雷被打掉后的规则定死"],
    [/待用户执行/u, "先在编辑器里把 ConfigScene 生成出来"],
    [/C4/u, "把全局对象迁进 ConfigScene"],
    [/C6/u, "把场景切换改成 additive 流程"],
    [/C7/u, "把架构文档同步到现在的实现"],
    [/M0 其余/u, "把热更和构建矩阵先记成后续大项"],
    [/T0-1/u, "先把角色编目标成 battle_characters"],
    [/T0-2/u, "把角色资源 AB 重新构建并校验一遍"],
    [/已实测坑/u, "避开 Inspector 默认 AB 名这个坑"],
    [/提示/u, "记住编辑器联调不用重打 AB"],
    [/退出条件/u, "按退出条件把 P0 验收打满"],
    [/前置/u, "先把 P1 的前置检查跑完"],
    [/真机/u, "把 P1 真机回归完整跑一遍"],
    [/TryOpenAsync/u, "给面板打开流程补上异步版本"],
    [/调用点改造/u, "把主要面板入口都换到异步打开"],
    [/边界/u, "把 AB 异步改造的边界守住"],
    [/T3-1/u, "把编队面板上多余的 catalog 引用清掉"],
    [/T3-2/u, "把 UI 共享依赖包拆出来"],
    [/T3-3/u, "把不该打 AB 的面板和 LoadingGroup 收口"],
    [/必读/u, "先把伤害构成这个怀疑点查清楚再动手"],
    [/工作区 3 处保留修改/u, "把工作区残留改动收口后再提"],
    [/提交纪律/u, "提交时把业务改动和杂项改动分开"],
    [/正式投入使用/u, "把进度面板按正式用法再联调一遍"],
    [/07：T2\.1\/T2\.2/u, "把 07 文档里漏勾的两张卡补回去"],
    [/M1_TeamSelect/u, "把 M1_TeamSelect 的旧验收记录清一下"],
    [/BattleTeamSelectPanel/u, "确认旧的 BattleTeamSelectPanel 资源有没有删掉"],
    [/跨引用旧名/u, "把文档里旧的 Modules 路径名顺手改掉"],
  ];

  for (const [pattern, title] of rules) {
    if (pattern.test(clean)) return title;
  }

  if (tail) {
    return `把${head}这块处理掉`;
  }

  return `处理：${head}`;
}

function toOralDescription(rawText) {
  const clean = stripMarkdown(rawText);
  const [head, ...rest] = clean.split("：");
  const tail = rest.join("：").trim();
  const plainHead = removeLeadingTicket(head);

  if (tail) {
    return `现在要跟进的是：${tail}。简单说，就是把“${plainHead}”这件事真正跑通。`;
  }

  return `这项还没落地，按文档继续推进“${plainHead}”这件事。`;
}

function inferTaskType(text, sectionTitle) {
  const source = `${sectionTitle} ${text}`;
  if (/文档|README|回填|同步|归档|清单|说明/u.test(source)) return "文档整理";
  if (/手感|热区|拖拽区|自动开火|切人键|输入适配|用户体验/u.test(source)) return "用户体验";
  if (/真机|回归|验证|检查|退出条件|验收|Play/u.test(source)) return "测试验证";
  if (/隐患|问题|修|清理|NRE|剥离|滞后|删除|坑/u.test(source)) return "Bug修复";
  if (/数值|配置/u.test(source)) return "数据配置";
  return "模块开发";
}

function inferPriority(text, sectionTitle) {
  const source = `${sectionTitle} ${text}`;
  if (/最小链路真机|前置|阻断|必须|真机|出包|battle_characters|teamCatalog|隐患待修|退出条件/u.test(source)) {
    return "高";
  }
  if (/长期项|远期|M2|M3|不阻塞/u.test(source)) {
    return "低";
  }
  if (/提示|可选|顺手|文档一致性/u.test(source)) {
    return "中";
  }
  return "中";
}

function inferStatus(text, sectionTitle) {
  const source = `${sectionTitle} ${text}`;
  if (/Demo 线|真机资源与战斗装配|最小链路真机|触摸层真机回归|battle_characters|teamCatalog|出包|真机/u.test(source)) {
    return "进行中";
  }
  return "未开始";
}

function buildTasksFromDoc(markdown) {
  const lines = markdown.split(/\r?\n/);
  const tasks = [];
  let sectionTitle = "";
  let canCollect = false;

  for (const line of lines) {
    if (line.startsWith("## ")) {
      if (/^## 3\./u.test(line) || /^## 5\./u.test(line)) {
        canCollect = true;
      } else {
        canCollect = false;
      }
    }

    if (line.startsWith("### ")) {
      sectionTitle = stripMarkdown(line.replace(/^###\s*/, ""));
      continue;
    }

    if (!canCollect) continue;

    let rawTask = "";
    if (line.trim().startsWith("- [ ]")) {
      rawTask = line.replace(/^\s*-\s+\[\s\]\s*/, "").trim();
    } else if (/^\|\s*\[\s\]\s*/u.test(line.trim())) {
      rawTask = line
        .trim()
        .replace(/^\|\s*\[\s\]\s*/, "")
        .split("|")[0]
        .trim();
    } else {
      continue;
    }

    const cleanTask = stripMarkdown(rawTask);

    tasks.push({
      id: randomUUID(),
      title: toOralTitle(cleanTask),
      taskType: inferTaskType(cleanTask, sectionTitle),
      description: toOralDescription(cleanTask),
      status: inferStatus(cleanTask, sectionTitle),
      priority: inferPriority(cleanTask, sectionTitle),
      owner: "Tea",
      updatedAt: new Date().toISOString(),
      source: sectionTitle,
      raw: cleanTask,
    });
  }

  return tasks;
}

async function generateProgressData() {
  const markdown = await fs.readFile(DOC_TASK_FILE, "utf8");
  const tasks = buildTasksFromDoc(markdown);
  return {
    meta: {
      title: "Thrud Remake 进度监控表",
      updatedAt: new Date().toISOString(),
      source: "Docs/99_下一步行动/README.md",
      taskCount: tasks.length,
    },
    tasks,
  };
}

async function writeProgressData() {
  const data = await generateProgressData();
  await fs.mkdir(path.dirname(OUTPUT_FILE), { recursive: true });
  await fs.writeFile(OUTPUT_FILE, JSON.stringify(data, null, 2), "utf8");
  return data;
}

if (require.main === module) {
  writeProgressData()
    .then((data) => {
      console.log(`[sync-doc-tasks] 已写入 ${data.tasks.length} 条任务到 progress.json`);
    })
    .catch((error) => {
      console.error("[sync-doc-tasks] 生成失败:", error);
      process.exit(1);
    });
}

module.exports = {
  generateProgressData,
  writeProgressData,
};
