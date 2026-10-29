// Fenced ```mermaid blocks reach the browser as <pre><code class="language-mermaid">: the server's render leaves them as
// code. This draws each one as a diagram in place. Mermaid is loaded only when a block is found, so other pages never
// carry it. A block Mermaid cannot parse stays as code with a note under it.

let nextId = 0;

const isDark = () => {
  const forced = document.documentElement.dataset.theme;
  if (forced === 'dark' || forced === 'light') return forced === 'dark';
  return window.matchMedia('(prefers-color-scheme: dark)').matches;
};

/** Replaces the mermaid code blocks under `root`. Stops early once `cancelled()` says the content has changed. */
export async function drawMermaidDiagrams(root: HTMLElement, cancelled: () => boolean) {
  const blocks = [...root.querySelectorAll<HTMLElement>('pre > code.language-mermaid')];
  if (blocks.length === 0) return;
  const { default: mermaid } = await import('mermaid');
  // Strict: labels are sanitised and click handlers are dropped, since the source is user-written.
  mermaid.initialize({
    startOnLoad: false,
    securityLevel: 'strict',
    theme: isDark() ? 'dark' : 'default',
  });
  for (const code of blocks) {
    if (cancelled()) return;
    const pre = code.parentElement;
    if (!pre?.isConnected) continue;
    try {
      const { svg } = await mermaid.render(`md-diagram-${++nextId}`, code.textContent ?? '');
      if (cancelled()) return;
      const figure = document.createElement('div');
      figure.dataset.mdDiagram = 'mermaid';
      figure.innerHTML = svg;
      pre.replaceWith(figure);
    } catch (error) {
      const note = document.createElement('p');
      note.dataset.mdDiagramError = '';
      note.textContent = `This diagram could not be drawn: ${error instanceof Error ? error.message : String(error)}`;
      pre.after(note);
    }
  }
}
