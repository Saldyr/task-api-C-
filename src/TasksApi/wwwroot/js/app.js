// UI logic: state, API calls, rendering.
import { ApiError, onRequest, request } from './api.js';
import { createInspector } from './inspector.js';

const $ = (id) => document.getElementById(id);

const state = {
  page: 1,
  pageSize: 8,
  totalPages: 1,
  search: '',
  isDone: '',
  categoryId: '',
  overdue: false,
  sortBy: 'CreatedAt',
  sortDir: 'Desc',
  categories: [],
  stats: null,
  editing: null, // task being edited, or null
};

const dateFormat = new Intl.DateTimeFormat('fr-FR', { dateStyle: 'medium', timeStyle: 'short' });

onRequest(createInspector($('inspector-list'), $('inspector-clear')));

// ---------- Loading ----------

async function loadTasks(label) {
  const params = new URLSearchParams({
    page: state.page,
    pageSize: state.pageSize,
    sortBy: state.sortBy,
    sortDir: state.sortDir,
  });
  if (state.search) params.set('search', state.search);
  if (state.isDone) params.set('isDone', state.isDone);
  if (state.categoryId) params.set('categoryId', state.categoryId);
  if (state.overdue) params.set('overdue', 'true');

  const result = await request('GET', `/api/tasks?${params}`, { label });
  state.totalPages = Math.max(result.totalPages, 1);
  renderTasks(result);
}

async function loadStats(label) {
  state.stats = await request('GET', '/api/tasks/stats', { label });
  renderStats();
  renderCategories();
}

async function loadCategories(label) {
  state.categories = await request('GET', '/api/categories', { label });
  renderCategories();
  renderCategorySelects();
}

// After a change, reload everything that may have changed.
async function refreshAll(label = 'Rafraîchissement') {
  try {
    await Promise.all([loadTasks(label), loadStats(label), loadCategories(label)]);
  } catch (error) {
    showBanner(error);
  }
}

// ---------- Rendering ----------

function renderStats() {
  const s = state.stats;
  $('stat-total').textContent = s.total;
  $('stat-pending').textContent = s.pending;
  $('stat-done').textContent = s.done;
  $('stat-overdue').textContent = s.overdue;
}

function renderCategories() {
  const list = $('category-list');
  list.replaceChildren();

  list.append(categoryItem({ id: '', name: 'Toutes les catégories', taskCount: state.stats?.total }, false));
  for (const category of state.categories) {
    list.append(categoryItem(category, true));
  }
}

function categoryItem(category, editable) {
  const li = document.createElement('li');
  li.className = 'category' + (String(state.categoryId) === String(category.id) ? ' active' : '');

  const select = document.createElement('button');
  select.type = 'button';
  select.className = 'category-name';
  select.title = 'Filtrer sur cette catégorie';
  select.textContent = category.name;
  const count = document.createElement('span');
  count.className = 'count';
  count.textContent = category.taskCount ?? '';
  select.append(count);
  select.addEventListener('click', () => {
    state.categoryId = String(category.id);
    $('filter-category').value = state.categoryId;
    state.page = 1;
    renderCategories();
    loadTasks('Filtrer par catégorie').catch(showBanner);
  });
  li.append(select);

  if (editable) {
    const stat = state.stats?.byCategory.find((c) => c.categoryId === category.id);
    if (stat && stat.total > 0) {
      const bar = document.createElement('div');
      bar.className = 'progress';
      bar.title = `${stat.done} faite(s) sur ${stat.total}`;
      const fill = document.createElement('span');
      fill.style.width = `${Math.round((stat.done / stat.total) * 100)}%`;
      bar.append(fill);
      li.append(bar);
    }

    const actions = document.createElement('span');
    actions.className = 'category-actions';
    actions.append(
      iconButton('✎', 'Renommer', () => renameCategory(category)),
      iconButton('✕', 'Supprimer', () => deleteCategory(category)),
    );
    li.append(actions);
  }
  return li;
}

function renderCategorySelects() {
  const formSelect = $('task-category');
  const filterSelect = $('filter-category');
  const formValue = formSelect.value;

  formSelect.replaceChildren(option('', 'Aucune'));
  filterSelect.replaceChildren(option('', 'Toutes les catégories'));
  for (const c of state.categories) {
    formSelect.append(option(c.id, c.name));
    filterSelect.append(option(c.id, c.name));
  }
  formSelect.value = state.categories.some((c) => String(c.id) === formValue) ? formValue : '';
  filterSelect.value = state.categories.some((c) => String(c.id) === state.categoryId) ? state.categoryId : '';
}

function renderTasks(result) {
  const list = $('task-list');
  list.replaceChildren(...result.items.map(taskItem));
  $('task-empty').hidden = result.items.length > 0;
  $('page-info').textContent = `Page ${result.page} / ${Math.max(result.totalPages, 1)} · ${result.totalCount} tâche(s)`;
  $('page-prev').disabled = result.page <= 1;
  $('page-next').disabled = result.page >= result.totalPages;
}

