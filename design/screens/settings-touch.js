/*
 * Project settings in the touch shell (SHIP-1809, K7: the phone design for SHIP-732); styles in settings-touch.css.
 * Loaded at the end of settings.html, after its own script, and works on the DOM that script draws. At 900px and below:
 *   - Settings opens on an index of the six panes, each with what it holds, its count and a dot for unsaved changes
 *     or errors (P1); tapping one opens it full width with ‹ back at the far left of the top bar, Menu at the far
 *     right (as the built TouchTopBar) and the pane in the crumb (P2)
 *   - the project's one draft stays: when it has changes, the Save bar shows on every pane and the index,
 *     "n unsaved changes ▾" (the list as a sheet), Discard and Save (P3)
 *   - States: the type as a sideways segmented row; each state a two-line row with a 32px ⠿ handle (press and drag
 *     to reorder) and a 32px ⋯ that opens the row menu as a sheet with Close (P4, P5)
 *   - Members: a row per person, role select and remove at 32px, the add field full width (P6)
 *   - Sprints & forecast, Integrations and Demos: the desktop's forms one column; Demos keeps its own Save buttons (P7)
 *   - nothing is desktop only: every pane can be changed on a phone (P8)
 * Above 900px nothing changes; ?notouch shows the desktop page at any width.
 * Review states (add ?shot for screenshots):
 *   (no tab)                    the pane index
 *   ?tab=<pane>                 a pane (general, states, members, sprints, integrations, demos)
 *   &dirty                      edits on that pane (General: name and Saturday; States: rename Triaged), so the Save bar shows
 *   &changes                    with &dirty, the unsaved changes list open
 *   ?tab=states&drag=<id>       a state lifted by its handle, the drop line under the next row
 *   ?tab=states&menu=<id>       a state's ⋯ sheet
 *   ?tab=demos&at=yt            with ?shot, only the YouTube panel (yt= picks its state, as on desktop)
 */
