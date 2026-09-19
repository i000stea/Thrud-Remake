const THEME_STORAGE_KEY = "progress-web-theme";
// 行内改动（状态 / 优先级 / 排序）采用防抖写盘：停止操作 5 秒后统一写入，
// 期间再有新改动就重新计时。
const SAVE_DEBOUNCE_MS = 5000;
const SAVE_COUNTDOWN_TICK_MS = 250;
const state = {
  tasks: [],
  keyword: "",
  sortKey: "priority",
  sortDirection: "desc",
  hideCompleted: false,
  editingTaskId: "",
  theme: "dark",
  filters: {
    statuses: new Set(),
    taskTypes: new Set(),
  },
  // 待写入的改动：taskId -> 该任务被改动的字段（同一任务多次改动会合并成一次 PATCH）
  pendingPatches: new Map(),
  saveTimerId: null,
  saveCountdownId: null,
  lastChangeAt: 0,
  flushPromise: null,
};

const STATUS_OPTIONS = ["未开始", "进行中", "已完成"];
const PRIORITY_OPTIONS = ["低", "中", "高", "紧急"];
const TASK_TYPE_OPTIONS = ["模块开发", "Bug修复", "用户体验", "文档整理", "数据配置", "测试验证", "其他"];

const PRIORITY_ORDER = {
  紧急: 4,
  高: 3,
  中: 2,
  低: 1,
};

// 排序拖拽条：0 表示「特别紧急」，数值越小越靠前
const SORT_ORDER_MIN = 0;
const SORT_ORDER_MAX = 10;
const DEFAULT_SORT_ORDER = 5;

function normalizeSortOrder(value) {
  const numeric = Number(value);
  if (!Number.isFinite(numeric)) return DEFAULT_SORT_ORDER;
  return Math.min(SORT_ORDER_MAX, Math.max(SORT_ORDER_MIN, Math.round(numeric)));
}

// 统一成「值越大越靠前」的权重，这样升/降序逻辑和优先级保持一致
function getOrderRank(task) {
  return SORT_ORDER_MAX - normalizeSortOrder(task.sortOrder);
}

const STATUS_ORDER = {
  进行中: 3,
  未开始: 2,
  已完成: 1,
};

const TYPE_ORDER = {
  模块开发: 1,
  Bug修复: 2,
  用户体验: 3,
  文档整理: 4,
  数据配置: 5,
  测试验证: 6,
  其他: 7,
};

const elements = {
  body: document.body,
  stats: document.getElementById("stats"),
  form: document.getElementById("taskForm"),
  formSortOrderValue: document.getElementById("formSortOrderValue"),
  tableBody: document.getElementById("taskTableBody"),
  template: document.getElementById("taskRowTemplate"),
  searchInput: document.getElementById("searchInput"),
  statusFilterGroup: document.getElementById("statusFilterGroup"),
  typeFilterGroup: document.getElementById("typeFilterGroup"),
  saveState: document.getElementById("saveState"),
  reloadButton: document.getElementById("reloadButton"),
  toggleCompletedButton: document.getElementById("toggleCompletedButton"),
  themeToggleButton: document.getElementById("themeToggleButton"),
  flushButton: document.getElementById("flushButton"),
  openCreateButton: document.getElementById("openCreateButton"),
  modal: document.getElementById("taskModal"),
  modalTitle: document.getElementById("modalTitle"),
  modalSubtitle: document.getElementById("modalSubtitle"),
  closeModalButton: document.getElementById("closeModalButton"),
  cancelModalButton: document.getElementById("cancelModalButton"),
  submitTaskButton: document.getElementById("submitTaskButton"),
  sortButtons: Array.from(document.querySelectorAll(".sort-button")),
};

function setSaveState(text, type = "default") {
  elements.saveState.textContent = text;
  const colorMap = {
    default: "var(--text-muted)",
    loading: "var(--warning)",
    pending: "var(--accent)",
    success: "var(--success)",
    error: "var(--danger)",
  };
  elements.saveState.style.color = colorMap[type] || colorMap.default;
}

