/*
 * All sprints in the touch shell (SHIP-1807, K7: the phone design for SHIP-723); styles in sprints-touch.css. Loaded at
 * the end of sprints.html, after its own script. At 900px and below:
 *   - the top bar names the page; the head is one row: the project's sprint count, Plan sprints and + New sprint (P1)
 *   - the velocity chart redrawn for the phone, every bar labelled, the velocity line dashed (P3)
 *   - the table becomes sections Current, Future (soonest first) and Past (newest first) of rows: name and dates, the
 *     goal (two lines), Capacity · Committed · Completed, and a member's capacity note as its own line (P2, P4, P8)
 *   - tapping a row opens that sprint; its ⋯ opens a sheet on the K1 anatomy with the sprint's tabs and its edits (P6)
 *   - New sprint is full screen with the desktop's defaults, Copy capacity as a switch, and the Save bar (P5)
 *   - no sprints yet: one panel saying what the page is for, with + New sprint (P7)
 * Above 900px nothing changes; ?notouch shows the desktop page at any width.
 * Review states (add ?shot for screenshots):
 *   ?at=list               with ?shot, hides the head and the chart so the three sections show from the top
 *   ?sheet=more:s24        a sprint's ⋯ sheet (s21–s26)
 *   ?new | ?new=overlap    New sprint full screen, untouched, or with a start date that overlaps Sprint 26
 *   ?empty                 a project with no sprints
 */