function taskItem(task) {
  const overdue = !task.isDone && task.dueDate && new Date(task.dueDate) < new Date();
  const li = document.createElement('li');
  li.className = 'task' + (task.isDone ? ' done' : '') + (overdue ? ' overdue' : '');

  const checkbox = document.createElement('input');
  checkbox.type = 'checkbox';
  checkbox.checked = task.isDone;
  checkbox.title = 'Marquer comme faite (PATCH)';
  checkbox.addEventListener('change', () => toggleStatus(task, checkbox));

  const body = document.createElement('div');
  body.className = 'task-body';
  body.append(textEl('div', 'task-title', task.title));
  if (task.description) body.append(textEl('div', 'task-description', task.description));

  const meta = document.createElement('div');
  meta.className = 'task-meta';
  if (task.categoryName) meta.append(textEl('span', 'badge', task.categoryName));
  if (task.dueDate) {
    meta.append(textEl('span', 'badge' + (overdue ? ' badge-alert' : ''),
      `${overdue ? 'En retard · ' : 'Échéance · '}${dateFormat.format(new Date(task.dueDate))}`));
  }
  meta.append(textEl('span', 'muted', `Créée le ${dateFormat.format(new Date(task.createdAt))} · #${task.id}`));
  body.append(meta);

  const actions = document.createElement('div');
  actions.className = 'task-actions';
  actions.append(
    textButton('Modifier', () => startEdit(task.id)),
    textButton('Supprimer', () => deleteTask(task), 'danger'),
  );

  li.append(checkbox, body, actions);
  return li;
}

// ---------- Actions ----------

async function submitTask(event) {
  event.preventDefault();
  clearFormErrors();

  const due = $('task-due').value;
  const categoryId = $('task-category').value;
  const payload = {
    title: $('task-title').value,
    description: $('task-description').value || null,
    // datetime-local gives a local time; toISOString converts it to UTC (Z suffix).
    dueDate: due ? new Date(due).toISOString() : null,
    categoryId: categoryId ? Number(categoryId) : null,
  };

  try {
    if (state.editing) {
      await request('PUT', `/api/tasks/${state.editing.id}`, {
        body: { ...payload, isDone: state.editing.isDone },
        label: 'Enregistrer la tâche',
      });
    } else {
      await request('POST', '/api/tasks', { body: payload, label: 'Ajouter une tâche' });
    }
    resetForm();
    await refreshAll();
  } catch (error) {
    showFormErrors(error);
  }
}

async function startEdit(id) {
  try {
    const task = await request('GET', `/api/tasks/${id}`, { label: 'Ouvrir une tâche' });
    state.editing = task;
    $('task-title').value = task.title;
    $('task-description').value = task.description ?? '';
    $('task-due').value = task.dueDate ? toLocalInput(task.dueDate) : '';
    $('task-category').value = task.categoryId ?? '';
    $('task-form-title').textContent = `Modifier la tâche #${task.id}`;
    $('task-submit').textContent = 'Enregistrer (PUT)';
    $('task-cancel').hidden = false;
    clearFormErrors();
    $('task-form').scrollIntoView({ behavior: 'smooth', block: 'start' });
    $('task-title').focus();
  } catch (error) {
    showBanner(error);
  }
}

async function toggleStatus(task, checkbox) {
  try {
    await request('PATCH', `/api/tasks/${task.id}/status`, {
      body: { isDone: checkbox.checked },
      label: checkbox.checked ? 'Cocher une tâche' : 'Décocher une tâche',
    });
    await refreshAll();
  } catch (error) {
    checkbox.checked = !checkbox.checked;
    showBanner(error);
  }
}

async function deleteTask(task) {
  if (!confirm(`Supprimer « ${task.title} » ?`)) return;
  try {
    await request('DELETE', `/api/tasks/${task.id}`, { label: 'Supprimer une tâche' });
    if (state.editing?.id === task.id) resetForm();
    await refreshAll();
  } catch (error) {
    showBanner(error);
  }
}

async function createCategory(event) {
  event.preventDefault();
  $('category-error').textContent = '';
  try {
    await request('POST', '/api/categories', { body: { name: $('category-name').value }, label: 'Ajouter une catégorie' });
    $('category-name').value = '';
    await refreshAll();
  } catch (error) {
    $('category-error').textContent = describeError(error);
  }
}

async function renameCategory(category) {
  const name = prompt('Nouveau nom de la catégorie', category.name);
  if (name === null) return;
  $('category-error').textContent = '';
  try {
    await request('PUT', `/api/categories/${category.id}`, { body: { name }, label: 'Renommer une catégorie' });
    await refreshAll();
  } catch (error) {
    $('category-error').textContent = describeError(error);
  }
}

