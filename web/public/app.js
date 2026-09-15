const THEME_STORAGE_KEY = "progress-web-theme";
const state = {
  tasks: [],
  keyword: "",
  sortKey: "updatedAt",
  sortDirection: "desc",
  hideCompleted: false,
  editingTaskId: "",
  theme: "dark",
  filters: {
    statuses: new Set(),
    taskTypes: new Set(),
  },
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
  tableBody: document.getElementById("taskTableBody"),
  template: document.getElementById("taskRowTemplate"),
  searchInput: document.getElementById("searchInput"),
  statusFilterGroup: document.getElementById("statusFilterGroup"),
  typeFilterGroup: document.getElementById("typeFilterGroup"),
  saveState: document.getElementById("saveState"),
  reloadButton: document.getElementById("reloadButton"),
  toggleCompletedButton: document.getElementById("toggleCompletedButton"),
  themeToggleButton: document.getElementById("themeToggleButton"),
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
    case "priority":
      result = (PRIORITY_ORDER[a.priority] || 0) - (PRIORITY_ORDER[b.priority] || 0);
      break;
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

function renderTable() {
  const tasks = getFilteredTasks();
  elements.tableBody.innerHTML = "";

  if (!tasks.length) {
    elements.tableBody.innerHTML = `<tr><td colspan="8" class="empty">当前没有匹配任务</td></tr>`;
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
    setInlineSelectValue(statusSelect, task.status || "未开始");
    setInlineSelectValue(prioritySelect, task.priority || "中");
    row.querySelector('[data-field="owner"]').textContent = task.owner || "-";
    row.querySelector('[data-field="updatedAt"]').textContent = formatDateTime(task.updatedAt);

    statusSelect.addEventListener("change", () => {
      statusSelect.dataset.value = statusSelect.value;
      patchTask(task.id, { status: statusSelect.value }, { closeModalAfterSave: false });
    });

    prioritySelect.addEventListener("change", () => {
      prioritySelect.dataset.value = prioritySelect.value;
      patchTask(task.id, { priority: prioritySelect.value }, { closeModalAfterSave: false });
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
  elements.form.elements.owner.value = task?.owner || "";
  elements.form.elements.description.value = task?.description || "";
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

async function deleteTask(id) {
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
    owner: formData.get("owner"),
  };

  if (state.editingTaskId) {
    patchTask(state.editingTaskId, payload);
    return;
  }

  addTask(payload);
});

elements.searchInput.addEventListener("input", (event) => {
  state.keyword = event.target.value;
  renderTable();
});

elements.reloadButton.addEventListener("click", () => {
  loadTasks();
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

initializeTheme();
renderButtons();
loadTasks();