function escapeHtml(value) {
  return String(value || "")
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;")
    .replace(/'/g, "&#39;");
}

function formatDateTime(value) {
  if (!value) return "-";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return date.toLocaleString("zh-CN", { hour12: false });
}

function getThemeLabel() {
  return state.theme === "dark" ? "切换亮色" : "切换暗色";
}

function applyTheme(theme) {
  state.theme = theme === "light" ? "light" : "dark";
  elements.body.dataset.theme = state.theme;
  elements.themeToggleButton.textContent = getThemeLabel();
  window.localStorage.setItem(THEME_STORAGE_KEY, state.theme);
}

function initializeTheme() {
  const storedTheme = window.localStorage.getItem(THEME_STORAGE_KEY) || "dark";
  applyTheme(storedTheme);
}

function renderStats() {
  const total = state.tasks.length;
  const doing = state.tasks.filter((item) => item.status === "进行中").length;
  const todo = state.tasks.filter((item) => item.status === "未开始").length;
  const done = state.tasks.filter((item) => item.status === "已完成").length;

  const items = [
    { label: "总任务数", value: total },
    { label: "进行中", value: doing },
    { label: "未开始", value: todo },
    { label: "已完成", value: done },
  ];

  elements.stats.innerHTML = items
    .map(
      (item) => `
        <article class="stat">
          <span>${item.label}</span>
          <strong>${item.value}</strong>
        </article>
      `
    )
    .join("");
}

function compareBySort(a, b) {
  let result = 0;

  switch (state.sortKey) {
    case "priority": {
      result = (PRIORITY_ORDER[a.priority] || 0) - (PRIORITY_ORDER[b.priority] || 0);
      if (result === 0) {
        // 相同优先级下，再按排序数值（越小越靠前）
        result = getOrderRank(a) - getOrderRank(b);
      }
      break;
    }
    case "sortOrder": {
      result = getOrderRank(a) - getOrderRank(b);
      if (result === 0) {
        result = (PRIORITY_ORDER[a.priority] || 0) - (PRIORITY_ORDER[b.priority] || 0);
      }
      break;
    }
    case "status":
      result = (STATUS_ORDER[a.status] || 0) - (STATUS_ORDER[b.status] || 0);
      break;
    case "type":
      result = (TYPE_ORDER[a.taskType] || 99) - (TYPE_ORDER[b.taskType] || 99);
      break;
    case "title":
      result = String(a.title || "").localeCompare(String(b.title || ""), "zh-CN");
      break;
    case "updatedAt":
    default:
      result = new Date(a.updatedAt || 0).getTime() - new Date(b.updatedAt || 0).getTime();
      break;
  }

  if (result === 0) {
    result = new Date(b.updatedAt || 0).getTime() - new Date(a.updatedAt || 0).getTime();
  }

  return state.sortDirection === "asc" ? result : -result;
}

function getFilteredTasks() {
  const keyword = state.keyword.trim().toLowerCase();
  return state.tasks
    .filter((task) => !state.hideCompleted || task.status !== "已完成")
    .filter((task) => state.filters.statuses.size === 0 || state.filters.statuses.has(task.status))
    .filter((task) => state.filters.taskTypes.size === 0 || state.filters.taskTypes.has(task.taskType))
    .filter((task) => {
      if (!keyword) return true;
      return [task.title, task.description, task.owner, task.taskType]
        .filter(Boolean)
        .some((value) => String(value).toLowerCase().includes(keyword));
    })
    .sort(compareBySort);
}

function fillBadge(node, value) {
  node.textContent = value || "-";
  node.dataset.value = value || "";
}

function renderFilterGroup(container, options, selectedSet, groupName) {
  container.innerHTML = options.map((option) => {
    const activeClass = selectedSet.has(option) ? "is-active" : "";
    return `<button type="button" class="filter-chip ${activeClass}" data-filter-group="${groupName}" data-filter-value="${option}">${option}</button>`;
  }).join("");
}

function setInlineSelectValue(node, value) {
  node.value = value || "";
  node.dataset.value = value || "";
}

// 排序=0 的行标记为「特别紧急」：整行加独立动画，并在优先级旁显示标签
function applyOrderState(row, value) {
  const order = normalizeSortOrder(value);
  const valueNode = row.querySelector('[data-field="sortOrderValue"]');
  if (valueNode) valueNode.textContent = String(order);
  row.classList.toggle("is-priority-critical", order === 0);
  return order;
}

function renderTable() {
  const tasks = getFilteredTasks();
  elements.tableBody.innerHTML = "";

  if (!tasks.length) {
    elements.tableBody.innerHTML = `<tr><td colspan="9" class="empty">当前没有匹配任务</td></tr>`;
    return;
  }

  tasks.forEach((task) => {
    const fragment = elements.template.content.cloneNode(true);
    const row = fragment.querySelector("tr");
    row.dataset.id = task.id;

    if (task.priority === "高") row.classList.add("is-priority-high");
    if (task.priority === "紧急") row.classList.add("is-priority-urgent");
    if (task.status === "进行中") row.classList.add("is-status-doing");
    if (task.status === "已完成") row.classList.add("is-status-done");

    row.querySelector('[data-field="title"]').textContent = task.title || "-";
    row.querySelector('[data-field="description"]').textContent = task.description || "-";
    fillBadge(row.querySelector('[data-field="taskType"]'), task.taskType || "其他");
    const statusSelect = row.querySelector('[data-field="status"]');
    const prioritySelect = row.querySelector('[data-field="priority"]');
    const orderSlider = row.querySelector('[data-field="sortOrder"]');
    setInlineSelectValue(statusSelect, task.status || "未开始");
    setInlineSelectValue(prioritySelect, task.priority || "中");
    orderSlider.value = String(normalizeSortOrder(task.sortOrder));
    applyOrderState(row, orderSlider.value);
    row.querySelector('[data-field="owner"]').textContent = task.owner || "-";
    row.querySelector('[data-field="updatedAt"]').textContent = formatDateTime(task.updatedAt);

    statusSelect.addEventListener("change", () => {
      statusSelect.dataset.value = statusSelect.value;
      schedulePatch(task.id, { status: statusSelect.value });
    });

    prioritySelect.addEventListener("change", () => {
      prioritySelect.dataset.value = prioritySelect.value;
      schedulePatch(task.id, { priority: prioritySelect.value });
    });

    // 拖动时只更新本行视觉（不重绘表格，避免打断拖拽），松手后交给防抖写入
    orderSlider.addEventListener("input", () => {
      const sortOrder = applyOrderState(row, orderSlider.value);
      schedulePatch(task.id, { sortOrder });
    });

    row.querySelector('[data-action="edit"]').addEventListener("click", () => {
      openModal(task);
    });

    row.querySelector('[data-action="delete"]').addEventListener("click", () => {
      if (!window.confirm(`确定删除任务“${task.title}”吗？`)) return;
      deleteTask(task.id);
    });

    elements.tableBody.appendChild(fragment);
  });
}

function renderButtons() {
  elements.toggleCompletedButton.textContent = state.hideCompleted ? "显示已完成" : "隐藏已完成";
  elements.themeToggleButton.textContent = getThemeLabel();
  elements.flushButton.disabled = state.pendingPatches.size === 0;
  elements.sortButtons.forEach((button) => {
    const isActive = button.dataset.sortKey === state.sortKey;
    button.classList.toggle("is-active", isActive);
    button.classList.toggle("is-desc", isActive && state.sortDirection === "desc");
  });
}

function renderFilters() {
  renderFilterGroup(elements.statusFilterGroup, STATUS_OPTIONS, state.filters.statuses, "statuses");
  renderFilterGroup(elements.typeFilterGroup, TASK_TYPE_OPTIONS, state.filters.taskTypes, "taskTypes");
}

function renderAll() {
  renderStats();
  renderFilters();
  renderTable();
  renderButtons();
}

function resetForm(task = null) {
  elements.form.reset();
  const taskIdInput = elements.form.elements.id;
  taskIdInput.value = task?.id || "";
  elements.form.elements.title.value = task?.title || "";
  elements.form.elements.taskType.value = task?.taskType || "模块开发";
  elements.form.elements.status.value = task?.status || "进行中";
  elements.form.elements.priority.value = task?.priority || "中";
  setFormSortOrder(normalizeSortOrder(task?.sortOrder));
  elements.form.elements.owner.value = task?.owner || "";
  elements.form.elements.description.value = task?.description || "";
}

function setFormSortOrder(value) {
  const order = normalizeSortOrder(value);
  elements.form.elements.sortOrder.value = String(order);
  elements.formSortOrderValue.textContent = String(order);
}

function openModal(task = null) {
  state.editingTaskId = task?.id || "";
  elements.modal.classList.remove("hidden");
  elements.modal.setAttribute("aria-hidden", "false");
  elements.modalTitle.textContent = task ? "编辑任务" : "新增任务";
  elements.modalSubtitle.textContent = task ? "修改后会立即写回本地 `json` 文件。" : "保存后会立即写回本地 `json` 文件。";
  elements.submitTaskButton.textContent = task ? "保存修改" : "新增任务";
  resetForm(task);
}

function closeModal() {
  elements.modal.classList.add("hidden");
  elements.modal.setAttribute("aria-hidden", "true");
  state.editingTaskId = "";
}

async function request(url, options = {}) {
  const response = await fetch(url, {
    headers: {
      "Content-Type": "application/json",
      ...(options.headers || {}),
    },
    ...options,
  });

  const data = await response.json().catch(() => ({}));
  if (!response.ok) {
    throw new Error(data.message || "请求失败");
  }
  return data;
}

async function loadTasks() {
  setSaveState("读取中...", "loading");
  try {
    const data = await request("/api/progress", { method: "GET" });
    state.tasks = Array.isArray(data.tasks) ? data.tasks : [];
    renderAll();
    setSaveState(`已读取 ${state.tasks.length} 条任务`, "success");
  } catch (error) {
    setSaveState(error.message, "error");
  }
}

async function addTask(payload) {
  setSaveState("正在写入...", "loading");
  try {
    const data = await request("/api/tasks", {
      method: "POST",
      body: JSON.stringify(payload),
    });
    state.tasks.unshift(data.task);
    renderAll();
    closeModal();
    setSaveState("新增成功，已写入本地文件", "success");
  } catch (error) {
    setSaveState(error.message, "error");
  }
}

async function patchTask(id, payload, options = {}) {
  const { closeModalAfterSave = true } = options;
  setSaveState("正在保存修改...", "loading");
  try {
    const data = await request(`/api/tasks/${encodeURIComponent(id)}`, {
      method: "PATCH",
      body: JSON.stringify(payload),
    });
    const index = state.tasks.findIndex((task) => task.id === id);
    if (index >= 0) {
      state.tasks[index] = data.task;
    }
    renderAll();
    if (closeModalAfterSave) {
      closeModal();
    }
    setSaveState("修改已保存", "success");
  } catch (error) {
    setSaveState(error.message, "error");
  }
}

/* ---------- 防抖写盘：停止操作 5 秒后统一保存 ---------- */

function renderPendingState() {
  const count = state.pendingPatches.size;
  if (!count) {
    stopSaveCountdown();
    elements.flushButton.disabled = true;
    return;
  }
  const elapsed = Date.now() - state.lastChangeAt;
  const remainSeconds = Math.max(1, Math.ceil((SAVE_DEBOUNCE_MS - elapsed) / 1000));
  elements.flushButton.disabled = false;
  setSaveState(`未保存 ${count} 处改动 · ${remainSeconds}s 后自动写入`, "pending");
}

function stopSaveCountdown() {
  if (state.saveCountdownId) {
    window.clearInterval(state.saveCountdownId);
    state.saveCountdownId = null;
  }
}

function startSaveCountdown() {
  if (state.saveCountdownId) return;
  state.saveCountdownId = window.setInterval(() => {
    if (!state.pendingPatches.size) {
      stopSaveCountdown();
      return;
    }
    renderPendingState();
  }, SAVE_COUNTDOWN_TICK_MS);
}

// 本地先按新值生效（表格/弹窗立刻反映最新意图），写盘交给 5 秒防抖
function schedulePatch(taskId, patch) {
  const task = state.tasks.find((item) => item.id === taskId);
  if (task) Object.assign(task, patch);
  state.pendingPatches.set(taskId, { ...(state.pendingPatches.get(taskId) || {}), ...patch });

  state.lastChangeAt = Date.now();
  window.clearTimeout(state.saveTimerId);
  state.saveTimerId = window.setTimeout(() => {
    state.saveTimerId = null;
    flushPendingSaves();
  }, SAVE_DEBOUNCE_MS);

  startSaveCountdown();
  renderPendingState();
}

async function runFlush() {
  window.clearTimeout(state.saveTimerId);
  state.saveTimerId = null;
  stopSaveCountdown();
  elements.flushButton.disabled = true;

  const entries = Array.from(state.pendingPatches.entries());
  state.pendingPatches.clear();
  setSaveState(`正在写入 ${entries.length} 处改动...`, "loading");

  let saved = 0;
  const failed = [];
  let lastError = "";
  for (const [taskId, patch] of entries) {
    try {
      const data = await request(`/api/tasks/${encodeURIComponent(taskId)}`, {
        method: "PATCH",
        body: JSON.stringify(patch),
      });
      const index = state.tasks.findIndex((task) => task.id === taskId);
      if (index >= 0) {
        state.tasks[index] = data.task;
      }
      saved += 1;
    } catch (error) {
      failed.push([taskId, patch]);
      lastError = error.message;
    }
  }

  renderAll();

  if (failed.length) {
    // 保留改动不丢：重新排队，等待用户点「立即保存」或下次改动时重试
    failed.forEach(([taskId, patch]) => {
      state.pendingPatches.set(taskId, { ...(state.pendingPatches.get(taskId) || {}), ...patch });
    });
    elements.flushButton.disabled = false;
    setSaveState(`写入失败 ${failed.length} 处：${lastError}（改动仍保留，可点「立即保存」重试）`, "error");
    return;
  }

  if (state.pendingPatches.size) {
    // 写入期间又产生了新改动，继续按新的计时走
    renderPendingState();
    return;
  }

  setSaveState(saved ? `已保存 ${saved} 处改动` : "没有需要写入的改动", "success");
}

// 写入中的请求会被复用，避免「重新读取」与防抖定时器并发写同一批数据
function flushPendingSaves() {
  if (state.flushPromise) return state.flushPromise;
  if (!state.pendingPatches.size) return Promise.resolve();
  state.flushPromise = runFlush().finally(() => {
    state.flushPromise = null;
  });
  return state.flushPromise;
}

async function deleteTask(id) {
  state.pendingPatches.delete(id);
  setSaveState("正在删除...", "loading");
  try {
    await request(`/api/tasks/${encodeURIComponent(id)}`, { method: "DELETE" });
    state.tasks = state.tasks.filter((task) => task.id !== id);
    renderAll();
    setSaveState("任务已删除", "success");
  } catch (error) {
    setSaveState(error.message, "error");
  }
}

elements.form.addEventListener("submit", (event) => {
  event.preventDefault();
  const formData = new FormData(elements.form);
  const payload = {
    title: formData.get("title"),
    taskType: formData.get("taskType"),
    description: formData.get("description"),
    status: formData.get("status"),
    priority: formData.get("priority"),
    sortOrder: normalizeSortOrder(formData.get("sortOrder")),
    owner: formData.get("owner"),
  };

  if (state.editingTaskId) {
    // 弹窗是显式「保存」动作，立即写入；该任务此前排队的行内改动已被表单值覆盖
    state.pendingPatches.delete(state.editingTaskId);
    patchTask(state.editingTaskId, payload);
    return;
  }

  addTask(payload);
});

elements.form.elements.sortOrder.addEventListener("input", (event) => {
  elements.formSortOrderValue.textContent = String(normalizeSortOrder(event.target.value));
});

elements.searchInput.addEventListener("input", (event) => {
  state.keyword = event.target.value;
  renderTable();
});

elements.reloadButton.addEventListener("click", async () => {
  await flushPendingSaves();
  loadTasks();
});

elements.flushButton.addEventListener("click", () => {
  flushPendingSaves();
});

elements.toggleCompletedButton.addEventListener("click", () => {
  state.hideCompleted = !state.hideCompleted;
  renderAll();
});

elements.themeToggleButton.addEventListener("click", () => {
  applyTheme(state.theme === "dark" ? "light" : "dark");
});

elements.openCreateButton.addEventListener("click", () => {
  openModal();
});

elements.sortButtons.forEach((button) => {
  button.addEventListener("click", () => {
    const nextKey = button.dataset.sortKey;
    if (state.sortKey === nextKey) {
      state.sortDirection = state.sortDirection === "desc" ? "asc" : "desc";
    } else {
      state.sortKey = nextKey;
      state.sortDirection = nextKey === "title" || nextKey === "type" ? "asc" : "desc";
    }
    renderAll();
  });
});

[elements.statusFilterGroup, elements.typeFilterGroup].forEach((group) => {
  group.addEventListener("click", (event) => {
    const button = event.target.closest(".filter-chip");
    if (!button) return;
    const groupName = button.dataset.filterGroup;
    const value = button.dataset.filterValue;
    const targetSet = state.filters[groupName];
    if (targetSet.has(value)) {
      targetSet.delete(value);
    } else {
      targetSet.add(value);
    }
    renderAll();
  });
});

elements.closeModalButton.addEventListener("click", closeModal);
elements.cancelModalButton.addEventListener("click", closeModal);
elements.modal.addEventListener("click", (event) => {
  if (event.target.dataset.action === "close-modal") {
    closeModal();
  }
});

document.addEventListener("keydown", (event) => {
  if (event.key === "Escape" && !elements.modal.classList.contains("hidden")) {
    closeModal();
  }
});

// 切走标签页 / 关页面 / 最小化时，不等 5 秒，直接把待写入的改动落盘
document.addEventListener("visibilitychange", () => {
  if (document.visibilityState === "hidden") {
    flushPendingSaves();
  }
});

window.addEventListener("pagehide", () => {
  flushPendingSaves();
});

initializeTheme();
renderButtons();
loadTasks();
