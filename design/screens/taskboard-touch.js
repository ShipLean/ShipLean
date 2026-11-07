/*
 * The sprint Taskboard in the touch shell (SHIP-725); styles in taskboard-touch.css. Loaded after taskboard.html's own
 * script, whose state and move it reuses (SL.tb). At 900px and below it draws the board as SHIP-722's Feature board:
 *   - a pager with one page per task state (the next one peeks) and state chips above it with their counts
 *   - on every page, every open story or bug of the sprint as a lane: its compact index card, then its tasks in that
 *     state, or None; closed stories as one-line lanes in backlog order, as desktop (D8); Unparented last (D12)
 *   - grouped by People, everyone on the project is a lane (D16)
 *   - a ⋯ on every task opening its actions (Pick up, Move to a state, another story, Assignee, Open); a long press
 *     lifts the task to drag it to another lane, or to the screen's edge to turn the page; let go without moving and
 *     the actions open. Tapping a task opens it. After a move the pager stays on the page you were reading.
 *   - what only the Taskboard has: SHIP-724's shared sprint header (picker sheet, ⋯, goal, tabs; D10, D11, D17), Pick up
 *     on the card (D13), "All tasks done" in the lane (D14), the settings dialog full screen (D15), and a teammate's move
 *     pulsing the chip of the state it landed in.
 * Moves go through the board's own move, so auto-assign on Active holds. Above 900px all of it is hidden.
 * Review states (add ?shot for screenshots):
 *   ?col=active                              turns to that state's page (its name, lower case, - for spaces)
 *   ?sheet=task|story|assign&task=SHIP-172   a task's action sheet, or its Move to story / Assign sheet
 *   ?sheet=filter|more                       the Filter sheet and the board's ⋯ More
 *   ?sheet=sprint|sprintmore                 the sprint picker and the header's ⋯ (SHIP-724's shared header)
 *   ?filter=mei,none                         starts filtered to those people
 *   ?alldone                                 SHIP-144's last task closed, so its lane offers "Move to Resolved"
 *   ?add                                     the "+ Add" row (story or bug) open
 *   ?addtask=SHIP-141                        the "+ Task" field open on that story (on the first page)
 *   ?drag=SHIP-176&to=active                 a task lifted and held over its lane on that page
 *   ?at=SHIP-129                             with ?shot, hides what is above that lane on every page
 *                                            (Chrome's --screenshot ignores scrolling)
 *   ?bump=active                             that state's chip pulsing, as after a teammate's move
 */
