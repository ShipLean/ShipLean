/*
 * The sprint Retro tab in the touch shell (SHIP-727); styles in retro-touch.css. Load at the end of sprint-retro.html,
 * after its own script. At 900px and below, after every render of the page it:
 *   - puts the view toggle, Filter (n) and ⋯ More on one toolbar row; Written by and Decision move into the Filter sheet
 *   - turns the four numbers into buttons: Retros, Recurring and Action items open the rail's section as a sheet,
 *     To decide turns the To decide filter on and off
 *   - adds four category tiles above the board; the one picked is the only column on screen (By category)
 *   - collapses each By work item block to its counts; tapping its head opens it
 *   - opens the theme picker, a note's ⋯ and the Recurring chip as sheets, and the New action item dialog full screen
 * Everything else (decide, Not needed, Add, the dialog itself) is the page's own code. Above 900px nothing changes;
 * ?notouch shows the desktop page at any width.
 * Review states: ?cat=well|unplanned|bad|improve, ?sheet=filter|more|sprint|coverage|actions|recurring|theme:<point>|note:<point>,
 * ?open=<item key> (By work item), ?dialog=<point> (New action item), ?alt=stack|pager (D1 alternatives), plus the
 * page's own ?view=items, ?decide=open, ?why=<point> and ?adding=sprint:<category>.
 */
