/*
 * Board settings in the touch shell (SHIP-1809, K7; the phone design for the Stories board settings that SHIP-732's
 * States pane links to); styles in board-settings-touch.css. Loaded at the end of board-settings.html. At 900px and below
 * it follows the Taskboard settings (SHIP-725 D15): a form dialog, so full screen, ✕ at the top left, the sections as a
 * sideways chip row, and the footer as the Save bar with Discard (SHIP-805 rule 15) (P9). Columns, lanes and style rules
 * reorder by pressing the 32px ⠿ handle and dragging; the move goes through the page's own Alt+↑/↓ (P4, as Settings).
 * Above 900px nothing changes; ?notouch shows the desktop page at any width.
 * Review states (add ?shot for screenshots):
 *   ?pane=styles               the style rules (any ?pane= as desktop)
 *   &drag=<row>                with ?pane=styles, that rule lifted by its handle, the drop line two rules down
 *   &dirty                     with ?pane=styles, the second rule turned off, so the Save bar says what is unsaved
 */
(() => {
  const q = new URLSearchParams(location.search);
  if (q.has('notouch')) { document.querySelector('link[href="board-settings-touch.css"]')?.remove(); return; }
  const mq = matchMedia('(max-width: 900px)');
  mq.addEventListener('change', () => location.reload());
  if (!mq.matches) return;

  const pane = document.getElementById('pane'), foot = document.getElementById('foot');

  /* The footer is redrawn on every edit: its Cancel says Discard here (SHIP-805 rule 15). */
  const relabel = () => { const c = foot.querySelector('#cancel'); if (c && c.textContent !== 'Discard') c.textContent = 'Discard'; };
  new MutationObserver(relabel).observe(foot, { childList: true });
  relabel();

  /* Touch reorder: press the handle and drag; the drop line shows where it lands. */
  let drag = null;
  const rowsOf = list => [...pane.querySelectorAll(`[data-list="${list}"][data-row]`)];
  const clearDrop = () => pane.querySelectorAll('.drop-before, .drop-after').forEach(r => r.classList.remove('drop-before', 'drop-after'));
  pane.addEventListener('pointerdown', e => {
    const g = e.target.closest('[data-grip]'); if (!g) return;
    e.preventDefault();
    const r = g.closest('[data-row][data-list]');
    drag = { list: r.dataset.list, from: +r.dataset.row, to: null, pid: e.pointerId };
    g.setPointerCapture(e.pointerId);
    r.classList.add('bst-lifted');
  });
  pane.addEventListener('pointermove', e => {
    if (!drag || e.pointerId !== drag.pid) return;
    clearDrop();
    const over = rowsOf(drag.list).find(r => { const b = r.getBoundingClientRect(); return e.clientY >= b.top && e.clientY < b.bottom; });
    if (!over || +over.dataset.row === drag.from) { drag.to = null; return; }
    const b = over.getBoundingClientRect(), before = e.clientY < b.top + b.height / 2;
    over.classList.add(before ? 'drop-before' : 'drop-after');
    let to = +over.dataset.row + (before ? 0 : 1); if (drag.from < to) to--;
    drag.to = to;
  });
  const endDrag = () => {
    if (!drag) return;
    const { list, from, to } = drag; drag = null; clearDrop();
    pane.querySelector('.bst-lifted')?.classList.remove('bst-lifted');
    if (to == null || to === from) return;
    const key = to < from ? 'ArrowUp' : 'ArrowDown', step = to < from ? -1 : 1;
    for (let i = from; i !== to; i += step) pane.querySelector(`[data-list="${list}"][data-row="${i}"] [data-grip]`)?.dispatchEvent(new KeyboardEvent('keydown', { key, altKey: true, bubbles: true }));
  };
  pane.addEventListener('pointerup', endDrag);
  pane.addEventListener('pointercancel', endDrag);

  /* Review states */
  if (q.get('pane') === 'styles') {
    if (q.has('dirty')) pane.querySelector('[data-ren="1"]')?.click();
    const dg = q.get('drag');
    if (dg != null) {
      const r = pane.querySelector(`[data-list="styles"][data-row="${dg}"]`);
      r?.classList.add('bst-lifted');
      pane.querySelector(`[data-list="styles"][data-row="${+dg + 2}"]`)?.classList.add('drop-before');
    }
  }
})();
