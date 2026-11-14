const escapeHtml = (text: string) =>
  text.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');

const TOKEN = /(&quot;(?:[^&]|&(?!quot;))*?&quot;)(\s*:)?|\b(true|false|null)\b|(-?\d+(?:\.\d+)?)/g;

/** The JSON text as HTML with keys, strings, numbers and literals in spans; the text is escaped first. */
export function highlightJson(source: string): string {
  return escapeHtml(source).replace(
    TOKEN,
    (
      _match,
      str: string | undefined,
      colon: string | undefined,
      literal?: string,
      num?: string,
    ) => {
      if (str !== undefined) {
        return colon !== undefined
          ? `<span class="json-k">${str}</span>${colon}`
          : `<span class="json-s">${str}</span>`;
      }
      return literal !== undefined
        ? `<span class="json-b">${literal}</span>`
        : `<span class="json-n">${num}</span>`;
    },
  );
}