(() => {
  const q = new URLSearchParams(location.search);
  if (q.has('notouch')) { document.querySelector('link[href="retro-touch.css"]')?.remove(); return; }
  const mq = matchMedia('(max-width: 900px)');
  mq.addEventListener('change', () => location.reload());
  if (!mq.matches) return;

  const { esc, tIcon } = SL;
  const { themes, catLabel, catOrder, retros } = RETRO;
  const root = document.documentElement;
  const $ = s => document.querySelector(s);
  const el = (tag, cls, html) => { const e = document.createElement(tag); if (cls) e.className = cls; if (html != null) e.innerHTML = html; return e; };
  const dots = '<svg viewBox="0 0 16 16" width="16" height="16" aria-hidden="true"><circle cx="3.5" cy="8" r="1.4" fill="currentColor"/><circle cx="8" cy="8" r="1.4" fill="currentColor"/><circle cx="12.5" cy="8" r="1.4" fill="currentColor"/></svg>';
  if (q.get('alt')) root.dataset.alt = q.get('alt');

  const page = $('#page'), toolbar = $('.toolbar'), rail = $('.rail');
  const viewSeg = $('[aria-label="View"]'), srcSeg = $('[aria-label="Written by"]'), decSeg = $('[aria-label="Decision"]');
  const pressed = seg => seg.querySelector('[aria-pressed="true"]');
  const isItems = () => pressed(viewSeg).dataset.view === 'items';
  let cat = catOrder.includes(q.get('cat')) ? q.get('cat') : 'well';

  /* Toolbar: view · Filter (n) · ⋯ More */
  const bar = el('div', 'rt-bar');
  const filterBtn = el('button', 'btn rt-filter', 'Filter');
  filterBtn.type = 'button'; filterBtn.setAttribute('aria-haspopup', 'dialog');
  const moreBtn = el('button', 'btn rt-more', dots);
  moreBtn.type = 'button'; moreBtn.setAttribute('aria-label', 'More'); moreBtn.setAttribute('aria-haspopup', 'dialog');
  bar.append(viewSeg, el('span', 'spacer'), filterBtn, moreBtn);
  toolbar.append(bar);
  const segHome = el('span'); segHome.hidden = true; toolbar.append(segHome, srcSeg, decSeg);

  /* The sprint header's ⋯ (SHIP-724 D10): the same sheet on every sprint tab. Close sprint isn't offered on an ended sprint. */
  const hmore = el('button', 'rt-hmore', dots);
  hmore.type = 'button'; hmore.setAttribute('aria-label', 'Sprint actions'); hmore.setAttribute('aria-haspopup', 'dialog');
  $('.sprint-head .row').append(hmore);

  /* The numbers as buttons */
  const kpis = [...document.querySelectorAll('.sprint-head .kpi')];
  const kpiKeys = ['coverage', 'recurring', 'decide', 'actions'];
  const kpiLabels = ['Retros', 'Recurring themes', 'Points to decide', 'Action items'];
  kpis.forEach((k, i) => {
    k.dataset.k = kpiKeys[i]; k.setAttribute('role', 'button'); k.tabIndex = 0;
    k.querySelector('.l').textContent = kpiLabels[i];
  });

  /* Category tiles, before the board */
  const tiles = el('div', 'rt-tiles');
  tiles.setAttribute('role', 'group'); tiles.setAttribute('aria-label', 'Category');

  /* One sheet */
  const sheet = el('div', 'rt-sheet');
  sheet.hidden = true;
  sheet.setAttribute('role', 'dialog'); sheet.setAttribute('aria-modal', 'true');
  sheet.innerHTML = '<header class="sheet-head"><b></b><button class="btn ghost sheet-x" type="button">Close</button></header><div class="sb"></div><div class="foot" hidden></div>';
  document.body.append(sheet);
  const sb = sheet.querySelector('.sb'), foot = sheet.querySelector('.foot');
  let moved = []; // [node, placeholder] moved into the sheet while it is open
  function openSheet(kind, title, html, footHtml) {
    closeSheet();
    sheet.dataset.kind = kind;
    sheet.querySelector('.sheet-head b').textContent = title;
    sb.innerHTML = html ?? '';
    foot.hidden = !footHtml; foot.innerHTML = footHtml || '';
    sheet.hidden = false; root.dataset.sheet = 'retro';
  }
  function moveIn(node) { const ph = el('span'); ph.hidden = true; node.before(ph); moved.push([node, ph]); sb.append(node); }
  function closeSheet() {
    if (sheet.hidden) return;
    moved.forEach(([n, ph]) => ph.replaceWith(n)); moved = [];
    sheet.hidden = true; delete sheet.dataset.kind;
    if (root.dataset.sheet === 'retro') delete root.dataset.sheet;
  }
  sheet.querySelector('.sheet-x').addEventListener('click', closeSheet);
  document.querySelector('.scrim')?.addEventListener('click', () => { if (!sheet.hidden) closeSheet(); });
  document.addEventListener('keydown', e => { if (e.key === 'Escape' && !sheet.hidden) closeSheet(); });

  const railSection = name => [...rail.querySelectorAll('section')].find(s => s.querySelector('.overline').textContent === name);
  function openRail(kind) {
    const [name, title, foot] = {
      coverage: ['Coverage', 'Coverage', ''],
      actions: ['Action items', 'Action items', ''],
      recurring: ['Recurring', 'Recurring themes', '<a class="btn" href="retro-insights.html">Open insights</a>'],
    }[kind];
    openSheet(kind, title, '', foot);
    const s = railSection(name);
    [...s.children].filter(c => !c.classList.contains('overline')).forEach(moveIn);
  }

  function openFilter() {
    openSheet('filter', 'Filter', '', '<button class="btn" type="button" data-clear>Clear</button><button class="btn primary" type="button" data-done>Show points</button>');
    const a = el('div', 'opt', '<span>Written by</span>'), b = el('div', 'opt', '<span>Points</span>');
    sb.append(a, b);
    moveIn(srcSeg); a.append(srcSeg);
    moveIn(decSeg); b.append(decSeg);
    sb.append(el('p', 'hint', 'To decide hides Went well and every point that has an action item or is marked Not needed.'));
  }
  foot.addEventListener('click', e => {
    if (e.target.closest('[data-clear]')) { srcSeg.querySelector('[data-src=all]').click(); decSeg.querySelector('[data-decide=all]').click(); }
    if (e.target.closest('[data-done]')) closeSheet();
  });

  function openMore() {
    openSheet('more', 'Sprint 24 retro', `
      <button class="srow" type="button" data-go="coverage">Coverage<span class="sub">5 of 7 with a retro ›</span></button>
      <button class="srow" type="button" data-go="actions">Action items<span class="sub">2 new, 1 open ›</span></button>
      <button class="srow" type="button" data-go="recurring">Recurring themes<span class="sub">${$('#kRecurring').textContent} ›</span></button>
      <div class="grp">Go to</div>
      <a class="srow" href="retro-insights.html">Retrospective insights<span class="sub">›</span></a>
      <a class="srow" href="retros.html">Search retrospectives<span class="sub">›</span></a>`);
  }
  function openSprint() {
    openSheet('sprint', 'Sprint 24', `
      <button class="srow" type="button">Edit goal</button>
      <button class="srow" type="button">Edit sprint<span class="sub">name, dates, goal</span></button>
      <a class="srow" href="sprints.html">All sprints<span class="sub">›</span></a>
      <p class="hint">The sprint header's ⋯, the same on every sprint tab (SHIP-724). Close sprint shows only while the sprint is open.</p>`);
  }
  hmore.addEventListener('click', openSprint);
  sb.addEventListener('click', e => {
    const go = e.target.closest('[data-go]');
    if (go) { openRail(go.dataset.go); return; }
    const th = e.target.closest('[data-pick-theme]');
    if (th) {
      const { p } = RC.pointById(sheet.dataset.point);
      p.theme = th.dataset.pickTheme; closeSheet(); RC.render();
      SL.toast(`Moved to # ${esc(themes[p.theme].name)}`);
      return;
    }
    const act = e.target.closest('[data-note-act]');
    if (act) {
      const id = sheet.dataset.point; closeSheet();
      if (act.dataset.noteAct === 'theme') openTheme(id);
      if (act.dataset.noteAct === 'archive') SL.toast('Point archived. <b>Undo</b>');
    }
  });

  function openTheme(id) {
    const { p } = RC.pointById(id);
    const used = {};
    retros.filter(r => r.sprint === 'S24').forEach(r => r.points.forEach(x => { used[x.theme] = (used[x.theme] ?? 0) + 1; }));
    const rows = Object.entries(themes).filter(([k]) => k !== 'none').sort((a, b) => (used[b[0]] ?? 0) - (used[a[0]] ?? 0));
    openSheet('theme', 'Move to theme', `<div class="ident">${esc(p.text)}<small>${catLabel[p.cat]}</small></div>
      ${rows.map(([k, t]) => `<button class="srow" type="button" role="menuitemradio" aria-checked="${k === p.theme}" data-pick-theme="${k}"><span class="ck">${k === p.theme ? '✓' : ''}</span>${esc(t.name)}<span class="sub">${used[k] ? `${used[k]} this sprint` : ''}</span></button>`).join('')}
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

  /* Taps the page's own code would handle as hover or a toast */
  document.addEventListener('click', e => {
    const t = e.target;
    const theme = t.closest?.('[data-rc-theme]');
    if (theme) { e.stopPropagation(); openTheme(theme.dataset.rcTheme); return; }
    const more = t.closest?.('[data-rc-more]');
    if (more) { e.stopPropagation(); openNote(more.dataset.rcMore); return; }
    if (t.closest?.('.rc-grp-h [data-rc-tip]')) { e.stopPropagation(); openRail('recurring'); return; }
  }, true);

  filterBtn.addEventListener('click', openFilter);
  moreBtn.addEventListener('click', openMore);
  document.querySelector('.sprint-head .kpis').addEventListener('click', e => {
    const k = e.target.closest('.kpi'); if (!k) return;
    if (k.dataset.k === 'decide') {
      const on = pressed(decSeg).dataset.decide === 'open';
      decSeg.querySelector(`[data-decide=${on ? 'all' : 'open'}]`).click();
      if (!on && cat === 'well') { cat = catOrder.find(c => c !== 'well' && openIn(c)) ?? cat; restructure(); }
      return;
    }
    openRail(k.dataset.k);
  });
  tiles.addEventListener('click', e => { const b = e.target.closest('[data-cat]'); if (b) { cat = b.dataset.cat; restructure(); } });

  const openIn = c => retros.filter(r => r.sprint === 'S24').flatMap(r => r.points).filter(p => p.cat === c && RC.isOpen(p)).length;

  /* After each render */
  function restructure() {
    const decideOn = pressed(decSeg).dataset.decide === 'open';
    const n = (pressed(srcSeg).dataset.src !== 'all') + decideOn;
    filterBtn.innerHTML = n ? `Filter <span class="n">${n}</span>` : 'Filter';
    if (n) filterBtn.dataset.n = n; else delete filterBtn.dataset.n;
    kpis[2].setAttribute('aria-pressed', decideOn);

    const board = page.querySelector(':scope > .rc-board');
    tiles.remove();
    const alt = /stack|pager/.test(root.dataset.alt ?? '');
    if (board && !isItems() && !alt) {
      if (decideOn && cat === 'well') cat = catOrder.find(c => c !== 'well' && openIn(c)) ?? 'unplanned';
      board.dataset.cat = cat;
      tiles.innerHTML = catOrder.map(c => {
        const col = board.querySelector(`.rc-col.${c}`);
        const count = +col.querySelector('header .n').textContent;
        const open = openIn(c);
        const sub = c === 'well' ? (decideOn ? 'nothing to decide' : 'never needs deciding') : open ? `${open} to decide` : 'all decided';
        return `<button type="button" class="rt-tile ${c}${count ? '' : ' empty'}" data-cat="${c}" aria-pressed="${c === cat}"><b>${catLabel[c]}</b><span class="n">${count}</span><span class="sub${open && c !== 'well' ? ' todo' : ''}">${sub}</span></button>`;
      }).join('');
      board.before(tiles);
    }

    page.querySelectorAll('.item-block').forEach(block => {
      if (block.querySelector('.rt-sum')) return;
      const head = block.querySelector('header');
      const key = head.querySelector('.key').textContent;
      const counts = catOrder.map(c => { const col = block.querySelector(`.rc-col.${c}`); const k = col ? col.querySelectorAll('.rc-note:not(.rc-editor)').length : 0; if (col && !k && !col.querySelector('.rc-editor')) col.classList.add('rt-empty'); return [c, k]; });
      const rs = retros.filter(r => r.subject === key && r.sprint === 'S24');
      const c = RC.counts(rs);
      head.append(el('span', 'rt-chev', '›'));
      head.append(el('div', 'rt-sum', `${RC.countStrip(Object.fromEntries(counts))}${RC.todo(c.open)}${RC.progress(c)}`));
      head.setAttribute('role', 'button'); head.tabIndex = 0; head.setAttribute('aria-expanded', 'false');
      const open = el('div', 'rt-open', `<a class="btn" href="retro-item.html?key=${key}">Open ${key} retro ›</a>`);
      block.append(open);
      if (q.get('open') === key || openKeys.has(key)) { block.dataset.open = ''; head.setAttribute('aria-expanded', 'true'); }
    });
    pageObs?.takeRecords(); // our own changes to #page are not a render
  }
  const openKeys = new Set();
  page.addEventListener('click', e => {
    const head = e.target.closest('.item-block > header');
    if (!head || e.target.closest('a')) return;
    const block = head.parentElement, key = head.querySelector('.key').textContent;
    const open = !('open' in block.dataset);
    if (open) { block.dataset.open = ''; openKeys.add(key); } else { delete block.dataset.open; openKeys.delete(key); }
    head.setAttribute('aria-expanded', open);
  });
  var pageObs = new MutationObserver(() => restructure());
  pageObs.observe(page, { childList: true });

  /* New action item: full screen. Create moves into the header; Cancel goes (✕ does it); the footer note goes to the
     end of the body. */
  new MutationObserver(muts => muts.forEach(m => m.addedNodes.forEach(n => {
    if (!(n instanceof HTMLElement) || !n.classList.contains('rc-overlay')) return;
    const dlg = n.querySelector('.rc-dialog');
    const create = dlg.querySelector('[data-create]');
    create.classList.add('rt-create');
    create.textContent = 'Create';
    dlg.querySelector('header').append(create);
    dlg.querySelector('header .seg').addEventListener('click', e => { const b = e.target.closest('[data-mode]'); if (b) create.textContent = b.dataset.mode === 'new' ? 'Create' : 'Add'; });
    const noteEl = dlg.querySelector('footer [data-only]');
    if (noteEl) { noteEl.classList.add('rt-foot-note'); dlg.querySelector('.body').append(noteEl); }
    dlg.querySelector('#rcTitle')?.blur();
  }))).observe(document.body, { childList: true });

  restructure();

  if (q.get('dialog')) RC.openActionDialog([q.get('dialog')], retros.filter(r => r.sprint === 'S24'));
  const s = q.get('sheet');
  if (s === 'filter') openFilter();
  else if (s === 'more') openMore();
  else if (s === 'sprint') openSprint();
  else if (['coverage', 'actions', 'recurring'].includes(s)) openRail(s);
  else if (s?.startsWith('theme:')) openTheme(s.slice(6));
  else if (s?.startsWith('note:')) openNote(s.slice(5));
})();
