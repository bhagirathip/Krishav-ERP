// Notes used to be typed as plain text using a markdown-lite convention
// (**bold**, *italic*/_italic_, "- "/"* " bullet lines) in a <textarea>.
// They are now edited in a real rich-text (contenteditable) editor that
// stores actual HTML - these helpers let both the editor (to migrate an
// old plain-text note into HTML the first time it's opened) and the print
// view (which must still render notes saved before the migration) share
// one definition of what the old convention means.

export function looksLikeHtml(value) {
  return /<[a-z][\s\S]*>/i.test(String(value ?? ''));
}

function escapeHtml(value) {
  return String(value ?? '')
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#039;');
}

function renderInline(value) {
  let html = escapeHtml(value);
  html = html.replace(/\*\*([^*]+)\*\*/g, '<b>$1</b>');
  html = html.replace(/(^|[^*])\*([^*]+)\*/g, '$1<i>$2</i>');
  html = html.replace(/(^|[^_])_([^_]+)_/g, '$1<i>$2</i>');
  return html;
}

export function legacyNoteToHtml(value) {
  const lines = String(value ?? '').split(/\r\n|\r|\n/);
  const parts = [];
  let bulletItems = null;
  const flushBullets = () => {
    if (bulletItems) { parts.push(`<ul>${bulletItems.join('')}</ul>`); bulletItems = null; }
  };

  lines.forEach(line => {
    const trimmed = line.trim();
    const bullet = trimmed.match(/^[-*]\s+(.*)$/);
    if (bullet) {
      bulletItems = bulletItems || [];
      bulletItems.push(`<li>${renderInline(bullet[1])}</li>`);
    } else {
      flushBullets();
      if (trimmed !== '') parts.push(`<div>${renderInline(line)}</div>`);
    }
  });
  flushBullets();

  return parts.join('');
}

// What the rich-text editor should be initialized with for a note that may
// already be HTML (saved since the editor shipped) or still be in the old
// plain-text convention (saved before).
export function noteToEditableHtml(value) {
  if (!value) return '';
  return looksLikeHtml(value) ? value : legacyNoteToHtml(value);
}

// The note's HTML comes from a contenteditable box an authorized Lab Master
// editor typed into, not untrusted public input, but the print HTML is
// injected into an iframe via document.write - this is a cheap safety net
// against that ever executing a stray pasted <script>.
export function sanitizeNoteHtml(value) {
  return String(value ?? '').replace(/<\/?(script|iframe|object|embed)[^>]*>/gi, '');
}