(() => {
  const q = new URLSearchParams(location.search);
  if (q.has('notouch')) { document.querySelector('link[href="sprints-touch.css"]')?.remove(); return; }
  const mq = matchMedia('(max-width: 900px)');
  mq.addEventListener('change', () => location.reload());
  if (!mq.matches) return;

  const { VELOCITY, sprints, history, committed, teamCapacity, esc, toast } = SL;
  const root = document.documentElement;
  const $ = s => document.querySelector(s);
  const el = (tag, cls, html) => { const e = document.createElement(tag); if (cls) e.className = cls; if (html != null) e.innerHTML = html; return e; };
  const svg = d => `<svg viewBox="0 0 16 16" width="16" height="16" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="${d}"/></svg>`;
  const dots = '<svg viewBox="0 0 16 16" width="16" height="16" aria-hidden="true"><circle cx="3.5" cy="8" r="1.4" fill="currentColor"/><circle cx="8" cy="8" r="1.4" fill="currentColor"/><circle cx="12.5" cy="8" r="1.4" fill="currentColor"/></svg>';
  const ICON = {
    backlog: svg('M3 4h10M3 8h10M3 12h7'),
    taskboard: svg('M2.5 3h3v10h-3zM6.5 3h3v7h-3zM10.5 3h3v5h-3z'),
    capacity: svg('M8 2.5a5.5 5.5 0 1 0 5.5 5.5M8 8V2.5M8 8h5.5'),
    retro: svg('M3 8a5 5 0 1 0 1.5-3.5M3 2.5v3h3'),
    demo: svg('M3 3.5h10v7H3zM6.5 5.5l3 1.5-3 1.5zM6 13h4'),
    edit: svg('M3 13l1-3.5L11 2.5l2.5 2.5-7 7zM9.5 4l2.5 2.5'),
    close: svg('M3 3.5h10v9H3zM6 8h4'),
  };

  const empty = q.has('empty');
  const list = empty ? [] : sprints.map(s => ({ ...s }));
  const byId = id => list.find(s => s.id === id);
  const statusLabel = { past: 'Past', current: 'Current', future: 'Future' };

  /* Head: + New sprint beside Plan sprints */
  const head = $('.head');
  const newBtn = el('button', 'btn primary spt-new', '+ New sprint');
  newBtn.type = 'button';
  head.append(newBtn);
  if (empty) head.querySelector('.sm').textContent = 'Storefront · no sprints yet';

  /* Cards: name them so the CSS can tell them apart, then add the phone pieces */
  const cards = [...document.querySelectorAll('.main > .card')];
  cards[0].classList.add('vel');
  cards[1].classList.add('tbl');
  const velBox = cards[0].querySelector('#vel').parentElement;
  velBox.insertAdjacentHTML('beforeend', velChart());
  const listBox = el('div', 'spt-list');
  cards[1].after(listBox);
  const emptyBox = el('div', 'spt-empty', `<b>No sprints yet</b><p>A sprint is a timebox the team commits points to. Create the first one; velocity shows here once three have ended.</p><button class="btn primary" type="button" data-new>+ New sprint</button>`);
  emptyBox.hidden = !empty;
  if (empty) root.classList.add('spt-is-empty');
  cards[1].after(emptyBox);

  // Velocity: the desktop's grouped bars at 366px, every bar labelled (nothing hover-only), the velocity line dashed.
  function velChart() {
    const W = 340, H = 150, L = 22, B = 20, T = 14, max = 25;
    const y = v => T + (1 - v / max) * (H - T - B), gw = (W - L) / history.length, bw = 22;
    let s = `<svg class="spt-vel" viewBox="0 0 ${W} ${H}" role="img" aria-label="Committed and completed story points for Sprints 19 to 23; velocity ${VELOCITY}">`;
    [0, 10, 20].forEach(v => { s += `<line x1="${L}" y1="${y(v)}" x2="${W}" y2="${y(v)}" stroke="var(--color-border)" stroke-width="${v ? .6 : 1}"/><text x="${L - 5}" y="${y(v) + 4}" text-anchor="end">${v}</text>`; });
    s += `<line x1="${L}" y1="${y(VELOCITY)}" x2="${W}" y2="${y(VELOCITY)}" stroke="var(--color-muted)" stroke-dasharray="4 3"/>`;
    const bar = (x, v, color) => `<path d="M${x},${y(0)} V${y(v) + 3} Q${x},${y(v)} ${x + 3},${y(v)} H${x + bw - 3} Q${x + bw},${y(v)} ${x + bw},${y(v) + 3} V${y(0)} Z" fill="${color}"/><text class="v" x="${x + bw / 2}" y="${y(v) - 4}" text-anchor="middle">${v}</text>`;
    history.forEach(([n, c, d], i) => {
      const cx = L + gw * i + gw / 2;
      s += bar(cx - bw - 1, c, 'var(--chart-committed)') + bar(cx + 1, d, 'var(--chart-completed)');
      s += `<text x="${cx}" y="${H - 5}" text-anchor="middle">${n.replace('Sprint ', 'S')}</text>`;
    });
    return s + `</svg><div class="sm muted" style="padding:2px 0 4px">Velocity <b style="color:var(--color-text)">${VELOCITY}</b> (dashed): the average completed over the last three. S19 = Sprint 19.</div>`;
  }

  // Rows: name and dates; the goal; Capacity · Committed · Completed; a capacity note as its own line (P8).
  function rowHtml(s) {
    const tc = teamCapacity(s);
    const com = s.status === 'past' ? s.committed : committed(s.id);
    const done = s.status === 'past' ? `<b>${s.completed}</b>` : s.status === 'current' ? `<b>${s.completed}</b> so far` : '<b>—</b>';
    const cap = tc == null ? `<span class="vel">Capacity <b>${VELOCITY} · velocity</b></span>` : `<span>Capacity <b>${tc}</b></span>`;
    return `<li><div class="spt-row${s.status === 'current' ? ' cur' : ''}" data-id="${s.id}">
      <a class="go" href="${s.status === 'current' ? 'sprint-backlog.html' : '#'}">
        <span class="l1"><b>${esc(s.name)}</b><span class="d">${s.start} – ${s.end}</span></span>
        <span class="goal${s.goal ? '' : ' none'}">${s.goal ? esc(s.goal) : 'No goal yet'}</span>
        <span class="figs">${cap}<span>Committed <b>${com || '—'}</b></span><span>Completed ${done}</span></span>
        ${s.note ? `<span class="note">${esc(s.note)}</span>` : ''}
      </a>
      <button class="more" type="button" data-more="${s.id}" aria-label="More for ${esc(s.name)}" aria-haspopup="dialog" aria-expanded="false">${dots}</button>
    </div></li>`;
  }
  function drawList() {
    const cur = list.filter(s => s.status === 'current');
    const fut = list.filter(s => s.status === 'future'); // oldest first: the next sprint on top
    const past = list.filter(s => s.status === 'past').reverse(); // newest first, as desktop
    const sec = (title, rows, sub = '') => rows.length ? `<section class="spt-sec"><h2>${title}${sub ? `<span>${sub}</span>` : ''}</h2><ul>${rows.map(rowHtml).join('')}</ul></section>` : '';
    listBox.innerHTML = sec('Current', cur) + sec('Future', fut, `· ${fut.length}`) + sec('Past', past, `· ${past.length}`);
  }
  drawList();

  /* The ⋯ sheet (K1): one at a time over the shell's scrim (<html data-sheet="spt">) */
  const sheet = el('div', 'spt-sheet');
  sheet.hidden = true;
  sheet.setAttribute('role', 'dialog'); sheet.setAttribute('aria-modal', 'true');
  document.body.append(sheet);
  let open = null;
  const opt = (icon, label, { hint, val, chev, cls = '', href } = {}) =>
    `<${href ? `a href="${href}"` : 'button type="button"'} class="opt${cls ? ` ${cls}` : ''}"><span class="ic">${icon}</span><span class="tx"><span>${label}</span>${hint ? `<small>${hint}</small>` : ''}</span>${val ? `<span class="val">${val}</span>` : ''}${chev ? '<span class="chev" aria-hidden="true">›</span>' : ''}</${href ? 'a' : 'button'}>`;
  function openSheet(id) {
    const s = byId(id); if (!s) return;
    open = id;
    root.dataset.sheet = 'spt';
    listBox.querySelectorAll('[data-more]').forEach(b => b.setAttribute('aria-expanded', b.dataset.more === id));
    const tc = teamCapacity(s);
    const com = s.status === 'past' ? s.committed : committed(s.id);
    const ended = s.status !== 'future';
    sheet.setAttribute('aria-label', s.name);
    sheet.innerHTML = `<header class="sheet-head"><span class="ttl"><b>${esc(s.name)}</b><small>${s.start} – ${s.end} · ${statusLabel[s.status]}</small></span><button class="btn ghost" type="button" data-close>Close</button></header>
      <div class="body">
        <div class="grp">Open</div>
        ${opt(ICON.backlog, 'Backlog', { val: com ? `${com} pts` : 'Empty', chev: true, href: s.status === 'current' ? 'sprint-backlog.html' : '#' })}
        ${opt(ICON.taskboard, 'Taskboard', { chev: true, href: s.status === 'current' ? 'taskboard.html' : '#' })}
        ${opt(ICON.capacity, 'Capacity', { val: tc == null ? `${VELOCITY} · velocity` : `${tc} pts`, chev: true, href: s.status === 'current' ? 'capacity.html' : '#' })}
        ${ended ? opt(ICON.retro, 'Retro', { chev: true, href: s.status === 'current' ? 'sprint-retro.html' : '#' }) : ''}
        ${ended ? opt(ICON.demo, 'Demo', { chev: true, href: s.status === 'current' ? 'sprint-demo.html' : '#' }) : ''}
        <hr>
        ${opt(ICON.edit, 'Edit sprint', { hint: 'Name, dates and goal' })}
        ${s.status === 'current' ? opt(ICON.close, 'Close sprint', { hint: 'Choose where unfinished work goes', chev: true, href: 'sprint-end.html' }) : ''}
        ${s.status === 'past' ? '<p class="foot">Closed. Its committed and completed points are read from the history at its end date and don\'t change.</p>' : ''}
      </div>`;
    sheet.hidden = false;
  }
  function closeSheet() {
    open = null; sheet.hidden = true;
    listBox.querySelectorAll('[data-more]').forEach(b => b.setAttribute('aria-expanded', 'false'));
    if (root.dataset.sheet === 'spt') delete root.dataset.sheet;
  }
  listBox.addEventListener('click', e => { const b = e.target.closest('[data-more]'); if (b) openSheet(b.dataset.more); });
  sheet.addEventListener('click', e => {
    if (e.target.closest('[data-close]')) return closeSheet();
    const o = e.target.closest('.opt'); if (!o) return;
    if (o.tagName === 'BUTTON') { closeSheet(); toast('Opens Edit sprint, full screen (SHIP-724 D9)'); }
  });
  $('.scrim')?.addEventListener('click', () => { if (open) closeSheet(); });

  /* New sprint, full screen with the Save bar (P5). Defaults as desktop: the next number, the next working day after
     the last sprint, the same length; Copy capacity on. Overlapping dates are refused on Create, inline. */
  const last = sprints[sprints.length - 1];
  const D0 = empty ? { name: 'Sprint 1', start: '', end: '', goal: '', copy: false } : { name: 'Sprint 27', start: '2026-11-30', end: '2026-12-11', goal: '', copy: true };
  const full = el('div', 'spt-full'); full.hidden = true;
  full.setAttribute('role', 'dialog'); full.setAttribute('aria-modal', 'true'); full.setAttribute('aria-label', 'New sprint');
  const bar = el('div', 'spt-savebar'); bar.hidden = true;
  bar.setAttribute('role', 'region'); bar.setAttribute('aria-label', 'Create the sprint');
  document.body.append(full, bar);
  let F = null, err = '';
  // Length as the desktop says it: weeks, and the working days in it (Monday to Friday).
  const days = (a, b) => {
    if (!a || !b || b < a) return null;
    let wd = 0, n = 0;
    for (const d = new Date(a + 'T00:00'), e = new Date(b + 'T00:00'); d <= e; d.setDate(d.getDate() + 1), n++) if (d.getDay() % 6) wd++;
    return `${Math.round(n / 7)} weeks · ${wd} working days`;
  };
  function drawFull() {
    const n = days(F.start, F.end);
    full.innerHTML = `<div class="fbar"><button class="tbtn" type="button" data-x aria-label="Close">${svg('M4 4l8 8M12 4l-8 8')}</button><b>New sprint</b></div>
      <div class="fbody">
        <div class="fld"><label for="spt-name">Name</label><input class="field" id="spt-name" value="${esc(F.name)}"></div>
        <div class="two">
          <div class="fld"><label for="spt-start">Start</label><input class="field" id="spt-start" type="date" value="${F.start}"${err ? ' aria-invalid="true" aria-describedby="spt-err"' : ''}></div>
          <div class="fld"><label for="spt-end">End</label><input class="field" id="spt-end" type="date" value="${F.end}"></div>
        </div>
        ${err ? `<div class="err" id="spt-err" role="alert">${err}</div>` : `<div class="len">${n || 'Pick the dates'}</div>`}
        <div class="fld"><label for="spt-goal">Goal</label><textarea class="field" id="spt-goal" rows="3" placeholder="Optional">${esc(F.goal)}</textarea></div>
        ${empty ? '' : `<button class="sw-row" type="button" role="switch" aria-checked="${F.copy}" data-copy><span class="tx"><span>Copy each member's capacity</span><small>From ${esc(sprints.find(s => s.id === 's25').name)}. Off: plan against velocity (${VELOCITY}).</small></span><span class="sw" aria-hidden="true"></span></button>`}
        <p class="hint">${empty ? 'Sprints usually run two weeks. The next one will default to the day after this one ends.' : `Defaults: the next number, starting the next working day after ${esc(last.name)}, the same length as it. Overlapping dates are refused.`}</p>
      </div>`;
    bar.innerHTML = `<span class="what${err ? ' err' : ''}">${err ? 'Fix the dates to create it' : `Not created yet`}</span><button class="btn" type="button" data-discard>Discard</button><button class="btn primary" type="button" data-create>Create sprint</button>`;
  }
  function openNew(state) {
    F = { ...D0 }; err = '';
    if (state === 'overlap') { F.start = '2026-11-23'; F.end = '2026-12-04'; err = `Overlaps ${esc(last.name)} (${last.start} – ${last.end}). Start on 30 Nov or later.`; }
    root.classList.add('spt-full-open');
    full.hidden = bar.hidden = false;
    drawFull();
  }
  function closeNew() { full.hidden = bar.hidden = true; root.classList.remove('spt-full-open'); }
  document.addEventListener('click', e => { if (e.target.closest('.spt-new, [data-new]')) openNew(); });
  full.addEventListener('input', e => {
    const k = { 'spt-name': 'name', 'spt-start': 'start', 'spt-end': 'end', 'spt-goal': 'goal' }[e.target.id];
    if (k) F[k] = e.target.value;
  });
  full.addEventListener('click', e => {
    if (e.target.closest('[data-x]')) return closeNew();
    if (e.target.closest('[data-copy]')) { F.copy = !F.copy; return drawFull(); }
  });
  bar.addEventListener('click', e => {
    if (e.target.closest('[data-discard]')) return closeNew();
    if (!e.target.closest('[data-create]')) return;
    if (!F.start || !F.end || F.end < F.start) { err = 'End must be on or after the start.'; return drawFull(); }
    if (!empty && F.start <= '2026-11-27') { err = `Overlaps ${esc(last.name)} (${last.start} – ${last.end}). Start on 30 Nov or later.`; return drawFull(); }
    const fmt = d => new Date(d + 'T00:00').toLocaleDateString('en-GB', { day: 'numeric', month: 'short' });
    list.push({ id: 'new', name: F.name.trim() || D0.name, start: fmt(F.start), end: fmt(F.end), status: 'future', goal: F.goal.trim(), capacity: F.copy ? { ...sprints.find(s => s.id === 's25').capacity } : {} });
    emptyBox.hidden = true; root.classList.remove('spt-is-empty');
    closeNew(); drawList(); toast(`Created ${esc(F.name)}`);
  });

  /* Review states */
  const sh = q.get('sheet');
  if (sh?.startsWith('more:')) openSheet(sh.slice(5));
  if (q.has('new')) openNew(q.get('new'));
  if (q.get('at') === 'list' && q.has('shot')) document.head.append(el('style', null, '.head, .card.vel { display: none !important; } .main { padding-top: 12px; }'));
})();
