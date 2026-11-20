// Renders the HTTP request log: what is sent, what comes back, and which C# code handled it.

const MAX_ENTRIES = 60;

const STATUS_HELP = {
  200: 'OK : succès, le corps contient les données demandées.',
  201: "Created : ressource créée. L'en-tête Location donne son adresse.",
  204: 'No Content : succès, sans corps de réponse.',
  400: 'Bad Request : requête invalide. Le détail par champ est dans « errors » (validation par [ApiController]).',
  404: "Not Found : la ressource demandée n'existe pas.",
  409: "Conflict : la requête contredit l'état actuel, par exemple un nom déjà utilisé.",
  500: 'Internal Server Error : exception non gérée côté serveur.',
};

// Which C# code handles each route: links what is on screen to the project files.
const ROUTES = [
  ['GET', /^\/api\/tasks$/, 'TasksController.GetAll → TaskService.GetAllAsync'],
  ['GET', /^\/api\/tasks\/stats$/, 'TasksController.GetStats → TaskService.GetStatsAsync'],
  ['GET', /^\/api\/tasks\/\d+$/, 'TasksController.GetById → TaskService.GetByIdAsync'],
  ['POST', /^\/api\/tasks$/, 'TasksController.Create → TaskService.CreateAsync'],
  ['PUT', /^\/api\/tasks\/\d+$/, 'TasksController.Update → TaskService.UpdateAsync'],
  ['PATCH', /^\/api\/tasks\/\d+\/status$/, 'TasksController.SetStatus → TaskService.SetStatusAsync'],
  ['DELETE', /^\/api\/tasks\/\d+$/, 'TasksController.Delete → TaskService.DeleteAsync'],
  ['GET', /^\/api\/categories$/, 'CategoriesController.GetAll → CategoryService.GetAllAsync'],
  ['GET', /^\/api\/categories\/\d+$/, 'CategoriesController.GetById → CategoryService.GetByIdAsync'],
  ['POST', /^\/api\/categories$/, 'CategoriesController.Create → CategoryService.CreateAsync'],
  ['PUT', /^\/api\/categories\/\d+$/, 'CategoriesController.Update → CategoryService.UpdateAsync'],
  ['DELETE', /^\/api\/categories\/\d+$/, 'CategoriesController.Delete → CategoryService.DeleteAsync'],
];

export function createInspector(list, clearButton) {
  clearButton.addEventListener('click', () => list.replaceChildren());

  return function add(entry) {
    const autoOpen = entry.method !== 'GET' || entry.networkError || entry.status >= 400;
    list.prepend(renderEntry(entry, autoOpen));
    while (list.children.length > MAX_ENTRIES) list.lastElementChild.remove();
  };
}