(() => {
  const F = SL.tb;
  if (!F) return;
  const { S, columns, cat, ORPHAN } = F;
  const { people, ME, esc, tIcon, av, toast, states, sprints } = SL;
  const q = new URLSearchParams(location.search);
  const root = document.documentElement;
  const touch = () => matchMedia('(max-width: 900px)').matches;
  const el = (tag, cls, html) => { const e = document.createElement(tag); if (cls) e.className = cls; if (html != null) e.innerHTML = html; return e; };
  const svg = d => `<svg viewBox="0 0 16 16" width="16" height="16" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="${d}"/></svg>`;
  const dots = '<svg viewBox="0 0 16 16" width="16" height="16" aria-hidden="true"><circle cx="3.5" cy="8" r="1.4" fill="currentColor"/><circle cx="8" cy="8" r="1.4" fill="currentColor"/><circle cx="12.5" cy="8" r="1.4" fill="currentColor"/></svg>';
  const ICON = { filter: svg('M2.5 3.5h11l-4.2 5v4l-2.6 1.2V8.5z'), open: svg('M6 3h7v7M13 3 4 12'), go: svg('m6 3 5 5-5 5') };
  const slug = c => c.toLowerCase().replace(/\s+/g, '-');
  const firstActive = columns.find(c => cat(c) === 'in-progress');

  if (q.has('alldone')) { const t = S.tasks.find(x => x.key === 'SHIP-176'); if (t) { t.state = 'Closed'; t.assignee = 'mei'; } }

  /* Sprint header: SHIP-724's shared phone header (its D5, D6, D10): picker · Current · ⋯, then the dates, the goal
     (two lines, tap to expand) and the tabs. The picker and ⋯ open sheets. */
  const shead = document.querySelector('.sprint-head');
  const s24 = sprints.find(x => x.id === 's24');
  shead.append(el('button', 'btn tbt-hmore', dots), el('button', 'tbt-goal', `<span class="overline">Goal</span><span class="txt">${esc(s24.goal)}</span>`));
  const hmore = shead.querySelector('.tbt-hmore'), goal = shead.querySelector('.tbt-goal');
  hmore.type = goal.type = 'button';
  hmore.setAttribute('aria-label', 'More sprint actions');
  goal.setAttribute('aria-expanded', 'false');
  hmore.addEventListener('click', () => openSheet('sprintmore'));
  goal.addEventListener('click', () => goal.setAttribute('aria-expanded', goal.getAttribute('aria-expanded') !== 'true'));
  shead.querySelector('.picker').addEventListener('click', () => { if (touch()) openSheet('sprint'); });
  // The current tab in view: scroll the tabs strip only (scrollIntoView would also scroll a page framing this one).
  const tabs = document.querySelector('.toolbar .tabs'), curTab = tabs?.querySelector('[aria-current="page"]');
  if (curTab) tabs.scrollLeft = curTab.offsetLeft - (tabs.clientWidth - curTab.offsetWidth) / 2;

  /* Toolbar row, the "+ Add" row, the state chips and the pager, in the grid's place. */
  const toolbar = document.querySelector('.toolbar');
  const bar = el('div', 'tbt-bar');
  toolbar.append(bar);
  const addRow = el('div', 'tbt-addstory', `<select class="field" aria-label="Type"><option>Story</option><option>Bug</option></select>
    <input class="field" aria-label="Title" placeholder="Title of the story or bug" enterkeyhint="done">
    <button class="btn primary" type="button">Add</button>
    <p class="hint">Adds it to Sprint 24, at the bottom. Stays open for the next one.</p>`);
  addRow.hidden = !q.has('add');
  toolbar.after(addRow);
  const strip = el('nav', 'tbt-strip');
  strip.setAttribute('aria-label', 'Task states');
  addRow.after(strip);
  const pager = el('div', 'tbt-pager');
  pager.setAttribute('aria-label', 'Taskboard');
  document.querySelector('.board').after(pager);
  const sheet = el('div', 'tbt-sheet');
  sheet.hidden = true;
  sheet.setAttribute('role', 'dialog');
  sheet.setAttribute('aria-modal', 'true');
  document.body.append(sheet);

  let addingTo = q.get('addtask');
  let current = 0;
  if (q.get('filter')) { q.get('filter').split(',').filter(p => p === 'none' || people[p]).forEach(p => S.filter.add(p)); F.render(); }

  const tasksOf = key => S.tasks.filter(t => (t.parent || ORPHAN) === key);
  const shownIn = c => S.tasks.filter(F.visible).filter(t => t.state === c);
  const pickable = t => !t.assignee && cat(t.state) === 'proposed';

  function drawBar() {
    const n = S.filter.size;
    const faces = n ? `<span class="tbt-face-row">${[...S.filter].slice(0, 3).map(p => p === 'none' ? '<span class="av none"></span>' : av(p)).join('')}</span>` : '';
    bar.innerHTML = `
      <div class="seg" role="group" aria-label="Group by">${['stories', 'people'].map(g => `<button type="button" data-g="${g}" aria-pressed="${S.group === g}">${g === 'stories' ? 'Stories' : 'People'}</button>`).join('')}</div>
      <button class="btn tbt-filter" type="button" data-open="filter"${n ? ` data-n="${n}"` : ''} aria-label="Filter${n ? `, ${n} on` : ''}">${n ? faces : ICON.filter}Filter${n ? ` <span class="n">${n}</span>` : ''}</button>
      <button class="btn tbt-more" type="button" data-open="more" aria-label="More" aria-haspopup="dialog">${dots}</button>
      <span class="spacer"></span>
      <button class="btn primary" type="button" data-add aria-expanded="${!addRow.hidden}">+ Add</button>`;
  }

  let lastCounts = null;
  function drawStrip() {
    const counts = columns.map(c => shownIn(c).length);
    strip.innerHTML = columns.map((c, i) => `<button type="button" data-go="${i}" aria-current="${i === current}"${q.get('bump') === slug(c) || (lastCounts && counts[i] > lastCounts[i]) ? ' class="bump"' : ''}><span class="st ${cat(c)}">${c}</span><span class="n">${counts[i]}</span></button>`).join('');
    lastCounts = counts;
  }

  function card(t) {
    const r = F.ruleFor(t), p = F.parentOf(t);
    const tags = t.tags.map(g => `<span class="tag" style="--tag:${F.swatch[S.tagColors[g]] || 'var(--color-muted)'}">${esc(g)}</span>`).join('');
    const par = S.group === 'people' ? `<div class="par">${p ? `${tIcon(p.type)}<span>${esc(p.title)}</span>` : '<span>No parent</span>'}</div>` : '';
    const who = pickable(t) ? `<button class="pick" type="button" data-pick="${t.key}">Pick up</button>` : t.assignee ? av(t.assignee) : '<span class="av none" title="Unassigned"></span>';
    return `<article class="card${r ? ' styled' : ''}${r && r.bold ? ' bold' : ''}" data-key="${t.key}" data-type="task"${r ? ` style="--rule:${F.swatch[r.color]}"` : ''} tabindex="0" aria-label="${esc(t.title)}, ${t.state}">
      ${par}<div class="ct">${tIcon('task')}<span class="title">${esc(t.title)}</span></div>
      <button class="tbt-cm" type="button" data-task="${t.key}" aria-label="Actions for ${t.key}" aria-haspopup="dialog">${dots}</button>
      <div class="cm"><span class="key">${t.key}</span>${tags}<span class="spacer"></span>${who}</div>
    </article>`;
  }

  function cell(row, c, ci, list) {
    const here = list.filter(t => t.state === c), shown = here.filter(F.visible), hidden = here.length - shown.length;
    const adding = S.group === 'stories' && ci === 0 ? (addingTo === row
      ? `<div class="tbt-addtask"><input class="field" aria-label="Task title" placeholder="Task title" enterkeyhint="done"><button class="btn primary" type="button" data-addtask-go="${row}">Add</button></div>`
      : `<button class="tbt-add" type="button" data-addtask="${row}">+ Task</button>`) : '';
    return `<div class="tbt-cell" data-row="${row}" data-col="${c}">${shown.map(card).join('')}${!shown.length && !hidden ? '<div class="tbt-none">None</div>' : ''}${hidden ? `<div class="tbt-hidden">${hidden}${shown.length ? ' more' : ''} hidden by Filter</div>` : ''}${adding}</div>`;
  }

  // One story's lane on one page: its compact index card, then its tasks in this state, or None.
  function storyLane(p, c, ci) {
    const all = tasksOf(p.key), shownHere = all.filter(t => t.state === c && F.visible(t)).length;
    const done = all.filter(t => cat(t.state) === 'completed').length;
    const collapsed = S.collapsed.has(p.key);
    const toggle = `<button class="ib" type="button" data-collapse="${p.key}" aria-expanded="${!collapsed}" aria-label="${collapsed ? 'Expand' : 'Collapse'} ${p.key}">${collapsed ? '▶' : '▼'}</button>`;
    if (collapsed) return `<div class="tbt-lane" data-at="${p.key}"><div class="rowh collapsed">${toggle}${tIcon(p.type)}<span class="t1">${esc(p.title)}</span><span class="cnt">${shownHere ? `${shownHere} here · ` : ''}☑ ${done}/${all.length}</span><span class="st ${cat(p.state)}">${p.state}</span></div></div>`;
    const stSel = `<span class="stsel"><span class="st ${cat(p.state)}"></span><select data-pstate="${p.key}" aria-label="State of ${p.key}">${states.story.filter(s => cat(s) !== 'removed').map(n => `<option${n === p.state ? ' selected' : ''}>${n}</option>`).join('')}</select></span>`;
    const prog = all.length ? `<span class="prog">☑ ${done}/${all.length}<span class="bar"><i style="width:${Math.round(done / all.length * 100)}%"></i></span></span>` : '<span class="prog">No tasks yet</span>';
    const suggest = all.length && done === all.length && !['resolved', 'completed', 'removed'].includes(cat(p.state)) && !S.dismissed.has(p.key);
    return `<div class="tbt-lane" data-at="${p.key}">
      <div class="rowh">
        <div class="top">${toggle}${tIcon(p.type)}<span class="t1">${esc(p.title)}</span><span class="pts">${p.points} pts</span></div>
        <div class="meta"><span class="key">${p.key}</span>${stSel}${av(p.assignee)}${prog}</div>
        ${suggest ? `<div class="suggest"><b>All tasks done.</b><span class="spacer"></span><button class="btn primary" type="button" data-resolve="${p.key}">Move to Resolved</button><button class="ib" type="button" data-dismiss="${p.key}" aria-label="Dismiss">✕</button></div>` : ''}
      </div>
      ${cell(p.key, c, ci, all)}
    </div>`;
  }

  // Tasks with no story or bug: always the last lane, so there's somewhere to drop or add one (D12).
  function orphanLane(c, ci) {
    const all = tasksOf(ORPHAN), collapsed = S.collapsed.has(ORPHAN);
    const toggle = `<button class="ib" type="button" data-collapse="${ORPHAN}" aria-expanded="${!collapsed}" aria-label="${collapsed ? 'Expand' : 'Collapse'} unparented tasks">${collapsed ? '▶' : '▼'}</button>`;
    return `<div class="tbt-lane" data-at="${ORPHAN}"><div class="tbt-oh">${toggle}${tIcon('task')}<b>Unparented</b><span class="hint">${all.length} task${all.length === 1 ? '' : 's'} · no points</span></div>${collapsed ? '' : cell(ORPHAN, c, ci, all)}</div>`;
  }

  function storiesPage(c, ci) {
    // Backlog order; Completed stories start collapsed to one line (S.collapsed), as on desktop (D8).
    return [...S.parents.map(p => storyLane(p, c, ci)), orphanLane(c, ci)].join('');
  }

  // Everyone on the project, Unassigned first, as on desktop (D16).
  function peoplePage(c, ci) {
    const rows = ['none', ...Object.keys(people)].filter(r => !S.filter.size || S.filter.has(r));
    return rows.map(row => {
      const all = S.tasks.filter(t => (t.assignee || 'none') === row);
      const label = row === 'none' ? '<span class="av none"></span><b>Unassigned</b>' : `${av(row)}<b>${people[row].name}${row === ME ? ' (you)' : ''}</b>`;
      return `<div class="tbt-lane" data-at="${row}"><div class="tbt-ph">${label}<span class="hint">${all.length ? `${all.length} task${all.length === 1 ? '' : 's'}` : 'No tasks in this sprint'}</span></div>${cell(row, c, ci, all)}</div>`;
    }).join('');
  }

  function draw() {
    drawBar();
    const left = pager.scrollLeft;
    pager.innerHTML = columns.map((c, ci) => `<section class="tbt-page" data-col="${slug(c)}" aria-label="${c}">
      <div class="tbt-colh"><span class="st ${cat(c)}">${c}</span><span class="n">${shownIn(c).length}</span></div>
      ${S.group === 'people' ? peoplePage(c, ci) : storiesPage(c, ci)}</section>`).join('');
    pager.scrollLeft = left;
    drawStrip();
    const at = q.get('at');
    if (at && q.has('shot')) {
      [document.querySelector('.sprint-head'), toolbar, addRow, strip].forEach(x => { x.style.display = 'none'; });
      pager.querySelectorAll('.tbt-page').forEach(pg => {
        const target = pg.querySelector(`[data-at="${at}"]`);
        if (!target) return;
        for (let s = target.previousElementSibling; s && !s.classList.contains('tbt-colh'); s = s.previousElementSibling) s.style.display = 'none';
      });
    }
  }

  /* Turning pages */
  const pages = () => [...pager.querySelectorAll('.tbt-page')];
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

  /* Sheets: one at a time, over the shell's scrim (<html data-sheet="tbt">). */
  let sheetState = null;
  function openSheet(kind, key) {
    sheetState = { kind, key };
    root.dataset.sheet = 'tbt';
    sheet.hidden = false;
    pager.querySelectorAll('.card.lifted').forEach(c => c.classList.remove('lifted'));
    if (key) pager.querySelectorAll(`.card[data-key="${key}"]`).forEach(c => c.classList.add('lifted'));
    drawSheet();
  }
  function closeSheet() {
    sheetState = null;
    sheet.hidden = true;
    pager.querySelectorAll('.card.lifted').forEach(c => c.classList.remove('lifted'));
    if (root.dataset.sheet === 'tbt') delete root.dataset.sheet;
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
        ${pickable(t) ? `<button class="opt primary" type="button" data-pickup="${t.key}"><span class="tx"><span>Pick up</span><small>Assigns you and moves it to ${firstActive}</small></span></button><hr>` : ''}
        <div class="lbl">Move to</div>
        ${columns.map(c => `<button class="opt" type="button" role="menuitemradio" aria-checked="${c === t.state}" data-state="${c}"><span class="chk">${c === t.state ? '✓' : ''}</span><span class="st ${cat(c)}">${c}</span>${c === t.state ? '<span class="val">here</span>' : ''}</button>`).join('')}
        <hr>
        <button class="opt" type="button" data-to="story"><span class="tx"><span>Move to another story</span><small>${p ? `${tIcon(p.type)} ${p.key} ${esc(p.title)}` : 'No parent'}</small></span><span class="go">${ICON.go}</span></button>
        <button class="opt" type="button" data-to="assign"><span class="tx"><span>Assignee</span></span><span class="val">${personName(t.assignee)}</span><span class="go">${ICON.go}</span></button>
        <button class="opt" type="button" data-openitem="${t.key}"><span class="tx"><span>Open ${t.key}</span><small>Full screen</small></span><span class="go">${ICON.open}</span></button>
        <p class="note">Or press and hold the task, then drag it to another lane or to the screen's edge for another state.</p>
      </div>`;
    } else if (kind === 'story') {
      const typed = sheet.querySelector('.msearch .field')?.value || '';
      const term = typed.toLowerCase();
      const ps = S.parents.filter(p => !term || `${p.key} ${p.title}`.toLowerCase().includes(term));
      sheet.setAttribute('aria-label', `Move ${t.key} to another story`);
      sheet.innerHTML = `${head('Move to story', `<button class="btn ghost" type="button" data-back aria-label="Back">‹</button>`)}
        <div class="msearch"><input class="field" type="search" placeholder="Find a story or bug in Sprint 24" aria-label="Find a story or bug" value="${esc(typed)}"></div><div class="body">
        ${ps.map(p => `<button class="opt" type="button" role="menuitemradio" aria-checked="${p.key === t.parent}" data-parent="${p.key}"><span class="chk">${p.key === t.parent ? '✓' : ''}</span>${tIcon(p.type)}<span class="tx"><span>${esc(p.title)}</span><small>${p.key} · ${p.state}</small></span></button>`).join('') || '<p class="note">Nothing matches.</p>'}
        <hr><button class="opt" type="button" role="menuitemradio" aria-checked="${!t.parent}" data-parent="${ORPHAN}"><span class="chk">${t.parent ? '' : '✓'}</span>${tIcon('task')}<span class="tx"><span>No parent</span><small>Unparented: carries no sprint points</small></span></button>
        <p class="note">Only this sprint's stories and bugs. Moving a task never changes its sprint.</p></div>`;
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
      sheet.setAttribute('aria-label', 'More');
      sheet.innerHTML = `${head('Taskboard')}<div class="body">
        <button class="opt" type="button" data-all="collapse"><span class="tx"><span>Collapse all stories</span></span></button>
        <button class="opt" type="button" data-all="expand"><span class="tx"><span>Expand all stories</span></span></button>
        <hr>
        <button class="opt" type="button" data-settings><span class="tx"><span>Taskboard settings</span><small>Card fields, styles and tag colours, for everyone</small></span><span class="go">${ICON.go}</span></button>
      </div>`;
    } else if (kind === 'sprint') {
      const label = { past: 'Past', current: 'Current', future: 'Future' };
      sheet.setAttribute('aria-label', 'Sprints');
      sheet.innerHTML = `${head('Sprints')}<p class="note sub">Opens the same tab (Taskboard) of the sprint you pick.</p><div class="body">
        ${sprints.map(x => `<button class="opt" type="button" role="menuitemradio" aria-checked="${x.id === 's24'}" data-sprint="${x.id}"><span class="chk">${x.id === 's24' ? '✓' : ''}</span><span class="tx"><span>${x.name}</span><small>${x.start} – ${x.end}</small></span><span class="chip${x.status === 'current' ? ' accent' : ''}">${label[x.status]}</span></button>`).join('')}
        <hr><a class="opt" href="sprints.html"><span class="tx"><span>All sprints</span></span><span class="go">${ICON.go}</span></a>
      </div>`;
    } else if (kind === 'sprintmore') {
      sheet.setAttribute('aria-label', 'Sprint 24');
      sheet.innerHTML = `${head('Sprint 24')}<div class="body">
        <button class="opt" type="button" data-edit="goal"><span class="tx"><span>Edit goal</span></span></button>
        <button class="opt" type="button" data-edit="sprint"><span class="tx"><span>Edit sprint</span><small>Name, dates and goal</small></span></button>
        <a class="opt" href="sprints.html"><span class="tx"><span>All sprints</span></span><span class="go">${ICON.go}</span></a>
        <hr>
        <a class="opt" href="sprint-end.html"><span class="tx"><span>Close sprint</span><small>Choose where unfinished work goes</small></span><span class="go">${ICON.go}</span></a>
      </div>`;
    }
  }

  // The row a move goes to: the story when grouped by Stories, the person when grouped by People (the desktop's move).
  const rowOf = t => S.group === 'stories' ? t.parent || ORPHAN : (t.assignee || 'none');
  const pickUp = key => { const t = S.tasks.find(x => x.key === key); F.move(t.key, S.group === 'stories' ? rowOf(t) : ME, firstActive); };

  sheet.addEventListener('click', e => {
    const s = sheetState; if (!s) return;
    const t = S.tasks.find(x => x.key === s.key);
    if (e.target.closest('[data-close]')) return closeSheet();
    if (e.target.closest('[data-back]')) { s.kind = 'task'; return drawSheet(); }
    const pu = e.target.closest('[data-pickup]');
    if (pu) { closeSheet(); pickUp(pu.dataset.pickup); return; }
    const st = e.target.closest('[data-state]');
    if (st) { closeSheet(); if (st.dataset.state !== t.state) F.move(t.key, rowOf(t), st.dataset.state); return; }
    const to = e.target.closest('[data-to]');
    if (to) { s.kind = to.dataset.to; return drawSheet(); }
    const par = e.target.closest('[data-parent]');
    if (par) {
      closeSheet();
      const np = par.dataset.parent === ORPHAN ? null : par.dataset.parent;
      if (np === t.parent) return;
      if (S.group === 'stories') F.move(t.key, par.dataset.parent, t.state);
      else { t.parent = np; F.render(); toast(`“${esc(t.title)}” ${np ? `moved under ${np}` : 'parent removed'}`); }
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
    if (e.target.closest('[data-settings]')) { closeSheet(); F.openSettings(); return; }
    const ed = e.target.closest('[data-edit]');
    if (ed) { closeSheet(); toast(`${ed.dataset.edit === 'goal' ? 'Edit goal' : 'Edit sprint'} works as on the Sprint backlog (SHIP-724): ${ed.dataset.edit === 'goal' ? 'in place with the Save bar' : 'full screen'}`); return; }
    const sp = e.target.closest('[data-sprint]');
    if (sp) { closeSheet(); if (sp.dataset.sprint !== 's24') toast(`Opens the ${sprints.find(x => x.id === sp.dataset.sprint).name} Taskboard`); return; }
    const all = e.target.closest('[data-all]');
    if (all) {
      closeSheet();
      [...S.parents.map(p => p.key), ORPHAN].forEach(k => all.dataset.all === 'collapse' ? S.collapsed.add(k) : S.collapsed.delete(k));
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
    toast(`${addRow.querySelector('select').value} “${esc(input.value.trim())}” added to Sprint 24`);
    input.value = ''; input.focus();
  });

  /* Settings full screen (D15): the dialog's Cancel says Discard (SHIP-805 rule 15). */
  const settingsDlg = document.getElementById('settings');
  const cancel = settingsDlg.querySelector('footer .btn:not(.primary)');
  const labelCancel = () => { cancel.textContent = touch() ? 'Discard' : 'Cancel'; };
  labelCancel(); matchMedia('(max-width: 900px)').addEventListener('change', labelCancel);

  /* Taps in the pager: collapse, + Task, Pick up, All done, ⋯, and opening a task. */
  let suppressClick = false;
  pager.addEventListener('click', e => {
    if (suppressClick) { suppressClick = false; e.stopPropagation(); e.preventDefault(); return; }
    const c = e.target.closest('[data-collapse]'); if (c) { const k = c.dataset.collapse; S.collapsed.has(k) ? S.collapsed.delete(k) : S.collapsed.add(k); F.render(); return; }
    const pk = e.target.closest('[data-pick]'); if (pk) { pickUp(pk.dataset.pick); return; }
    const rs = e.target.closest('[data-resolve]');
    if (rs) { const p = S.parents.find(x => x.key === rs.dataset.resolve); p.state = states.story.find(s => cat(s) === 'resolved'); F.render(); toast(`${av(ME)} ${p.key} → ${p.state}`); return; }
    const ds = e.target.closest('[data-dismiss]'); if (ds) { S.dismissed.add(ds.dataset.dismiss); F.render(); return; }
    const m = e.target.closest('[data-task]'); if (m) { openSheet('task', m.dataset.task); return; }
    const a = e.target.closest('[data-addtask]'); if (a) { addingTo = a.dataset.addtask; draw(); pager.querySelector('.tbt-addtask .field').focus(); return; }
    const go = e.target.closest('[data-addtask-go]');
    if (go) { const f = pager.querySelector('.tbt-addtask .field'); if (f.value.trim()) { toast(`Task “${esc(f.value.trim())}” added ${go.dataset.addtaskGo === ORPHAN ? 'with no parent' : `to ${go.dataset.addtaskGo}`}`); f.value = ''; } f.focus(); return; }
    const k = e.target.closest('.card'); if (k && !e.target.closest('button, a, select, input')) toast(`Opens <b>${k.dataset.key}</b> full screen over the board (the item modal, SHIP-714); ✕ comes back here`);
  }, true);
  pager.addEventListener('change', e => {
    const sel = e.target.closest('[data-pstate]'); if (!sel) return;
    const p = S.parents.find(x => x.key === sel.dataset.pstate); p.state = sel.value;
    if (!p.assignee && cat(p.state) === 'in-progress') p.assignee = ME;
    if (cat(p.state) === 'completed') S.collapsed.add(p.key);
    F.render(); toast(`${p.key} → ${esc(p.state)}${cat(p.state) === 'completed' ? ' (collapsed to one line)' : ''}`);
  });

  /* Long press and touch drag (SHIP-722 D2, as SHIP-719). */
  let press = null, drag = null;
  const edges = ['left', 'right'].map(side => { const x = el('div', `tbt-edge ${side}`, side === 'left' ? '‹' : '›'); x.setAttribute('aria-hidden', 'true'); document.body.append(x); return x; });
  pager.addEventListener('pointerdown', e => {
    const card = e.target.closest('.card');
    if (!card || e.target.closest('button, a, input, select') || e.button > 0) return;
    press = { card, x: e.clientX, y: e.clientY, timer: setTimeout(() => lift(card, press.x, press.y), 450) };
  });
  const cancelPress = () => { if (press) clearTimeout(press.timer); press = null; };
  const clearDrop = () => pager.querySelectorAll('.drop-before, .drop-end, .tbt-cell.over').forEach(x => x.classList.remove('drop-before', 'drop-end', 'over'));
  function target(x, y) {
    const hit = document.elementFromPoint(x, y);
    const c = hit?.closest('.tbt-cell');
    if (c) return c;
    const lane = hit?.closest('.tbt-lane');
    if (lane) return lane.querySelector('.tbt-cell');
    const page = hit?.closest('.tbt-page') || (hit?.closest('.tbt-pager') && pages().reduce((a, pg) => {
      const r = pg.getBoundingClientRect(), d = x < r.left ? r.left - x : x > r.right ? x - r.right : 0;
      return !a || d < a.d ? { pg, d } : a;
    }, null)?.pg);
    return page ? page.querySelector(`.tbt-cell[data-row="${drag.row}"]`) : null;
  }
  function lift(card, x, y) {
    if (!press) return;
    navigator.vibrate?.(10);
    const r = card.getBoundingClientRect();
    const ghost = card.cloneNode(true);
    ghost.classList.add('tbt-ghost');
    ghost.classList.remove('flash');
    ghost.style.width = r.width + 'px';
    document.body.append(ghost);
    card.classList.add('dragging');
    drag = { key: card.dataset.key, card, ghost, row: card.closest('.tbt-cell').dataset.row, dx: x - r.left, dy: y - r.top, x, y, sx: x, sy: y, moved: false, edgeDir: 0, edgeSince: 0, raf: 0 };
    root.dataset.dragging = '';
    place(x, y);
    drag.raf = requestAnimationFrame(tick);
  }
  function place(x, y) {
    drag.x = x; drag.y = y;
    drag.moved = drag.moved || Math.hypot(x - drag.sx, y - drag.sy) > 8;
    drag.ghost.style.transform = `translate(${x - drag.dx}px, ${y - drag.dy}px) rotate(-2deg)`;
    clearDrop();
    const c = target(x, y);
    if (!c) return;
    c.classList.add('over');
    const b = [...c.querySelectorAll(':scope > .card:not(.dragging)')].find(k => { const r = k.getBoundingClientRect(); return y < r.top + r.height / 2; });
    if (b) b.classList.add('drop-before'); else c.classList.add('drop-end');
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
    else if (press && Math.hypot(e.clientX - press.x, e.clientY - press.y) > 8) cancelPress();
  });
  document.addEventListener('touchmove', e => { if (drag) e.preventDefault(); }, { passive: false });
  document.addEventListener('pointerup', e => {
    if (!drag) { cancelPress(); return; }
    const c = target(e.clientX, e.clientY), d = endDrag();
    suppressClick = true;
    setTimeout(() => { suppressClick = false; }, 300);
    if (!d.moved) { openSheet('task', d.key); return; }
    if (!c) return;
    const before = [...c.querySelectorAll(':scope > .card')].find(k => k.dataset.key !== d.key && e.clientY < k.getBoundingClientRect().top + k.getBoundingClientRect().height / 2);
    F.move(d.key, c.dataset.row, c.dataset.col, ME, before?.dataset.key || null);
  });
  document.addEventListener('pointercancel', () => { if (drag) endDrag(); cancelPress(); });
  pager.addEventListener('contextmenu', e => { if (e.target.closest('.card')) e.preventDefault(); });

  /* Review states */
  document.addEventListener('tb:render', () => { draw(); drawSheet(); });
  draw();
  const col = q.get('col');
  if (col) { current = -1; goTo(columns.findIndex(c => slug(c) === col), false); }
  if (!touch()) return;
  requestAnimationFrame(() => {
    const k = q.get('sheet');
    if (k) openSheet(k, ['task', 'story', 'assign'].includes(k) ? q.get('task') || 'SHIP-172' : null);
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
      const c = pages()[i].querySelector(`.tbt-cell[data-row="${drag.row}"]`);
      const cr = c.getBoundingClientRect();
      place(cr.left + cr.width * .45, cr.top + 18);
    });
  });
})();