(() => {
  const q = new URLSearchParams(location.search);
  if (q.has('notouch')) { document.querySelector('link[href="settings-touch.css"]')?.remove(); return; }
  const mq = matchMedia('(max-width: 900px)');
  mq.addEventListener('change', () => location.reload());
  if (!mq.matches) return;

  const root = document.documentElement;
  const $ = s => document.querySelector(s);
  const el = (tag, cls, html) => { const e = document.createElement(tag); if (cls) e.className = cls; if (html != null) e.innerHTML = html; return e; };
  const escape = () => document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
  const SUB = {
    general: 'Name, description, working days',
    states: 'Each type\'s states, their order and category',
    members: 'Who is in Storefront, and their role',
    sprints: 'Sprint length, velocity window and override',
    integrations: 'The Jira connection and import',
    demos: 'Automatic demos, brand and YouTube',
  };
  const LABEL = { general: 'General', states: 'States', members: 'Members', sprints: 'Sprints & forecast', integrations: 'Integrations', demos: 'Demos' };

  /* Index or pane (P1, P2). The desktop opens on States; a phone opens the index unless the address names a pane. */
  const named = q.get('tab') || location.hash.slice(1);
  const setIndex = on => { root.classList.toggle('set-index', on); syncCrumb(); window.scrollTo(0, 0); };
  // The text character ‹, as TouchTopBar (SHIP-1863)
  const back = el('button', 'tbtn set-back', '<span aria-hidden="true">‹</span>');
  back.type = 'button'; back.setAttribute('aria-label', 'Back to Settings');
  const bar0 = $('.appbar');
  bar0.prepend(back);
  // As the built TouchTopBar: with ‹ back set, ‹ is at the far left and Menu moves to the far right (after Search).
  const mnav = bar0.querySelector('.mnav');
  const placeMenu = pane => { if (pane) bar0.append(mnav); else back.after(mnav); };
  back.addEventListener('click', () => { history.replaceState(null, '', location.pathname + location.search.replace(/([?&])tab=[^&]*&?/, '$1').replace(/[?&]$/, '')); setIndex(true); });

  const crumbs = bar0.querySelector('.crumbs');
  const crumb0 = crumbs.innerHTML;
  function syncCrumb() {
    const cur = $('#tabs [aria-current="page"]')?.dataset.tab;
    crumbs.innerHTML = crumb0;
    placeMenu(!root.classList.contains('set-index') && !!cur);
    if (!root.classList.contains('set-index') && cur) {
      crumbs.lastElementChild.outerHTML = '<span>Settings</span>';
      crumbs.insertAdjacentHTML('beforeend', `<span>›</span><b>${LABEL[cur]}</b>`);
    }
  }

  /* The index rows: the page redraws the tab list on every edit, so decorate each redraw. */
  const tabs = $('#tabs');
  function decorate() {
    tabs.querySelectorAll('.stab:not(.set-done)').forEach(a => {
      const label = a.firstChild.nodeType === 3 ? a.firstChild : null;
      if (label) a.replaceChild(el('span', 'tx', `<span>${label.textContent}</span><small>${SUB[a.dataset.tab]}</small>`), label);
      a.classList.add('set-done');
    });
    syncCrumb();
  }
  new MutationObserver(decorate).observe(tabs, { childList: true });
  decorate();
  tabs.addEventListener('click', e => { if (e.target.closest('[data-tab]')) setIndex(false); });
  setIndex(!['general', 'states', 'members', 'sprints', 'integrations', 'demos'].includes(named));

  /* The Save bar (P3): the head's status, Discard and Save move into it; it shows while the draft has changes. */
  const head = $('.head');
  const bar = el('div', 'set-savebar');
  bar.setAttribute('role', 'region'); bar.setAttribute('aria-label', 'Unsaved changes');
  bar.append($('#status'), $('#btnDiscard'), $('#btnSave'));
  document.body.append(bar, $('#changesPop')); // the list opens as a sheet over the scrim, so it can't sit in the bar
  const save = $('#btnSave'), pop = $('#changesPop');
  const syncBar = () => {
    const on = !save.disabled && !save.hidden;
    bar.hidden = !on; root.classList.toggle('set-dirty', on);
  };
  new MutationObserver(syncBar).observe(save, { attributes: true, attributeFilter: ['disabled', 'hidden'] });
  syncBar();
  head.querySelector('.spacer')?.remove();

  /* Sheets on the K1 anatomy (title, Close) over the shell's scrim: the row menu and the unsaved-changes list. */
  const menu = $('#menu');
  let menuFor = null;
  document.addEventListener('click', e => { const m = e.target.closest('[data-menu]'); if (m) menuFor = m.dataset.menu; }, true);
  function sheetHead(node, title) {
    if (node.hidden || node.querySelector('.sheet-head')) return;
    node.prepend(el('header', 'sheet-head', `<b>${title}</b><button class="btn ghost" type="button" data-set-close>Close</button>`));
  }
  const syncSheets = () => {
    if (!menu.hidden) {
      const name = document.querySelector(`[data-sname="${menuFor}"]`)?.value || 'State';
      sheetHead(menu, name);
    }
    sheetHead(pop, 'Unsaved changes');
    const open = !menu.hidden || !pop.hidden;
    if (open) root.dataset.sheet = 'set'; else if (root.dataset.sheet === 'set') delete root.dataset.sheet;
  };
  [menu, pop].forEach(n => new MutationObserver(syncSheets).observe(n, { attributes: true, attributeFilter: ['hidden'], childList: true }));
  document.addEventListener('click', e => { if (e.target.closest('[data-set-close]')) { e.stopPropagation(); escape(); } }, true);
  $('.scrim')?.addEventListener('click', () => { if (root.dataset.sheet === 'set') escape(); });

  /* Touch reorder (P4): press the 32px handle and drag; the drop line shows where it lands. The move itself goes
     through the page's own Alt+↑/↓ on the handle, one step at a time, so the order rules stay in one place. */
  const content = $('#content');
  let drag = null;
  const rows = () => [...content.querySelectorAll('tr.srow')];
  const clearDrop = () => content.querySelectorAll('.drop-before, .drop-after').forEach(r => r.classList.remove('drop-before', 'drop-after'));
  content.addEventListener('pointerdown', e => {
    const g = e.target.closest('[data-grip]'); if (!g || g.disabled) return;
    e.preventDefault();
    const tr = g.closest('tr.srow');
    drag = { id: tr.dataset.id, from: rows().indexOf(tr), to: null, pid: e.pointerId };
    g.setPointerCapture(e.pointerId);
    tr.classList.add('set-lifted');
  });
  content.addEventListener('pointermove', e => {
    if (!drag || e.pointerId !== drag.pid) return;
    clearDrop();
    const list = rows(), over = list.find(r => { const b = r.getBoundingClientRect(); return e.clientY >= b.top && e.clientY < b.bottom; });
    if (!over || over.dataset.id === drag.id) { drag.to = null; return; }
    const b = over.getBoundingClientRect(), before = e.clientY < b.top + b.height / 2;
    over.classList.add(before ? 'drop-before' : 'drop-after');
    let to = list.indexOf(over) + (before ? 0 : 1); if (drag.from < to) to--;
    drag.to = to;
  });
  const endDrag = () => {
    if (!drag) return;
    const { id, from, to } = drag; drag = null; clearDrop();
    content.querySelector('.set-lifted')?.classList.remove('set-lifted');
    if (to == null || to === from) return;
    const key = to < from ? 'ArrowUp' : 'ArrowDown';
    for (let i = 0; i < Math.abs(to - from); i++) content.querySelector(`[data-grip="${id}"]`)?.dispatchEvent(new KeyboardEvent('keydown', { key, altKey: true, bubbles: true }));
  };
  content.addEventListener('pointerup', endDrag);
  content.addEventListener('pointercancel', endDrag);

  /* The picked type stays in view in the sideways row. */
  const showType = () => {
    const seg = content.querySelector('.typeseg'), on = seg?.querySelector('[aria-pressed="true"]'); if (!on) return;
    const a = seg.getBoundingClientRect(), b = on.getBoundingClientRect();
    if (b.right > a.right || b.left < a.left) seg.scrollLeft += b.left - a.left - 12;
  };
  new MutationObserver(showType).observe(content, { childList: true });
  showType();

  /* Review states */
  const tab = q.get('tab');
  const input = (sel, value) => { const f = document.querySelector(sel); if (f) { f.value = value; f.dispatchEvent(new Event('input', { bubbles: true })); } };
  if (q.has('dirty')) {
    if (tab === 'general') { input('#fName', 'Storefront web'); document.querySelector('[data-day="5"]')?.click(); }
    if (tab === 'states') input('[data-sname="bug-triaged"]', 'Needs triage');
  }
  if (q.has('changes')) $('#btnChanges')?.click();
  const dg = q.get('drag');
  if (dg) {
    const tr = content.querySelector(`tr.srow[data-id="${dg}"]`);
    tr?.classList.add('set-lifted');
    tr?.nextElementSibling?.nextElementSibling?.classList.add('drop-before');
  }
  const mn = q.get('menu');
  if (mn) { content.querySelector(`[data-menu="${mn}"]`)?.click(); document.activeElement?.blur(); }
  if (q.get('at') === 'yt' && q.has('shot')) root.classList.add('set-at-yt');
})();
