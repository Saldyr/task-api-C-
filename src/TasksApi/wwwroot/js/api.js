// HTTP client of the UI: every call is recorded for the inspector.

export class ApiError extends Error {
  constructor(status, body) {
    super(body?.detail || body?.title || `Erreur HTTP ${status}`);
    this.status = status;
    this.body = body;
  }
}

const listeners = [];
let sequence = 0;

export function onRequest(listener) {
  listeners.push(listener);
}

// label: the user action that triggered the request (shown in the inspector).
export async function request(method, url, { body, label } = {}) {
  const headers = { Accept: 'application/json' };
  const init = { method, headers };
  if (body !== undefined) {
    headers['Content-Type'] = 'application/json';
    init.body = JSON.stringify(body);
  }

  const entry = { id: ++sequence, label, method, url, requestHeaders: { ...headers }, requestBody: body, sentAt: new Date() };
  const start = performance.now();

  let response;
  try {
    response = await fetch(url, init);
  } catch (error) {
    entry.durationMs = Math.round(performance.now() - start);
    entry.networkError = error.message;
    notify(entry);
    throw error;
  }

  const text = await response.text();
  entry.durationMs = Math.round(performance.now() - start);
  entry.status = response.status;
  entry.statusText = response.statusText;
  entry.responseHeaders = Object.fromEntries(response.headers.entries());
  entry.responseBody = parseBody(text);
  notify(entry);

  if (!response.ok) {
    throw new ApiError(response.status, entry.responseBody);
  }
  return entry.responseBody;
}

function parseBody(text) {
  if (text === '') return undefined;
  try {
    return JSON.parse(text);
  } catch {
    return text;
  }
}

function notify(entry) {
  for (const listener of listeners) listener(entry);
}
