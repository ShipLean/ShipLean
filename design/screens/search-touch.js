/*
 * Retrospectives › Search in the touch shell (SHIP-730); styles in search-touch.css. Load at the end of retros.html,
 * after its own script, which exposes its state and rules as window.SRCH and calls SRCH.after() after each render.
 * At 900px and below it:
 *   - keeps the search field alone on the first toolbar row (16px), then Filter (n) and "n points in m retros"
 *   - shows each active filter as a chip with ✕ under the bar, and Clear all when there are two or more
 *   - draws the results as one list, one point a row: category and the work item key; the point (two lines, the match
 *     marked) with the root cause or "Match in the raw failure log" when that's where it matched; theme, ↻, sprint, author
 *   - opens a tapped row's whole retro full screen (the page's own retro panel, moved), the point outlined and scrolled
 *     to, with ‹ n of m › to step through the results; the address keeps selected and point, so Back closes it
 *   - opens the filters, the theme picker and a note's ⋯ as bottom sheets, and New action item full screen
 * The filtering, matching and the retro panel are the page's own code. Above 900px nothing changes; ?notouch shows the
 * desktop page at any width.
 * Review states: the page's own filters (?q, ?sprint, ?category, ?theme, ?level, ?role, ?decision, ?noSprint, ?unthemed,
 * ?recurring), ?selected=<retro>&point=<point> (the retro open), ?sheet=filter|theme:<point>|note:<point>,
 * ?dialog=<point> (New action item), and the alternatives ?alt=line|grouped (D1), sheet|inline (D2), selects (D3),
 * nochips (D4), nostep (D5), split (D6, 600px and up); several with +.
 */
