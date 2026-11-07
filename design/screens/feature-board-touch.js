/*
 * The Feature board in the touch shell (SHIP-722); styles in feature-board-touch.css. Loaded after feature-board.html's
 * own script, whose state and move it reuses (SL.fb). At 900px and below it draws the board as SHIP-719's boards do:
 *   - a pager with one page per task state (the next one peeks) and state chips above it with their counts
 *   - on every page, every story or bug of the Feature as a lane (D4): its compact index card, then its tasks in that
 *     state, or None; on a parent Feature a band per Feature; grouped by People the people are the lanes
 *   - closed stories as one-line lanes in backlog order, as on desktop (D8, changed 7 Nov in the SHIP-725 review)
 *   - a ⋯ on every task opening its actions (Move to a state, another story, Assignee, Open); a long press lifts the
 *     task to drag it to another lane, or to the screen's edge to turn the page; let go without moving and the actions
 *     open (D2). Tapping a task opens it (D3). After a move the pager stays on the page you were reading.
 * Moves go through the board's own move, so auto-assign on Active and "never changes the sprint" hold.
 * Above 900px all of it is hidden. Review states (add ?shot for screenshots):
 *   ?col=active                              turns to that state's page (its name, lower case, - for spaces)
 *   ?sheet=task|story|assign&task=SHIP-172   a task's action sheet, or its Move to story / Assign sheet
 *   ?sheet=filter, ?sheet=more               the Filter sheet and the ⋯ More action sheet
 *   ?filter=mei,none                         starts filtered to those people
 *   ?add                                     the "+ Add" row (story or bug) open
 *   ?addtask=SHIP-158                        the "+ Task" field open on that story (on the first page)
 *   ?drag=SHIP-176&to=active                 a task lifted and held over its lane on that page
 *   ?at=SHIP-158                             with ?shot, hides what is above that lane or Feature band on every page
 *                                            (Chrome's --screenshot ignores scrolling)
 */
