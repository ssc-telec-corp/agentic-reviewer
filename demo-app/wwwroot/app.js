// Notes page. User-supplied text is rendered with textContent only.
const api = (path, options = {}) =>
  fetch(path, {
    credentials: 'same-origin',
    headers: { 'Content-Type': 'application/json' },
    ...options,
  });

function renderNote(note) {
  const item = document.createElement('li');
  const title = document.createElement('h3');
  title.textContent = note.title;
  const body = document.createElement('p');
  body.textContent = note.body;
  const remove = document.createElement('button');
  remove.textContent = 'Delete';
  remove.addEventListener('click', async () => {
    await api(`/api/notes/${encodeURIComponent(note.id)}`, { method: 'DELETE' });
    item.remove();
  });
  item.append(title, body, remove);
  return item;
}

async function loadNotes() {
  const response = await api('/api/notes/');
  if (!response.ok) return;
  const list = document.getElementById('note-list');
  list.replaceChildren(...(await response.json()).map(renderNote));
  document.getElementById('notes').hidden = false;
  document.getElementById('login').hidden = true;
}

document.getElementById('login').addEventListener('submit', async (event) => {
  event.preventDefault();
  const form = new FormData(event.target);
  const response = await api('/api/login', {
    method: 'POST',
    body: JSON.stringify({ email: form.get('email'), password: form.get('password') }),
  });
  if (response.ok) await loadNotes();
});

document.getElementById('new-note').addEventListener('submit', async (event) => {
  event.preventDefault();
  const form = new FormData(event.target);
  await api('/api/notes/', {
    method: 'POST',
    body: JSON.stringify({ title: form.get('title'), body: form.get('body') }),
  });
  event.target.reset();
  await loadNotes();
});

loadNotes();