(() => {
  const q = new URLSearchParams(location.search);
  if (q.has('notouch')) { document.querySelector('link[href="search-touch.css"]')?.remove(); return; }
  const mq = matchMedia('(max-width: 900px)');
  mq.addEventListener('change', () => location.reload());
  if (!mq.matches) return;

  const { esc, tIcon, av, people, toast } = SL;
  const { themes, retros, catLabel, catOrder, recurring, sprintNames } = RETRO;
  const root = document.documentElement;
  const $ = s => document.querySelector(s);
  const byId = id => document.getElementById(id);
  const el = (tag, cls, html) => { const e = document.createElement(tag); if (cls) e.className = cls; if (html != null) e.innerHTML = html; return e; };
  const alt = (q.get('alt') ?? '').split(/[ +,]/).filter(Boolean);
  if (alt.length) root.dataset.alt = alt.join(' ');
  const has = a => alt.includes(a);
  const split = () => has('split') && innerWidth >= 600;
  const mode = () => (split() ? 'split' : has('inline') ? 'inline' : 'full');

  const sprintLabel = s => (s ? `Sprint ${s.slice(1)}` : 'No sprint');
  // Each filter: its control on the page, the label of a value, and the choices the sheet offers.
  const F = [
    { k: 'category', id: 'fCat', label: 'Category', any: 'Any', opts: catOrder.map(c => [c, catLabel[c]]), chips: true, dot: true },
    { k: 'sprint', id: 'fSprint', label: 'Sprint', any: 'Any sprint', opts: [...sprintNames].reverse().map(s => [s, sprintLabel(s)]), chip: v => sprintLabel(v) },
    { k: 'theme', id: 'fTheme', label: 'Theme', any: 'Any theme', opts: Object.entries(themes).filter(([k]) => k !== 'none').map(([k, t]) => [k, t.name]), chip: v => `# ${themes[v].name}` },
    { k: 'level', id: 'fLevel', label: 'Level', any: 'All', opts: [['stories', 'Stories and bugs'], ['features', 'Features']], chips: true },
    { k: 'role', id: 'fBy', label: 'Written by', any: 'Anyone', opts: [['conductor', 'Conductors'], ['sub-agent', 'Sub-agents'], ['person', 'People']], chips: true },
    { k: 'decision', id: 'fDecision', label: 'Decision', any: 'Any', opts: [['undecided', 'Undecided'], ['actioned', 'Actioned'], ['notNeeded', 'Not needed']], chips: true },
  ];
  const FLAGS = [
    { k: 'noSprint', id: 'fNoSprint', label: 'No sprint', hint: 'Only retros logged outside a sprint', chip: 'No sprint' },
    { k: 'unthemed', id: 'fUnthemed', label: 'Unthemed', hint: 'Only points with no theme yet', chip: 'Unthemed' },
    { k: 'recurring', id: 'fRecur', label: 'Recurring themes only', hint: 'Themes that keep coming back', chip: '↻ Recurring' },
  ];
  const valOf = f => byId(f.id).value;
  const optLabel = (f, v) => f.opts.find(([x]) => x === v)?.[1] ?? v;
  const activeCount = () => F.filter(f => valOf(f)).length + FLAGS.filter(f => byId(f.id).checked).length;

  /* Keep the address in step with the filters and the open retro, as the real app does (Back closes the retro). */
  function writeAddress(push) {
    const p = new URLSearchParams(location.search);
    ['q', ...F.map(f => f.k), ...FLAGS.map(f => f.k), 'selected', 'point'].forEach(k => p.delete(k));
    if (byId('q').value.trim()) p.set('q', byId('q').value.trim());
    F.forEach(f => { if (valOf(f)) p.set(f.k, valOf(f)); });
    FLAGS.forEach(f => { if (byId(f.id).checked) p.set(f.k, 'true'); });
    if (open) { p.set('selected', open.split('.')[0]); p.set('point', open); }
    const url = `${location.pathname}?${p}`.replace(/\?$/, '');
    if (url === location.pathname + location.search) return;
    try { history[push ? 'pushState' : 'replaceState'](null, '', url); } catch { /* file:// in some browsers */ }
  }

  /* Toolbar: the field, then Filter (n) · count, then the chips */
  const toolbar = $('.toolbar');
  byId('q').placeholder = 'Search points, root causes and logs';
  byId('q').setAttribute('enterkeyhint', 'search');
  byId('q').addEventListener('keydown', e => { if (e.key === 'Enter') e.target.blur(); });
  const bar = el('div', 'ss-bar', '<button class="btn ss-filter" type="button" aria-haspopup="dialog">Filter</button><span class="ss-count"></span>');
  const chips = el('div', 'ss-chips');
  toolbar.append(bar, chips);
  bar.querySelector('.ss-filter').addEventListener('click', openFilter);
  chips.addEventListener('click', e => {
    if (e.target.closest('.ss-clear')) { SRCH.clearAll(); return; }
    const c = e.target.closest('[data-unset]'); if (!c) return;
    const f = [...F, ...FLAGS].find(x => x.k === c.dataset.unset);
    if (f.opts) byId(f.id).value = ''; else byId(f.id).checked = false;
    SRCH.render();
  });

  /* The list */
  const list = el('div', 'ss-list');
  list.setAttribute('role', 'list');
  $('.split > .scroll').prepend(list);
  const detail = byId('detail');
  const pane = el('div', 'ss-pane'); // D6 (b)
  let open = q.get('point') || (q.get('selected') ? `${q.get('selected')}.0` : null);
  if (open && !retros.some(r => open.startsWith(r.id + '.'))) open = null;

  function row({ r, p, id }, qText) {
    // The page's own rule: a point matches on its text, root cause, theme, key or title; only otherwise on the raw log.
    const own = [p.text, p.cause || '', themes[p.theme].name, r.subject, r.title].join(' ').toLowerCase().includes(qText.toLowerCase());
    const inCause = qText && p.cause && p.cause.toLowerCase().includes(qText.toLowerCase());
    const inLog = qText && !own && SRCH.logText(r).toLowerCase().includes(qText.toLowerCase());
    const th = p.theme === 'none' ? '<i>Unthemed</i>' : `# ${SRCH.hi(themes[p.theme].name, qText)}`;
    if (has('line')) {
      return `<button class="ss-row" type="button" role="listitem" data-ss-row="${id}"${id === open ? ' aria-current="true"' : ''}><span class="catdot ${p.cat}" title="${catLabel[p.cat]}">${catLabel[p.cat]}</span><span class="t">${SRCH.hi(p.text, qText)}</span><span class="key">${r.subject}</span></button>`;
    }
    return `<button class="ss-row" type="button" role="listitem" data-ss-row="${id}"${id === open ? ' aria-current="true"' : ''}>
      <span class="l1"><span class="catdot ${p.cat}">${catLabel[p.cat]}</span><span class="spacer"></span>${has('grouped') ? '' : `<span class="ss-subj">${tIcon(r.type)}<span class="key">${r.subject}</span></span>`}</span>
      <span class="t">${SRCH.hi(p.text, qText)}</span>
      ${inCause ? `<span class="sub">Root cause: ${SRCH.hi(p.cause, qText)}</span>` : ''}${inLog ? '<span class="sub">Match in the raw failure log</span>' : ''}
      <span class="l3"><span class="th">${th}</span>${recurring(p.theme) ? '<span class="chip warn" title="Recurring">↻</span>' : ''}<span class="spacer"></span>${has('grouped') ? '' : `<span>${r.sprint ?? 'No sprint'}</span>${av(r.by)}`}</span>
    </button>`;
  }

  // The page renders the retro into #detail by id, so it must stay in the document: park it before redrawing the list.
  const park = () => { if (detail.parentNode !== $('.split')) $('.split').append(detail); };

  function after() {
    if (list.contains(detail)) park();
    const rows = SRCH.filtered(), qText = byId('q').value.trim();
    const n = activeCount(), fb = bar.querySelector('.ss-filter');
    fb.innerHTML = n ? `Filter<span class="n">${n}</span>` : 'Filter';
    if (n) fb.dataset.n = n; else delete fb.dataset.n;
    const nr = new Set(rows.map(x => x.r.id)).size;
    bar.querySelector('.ss-count').textContent = `${rows.length} point${rows.length === 1 ? '' : 's'} in ${nr} retro${nr === 1 ? '' : 's'}`;
    chips.innerHTML = F.filter(valOf).map(f => `<button class="ss-chip" type="button" data-unset="${f.k}" aria-label="Remove ${esc(f.label)} filter"><span>${esc(f.chip ? f.chip(valOf(f)) : optLabel(f, valOf(f)))}</span><span class="x" aria-hidden="true">✕</span></button>`).join('')
      + FLAGS.filter(f => byId(f.id).checked).map(f => `<button class="ss-chip" type="button" data-unset="${f.k}" aria-label="Remove ${f.label}"><span>${f.chip}</span><span class="x" aria-hidden="true">✕</span></button>`).join('')
      + (n > 1 ? '<button class="ss-clear" type="button">Clear all</button>' : '');

    if (open && !rows.some(x => x.id === open)) closeRetro(false);
    if (!rows.length) {
      list.innerHTML = `<div class="ss-empty"><span>No point matches${qText ? ` “${esc(qText)}”` : ''}.</span>${n || qText ? '<button class="btn" type="button" data-ss-clear>Clear search and filters</button>' : ''}</div>`;
    } else if (has('grouped')) {
      const groups = [];
      rows.forEach(x => { const g = groups.find(y => y.r === x.r); if (g) g.rows.push(x); else groups.push({ r: x.r, rows: [x] }); });
      list.innerHTML = groups.map(({ r, rows: rs }) => `<section class="ss-group"><header class="ss-ghead">${tIcon(r.type)}<span class="key">${r.subject}</span><b>${esc(r.title)}</b><span class="spacer"></span><span class="muted xs">${r.sprint ?? 'No sprint'}</span>${av(r.by)}</header>${rs.map(x => row(x, qText)).join('')}</section>`).join('');
    } else {
      list.innerHTML = rows.map(x => row(x, qText)).join('');
    }
    placeDetail();
    writeAddress(false);
  }
  SRCH.after = after;

  /* The open retro: the page's own panel, moved into the full-screen view (or the alternatives) */
  const full = el('div', 'ss-full');
  full.hidden = true;
  full.setAttribute('role', 'dialog'); full.setAttribute('aria-modal', 'true'); full.setAttribute('aria-label', 'Retrospective');
  full.innerHTML = `<header><button class="ib" type="button" data-ss-x aria-label="Close">✕</button><span class="ttl"><b>Retrospective</b><small></small></span>
    <span class="ss-step"><button type="button" data-ss-step="-1" aria-label="Previous result">‹</button><button type="button" data-ss-step="1" aria-label="Next result">›</button></span></header><div class="body"></div>`;
  document.body.append(full);
  const fullBody = full.querySelector('.body');

  function placeDetail() {
    const rows = SRCH.filtered();
    if (mode() === 'split') {
      if (!pane.isConnected) $('.split').append(pane);
      if (!open && rows[0]) { open = rows[0].id; SRCH.selected = open; SRCH.render(); return; }
      if (detail.parentNode !== pane) pane.append(detail);
      return;
    }
    if (!open) { full.hidden = true; delete root.dataset.full; return; }
    if (mode() === 'inline') {
      const r = list.querySelector(`[data-ss-row="${CSS.escape(open)}"]`);
      if (!r) return;
      const box = el('div', 'ss-inline'); box.append(detail); r.after(box);
      return;
    }
    if (detail.parentNode !== fullBody) fullBody.append(detail);
    const i = rows.findIndex(x => x.id === open), r = rows[i]?.r;
    // The title slot names the retro, which scrolls away when the view opens on the point.
    if (r) full.querySelector('.ttl b').innerHTML = `${tIcon(r.type)}<span class="key">${r.subject}</span><span class="t">${esc(r.title)}</span>`;
    full.querySelector('.ttl small').textContent = `Result ${i + 1} of ${rows.length}${r ? ` · ${r.sprint ? sprintLabel(r.sprint) : 'No sprint'}` : ''}`;
    full.querySelector('[data-ss-step="-1"]').disabled = i <= 0;
    full.querySelector('[data-ss-step="1"]').disabled = i >= rows.length - 1;
    full.hidden = false; root.dataset.full = '';
  }

  function openRetro(id, push = true, tapped = false) {
    const was = open;
    open = id; SRCH.selected = id;
    SRCH.render();
    if (tapped && mode() === 'inline' && was === id) { closeRetro(); return; }
    if (push) writeAddress(true);
    requestAnimationFrame(() => {
      const hit = detail.querySelector('.rc-note.is-hit');
      if (mode() === 'full' && hit) fullBody.scrollTop = Math.max(0, hit.offsetTop - fullBody.clientHeight / 3);
    });
  }
  function closeRetro(write = true) {
    if (!open) return;
    open = null;
    full.hidden = true; delete root.dataset.full;
    park();
    list.querySelector('.ss-inline')?.remove();
    list.querySelectorAll('[aria-current]').forEach(x => x.removeAttribute('aria-current'));
    if (write) writeAddress(false);
  }
  addEventListener('popstate', () => { const p = new URLSearchParams(location.search).get('point'); if (p) openRetro(p, false); else closeRetro(false); });

  list.addEventListener('click', e => {
    if (e.target.closest('[data-ss-clear]')) { SRCH.clearAll(); return; }
    const r = e.target.closest('[data-ss-row]');
    if (r) openRetro(r.dataset.ssRow, true, true);
  });
  full.addEventListener('click', e => {
    if (e.target.closest('[data-ss-x]')) { closeRetro(); return; }
    const s = e.target.closest('[data-ss-step]');
    if (s && !s.disabled) {
      const rows = SRCH.filtered(), i = rows.findIndex(x => x.id === open);
      const next = rows[i + +s.dataset.ssStep]; if (next) openRetro(next.id, false);
    }
  });
  $('.scrim')?.addEventListener('click', () => { if (!sheet.hidden) closeSheet(); else if (has('sheet') && open) closeRetro(); });
  document.addEventListener('keydown', e => { if (e.key === 'Escape') { if (!sheet.hidden) closeSheet(); else closeRetro(); } });

  /* One bottom sheet (SHIP-805 C4 recommendation: grab bar, title, Close on the right) */
  const sheet = el('div', 'ss-sheet');
  sheet.hidden = true;
  sheet.setAttribute('role', 'dialog'); sheet.setAttribute('aria-modal', 'true');
  sheet.innerHTML = '<header class="sheet-head"><b></b><button class="btn ghost sheet-x" type="button">Close</button></header><div class="sb"></div><div class="foot" hidden></div>';
  document.body.append(sheet);
  const sb = sheet.querySelector('.sb'), foot = sheet.querySelector('.foot');
  function openSheet(kind, title, html, footHtml) {
    sheet.dataset.kind = kind;
    sheet.querySelector('.sheet-head b').textContent = title;
    sb.innerHTML = html; sb.scrollTop = 0;
    foot.hidden = !footHtml; foot.innerHTML = footHtml || '';
    sheet.hidden = false; root.dataset.sheet = 'search';
  }
  function closeSheet() {
    if (sheet.hidden) return;
    sheet.hidden = true; delete sheet.dataset.kind; delete sheet.dataset.point;
    if (root.dataset.sheet === 'search') delete root.dataset.sheet;
  }
  sheet.querySelector('.sheet-x').addEventListener('click', closeSheet);

  /* Filter: changes apply as you pick (as on desktop); the foot shows how many points that leaves. */
  function filterHtml() {
    const sel = f => `<label class="ss-f"><span>${f.label}</span><select class="field" data-ss-f="${f.k}"><option value="">${f.any}</option>${f.opts.map(([v, l]) => `<option value="${v}"${valOf(f) === v ? ' selected' : ''}>${esc(l)}</option>`).join('')}</select></label>`;
    const choice = f => `<div class="ss-f" role="radiogroup" aria-label="${f.label}"><span>${f.label}</span><div class="ss-choices">${[['', f.any], ...f.opts].map(([v, l]) => `<button type="button" role="radio" aria-checked="${valOf(f) === v}" data-ss-f="${f.k}" data-v="${v}">${f.dot && v ? `<span class="catdot ${v}">${l}</span>` : esc(l)}</button>`).join('')}</div></div>`;
    return F.map(f => (f.chips && !has('selects') ? choice(f) : sel(f))).join('')
      + `<div class="ss-f"><span>Only</span>${FLAGS.map(f => `<label class="ss-switch"><span>${f.label}</span><small>${f.hint}</small><input type="checkbox" role="switch" data-ss-flag="${f.k}"${byId(f.id).checked ? ' checked' : ''}></label>`).join('')}</div>`;
  }
  function filterFoot() {
    const n = SRCH.filtered().length;
    return `<button class="btn" type="button" data-ss-fclear${activeCount() ? '' : ' disabled'}>Clear</button><button class="btn primary" type="button" data-ss-done>Show ${n} point${n === 1 ? '' : 's'}</button>`;
  }
  function openFilter() { openSheet('filter', 'Filter', filterHtml(), filterFoot()); }
  function refreshFilter() { const top = sb.scrollTop; sb.innerHTML = filterHtml(); sb.scrollTop = top; foot.innerHTML = filterFoot(); }
  sb.addEventListener('click', e => {
    const c = e.target.closest('button[data-ss-f]');
    if (c) { byId(F.find(f => f.k === c.dataset.ssF).id).value = c.dataset.v; SRCH.render(); refreshFilter(); }
  });
  sb.addEventListener('change', e => {
    const s = e.target.closest('select[data-ss-f]');
    if (s) { byId(F.find(f => f.k === s.dataset.ssF).id).value = s.value; SRCH.render(); refreshFilter(); }
    const t = e.target.closest('[data-ss-flag]');
    if (t) { byId(FLAGS.find(f => f.k === t.dataset.ssFlag).id).checked = t.checked; SRCH.render(); refreshFilter(); }
  });
  foot.addEventListener('click', e => {
    if (e.target.closest('[data-ss-fclear]')) { SRCH.clearAll(); refreshFilter(); }
    if (e.target.closest('[data-ss-done]')) closeSheet();
  });

  /* A note's theme and ⋯, as sheets (the sprint Retro tab's, SHIP-727) */
  function openTheme(id) {
    const { p } = RC.pointById(id);
    const used = {};
    retros.forEach(r => r.points.forEach(x => { used[x.theme] = (used[x.theme] ?? 0) + 1; }));
    const opts = Object.entries(themes).filter(([k]) => k !== 'none').sort((a, b) => (used[b[0]] ?? 0) - (used[a[0]] ?? 0));
    openSheet('theme', 'Move to theme', `<div class="ident">${esc(p.text)}<small>${catLabel[p.cat]}</small></div>
      ${opts.map(([k, t]) => `<button class="srow" type="button" role="menuitemradio" aria-checked="${k === p.theme}" data-pick-theme="${k}"><span class="ck">${k === p.theme ? '✓' : ''}</span>${esc(t.name)}<span class="sub">${used[k] ? `${used[k]} point${used[k] === 1 ? '' : 's'}` : ''}</span></button>`).join('')}
      <button class="srow" type="button" role="menuitemradio" aria-checked="${p.theme === 'none'}" data-pick-theme="none"><span class="ck">${p.theme === 'none' ? '✓' : ''}</span><i>Unthemed</i></button>
      <button class="srow" type="button" style="color:var(--color-accent)"><span class="ck">+</span>New theme…</button>`);
    sheet.dataset.point = id;
  }
  function openNote(id) {
    const { p } = RC.pointById(id);
    openSheet('note', 'Point', `<div class="ident">${esc(p.text)}<small>${catLabel[p.cat]} · your point</small></div>
      <button class="srow" type="button" data-note-act="theme">Move to theme…<span class="sub"># ${esc(themes[p.theme].name)}</span></button>
      <button class="srow danger" type="button" data-note-act="archive">Archive point</button>
      <p class="hint">Only your own points can be archived. An agent's points are never edited or archived.</p>`);
    sheet.dataset.point = id;
  }
  sb.addEventListener('click', e => {
    const th = e.target.closest('[data-pick-theme]');
    if (th) { const { p } = RC.pointById(sheet.dataset.point); p.theme = th.dataset.pickTheme; closeSheet(); SRCH.render(); toast(`Moved to # ${esc(themes[p.theme].name)}`); return; }
    const act = e.target.closest('[data-note-act]');
    if (act) { const id = sheet.dataset.point; closeSheet(); if (act.dataset.noteAct === 'theme') openTheme(id); else toast('Point archived. <b>Undo</b>'); }
  });
  detail.addEventListener('click', e => {
    const t = e.target.closest('[data-rc-theme]');
    if (t) { e.stopImmediatePropagation(); openTheme(t.dataset.rcTheme); return; }
    const m = e.target.closest('[data-rc-more]');
    if (m) { e.stopImmediatePropagation(); openNote(m.dataset.rcMore); return; }
    const link = e.target.closest('a.btn[href^="retro-item.html"], .rc-pin');
    if (link) { e.preventDefault(); e.stopImmediatePropagation(); toast(link.classList.contains('rc-pin') ? 'Opens the action item full screen (SHIP-714)' : 'Opens the item\'s retros (SHIP-729)'); }
  }, true);

  /* New action item: full screen, as on the sprint Retro tab (SHIP-727 D5 and D8: the shared dialog). */
  new MutationObserver(muts => muts.forEach(m => m.addedNodes.forEach(n => {
    if (!(n instanceof HTMLElement) || !n.classList.contains('rc-overlay')) return;
    const dlg = n.querySelector('.rc-dialog');
    const create = dlg.querySelector('[data-create]');
    create.classList.add('rt-create'); create.textContent = 'Create';
    dlg.querySelector('header').append(create);
    dlg.querySelector('header .seg')?.addEventListener('click', e => { const b = e.target.closest('[data-mode]'); if (b) create.textContent = b.dataset.mode === 'new' ? 'Create' : 'Add'; });
    const noteEl = dlg.querySelector('footer [data-only]');
    if (noteEl) { noteEl.classList.add('rt-foot-note'); dlg.querySelector('.body').append(noteEl); }
    dlg.querySelector('#rcTitle')?.blur();
  }))).observe(document.body, { childList: true });

  SRCH.selected = open;
  SRCH.render();
  if (open) openRetro(open, false);

  const s = q.get('sheet');
  if (s === 'filter') openFilter();
  else if (s?.startsWith('theme:')) openTheme(s.slice(6));
  else if (s?.startsWith('note:')) openNote(s.slice(5));
  if (q.get('dialog')) RC.openActionDialog([q.get('dialog')], SRCH.scope());
})();