(() => {
  const F = SL.fb;
  if (!F) return;
  const { S, columns, tree, cat } = F;
  const { people, ME, esc, tIcon, av, toast, states } = SL;
  const q = new URLSearchParams(location.search);
  const root = document.documentElement;
  const touch = () => matchMedia('(max-width: 900px)').matches;
  const el = (tag, cls, html) => { const e = document.createElement(tag); if (cls) e.className = cls; if (html != null) e.innerHTML = html; return e; };
  const svg = d => `<svg viewBox="0 0 16 16" width="16" height="16" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="${d}"/></svg>`;
  const dots = '<svg viewBox="0 0 16 16" width="16" height="16" aria-hidden="true"><circle cx="3.5" cy="8" r="1.4" fill="currentColor"/><circle cx="8" cy="8" r="1.4" fill="currentColor"/><circle cx="12.5" cy="8" r="1.4" fill="currentColor"/></svg>';
  const ICON = { filter: svg('M2.5 3.5h11l-4.2 5v4l-2.6 1.2V8.5z'), open: svg('M6 3h7v7M13 3 4 12'), go: svg('m6 3 5 5-5 5') };
  const slug = c => c.toLowerCase().replace(/\s+/g, '-');

  /* Toolbar row, the "+ Add" row, the state chips and the pager, in the grid's place. */
  const toolbar = document.querySelector('.toolbar');
  const bar = el('div', 'fbt-bar');
  toolbar.append(bar);
  const addRow = el('div', 'fbt-addstory', `<select class="field" aria-label="Type"><option>Story</option><option>Bug</option></select>
    <input class="field" aria-label="Title" placeholder="Title of the story or bug" enterkeyhint="done">
    <button class="btn primary" type="button">Add</button>
    <p class="hint">Adds it under ${F.KEY}, in the Backlog. Stays open for the next one.</p>`);
  addRow.hidden = !q.has('add');
  toolbar.after(addRow);
  const strip = el('nav', 'fbt-strip');
  strip.setAttribute('aria-label', 'Task states');
  addRow.after(strip);
  const pager = el('div', 'fbt-pager');
  pager.setAttribute('aria-label', 'Feature board');
  document.getElementById('board').after(pager);
  const sheet = el('div', 'fbt-sheet');
  sheet.hidden = true;
  sheet.setAttribute('role', 'dialog');
  sheet.setAttribute('aria-modal', 'true');
  document.body.append(sheet);

  let addingTo = q.get('addtask');
  let current = 0;
  if (q.get('filter')) { q.get('filter').split(',').filter(p => p === 'none' || people[p]).forEach(p => S.filter.add(p)); F.render(); }

  const sprintLabel = id => id ? F.sprintName(id) : 'Backlog';
  const tasksOf = key => S.tasks.filter(t => t.parent === key);
  const shownIn = c => S.tasks.filter(F.visible).filter(t => t.state === c);

  function drawBar() {
    const n = S.filter.size;
    const faces = n ? `<span class="fbt-face-row">${[...S.filter].slice(0, 3).map(p => p === 'none' ? '<span class="av none"></span>' : av(p)).join('')}</span>` : '';
    bar.innerHTML = `
      <div class="seg" role="group" aria-label="Group by">${['stories', 'people'].map(g => `<button type="button" data-g="${g}" aria-pressed="${S.group === g}">${g === 'stories' ? 'Stories' : 'People'}</button>`).join('')}</div>
      <button class="btn fbt-filter" type="button" data-open="filter"${n ? ` data-n="${n}"` : ''} aria-label="Filter${n ? `, ${n} on` : ''}">${n ? faces : ICON.filter}Filter${n ? ` <span class="n">${n}</span>` : ''}</button>
      <button class="btn fbt-more" type="button" data-open="more" aria-label="More" aria-haspopup="dialog">${dots}</button>
      <span class="spacer"></span>
      <button class="btn primary" type="button" data-add aria-expanded="${!addRow.hidden}">+ Add</button>`;
  }

  function drawStrip() {
    strip.innerHTML = columns.map((c, i) => `<button type="button" data-go="${i}" aria-current="${i === current}"><span class="st ${cat(c)}">${c}</span><span class="n">${shownIn(c).length}</span></button>`).join('');
  }

  function card(t) {
    const r = F.ruleFor(t), p = F.parentOf(t), multi = tree.length > 1;
    const swatch = { danger: 'var(--color-danger)', warn: 'var(--color-warn)', info: 'var(--color-info)' };
    const tagColors = { Blocked: 'danger', Spike: 'info', 'Tech debt': 'warn' };
    const tags = t.tags.map(g => `<span class="tag" style="--tag:${swatch[tagColors[g]] || 'var(--color-muted)'}">${esc(g)}</span>`).join('');
    const par = S.group === 'people' ? `<div class="par">${multi ? `${tIcon('feature')}<span>${esc(F.featureOf(p.feature).title)}</span><span>›</span>` : ''}${tIcon(p.type)}<span>${esc(p.title)}</span></div>` : '';
    return `<article class="card${r ? ' styled' : ''}${r && r.bold ? ' bold' : ''}" data-key="${t.key}" data-type="task"${r ? ` style="--rule:${swatch[r.color]}"` : ''} tabindex="0" aria-label="${esc(t.title)}, ${t.state}">
      ${par}<div class="ct">${tIcon('task')}<span class="title">${esc(t.title)}</span></div>
      <button class="fbt-cm" type="button" data-task="${t.key}" aria-label="Actions for ${t.key}" aria-haspopup="dialog">${dots}</button>
      <div class="cm"><span class="key">${t.key}</span>${tags}<span class="spacer"></span>${t.assignee ? av(t.assignee) : '<span class="av none" title="Unassigned"></span>'}</div>
    </article>`;
  }

  // One story's lane on one page: its compact index card, then its tasks in this state, or None.
  function storyLane(p, c, ci) {
    const all = tasksOf(p.key), here = all.filter(t => t.state === c), shown = here.filter(F.visible);
    const done = all.filter(t => cat(t.state) === 'completed').length;
    const collapsed = S.collapsed.has(p.key);
    const toggle = `<button class="ib" type="button" data-collapse="${p.key}" aria-expanded="${!collapsed}" aria-label="${collapsed ? 'Expand' : 'Collapse'} ${p.key}">${collapsed ? '▶' : '▼'}</button>`;
    if (collapsed) return `<div class="fbt-lane" data-at="${p.key}"><div class="rowh collapsed">${toggle}${tIcon(p.type)}<span class="t1">${esc(p.title)}</span><span class="cnt">${shown.length ? `${shown.length} here · ` : ''}☑ ${done}/${all.length}</span><span class="st ${cat(p.state)}">${p.state}</span></div></div>`;
    const stSel = `<span class="stsel"><span class="st ${cat(p.state)}"></span><select data-pstate="${p.key}" aria-label="State of ${p.key}">${states.story.filter(s => cat(s) !== 'removed').map(n => `<option${n === p.state ? ' selected' : ''}>${n}</option>`).join('')}</select></span>`;
    const prog = all.length ? `<span class="prog">☑ ${done}/${all.length}<span class="bar"><i style="width:${Math.round(done / all.length * 100)}%"></i></span></span>` : '';
    const hidden = here.length - shown.length;
    const adding = ci === 0 ? (addingTo === p.key
      ? `<div class="fbt-addtask"><input class="field" aria-label="Task title" placeholder="Task title" enterkeyhint="done"><button class="btn primary" type="button" data-addtask-go="${p.key}">Add</button></div>`
      : `<button class="fbt-add" type="button" data-addtask="${p.key}">+ Task</button>`) : '';
    return `<div class="fbt-lane" data-at="${p.key}">
      <div class="rowh">
        <div class="top">${toggle}${tIcon(p.type)}<span class="t1">${esc(p.title)}</span><span class="pts">${p.points} pts</span></div>
        <div class="meta"><span class="key">${p.key}</span>${stSel}${av(p.assignee)}${prog}</div>
      </div>
      <div class="fbt-cell" data-row="${p.key}" data-col="${c}">${shown.map(card).join('')}${!shown.length && !hidden ? '<div class="fbt-none">None</div>' : ''}${hidden ? `<div class="fbt-hidden">${hidden}${shown.length ? ' more' : ''} hidden by Filter</div>` : ''}${adding}</div>
    </div>`;
  }

  function sectionHead(f, direct) {
    const r = F.rollup([f.key]), closed = S.closedSections.has(f.key);
    const toggle = `<button class="ib" type="button" data-section="${f.key}" aria-expanded="${!closed}" aria-label="${closed ? 'Expand' : 'Collapse'} ${f.key}">${closed ? '▶' : '▼'}</button>`;
    if (direct) return `<div class="fbt-fh direct" data-at="${f.key}">${toggle}<span class="nm"><b>Directly under ${f.key}</b></span><span class="n">${r.closed}/${r.stories} closed</span></div>`;
    return `<div class="fbt-fh" data-at="${f.key}">${toggle}<span class="nm">${tIcon('feature')}<span class="key">${f.key}</span><b>${esc(f.title)}</b></span><a class="open" href="${f.key === 'SHIP-101' ? 'feature.html' : '#'}" aria-label="Open ${f.key}">${ICON.open}</a></div>`;
  }

  function storiesPage(c, ci) {
    return tree.map((f, i) => {
      const parents = S.parents.filter(p => p.feature === f.key);
      const multi = tree.length > 1;
      if (i === 0 && multi && !parents.length) return '';
      const head = multi ? sectionHead(f, i === 0) : '';
      if (multi && S.closedSections.has(f.key)) return head;
      // Backlog order; Completed stories start collapsed to one line (S.collapsed), as on desktop (D8).
      const lanes = parents.map(p => storyLane(p, c, ci)).join('');
      return head + (parents.length ? lanes : '<p class="fbt-empty">No stories or bugs yet.</p>');
    }).join('');
  }

  function peoplePage(c) {
    const all = S.tasks.filter(F.visible);
    const rows = ['none', ...Object.keys(people).sort((a, b) => people[a].name.localeCompare(people[b].name))].filter(r => all.some(t => (t.assignee || 'none') === r));
    return rows.map(row => {
      const ts = all.filter(t => (t.assignee || 'none') === row && t.state === c);
      const label = row === 'none' ? '<span class="av none"></span><b>Unassigned</b>' : `${av(row)}<b>${people[row].name}${row === ME ? ' (you)' : ''}</b>`;
      return `<div class="fbt-lane" data-at="${row}"><div class="fbt-ph">${label}<span class="hint">${all.filter(t => (t.assignee || 'none') === row).length} tasks</span></div>
        <div class="fbt-cell" data-row="${row}" data-col="${c}">${ts.map(card).join('') || '<div class="fbt-none">None</div>'}</div></div>`;
    }).join('');
  }

  function draw() {
    drawBar();
    const left = pager.scrollLeft;
    pager.innerHTML = columns.map((c, ci) => `<section class="fbt-page" data-col="${slug(c)}" aria-label="${c}">
      <div class="fbt-colh"><span class="st ${cat(c)}">${c}</span><span class="n">${shownIn(c).length}</span></div>
      ${S.group === 'people' ? peoplePage(c) : storiesPage(c, ci)}</section>`).join('');
    pager.scrollLeft = left;
    drawStrip();
    const at = q.get('at');
    if (at && q.has('shot')) {
      [document.querySelector('.fb-head'), toolbar, addRow, strip].forEach(x => { x.style.display = 'none'; });
      pager.querySelectorAll('.fbt-page').forEach(pg => {
        const target = pg.querySelector(`[data-at="${at}"]`);
        if (!target) return;
        for (let s = target.previousElementSibling; s && !s.classList.contains('fbt-colh'); s = s.previousElementSibling) s.style.display = 'none';
      });
    }
  }

  /* Turning pages */
  const pages = () => [...pager.querySelectorAll('.fbt-page')];
  const pageWidth = () => { const p = pages(); return p[1] ? p[1].offsetLeft - p[0].offsetLeft : pager.clientWidth; };
  function goTo(i, smooth) {
    const p = pages()[Math.max(0, Math.min(i, columns.length - 1))];
    if (p) pager.scrollTo({ left: p.offsetLeft - parseFloat(getComputedStyle(pager).paddingLeft), behavior: smooth ? 'smooth' : 'instant' });
  }
  pager.addEventListener('scroll', () => {
    const i = Math.round(pager.scrollLeft / pageWidth());
    if (i === current) return;
    current = i;
    strip.querySelectorAll('[data-go]').forEach(b => b.setAttribute('aria-current', +b.dataset.go === i));
  }, { passive: true });
  strip.addEventListener('click', e => { const b = e.target.closest('[data-go]'); if (b) goTo(+b.dataset.go, true); });

  /* Sheets: one at a time, over the shell's scrim (<html data-sheet="fbt">). */
  let sheetState = null;
  function openSheet(kind, key) {
    sheetState = { kind, key };
    root.dataset.sheet = 'fbt';
    sheet.hidden = false;
    pager.querySelectorAll('.card.lifted').forEach(c => c.classList.remove('lifted'));
    if (key) pager.querySelectorAll(`.card[data-key="${key}"]`).forEach(c => c.classList.add('lifted'));
    drawSheet();
  }
  function closeSheet() {
    sheetState = null;
    sheet.hidden = true;
    pager.querySelectorAll('.card.lifted').forEach(c => c.classList.remove('lifted'));
    if (root.dataset.sheet === 'fbt') delete root.dataset.sheet;
  }
  const head = (title, extra = '') => `<header class="sheet-head"><span class="who">${extra}<b>${title}</b></span><button class="btn ghost" type="button" data-close>Close</button></header>`;
  const personName = p => p ? `${people[p].name}${p === ME ? ' (you)' : ''}` : 'Unassigned';

  function drawSheet() {
    if (!sheetState) return;
    const { kind, key } = sheetState;
    const t = S.tasks.find(x => x.key === key);
    if (kind === 'task') {
      const p = F.parentOf(t);
      sheet.setAttribute('aria-label', `Actions for ${t.key}`);
      sheet.innerHTML = `${head(esc(t.title), `${tIcon('task')}<span class="key">${t.key}</span>`)}<div class="body">
        <div class="lbl">Move to</div>
        ${columns.map(c => `<button class="opt" type="button" role="menuitemradio" aria-checked="${c === t.state}" data-state="${c}"><span class="chk">${c === t.state ? '✓' : ''}</span><span class="st ${cat(c)}">${c}</span>${c === t.state ? '<span class="val">here</span>' : ''}</button>`).join('')}
        <hr>
        <button class="opt" type="button" data-to="story"><span class="tx"><span>Move to another story</span><small>${tIcon(p.type)} ${p.key} ${esc(p.title)}</small></span><span class="go">${ICON.go}</span></button>
        <button class="opt" type="button" data-to="assign"><span class="tx"><span>Assignee</span></span><span class="val">${personName(t.assignee)}</span><span class="go">${ICON.go}</span></button>
        <button class="opt" type="button" data-openitem="${t.key}"><span class="tx"><span>Open ${t.key}</span><small>Full screen</small></span><span class="go">${ICON.open}</span></button>
        <p class="note">Or press and hold the task, then drag it to another story or to the screen's edge for another state.</p>
      </div>`;
    } else if (kind === 'story') {
      const typed = sheet.querySelector('.msearch .field')?.value || '';
      const term = typed.toLowerCase();
      const groups = tree.map(f => ({ f, ps: S.parents.filter(p => p.feature === f.key && (!term || `${p.key} ${p.title}`.toLowerCase().includes(term))) })).filter(g => g.ps.length);
      sheet.setAttribute('aria-label', `Move ${t.key} to another story`);
      sheet.innerHTML = `${head('Move to story', `<button class="btn ghost" type="button" data-back aria-label="Back">‹</button>`)}
        <div class="msearch"><input class="field" type="search" placeholder="Find a story or bug" aria-label="Find a story or bug" value="${esc(typed)}"></div><div class="body">
        ${groups.map(g => `${tree.length > 1 ? `<div class="grp">${tIcon('feature')}<span class="key">${g.f.key}</span>${esc(g.f.title)}</div>` : ''}${g.ps.map(p => `<button class="opt" type="button" role="menuitemradio" aria-checked="${p.key === t.parent}" data-parent="${p.key}"><span class="chk">${p.key === t.parent ? '✓' : ''}</span>${tIcon(p.type)}<span class="tx"><span>${esc(p.title)}</span><small>${p.key} · ${sprintLabel(p.sprint)} · ${p.state}</small></span></button>`).join('')}`).join('') || '<p class="note">Nothing matches.</p>'}
        <p class="note">Moving a task never changes its sprint.</p></div>`;
      if (typed) { const f = sheet.querySelector('.msearch .field'); f.focus(); f.setSelectionRange(typed.length, typed.length); }
    } else if (kind === 'assign') {
      const opts = [...Object.keys(people), null];
      sheet.setAttribute('aria-label', `Assign ${t.key}`);
      sheet.innerHTML = `${head('Assignee', `<button class="btn ghost" type="button" data-back aria-label="Back">‹</button>`)}<div class="body">
        ${opts.map(p => `<button class="opt" type="button" role="menuitemradio" aria-checked="${p === t.assignee}" data-person="${p || 'none'}"><span class="chk">${p === t.assignee ? '✓' : ''}</span>${p ? av(p) : '<span class="av none"></span>'}<span class="tx"><span>${personName(p)}</span></span></button>`).join('')}
      </div>`;
    } else if (kind === 'filter') {
      const count = p => S.tasks.filter(t => (t.assignee || 'none') === p).length;
      sheet.setAttribute('aria-label', 'Filter');
      sheet.innerHTML = `${head('Show tasks for')}<div class="body">
        ${[...Object.keys(people), 'none'].map(p => `<label class="opt"><input type="checkbox" data-face="${p}"${S.filter.has(p) ? ' checked' : ''}>${p === 'none' ? '<span class="av none"></span>' : av(p)}<span class="tx"><span>${personName(p === 'none' ? null : p)}</span></span><span class="val">${count(p)}</span></label>`).join('')}
        <hr><button class="opt" type="button" data-clear${S.filter.size ? '' : ' disabled'}><span class="tx"><span>Show everyone's tasks</span></span></button>
        <p class="note">Every story keeps its lane; tasks that don't match are counted, not shown.</p>
      </div>`;
    } else if (kind === 'more') {
      const f = tree[0];
      sheet.setAttribute('aria-label', 'More');
      sheet.innerHTML = `${head('Feature board')}<div class="body">
        <button class="opt" type="button" data-all="collapse"><span class="tx"><span>Collapse all stories</span></span></button>
        <button class="opt" type="button" data-all="expand"><span class="tx"><span>Expand all stories</span></span></button>
        <hr>
        <a class="opt" href="taskboard.html?dialog=settings"><span class="tx"><span>Card settings</span><small>Shared with the Taskboard</small></span><span class="go">${ICON.go}</span></a>
        <a class="opt" href="${f.key === 'SHIP-101' ? 'feature.html' : '#'}"><span class="tx"><span>Open ${f.key}</span><small>${esc(f.title)}</small></span><span class="go">${ICON.open}</span></a>
      </div>`;
    }
  }

  // The row a move goes to: the story when grouped by Stories, the person when grouped by People (the desktop's move).
  const rowOf = t => S.group === 'stories' ? t.parent : (t.assignee || 'none');

  sheet.addEventListener('click', e => {
    const s = sheetState; if (!s) return;
    const t = S.tasks.find(x => x.key === s.key);
    if (e.target.closest('[data-close]')) return closeSheet();
    if (e.target.closest('[data-back]')) { s.kind = 'task'; return drawSheet(); }
    const st = e.target.closest('[data-state]');
    if (st) { closeSheet(); if (st.dataset.state !== t.state) F.move(t.key, rowOf(t), st.dataset.state); return; }
    const to = e.target.closest('[data-to]');
    if (to) { s.kind = to.dataset.to; return drawSheet(); }
    const par = e.target.closest('[data-parent]');
    if (par) {
      closeSheet();
      if (par.dataset.parent === t.parent) return;
      if (S.group === 'stories') F.move(t.key, par.dataset.parent, t.state);
      else { t.parent = par.dataset.parent; F.render(); toast(`“${esc(t.title)}” moved under ${t.parent}`); }
      return;
    }
    const per = e.target.closest('[data-person]');
    if (per) {
      closeSheet();
      const a = per.dataset.person === 'none' ? null : per.dataset.person;
      if (a === t.assignee) return;
      if (S.group === 'people') F.move(t.key, per.dataset.person, t.state);
      else { t.assignee = a; F.render(); toast(`“${esc(t.title)}” ${a ? `assigned to ${a === ME ? 'you' : people[a].name}` : 'unassigned'}`); }
      return;
    }
    const oi = e.target.closest('[data-openitem]');
    if (oi) { closeSheet(); toast(`Opens <b>${oi.dataset.openitem}</b> full screen over the board (the item modal, SHIP-714); ✕ comes back here`); return; }
    if (e.target.closest('[data-clear]')) { S.filter.clear(); F.render(); drawSheet(); return; }
    const all = e.target.closest('[data-all]');
    if (all) {
      closeSheet();
      S.parents.forEach(p => all.dataset.all === 'collapse' ? S.collapsed.add(p.key) : S.collapsed.delete(p.key));
      F.render();
    }
  });
  sheet.addEventListener('change', e => {
    const f = e.target.closest('[data-face]'); if (!f) return;
    f.checked ? S.filter.add(f.dataset.face) : S.filter.delete(f.dataset.face);
    F.render();
  });
  sheet.addEventListener('input', e => { if (e.target.closest('.msearch')) drawSheet(); });
  document.querySelector('.scrim')?.addEventListener('click', () => { if (sheetState) closeSheet(); }, true);
  document.addEventListener('keydown', e => { if (e.key === 'Escape' && sheetState) closeSheet(); });

  bar.addEventListener('click', e => {
    const g = e.target.closest('[data-g]'); if (g) { F.setGroup(g.dataset.g); return; }
    const o = e.target.closest('[data-open]'); if (o) { openSheet(o.dataset.open); return; }
    if (e.target.closest('[data-add]')) { addRow.hidden = !addRow.hidden; drawBar(); if (!addRow.hidden) addRow.querySelector('input').focus(); }
  });
  addRow.querySelector('.btn').addEventListener('click', () => {
    const input = addRow.querySelector('input');
    if (!input.value.trim()) { input.focus(); return; }
    toast(`${addRow.querySelector('select').value} “${esc(input.value.trim())}” added under ${F.KEY}`);
    input.value = ''; input.focus();
  });

  /* Taps in the pager: collapse, sections, closed stories, + Task, ⋯, and opening a task. */
  let suppressClick = false;
  pager.addEventListener('click', e => {
    if (suppressClick) { suppressClick = false; e.stopPropagation(); e.preventDefault(); return; }
    const c = e.target.closest('[data-collapse]'); if (c) { const k = c.dataset.collapse; S.collapsed.has(k) ? S.collapsed.delete(k) : S.collapsed.add(k); F.render(); return; }
    const s = e.target.closest('[data-section]'); if (s) { const k = s.dataset.section; S.closedSections.has(k) ? S.closedSections.delete(k) : S.closedSections.add(k); F.render(); return; }
    const m = e.target.closest('[data-task]'); if (m) { openSheet('task', m.dataset.task); return; }
    const a = e.target.closest('[data-addtask]'); if (a) { addingTo = a.dataset.addtask; draw(); pager.querySelector('.fbt-addtask .field').focus(); return; }
    const go = e.target.closest('[data-addtask-go]');
    if (go) { const f = pager.querySelector('.fbt-addtask .field'); if (f.value.trim()) { toast(`Task “${esc(f.value.trim())}” added to ${go.dataset.addtaskGo}`); f.value = ''; } f.focus(); return; }
    const k = e.target.closest('.card'); if (k && !e.target.closest('button, a, select, input')) toast(`Opens <b>${k.dataset.key}</b> full screen over the board (the item modal, SHIP-714); ✕ comes back here`);
  }, true);
  pager.addEventListener('change', e => {
    const sel = e.target.closest('[data-pstate]'); if (!sel) return;
    const p = S.parents.find(x => x.key === sel.dataset.pstate); p.state = sel.value;
    if (cat(p.state) === 'completed') S.collapsed.add(p.key);
    F.render(); toast(`${p.key} → ${esc(p.state)}${cat(p.state) === 'completed' ? ' (collapsed to one line)' : ''}`);
  });

  /* Long press and touch drag (D2, as SHIP-719). A long press lifts the task: drag it to another lane on this page, or
     hold it at the screen's left or right edge to turn to another state; near the top or bottom the page scrolls. Let
     go without moving and the task's actions open instead. The drop goes through the board's own move. */
  let press = null, drag = null;
  const edges = ['left', 'right'].map(side => { const x = el('div', `fbt-edge ${side}`, side === 'left' ? '‹' : '›'); x.setAttribute('aria-hidden', 'true'); document.body.append(x); return x; });
  pager.addEventListener('pointerdown', e => {
    const card = e.target.closest('.card');
    if (!card || e.target.closest('button, a, input, select') || e.button > 0) return;
    press = { card, x: e.clientX, y: e.clientY, timer: setTimeout(() => lift(card, press.x, press.y), 450) };
  });
  const cancelPress = () => { if (press) clearTimeout(press.timer); press = null; };
  const clearDrop = () => pager.querySelectorAll('.drop-before, .drop-end, .fbt-cell.over').forEach(x => x.classList.remove('drop-before', 'drop-end', 'over'));
  function target(x, y) {
    const hit = document.elementFromPoint(x, y);
    const cell = hit?.closest('.fbt-cell');
    if (cell) return cell;
    // Over a lane's card or between pages: that lane's cell, or the task's own lane, in the nearest page
    const lane = hit?.closest('.fbt-lane');
    if (lane) return lane.querySelector('.fbt-cell');
    const page = hit?.closest('.fbt-page') || (hit?.closest('.fbt-pager') && pages().reduce((a, pg) => {
      const r = pg.getBoundingClientRect(), d = x < r.left ? r.left - x : x > r.right ? x - r.right : 0;
      return !a || d < a.d ? { pg, d } : a;
    }, null)?.pg);
    return page ? page.querySelector(`.fbt-cell[data-row="${drag.row}"]`) : null;
  }
  function lift(card, x, y) {
    if (!press) return;
    navigator.vibrate?.(10);
    const r = card.getBoundingClientRect();
    const ghost = card.cloneNode(true);
    ghost.classList.add('fbt-ghost');
    ghost.classList.remove('flash');
    ghost.style.width = r.width + 'px';
    document.body.append(ghost);
    card.classList.add('dragging');
    drag = { key: card.dataset.key, card, ghost, row: card.closest('.fbt-cell').dataset.row, dx: x - r.left, dy: y - r.top, x, y, sx: x, sy: y, moved: false, edgeDir: 0, edgeSince: 0, raf: 0 };
    root.dataset.dragging = '';
    place(x, y);
    drag.raf = requestAnimationFrame(tick);
  }
  function place(x, y) {
    drag.x = x; drag.y = y;
    drag.moved = drag.moved || Math.hypot(x - drag.sx, y - drag.sy) > 8;
    drag.ghost.style.transform = `translate(${x - drag.dx}px, ${y - drag.dy}px) rotate(-2deg)`;
    clearDrop();
    const cell = target(x, y);
    if (!cell) return;
    cell.classList.add('over');
    const b = [...cell.querySelectorAll(':scope > .card:not(.dragging)')].find(c => { const r = c.getBoundingClientRect(); return y < r.top + r.height / 2; });
    if (b) b.classList.add('drop-before'); else cell.classList.add('drop-end');
  }
  function tick(t) {
    if (!drag) return;
    const dir = drag.x > innerWidth - 32 ? 1 : drag.x < 32 ? -1 : 0;
    edges.forEach((x, i) => x.classList.toggle('hot', dir === (i ? 1 : -1)));
    if (dir !== drag.edgeDir) { drag.edgeDir = dir; drag.edgeSince = t; }
    else if (dir && t - drag.edgeSince > 500) { goTo(current + dir, true); drag.edgeSince = t + 300; }
    const top = (document.querySelector('.appbar')?.getBoundingClientRect().bottom || 0) + 40;
    if (drag.y < top) window.scrollBy(0, -10); else if (drag.y > innerHeight - 48) window.scrollBy(0, 10);
    place(drag.x, drag.y);
    drag.raf = requestAnimationFrame(tick);
  }
  function endDrag() {
    const d = drag;
    cancelAnimationFrame(d.raf);
    d.ghost.remove();
    d.card.classList.remove('dragging');
    edges.forEach(x => x.classList.remove('hot'));
    clearDrop();
    delete root.dataset.dragging;
    drag = null;
    cancelPress();
    return d;
  }
  document.addEventListener('pointermove', e => {
    if (drag) place(e.clientX, e.clientY);
    else if (press && Math.hypot(e.clientX - press.x, e.clientY - press.y) > 8) cancelPress(); // a scroll, not a press
  });
  document.addEventListener('touchmove', e => { if (drag) e.preventDefault(); }, { passive: false });
  document.addEventListener('pointerup', e => {
    if (!drag) { cancelPress(); return; }
    const cell = target(e.clientX, e.clientY), d = endDrag();
    suppressClick = true;
    setTimeout(() => { suppressClick = false; }, 300);
    if (!d.moved) { openSheet('task', d.key); return; }
    if (!cell) return;
    const before = [...cell.querySelectorAll(':scope > .card')].find(c => c.dataset.key !== d.key && e.clientY < c.getBoundingClientRect().top + c.getBoundingClientRect().height / 2);
    F.move(d.key, cell.dataset.row, cell.dataset.col, before?.dataset.key || null);
  });
  document.addEventListener('pointercancel', () => { if (drag) endDrag(); cancelPress(); });
  pager.addEventListener('contextmenu', e => { if (e.target.closest('.card')) e.preventDefault(); });

  /* Review states */
  document.addEventListener('fb:render', () => { draw(); drawSheet(); });
  draw();
  const col = q.get('col');
  if (col) { current = -1; goTo(columns.findIndex(c => slug(c) === col), false); }
  if (!touch()) return;
  requestAnimationFrame(() => {
    const k = q.get('sheet');
    if (k) openSheet(k, ['task', 'story', 'assign'].includes(k) ? q.get('task') || 'SHIP-172' : null);
    // ?drag=KEY&to=<state>: the task lifted and held over its own lane on that page, for a screenshot
    const card = q.get('drag') && pager.querySelector(`.card[data-key="${q.get('drag')}"]`);
    if (!card) return;
    const r = card.getBoundingClientRect();
    press = { card, x: r.left + r.width / 2, y: r.top + 20 };
    lift(card, press.x, press.y);
    cancelAnimationFrame(drag.raf);
    const i = columns.findIndex(c => slug(c) === q.get('to'));
    if (i < 0) return;
    goTo(i, false);
    requestAnimationFrame(() => {
      const c = pages()[i].querySelector(`.fbt-cell[data-row="${drag.row}"]`);
      const cr = c.getBoundingClientRect();
      place(cr.left + cr.width * .45, cr.top + 18);
    });
  });
})();
