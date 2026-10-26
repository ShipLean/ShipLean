/*
 * Shared sample data and helpers for the ShipLean reference screens.
 * Load in <head> (not deferred) so ?theme and ?shot apply before first paint.
 *   ?theme=light|dark  forces a theme (default: follow the OS)
 *   ?shot              hides the Preview panel, for screenshots
 *   ?usermenu          opens the user menu (Your profile, Instance settings, Sign out)
 * Sample data is fictional. "Today" is Sat 24 Oct 2026, midway through Sprint 24.
 */
(() => {
  const params = new URLSearchParams(location.search);
  const root = document.documentElement;
  if (params.get('theme')) root.dataset.theme = params.get('theme');
  if (params.has('shot')) root.dataset.shot = '';

  const VELOCITY = 21; // average completed points over the last 3 sprints (20 · 22 · 21)
  const ME = 'priya';
  // The install itself (Instance settings). Priya is an instance admin, so her user menu shows Instance settings.
  const instance = { name: 'Larkspur Engineering', baseUrl: 'https://shiplean.larkspur.dev', admins: ['priya', 'tom'] };

  // Avatar colours are identity colours, like covers: the same in both themes.
  const people = {
    priya: { name: 'Priya Shah', initials: 'PS', color: '#2f7fd8', email: 'priya@larkspur.dev' },
    tom: { name: 'Tom Okafor', initials: 'TO', color: '#16a089', email: 'tom@larkspur.dev' },
    mei: { name: 'Mei Lin', initials: 'ML', color: '#7b4fc9', email: 'mei@larkspur.dev' },
    sam: { name: 'Sam Rivera', initials: 'SR', color: '#c9611a', email: 'sam@larkspur.dev' },
    ana: { name: 'Ana Costa', initials: 'AC', color: '#b83d7a', email: 'ana@larkspur.dev' },
  };

  // State name → category. States are custom per project; colour and maths follow the category.
  const stateCategory = { New: 'proposed', Active: 'in-progress', Resolved: 'resolved', 'In review': 'resolved', Closed: 'completed', Removed: 'removed' };
  // Each type's states, in order. SHIP added a custom "In review" Task state (Resolved category).
  const states = { story: ['New', 'Active', 'Resolved', 'Closed', 'Removed'], task: ['New', 'Active', 'In review', 'Closed', 'Removed'] };

  // Sprints: a flat list. Status (past/current/future) comes from the dates.
  // capacity: what each member entered on the Capacity tab. Only the sum is used. Empty = use velocity.
  const sprints = [
    { id: 's21', name: 'Sprint 21', start: '7 Sep', end: '18 Sep', status: 'past', goal: 'Reporting MVP', capacity: { priya: 4, tom: 4, mei: 0, sam: 4, ana: 4 }, committed: 20, completed: 20, note: 'Mei and Priya at a conference' },
    { id: 's22', name: 'Sprint 22', start: '21 Sep', end: '2 Oct', status: 'past', goal: 'Checkout performance', capacity: { priya: 6, tom: 5, mei: 5, sam: 5, ana: 3 }, committed: 24, completed: 22 },
    { id: 's23', name: 'Sprint 23', start: '5 Oct', end: '16 Oct', status: 'past', goal: 'Coupons v2', capacity: { priya: 6, tom: 5, mei: 5, sam: 4, ana: 3 }, committed: 23, completed: 21 },
    { id: 's24', name: 'Sprint 24', start: '19 Oct', end: '30 Oct', status: 'current', goal: 'Customers can check out with a saved card; passkey sign-in behind a flag.', capacity: { priya: 6, tom: 5, mei: 5, sam: 4, ana: 3 }, completed: 5 },
    { id: 's25', name: 'Sprint 25', start: '2 Nov', end: '13 Nov', status: 'future', goal: 'Remember-me and the reporting export', capacity: { priya: 6, tom: 5, mei: 5, sam: 4, ana: 2 } },
    { id: 's26', name: 'Sprint 26', start: '16 Nov', end: '27 Nov', status: 'future', goal: '', capacity: {} },
  ];
  // Velocity history for the chart: committed vs completed points per past sprint.
  const history = [['Sprint 19', 18, 16], ['Sprint 20', 22, 19], ['Sprint 21', 20, 20], ['Sprint 22', 24, 22], ['Sprint 23', 23, 21]];

  // Stories-level backlog in backlog_order. sprint: null = Backlog.
  const items = [
    ['141', 'story', 'Pay with saved card', 'Checkout v2', 'Active', 5, 's24', 'priya'],
    ['138', 'bug', 'Totals wrong when coupon removed', 'Checkout v2', 'Closed', 2, 's24', 'tom'],
    ['144', 'story', 'Address autocomplete', 'Checkout v2', 'Active', 3, 's24', 'mei'],
    ['129', 'story', 'Sign in with passkey', 'Mobile sign-in', 'Active', 8, 's24', 'sam'],
    ['150', 'story', 'Order confirmation email', 'Checkout v2', 'Closed', 3, 's24', 'ana'],
    ['151', 'story', 'Remember device for 30 days', 'Mobile sign-in', 'New', 5, 's25', null],
    ['153', 'bug', 'Crash when biometric prompt is cancelled', 'Mobile sign-in', 'New', 3, 's25', null],
    ['147', 'story', 'Export sprint report to CSV', 'Reporting', 'New', 5, 's25', null],
    ['156', 'story', 'Dashboard: cycle time widget', 'Dashboard widgets', 'New', 8, null, null],
    ['158', 'story', 'Gift card balance check', 'Checkout v2', 'New', 3, null, null],
    ['160', 'bug', 'Session expires during checkout', 'Checkout v2', 'New', 2, null, null],
    ['162', 'story', 'Rotate API keys from settings', 'Ops hygiene', 'New', 5, null, null],
    ['163', 'story', 'Audit log retention setting', 'Ops hygiene', 'New', 3, null, null],
    ['165', 'story', 'Weekly digest email', 'Reporting', 'New', 8, null, null],
    ['166', 'story', 'Saved filters on dashboard', 'Dashboard widgets', 'New', 5, null, null],
    ['170', 'story', 'SSO via Okta', 'Mobile sign-in', 'New', 13, null, null],
  ].map(([n, type, title, parent, state, points, sprint, assignee]) => ({ key: 'SHIP-' + n, type, title, parent, state, points, sprint, assignee }));

  // Features and Epics, in backlog_order at their own level. points = the item's own estimate.
  // A Feature can sit under another Feature (Dashboard widgets under Reporting).
  const epics = [
    { key: 'SHIP-90', title: 'Checkout conversion', state: 'Active', points: 40 },
    { key: 'SHIP-91', title: 'Account security', state: 'Active', points: 30 },
    { key: 'SHIP-92', title: 'Insights for merchants', state: 'New', points: 30 },
    { key: 'SHIP-93', title: 'Platform health', state: 'New', points: 10 },
  ];
  const features = [
    { key: 'SHIP-101', title: 'Checkout v2', parent: 'SHIP-90', state: 'Active', points: 20 },
    { key: 'SHIP-102', title: 'Mobile sign-in', parent: 'SHIP-91', state: 'Active', points: 21 },
    { key: 'SHIP-103', title: 'Reporting', parent: 'SHIP-92', state: 'Active', points: 13 },
    { key: 'SHIP-108', title: 'Dashboard widgets', parent: 'SHIP-103', state: 'New', points: 8 },
    { key: 'SHIP-104', title: 'Ops hygiene', parent: 'SHIP-93', state: 'New', points: 5 },
  ];
  // Stories and bugs completed in past sprints. Hidden on the backlog, counted in roll-ups.
  const doneItems = [
    ['120', 'story', 'Guest checkout', 'Checkout v2', 'Closed', 5, 's22', 'priya'],
    ['124', 'story', 'Apple Pay button', 'Checkout v2', 'Closed', 3, 's23', 'tom'],
    ['118', 'story', 'Password reset by email', 'Mobile sign-in', 'Closed', 3, 's22', 'sam'],
    ['126', 'bug', 'Wrong currency on receipts', 'Reporting', 'Closed', 2, 's23', 'ana'],
    ['131', 'story', 'Rate limiting on login', 'Ops hygiene', 'Closed', 3, 's23', 'mei'],
  ].map(([n, type, title, parent, state, points, sprint, assignee]) => ({ key: 'SHIP-' + n, type, title, parent, state, points, sprint, assignee }));
  const featureByTitle = t => features.find(f => f.title === t);

  // Projects this user is a member of (the picker never lists others). recent = opened lately.
  const projects = [
    { key: 'SHIP', name: 'Storefront', members: 5, recent: 1 },
    { key: 'PAY', name: 'Payments', members: 8, recent: 2 },
    { key: 'MOB', name: 'Mobile App', members: 6, recent: 3 },
    { key: 'DATA', name: 'Data Platform', members: 4 },
    { key: 'OPS', name: 'Internal Tools', members: 3 },
    { key: 'PART', name: 'Partner Portal', members: 7 },
  ];
  const PROJECT = 'SHIP';

  // Who else has each screen open right now (presence is per screen, never stored).
  const presence = {
    backlog: ['mei', 'tom'],
    features: ['ana'],
    planning: ['mei', 'sam', 'tom', 'ana'],
    'sprint-backlog': ['tom'],
    capacity: ['ana'],
    sprints: [],
    'sprint-end': ['sam'],
    taskboard: ['sam', 'mei'],
    board: ['tom', 'ana', 'mei'],
    'features-board': ['ana'],
    'board-settings': [],
    'work-item': ['mei'],
    feature: ['ana'],
    attachment: [],
    settings: [],
    import: [],
  };

  // Tasks under Sprint 24's stories and bugs. Unassigned + New = ready to pick up.
  // [title, state, assignee, key, points (informational), tags, priority]
  const tasks = {
    'SHIP-141': [['API: tokenised card lookup', 'In review', 'priya', 'SHIP-171', 2, [], 2], ['UI: saved card picker', 'Active', 'mei', 'SHIP-172', 2, [], 1], ['E2E test', 'New', null, 'SHIP-173', 1, [], 2]],
    'SHIP-138': [['Fix and regression test', 'Closed', 'tom', 'SHIP-174', 2, [], 2]],
    'SHIP-144': [['Provider spike', 'Closed', 'mei', 'SHIP-175', 1, ['Spike'], 2], ['Wire into the address form', 'New', null, 'SHIP-176', 2, [], 2]],
    'SHIP-129': [['WebAuthn registration', 'Closed', 'sam', 'SHIP-177', 3, [], 2], ['WebAuthn sign-in', 'Active', 'sam', 'SHIP-178', 3, [], 2], ['Fallback to password', 'New', null, 'SHIP-179', 2, ['Blocked'], 2], ['Feature flag', 'Closed', 'ana', 'SHIP-180', 1, [], 3]],
    'SHIP-150': [['Email template', 'Closed', 'ana', 'SHIP-181', 1, [], 2]],
  };
  // Sprint 24 tasks with no parent story or bug (allowed; they carry no sprint points). Same shape as above.
  const unparentedTasks = [['Rotate staging TLS certificates', 'New', null, 'SHIP-182', null, ['Tech debt'], 2], ['Update the on-call runbook', 'Active', 'tom', 'SHIP-183', null, [], 3]];

  const sum = o => Object.values(o).reduce((a, v) => a + v, 0);
  const teamCapacity = s => (Object.keys(s.capacity).length ? sum(s.capacity) : null); // null = no entries, use velocity
  const committed = id => items.filter(i => i.sprint === id).reduce((a, i) => a + i.points, 0);

  const typeLetter = { epic: 'E', feature: 'F', story: 'S', bug: 'B', task: 'T' };
  const typeName = { epic: 'Epic', feature: 'Feature', story: 'User Story', bug: 'Bug', task: 'Task' };
  const esc = s => String(s).replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' })[c]);
  const tIcon = t => `<span class="ti ${t}" title="${typeName[t]}">${typeLetter[t]}</span>`;
  const st = s => `<span class="st ${stateCategory[s]}">${s}</span>`;
  const av = p => (p ? `<span class="av" style="background:${people[p].color}" title="${people[p].name}">${people[p].initials}</span>` : '<span class="av none" title="Unassigned"></span>');

  // Shell: the side menu (project picker + navigation) and the top bar (where you are, who else is here, Live).
  // Pages keep calling SL.appBar(active) where the old top bar went; the rest of the page moves into .main.
  const page = (location.pathname.split('/').pop() || 'planning.html').replace('.html', '');
  const crumbsFor = {
    backlog: ['Backlogs', 'Stories'], planning: ['Backlogs', 'Stories', 'Planning'], features: ['Backlogs', new URLSearchParams(location.search).get('level') === 'epics' ? 'Epics' : 'Features'],
    'sprint-backlog': ['Sprints', 'Sprint 24', 'Backlog'], capacity: ['Sprints', 'Sprint 24', 'Capacity'], sprints: ['Sprints', 'All sprints'], 'sprint-end': ['Sprints', 'Sprint 24'],
    taskboard: ['Sprints', 'Sprint 24', 'Taskboard'], board: ['Boards', 'Stories'], 'features-board': ['Boards', new URLSearchParams(location.search).get('level') === 'epics' ? 'Epics' : 'Features'], 'board-settings': ['Boards', 'Stories', 'Settings'],
    'work-item': ['Backlogs', 'SHIP-141'], feature: ['Backlogs', 'Features', 'SHIP-101'], attachment: ['Backlogs', 'SHIP-101', 'design-v0.2.md'], settings: ['Settings'], import: ['Jira import'],
    'new-project': ['New project'], instance: ['Instance settings'], profile: ['Your profile'],
  };
  // Pages that belong to the install or to you, not to a project: the breadcrumb starts at ShipLean and there's no Live dot.
  const instanceLevel = new Set(['new-project', 'instance', 'profile']);

  function presenceStack(ids) {
    if (!ids.length) return '';
    const shown = ids.slice(0, 3), more = ids.length - shown.length;
    const names = ids.map(p => people[p].name).join(', ');
    return `<span class="presence"><button type="button" aria-haspopup="true" aria-expanded="false" aria-label="Also viewing: ${names}" title="Also viewing: ${names}">
        <span style="display:inline-flex">${shown.map(av).join('')}${more ? `<span class="av more">+${more}</span>` : ''}</span></button>
      <div class="presence-list" hidden><span class="overline">Also on this screen</span>${ids.map(p => `<div>${av(p)}${people[p].name}</div>`).join('')}</div></span>`;
  }

  function sideMenu(active) {
    const proj = projects.find(p => p.key === PROJECT);
    const link = (id, icon, label, href, sub) => `<a class="nav-i${sub ? ' sub' : ''}" href="${href}"${id === active || id === page ? ' aria-current="page"' : ''}>${sub ? '' : `<span class="ic">${icon}</span>`}${label}</a>`;
    return `<aside class="side" aria-label="Project navigation">
      <span class="logo"><i></i>ShipLean</span>
      <button class="proj-btn" type="button" aria-haspopup="listbox" aria-expanded="false" title="Switch project">
        <span class="pkey">${proj.key}</span><span class="nm"><b>${esc(proj.name)}</b><span>${proj.members} members</span></span><span class="muted">▾</span></button>
      ${link('backlogs', '☰', 'Backlogs', 'backlog.html')}
      ${link('boards', '▦', 'Boards', 'board.html')}
      ${link('sprints', '◷', 'Sprints', 'sprint-backlog.html')}
      ${active === 'sprints' ? link('sprint-backlog', '', 'Sprint 24 <span class="chip accent" style="height:16px;margin-left:auto">Current</span>', 'sprint-backlog.html', true) + link('sprints-all', '', 'All sprints', 'sprints.html', true) : ''}
      ${link('import', '⇪', 'Jira import', 'import.html')}
      ${link('settings', '⚙', 'Settings', 'settings.html')}
      <button class="me" type="button" aria-haspopup="menu" aria-expanded="false">${av(ME)}<span>${people[ME].name}</span><span class="muted">▴</span></button>
      <div class="user-menu" role="menu" aria-label="Your account" hidden>
        <div class="who">${av(ME)}<span><b>${people[ME].name}</b><span>${people[ME].email}</span></span></div>
        <a role="menuitem" href="profile.html"${page === 'profile' ? ' aria-current="page"' : ''}>Your profile</a>
        ${instance.admins.includes(ME) ? `<a role="menuitem" href="instance.html"${page === 'instance' ? ' aria-current="page"' : ''}>Instance settings<span class="chip" style="height:16px;margin-left:auto">Admin</span></a>` : ''}
        <hr><a role="menuitem" href="signin.html?state=signed-out">Sign out</a>
      </div>
      <div class="proj-picker" role="dialog" aria-label="Switch project" hidden>
        <div class="search"><input class="field" type="search" placeholder="Search projects by name or key" aria-label="Search projects"></div>
        <ul role="listbox" aria-label="Projects"></ul>
        <footer><a class="btn ghost" href="new-project.html">+ New project</a><a class="btn ghost" href="import.html">Import from Jira</a></footer>
      </div>
    </aside>`;
  }

  function wirePicker(side) {
    const btn = side.querySelector('.proj-btn'), pk = side.querySelector('.proj-picker'), input = pk.querySelector('input'), list = pk.querySelector('ul');
    let hi = 0, shown = [];
    const mark = (text, q) => { const i = q ? text.toLowerCase().indexOf(q) : -1; return i < 0 ? esc(text) : esc(text.slice(0, i)) + '<mark>' + esc(text.slice(i, i + q.length)) + '</mark>' + esc(text.slice(i + q.length)); };
    const opt = (p, q) => `<li role="option" data-key="${p.key}" aria-selected="${p.key === PROJECT}"><span class="pkey${p.key === PROJECT ? '' : ' alt'}">${mark(p.key, q)}</span><span class="n">${mark(p.name, q)}</span>${p.key === PROJECT ? '<span class="muted sm">current</span>' : `<span class="muted xs">${p.members}</span>`}</li>`;
    function draw() {
      const q = input.value.trim().toLowerCase();
      const match = p => !q || p.name.toLowerCase().includes(q) || p.key.toLowerCase().includes(q);
      if (q) { shown = projects.filter(match); list.innerHTML = shown.length ? shown.map(p => opt(p, q)).join('') : `<li class="none">No project matches “${esc(input.value)}”</li>`; }
      else {
        const recent = projects.filter(p => p.recent).sort((a, b) => a.recent - b.recent), rest = projects.filter(p => !p.recent).sort((a, b) => a.name.localeCompare(b.name));
        shown = [...recent, ...rest];
        list.innerHTML = `<li class="grp overline">Recent</li>${recent.map(p => opt(p, '')).join('')}<li class="grp overline">All your projects</li>${rest.map(p => opt(p, '')).join('')}`;
      }
      hi = Math.min(hi, Math.max(shown.length - 1, 0));
      list.querySelectorAll('[role=option]').forEach((li, i) => li.classList.toggle('hi', i === hi));
    }
    const open = v => { pk.hidden = !v; btn.setAttribute('aria-expanded', v); if (v) { input.value = ''; hi = 0; draw(); input.focus(); } };
    const pick = key => { open(false); btn.focus(); if (key !== PROJECT) toast(`Switching to <b>${key}</b>: the same screen in that project`); };
    btn.addEventListener('click', () => open(pk.hidden));
    input.addEventListener('input', () => { hi = 0; draw(); });
    input.addEventListener('keydown', e => {
      if (e.key === 'ArrowDown' || e.key === 'ArrowUp') { e.preventDefault(); hi = (hi + (e.key === 'ArrowDown' ? 1 : -1) + shown.length) % Math.max(shown.length, 1); draw(); }
      else if (e.key === 'Enter' && shown[hi]) pick(shown[hi].key);
      else if (e.key === 'Escape') { open(false); btn.focus(); }
    });
    list.addEventListener('click', e => { const li = e.target.closest('[role=option]'); if (li) pick(li.dataset.key); });
    document.addEventListener('click', e => { if (!pk.hidden && !side.contains(e.target)) open(false); });
    document.addEventListener('keydown', e => { if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'p' && !e.shiftKey) { e.preventDefault(); open(true); } });
  }

  function wireUserMenu(side) {
    const b = side.querySelector('.me'), m = side.querySelector('.user-menu');
    const open = v => { m.hidden = !v; b.setAttribute('aria-expanded', v); if (v) m.querySelector('a').focus(); };
    b.addEventListener('click', e => { e.stopPropagation(); open(m.hidden); });
    m.addEventListener('keydown', e => {
      const links = [...m.querySelectorAll('a')], i = links.indexOf(document.activeElement);
      if (e.key === 'ArrowDown' || e.key === 'ArrowUp') { e.preventDefault(); links[(i + (e.key === 'ArrowDown' ? 1 : -1) + links.length) % links.length].focus(); }
      else if (e.key === 'Escape') { open(false); b.focus(); }
    });
    document.addEventListener('click', e => { if (!m.hidden && !m.contains(e.target)) open(false); });
  }

  function wirePresence(bar) {
    const b = bar.querySelector('.presence button'); if (!b) return;
    const pop = bar.querySelector('.presence .presence-list');
    b.addEventListener('click', e => { e.stopPropagation(); pop.hidden = !pop.hidden; b.setAttribute('aria-expanded', !pop.hidden); });
    document.addEventListener('click', () => { pop.hidden = true; b.setAttribute('aria-expanded', false); });
  }

  function appBar(active) {
    const crumbs = crumbsFor[page] || [];
    queueMicrotask(() => {
      // Move the page into .main and put the side menu in front of it.
      const main = document.createElement('div'); main.className = 'shell-main';
      [...document.body.children].forEach(el => { if (el.tagName !== 'SCRIPT' && !el.matches('.toast, .tip, .preview')) main.append(el); });
      document.body.prepend(main);
      main.insertAdjacentHTML('beforebegin', sideMenu(active));
      wirePicker(document.querySelector('.side'));
      wireUserMenu(document.querySelector('.side'));
      if (params.has('usermenu')) document.querySelector('.side .me').click(); // ?usermenu opens the user menu, for screenshots
      wirePresence(main.querySelector('.appbar'));
    });
    return `<header class="appbar">
      <nav class="crumbs" aria-label="You are here"><span>${instanceLevel.has(page) ? 'ShipLean' : PROJECT}</span>${crumbs.map((c, i) => `<span>›</span>${i === crumbs.length - 1 ? `<b>${c}</b>` : `<span>${c}</span>`}`).join('')}</nav>
      <span class="spacer"></span>${presenceStack(presence[page] || [])}${instanceLevel.has(page) ? '' : '<span class="live" title="Real-time updates connected">Live</span>'}
    </header>`;
  }
  const setLive = on => { const l = document.querySelector('.appbar .live'); if (!l) return; l.classList.toggle('off', !on); l.textContent = on ? 'Live' : 'Reconnecting'; l.title = on ? 'Real-time updates connected' : 'Live updates paused, retrying'; };

  // Sprint header tabs, shared by the sprint pages.
  function sprintTabs(active) {
    const t = (id, label, href) => `<a class="tab" href="${href}"${id === active ? ' aria-current="page"' : ''}>${label}</a>`;
    return `<nav class="tabs" aria-label="Sprint">${t('backlog', 'Backlog', 'sprint-backlog.html')}${t('taskboard', 'Taskboard', 'taskboard.html')}${t('capacity', 'Capacity', 'capacity.html')}</nav>`;
  }

  let toastEl, toastTimer;
  function toast(html) {
    if (!toastEl) { toastEl = document.createElement('div'); toastEl.className = 'toast'; toastEl.setAttribute('role', 'status'); document.body.append(toastEl); }
    toastEl.innerHTML = html; toastEl.classList.add('show');
    clearTimeout(toastTimer); toastTimer = setTimeout(() => toastEl.classList.remove('show'), 2600);
  }

  let tipEl;
  function hover(el, text) {
    if (!tipEl) { tipEl = document.createElement('div'); tipEl.className = 'tip'; document.body.append(tipEl); }
    el.addEventListener('mousemove', e => { tipEl.textContent = text; tipEl.style.opacity = 1; tipEl.style.left = e.clientX + 12 + 'px'; tipEl.style.top = e.clientY - 28 + 'px'; });
    el.addEventListener('mouseleave', () => { tipEl.style.opacity = 0; });
  }

  // Preview panel (review aid): theme switch plus optional page actions [{ label, run }].
  function preview(actions = []) {
    const p = document.createElement('div');
    p.className = 'preview'; p.setAttribute('aria-label', 'Preview controls (review aid)');
    const cur = root.dataset.theme || 'os';
    p.innerHTML = `<b>Preview</b><div class="seg" role="group" aria-label="Theme">${['os', 'light', 'dark'].map(t => `<button type="button" data-theme-set="${t}" aria-pressed="${t === cur}">${t === 'os' ? 'OS' : t[0].toUpperCase() + t.slice(1)}</button>`).join('')}</div>`;
    actions.forEach(a => { const b = document.createElement('button'); b.type = 'button'; b.className = 'btn'; b.textContent = a.label; b.onclick = a.run; p.append(b); });
    p.addEventListener('click', e => {
      const b = e.target.closest('[data-theme-set]'); if (!b) return;
      if (b.dataset.themeSet === 'os') delete root.dataset.theme; else root.dataset.theme = b.dataset.themeSet;
      p.querySelectorAll('[data-theme-set]').forEach(x => x.setAttribute('aria-pressed', x === b));
    });
    document.body.append(p);
  }

  window.SL = { VELOCITY, ME, instance, people, stateCategory, states, sprints, history, items, tasks, unparentedTasks, epics, features, doneItems, featureByTitle, projects, PROJECT, presence, teamCapacity, committed, sum, esc, tIcon, st, av, appBar, setLive, sprintTabs, toast, hover, preview };
})();