async function deleteCategory(category) {
  if (!confirm(`Supprimer la catégorie « ${category.name} » ? Ses tâches seront conservées, sans catégorie.`)) return;
  try {
    await request('DELETE', `/api/categories/${category.id}`, { label: 'Supprimer une catégorie' });
    if (state.categoryId === String(category.id)) state.categoryId = '';
    await refreshAll();
  } catch (error) {
    $('category-error').textContent = describeError(error);
  }
}

// ---------- Form and errors ----------

function resetForm() {
  state.editing = null;
  $('task-form').reset();
  $('task-form-title').textContent = 'Nouvelle tâche';
  $('task-submit').textContent = 'Ajouter la tâche';
  $('task-cancel').hidden = true;
  clearFormErrors();
}

function clearFormErrors() {
  for (const node of document.querySelectorAll('#task-form .error')) node.textContent = '';
}

// A 400 validation response contains "errors": { "Title": ["..."] }.
// Each message is shown under the matching field.
function showFormErrors(error) {
  if (!(error instanceof ApiError)) {
    $('task-form-error').textContent = describeError(error);
    return;
  }
  const errors = error.body?.errors ?? {};
  let placed = false;
  for (const [field, messages] of Object.entries(errors)) {
    const target = document.querySelector(`#task-form [data-error-for="${CSS.escape(field)}"]`);
    if (target) {
      target.textContent = messages.join(' ');
      placed = true;
    }
  }
  if (!placed) $('task-form-error').textContent = describeError(error);
}

function describeError(error) {
  if (error instanceof ApiError) {
    const fieldMessages = Object.values(error.body?.errors ?? {}).flat();
    return fieldMessages.length ? fieldMessages.join(' ') : `${error.status} · ${error.message}`;
  }
  return `Pas de réponse du serveur (${error.message}).`;
}

let bannerTimer;
function showBanner(error) {
  const banner = $('banner');
  banner.textContent = describeError(error);
  banner.hidden = false;
  clearTimeout(bannerTimer);
  bannerTimer = setTimeout(() => { banner.hidden = true; }, 5000);
}

// ---------- Small DOM helpers ----------

function toLocalInput(iso) {
  const date = new Date(iso);
  return new Date(date.getTime() - date.getTimezoneOffset() * 60000).toISOString().slice(0, 16);
}

function option(value, label) {
  const node = document.createElement('option');
  node.value = value;
  node.textContent = label;
  return node;
}

function textEl(tag, className, text) {
  const node = document.createElement(tag);
  node.className = className;
  node.textContent = text;
  return node;
}

function textButton(label, onClick, className = '') {
  const button = document.createElement('button');
  button.type = 'button';
  button.className = className;
  button.textContent = label;
  button.addEventListener('click', onClick);
  return button;
}

function iconButton(icon, label, onClick) {
  const button = textButton(icon, onClick, 'icon');
  button.title = label;
  button.setAttribute('aria-label', label);
  return button;
}

// ---------- Events ----------

$('task-form').addEventListener('submit', submitTask);
$('task-cancel').addEventListener('click', resetForm);
$('category-form').addEventListener('submit', createCategory);

let searchTimer;
$('filter-search').addEventListener('input', (event) => {
  clearTimeout(searchTimer);
  searchTimer = setTimeout(() => {
    state.search = event.target.value.trim();
    state.page = 1;
    loadTasks('Rechercher').catch(showBanner);
  }, 300);
});

$('filter-status').addEventListener('change', (event) => {
  state.isDone = event.target.value;
  state.page = 1;
  loadTasks('Filtrer par statut').catch(showBanner);
});

$('filter-category').addEventListener('change', (event) => {
  state.categoryId = event.target.value;
  state.page = 1;
  renderCategories();
  loadTasks('Filtrer par catégorie').catch(showBanner);
});

$('filter-overdue').addEventListener('change', (event) => {
  state.overdue = event.target.checked;
  state.page = 1;
  loadTasks('Filtrer les retards').catch(showBanner);
});

$('sort-by').addEventListener('change', (event) => {
  state.sortBy = event.target.value;
  state.page = 1;
  loadTasks('Changer le tri').catch(showBanner);
});

$('sort-dir').addEventListener('click', () => {
  state.sortDir = state.sortDir === 'Desc' ? 'Asc' : 'Desc';
  $('sort-dir').textContent = state.sortDir === 'Desc' ? '↓ Décroissant' : '↑ Croissant';
  state.page = 1;
  loadTasks('Inverser le tri').catch(showBanner);
});

$('page-prev').addEventListener('click', () => {
  if (state.page > 1) {
    state.page--;
    loadTasks('Page précédente').catch(showBanner);
  }
});

$('page-next').addEventListener('click', () => {
  if (state.page < state.totalPages) {
    state.page++;
    loadTasks('Page suivante').catch(showBanner);
  }
});

refreshAll('Chargement de la page');