function renderEntry(entry, open) {
  const url = new URL(entry.url, location.origin);
  const li = el('li', 'entry');

  const summary = el('button', 'entry-summary');
  summary.type = 'button';
  summary.setAttribute('aria-expanded', String(open));
  summary.append(
    el('span', `method method-${entry.method.toLowerCase()}`, entry.method),
    el('span', 'entry-path', url.pathname + url.search),
    el('span', `status ${statusClass(entry)}`, entry.networkError ? 'ERR' : String(entry.status)),
    el('span', 'entry-duration', `${entry.durationMs} ms`),
    el('span', 'entry-label', entry.label || ''),
  );

  const details = el('div', 'entry-details');
  details.hidden = !open;
  summary.addEventListener('click', () => {
    details.hidden = !details.hidden;
    summary.setAttribute('aria-expanded', String(!details.hidden));
  });

  // Request
  details.append(el('h3', null, 'Requête envoyée'));
  const requestInfo = [
    ['Méthode', entry.method],
    ['URL', url.href],
    ['Côté C#', csharpTarget(entry.method, url.pathname)],
    ['Envoyée à', entry.sentAt.toLocaleTimeString('fr-FR')],
  ];
  details.append(definitionList(requestInfo));

  if (url.searchParams.size > 0) {
    details.append(el('h4', null, 'Paramètres de query string'));
    details.append(definitionList([...url.searchParams.entries()]));
  }
  details.append(el('h4', null, 'En-têtes'));
  details.append(definitionList(Object.entries(entry.requestHeaders)));
  details.append(el('h4', null, 'Corps'));
  details.append(entry.requestBody === undefined ? el('p', 'muted', '(aucun corps)') : jsonBlock(entry.requestBody));

  // Response
  if (entry.networkError) {
    details.append(el('h3', null, 'Réponse'));
    details.append(el('p', 'error', `Pas de réponse : ${entry.networkError}. L'API est-elle lancée ?`));
  } else {
    details.append(el('h3', null, `Réponse reçue : ${entry.status} ${entry.statusText} (${entry.durationMs} ms)`));
    const help = STATUS_HELP[entry.status];
    if (help) details.append(el('p', `status-help ${statusClass(entry)}`, help));
    details.append(el('h4', null, 'En-têtes'));
    details.append(definitionList(Object.entries(entry.responseHeaders)));
    details.append(el('h4', null, 'Corps'));
    details.append(entry.responseBody === undefined ? el('p', 'muted', '(aucun corps)') : jsonBlock(entry.responseBody));
  }

  // curl equivalent
  details.append(el('h3', null, 'Rejouer dans un terminal'));
  const curl = toCurl(entry, url);
  const curlBlock = el('pre', 'code', curl);
  const copy = el('button', 'copy', 'Copier');
  copy.type = 'button';
  copy.addEventListener('click', async () => {
    try {
      await navigator.clipboard.writeText(curl);
      copy.textContent = 'Copié';
    } catch {
      copy.textContent = 'Copie impossible';
    }
    setTimeout(() => { copy.textContent = 'Copier'; }, 1500);
  });
  details.append(curlBlock, copy);

  li.append(summary, details);
  return li;
}

function csharpTarget(method, path) {
  const route = ROUTES.find(([m, pattern]) => m === method && pattern.test(path));
  return route ? route[2] : '(route inconnue : géré par le pipeline, sans contrôleur)';
}

function statusClass(entry) {
  if (entry.networkError || entry.status >= 500) return 's5xx';
  if (entry.status >= 400) return 's4xx';
  return 's2xx';
}

function definitionList(pairs) {
  const dl = el('dl', 'kv');
  for (const [key, value] of pairs) {
    dl.append(el('dt', null, key), el('dd', null, String(value)));
  }
  return dl;
}

function jsonBlock(value) {
  const pre = el('pre', 'code json');
  if (typeof value === 'string') {
    pre.textContent = value;
  } else {
    // The text is escaped before color tags are added: no data is ever interpreted as HTML.
    pre.innerHTML = highlight(escapeHtml(JSON.stringify(value, null, 2)));
  }
  return pre;
}

function highlight(escapedJson) {
  return escapedJson.replace(
    /("(\\u[a-fA-F0-9]{4}|\\[^u]|[^\\"])*"(\s*:)?|\b(true|false|null)\b|-?\d+(?:\.\d+)?(?:[eE][+-]?\d+)?)/g,
    (match) => {
      let cls = 'j-num';
      if (match.startsWith('"')) cls = match.endsWith(':') ? 'j-key' : 'j-str';
      else if (match === 'true' || match === 'false') cls = 'j-bool';
      else if (match === 'null') cls = 'j-null';
      return `<span class="${cls}">${match}</span>`;
    });
}

function escapeHtml(text) {
  return text.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
}

function toCurl(entry, url) {
  const quote = (s) => `'${s.replace(/'/g, `'\\''`)}'`;
  let command = `curl -i -X ${entry.method} ${quote(url.href)}`;
  if (entry.requestBody !== undefined) {
    command += ` \\\n  -H ${quote('Content-Type: application/json')} \\\n  -d ${quote(JSON.stringify(entry.requestBody))}`;
  }
  return command;
}

function el(tag, className, text) {
  const node = document.createElement(tag);
  if (className) node.className = className;
  if (text !== undefined) node.textContent = text;
  return node;
}
