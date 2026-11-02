/*
 * SHIP-255: the behaviour shared by the Stories backlog (List) and Planning screens.
 * Selection, action bar, undo, inline estimates, the item modal, add here, paste review, readiness strip, keyboard.
 * Loaded after shared.js and before the page's script. A page calls SL.kit.init(host) and SL.kit.afterRender() at the
 * end of every render. host = { body, mode: 'list' | 'planning', render() }.
 * ?state=selected | paste | modal | queue | conflict | add | keys opens that state, for the review page and for screenshots.
 */
(() => {
  const { items, sprints, features, states, stateCategory, tIcon, esc, VELOCITY, teamCapacity, people } = SL;
  const params = new URLSearchParams(location.search);

  /* Sample data: enough unplanned, unready rows to plan with. Planned rows keep their order. */
  const add = [
    ['184', 'story', 'Refund to the original card', 'Checkout v2', null],
    ['186', 'story', 'Saved dashboard filters', 'Dashboard widgets', null],
    ['187', 'bug', 'Receipt email sent twice', null, null],
    ['188', 'story', 'Export orders as a spreadsheet', null, 3],
  ].map(([n, type, title, parent, points]) => ({ key: 'SHIP-' + n, type, title, parent, state: 'New', points, sprint: null, assignee: null }));
  items.push(...add);
  const order = ['SHIP-160', 'SHIP-158', 'SHIP-184', 'SHIP-163', 'SHIP-156', 'SHIP-162', 'SHIP-186', 'SHIP-165', 'SHIP-166', 'SHIP-187', 'SHIP-170', 'SHIP-188'];
  const planned = items.filter(i => i.sprint);
  const unplanned = order.map(k => items.find(i => i.key === k));
  items.length = 0; items.push(...planned, ...unplanned);
  const noAc = new Set(['SHIP-184', 'SHIP-156', 'SHIP-186', 'SHIP-188', 'SHIP-170']);
  const stale = new Set(['SHIP-165']);
  items.forEach(i => { i.ac = i.type === 'bug' || noAc.has(i.key) ? [] : ['Works on the saved card flow', 'Covered by an acceptance test']; i.tags = []; i.description = ''; i.kids = 0; });
  let nextKey = 190;

  /* The same five checks as the review_backlog tool (BacklogReviewer). */
  const words = t => new Set(t.toLowerCase().split(/[^a-z0-9]+/).filter(w => w.length > 2));
  const similar = (a, b) => { const x = words(a), y = words(b); const both = [...x].filter(w => y.has(w)).length; return both >= 2 && both / Math.min(x.size, y.size) >= .7; };
  const dupeOf = i => items.find(o => o !== i && similar(i.title, o.title));
  const CHECKS = [
    { id: 'unest', label: 'not estimated', test: i => i.points == null },
    { id: 'noac', label: 'no acceptance criteria', test: i => i.type === 'story' && !i.ac.length },
    { id: 'noparent', label: 'no parent', test: i => !i.parent },
    { id: 'stale', label: 'not changed in 30 days', test: i => stale.has(i.key) },
    { id: 'dupe', label: 'likely duplicates', test: i => !!dupeOf(i) },
  ];
  const unplannedOrFuture = () => items.filter(i => { const s = sprints.find(x => x.id === i.sprint); return !s || s.status === 'future'; }).filter(i => stateCategory[i.state] !== 'completed');
  const flagsOf = i => CHECKS.filter(c => c.test(i));

  const kit = { selected: new Set(), focus: null, q: '', queue: null, adding: null, moveMode: false, staleKey: null };
  let host, anchor = null, pop = null, undoTimer, pending = null, armed = false, conflictMsg = '', peekKey = null;
  const $ = id => document.getElementById(id);
  const item = k => items.find(i => i.key === k);
  const visibleKeys = () => [...host.body.querySelectorAll('tr.item')].map(r => r.dataset.key);
  const rankSorted = keys => items.filter(i => keys.has(i.key)).map(i => i.key);
  const sel = () => items.filter(i => kit.selected.has(i.key));
  const pts = list => list.reduce((a, i) => a + (i.points || 0), 0);
  const committedIn = id => items.filter(i => i.sprint === id).reduce((a, i) => a + (i.points || 0), 0);
  const capOf = s => teamCapacity(s) ?? VELOCITY;
  const openSprints = () => sprints.filter(s => s.status !== 'past');

  kit.filtering = () => !!kit.q || !!kit.queue;
  kit.passes = i => (!kit.queue || kit.queue.keys.has(i.key)) && (!kit.q || (i.key + ' ' + i.title).toLowerCase().includes(kit.q));
  kit.nextKey = () => 'SHIP-' + nextKey++;

  /* Row parts the pages call */
  kit.checkHead = () => `<th class="chk"><input type="checkbox" id="allChk" aria-label="Select all shown rows"></th>`;
  kit.checkCell = k => `<td class="chk"><input type="checkbox" data-chk aria-label="Select ${k}"${kit.selected.has(k) ? ' checked' : ''} tabindex="-1"></td>`;
  kit.rowClass = k => (kit.selected.has(k) ? ' sel' : '') + (kit.focus === k ? ' foc' : '');
  kit.titleLink = i => `<a class="t ttl" href="#" data-ttl>${esc(i.title)}</a>${i.kids ? `<span class="kids">+${i.kids} task${i.kids > 1 ? 's' : ''}</span>` : ''}`;
  kit.ptsCell = i => `<td class="pts"><button class="pts-btn${i.points == null ? ' unest' : ''}" type="button" data-pts tabindex="-1" title="${i.points == null ? 'Not estimated: click or type a number' : 'Click or type a number'}">${i.points ?? '?'}</button></td>`;

  /* Undo */
  function snapshot() { return { rows: items.map(i => ({ ...i })), sel: new Set(kit.selected) }; }
  function restore(s) { items.length = 0; items.push(...s.rows); kit.selected = s.sel; }
  function showUndo(msg, snap) {
    document.querySelector('.undo')?.remove(); clearTimeout(undoTimer);
    const el = document.createElement('div');
    el.className = 'undo' + (kit.selected.size ? '' : ' low'); el.setAttribute('role', 'status');
    el.innerHTML = `<span>${msg}</span><button type="button">Undo</button>`;
    el.querySelector('button').onclick = () => { restore(snap); el.remove(); host.render(); };
    document.body.append(el);
    undoTimer = setTimeout(() => el.remove(), 9000);
  }

  /* Every bulk write goes through here: rows that changed under you are left out and reported, the rest apply. */
  kit.act = function (msg, fn, { keep = false, rows = null } = {}) {
    const snap0 = snapshot();
    let stale = null;
    if (armed) {
      armed = false;
      const pool = rows ?? rankSorted(kit.selected);
      stale = pool.includes('SHIP-158') ? 'SHIP-158' : pool[0];
      const it = item(stale); if (it) it.points = 5; // what the teammate did; Undo must not touch it
      snap0.rows.find(r => r.key === stale).points = 5;
      kit.selected.delete(stale); kit.staleKey = stale;
    }
    const text = typeof msg === 'function' ? msg() : msg;
    fn();
    if (!keep) kit.selected.clear();
    conflictMsg = ''; pending = null; pop = null; kit.moveMode = false;
    if (stale) {
      kit.staleKey = null; kit.selected.add(stale);
      pending = { msg: `${stale} moved too`, fn, keep, rows: [stale] };
      conflictMsg = `<span>${SL.av('sam')} <b>${esc(text)}.</b> ${stale} wasn't included: Sam Rivera set it to 5 pts while you were working.</span><span class="spacer"></span>
        <button class="btn primary" type="button" data-c="retry">Include it anyway</button><button class="btn" type="button" data-c="skip">Leave it</button>`;
    }
    host.render();
    showUndo(text, snap0);
  };

  /* Move and set */
  const many = n => `${n} item${n === 1 ? '' : 's'}`;
  function toSprint(id) {
    const s = sprints.find(x => x.id === id), name = s ? s.name : 'Backlog';
    kit.act(() => `${many(sel().length)} moved to ${name}`, () => { sel().forEach(i => { i.sprint = id; }); });
  }
  function toEnd(where) {
    kit.act(() => `${many(sel().length)} moved to the ${where} of the backlog`, () => {
      const list = sel(), blockKeys = new Set(list.map(i => i.key));
      const rest = items.filter(i => !blockKeys.has(i.key));
      if (host.mode === 'planning') list.forEach(i => { i.sprint = null; });
      const at = where === 'bottom' ? rest.length : (host.mode === 'list' ? 0 : Math.max(0, rest.findIndex(i => !i.sprint)));
      rest.splice(at < 0 ? rest.length : at, 0, ...list);
      items.length = 0; items.push(...rest);
    });
  }
  function setField(field, value, label) {
    kit.act(() => `${label} on ${many(sel().length)}`, () => {
      sel().forEach(i => { if (field === 'tag') i.tags = [...new Set([...i.tags, value])]; else i[field] = value; });
    }, { keep: true });
  }
  function removeSel() {
    kit.act(() => `${many(sel().length)} set to Removed`, () => { const gone = new Set(sel().map(i => i.key)); const rest = items.filter(i => !gone.has(i.key)); items.length = 0; items.push(...rest); });
  }

  /* Action bar */
  function barHtml() {
    const list = sel(), total = pts(list), un = list.filter(i => i.points == null).length;
    const shown = new Set(visibleKeys()), hidden = list.filter(i => !shown.has(i.key)).length;
    const chips = openSprints().map((s, n) => {
      const adding = pts(list.filter(i => i.sprint !== s.id)), now = committedIn(s.id), cap = capOf(s), after = now + adding, over = after > cap;
      return `<button class="mv" type="button" data-to="${s.id}" title="Move the selection to ${s.name}">
        <span class="n">${kit.moveMode ? `<kbd>${n + 1}</kbd>` : ''}${s.name}${s.status === 'current' ? ' <span class="muted">current</span>' : ''}</span>
        <span class="cap${over ? ' over' : ''}${teamCapacity(s) == null ? ' velocity' : ''}"><i style="width:${Math.min(100, after / cap * 100)}%"></i></span>
        <span class="after${over ? ' over' : ''}">${now} + ${adding} = ${after} / ${cap}${over ? ` · +${after - cap} over` : ''}</span></button>`;
    }).join('');
    const menuBtn = (id, label) => `<span class="popwrap"><button class="btn" type="button" data-pop="${id}" aria-expanded="${pop === id}">${label} ▴</button>${pop === id ? popHtml(id) : ''}</span>`;
    return `<div class="count"><b>${list.length} selected</b><span>${total} pts${un ? ` · <span class="warn">${un} not estimated</span>` : ''}${hidden ? ` · <span class="warn">${hidden} hidden</span>` : ''}</span></div>
      <button class="btn ghost" type="button" data-clear title="Clear the selection">Clear <kbd>Esc</kbd></button>
      <span class="sep"></span>
      <div class="grp ${kit.moveMode ? 'moving' : ''}"><span class="overline">Move to</span>${chips}
        <button class="btn" type="button" data-end="top">${kit.moveMode ? '<kbd>T</kbd>' : ''}Top</button><button class="btn" type="button" data-end="bottom">${kit.moveMode ? '<kbd>B</kbd>' : ''}Bottom</button></div>
      <span class="sep"></span>
      <div class="grp"><span class="overline">Set</span>${menuBtn('points', 'Points')}${menuBtn('state', 'State')}${menuBtn('parent', 'Parent')}${menuBtn('tag', 'Tag')}<button class="btn" type="button" data-remove>Remove</button></div>`;
  }
  function popHtml(id) {
    const b = (attr, v, label) => `<button type="button" data-${attr}="${esc(v)}">${label}</button>`;
    if (id === 'points') return `<div class="upop" style="min-width:110px">${[1, 2, 3, 5, 8, 13].map(n => b('setpts', n, n)).join('')}${b('setpts', '', 'Not estimated')}</div>`;
    if (id === 'state') return `<div class="upop">${states.story.filter(s => s !== 'Removed').map(s => b('setstate', s, SL.st(s))).join('')}</div>`;
    if (id === 'parent') return `<div class="upop">${features.map(f => b('setparent', f.title, tIcon('feature') + esc(f.title))).join('')}${b('setparent', '', '<span class="muted">No parent</span>')}</div>`;
    return `<div class="upop"><div class="tagrow"><input class="field" id="tagNew" placeholder="Tag name" aria-label="Tag name"><button class="btn" type="button" data-tagadd>Add</button></div></div>`;
  }
  function drawBar() {
    const bar = $('bulkbar'); if (!bar) return;
    bar.hidden = !kit.selected.size;
    if (kit.selected.size) bar.innerHTML = barHtml();
    host.body.closest('table')?.classList.toggle('selecting', kit.selected.size > 0);
    const all = $('allChk');
    if (all) { const v = visibleKeys(); all.checked = v.length > 0 && v.every(k => kit.selected.has(k)); all.indeterminate = !all.checked && v.some(k => kit.selected.has(k)); }
    if (pop === 'tag') $('tagNew')?.focus();
  }
  function paintRows() {
    host.body.querySelectorAll('tr.item').forEach(r => {
      const k = r.dataset.key;
      r.classList.toggle('sel', kit.selected.has(k)); r.classList.toggle('foc', kit.focus === k);
      const c = r.querySelector('[data-chk]'); if (c) c.checked = kit.selected.has(k);
    });
    drawBar();
  }
  function range(from, to) {
    const v = visibleKeys(), a = v.indexOf(from), b = v.indexOf(to);
    if (a < 0 || b < 0) return [to];
    return v.slice(Math.min(a, b), Math.max(a, b) + 1);
  }
  function setFocus(k, scroll = true) {
    kit.focus = k; paintRows();
    if (scroll) host.body.querySelector(`tr.item[data-key="${k}"]`)?.scrollIntoView({ block: 'nearest' });
  }

  /* Inline estimate */
  function editPts(k, first = '') {
    const cell = host.body.querySelector(`tr.item[data-key="${k}"] td.pts`); if (!cell) return;
    const it = item(k); kit.focus = k; paintRows();
    cell.innerHTML = `<input class="pts-input" inputmode="decimal" aria-label="Points for ${k}" value="${first || (it.points ?? '')}">`;
    const inp = cell.firstChild; inp.focus(); if (first) inp.setSelectionRange(first.length, first.length); else inp.select();
    let done = false;
    const commit = step => {
      if (done) return; done = true;
      const v = inp.value.trim(), n = v === '' ? null : Number(v);
      if (v !== '' && !(n >= 0)) { done = false; inp.select(); return; }
      const changed = n !== it.points; it.points = n;
      const order = visibleKeys(), next = step ? order[order.indexOf(k) + step] : null;
      host.render();
      if (changed) SL.toast(`${k} ${n == null ? 'not estimated' : n + ' pts'}`);
      if (next) editPts(next); else setFocus(k);
    };
    inp.onkeydown = e => {
      e.stopPropagation();
      if (e.key === 'Enter' || (e.key === 'Tab' && !e.shiftKey) || e.key === 'ArrowDown') { e.preventDefault(); commit(1); }
      else if ((e.key === 'Tab' && e.shiftKey) || e.key === 'ArrowUp') { e.preventDefault(); commit(-1); }
      else if (e.key === 'Escape') { done = true; host.render(); setFocus(k); }
    };
    inp.onblur = () => commit(0);
  }

  /* The item modal: the app's existing ?item=KEY modal, now on Backlogs too. A stand-in with the fields planning needs. */
  function openItem(k) {
    const it = item(k); if (!it) return;
    document.querySelector('.backdrop.itm')?.remove();
    peekKey = k; kit.focus = k; paintRows();
    const opts = (vals, cur) => vals.map(([v, l]) => `<option value="${esc(v)}"${v === cur ? ' selected' : ''}>${esc(l)}</option>`).join('');
    const flags = flagsOf(it);
    const d = document.createElement('div'); d.className = 'backdrop itm';
    d.innerHTML = `<div class="dialog" role="dialog" aria-modal="true" aria-label="${it.key}">
      <header>${tIcon(it.type)}<span class="key">${it.key}</span><span class="saved" id="pSaved"></span><span class="spacer"></span>
        <a class="btn ghost" href="#" data-pfull>Full page</a><button class="ib" type="button" data-pclose aria-label="Close">✕</button></header>
      <div class="body item-body">
        ${flags.length ? `<div class="flags">${flags.map(f => `<span class="chip warn">${f.id === 'dupe' ? `Looks like ${dupeOf(it).key}` : f.label[0].toUpperCase() + f.label.slice(1)}</span>`).join('')}</div>` : '<div class="flags"><span class="chip ok">Ready to plan</span></div>'}
        <label class="f">Title<textarea class="field title" data-pf="title" rows="2">${esc(it.title)}</textarea></label>
        <div class="two">
          <label class="f">Points<input class="field" data-pf="points" inputmode="decimal" value="${it.points ?? ''}" placeholder="?"></label>
          <label class="f">State<select class="field" data-pf="state">${opts(states.story.filter(s => s !== 'Removed').map(s => [s, s]), it.state)}</select></label>
        </div>
        <label class="f">Parent<select class="field" data-pf="parent">${opts([['', 'No parent'], ...features.map(f => [f.title, f.title])], it.parent ?? '')}</select></label>
        <label class="f">Description<textarea class="field" data-pf="description" rows="4" placeholder="What and why">${esc(it.description)}</textarea></label>
        <div class="f" style="display:grid;gap:4px"><span class="overline">Acceptance criteria</span>
          <div class="ac" id="pAcList">${it.ac.map(a => `<label><input type="checkbox"> ${esc(a)}</label>`).join('')}<input class="field" id="pAc" placeholder="Add a criterion, then Enter" aria-label="Add a criterion"></div></div>
      </div></div>`;
    d.addEventListener('click', e => { if (e.target === d) closeItem(); });
    document.body.append(d);
  }
  function closeItem() { document.querySelector('.backdrop.itm')?.remove(); peekKey = null; }
  function savePanelField(e) {
    const f = e.target.dataset.pf; if (!f) return;
    const it = item(peekKey); if (!it) return;
    let v = e.target.value;
    if (f === 'points') v = v.trim() === '' ? null : Number(v);
    if (f === 'parent') v = v || null;
    if (it[f] === v) return;
    it[f] = v;
    host.render();
    if ($('pSaved')) $('pSaved').textContent = 'Saved';
  }

  /* Add row: adds in place and keeps what you typed in order. */
  kit.openAdd = place => { kit.adding = { type: 'story', parent: '', ...place }; host.render(); $('addTitle')?.focus(); };
  function addRowEl() {
    const a = kit.adding, cols = host.body.closest('table').querySelectorAll('thead th').length;
    const where = a.afterKey ? `below ${a.afterKey}` : a.sprint ? `at the top of ${sprints.find(s => s.id === a.sprint).name}` : (host.mode === 'list' ? 'at #1' : 'at the top of the Backlog');
    const tr = document.createElement('tr'); tr.className = 'add';
    tr.innerHTML = `<td colspan="${cols}"><div class="row">
      <select class="field" id="addType" aria-label="Type"><option value="story"${a.type === 'story' ? ' selected' : ''}>User Story</option><option value="bug"${a.type === 'bug' ? ' selected' : ''}>Bug</option></select>
      <input class="field" id="addTitle" style="flex:1" placeholder="Title, then Enter. Or paste many lines" aria-label="Title">
      <select class="field" id="addParent" aria-label="Parent"><option value="">No parent</option>${features.map(f => `<option${a.parent === f.title ? ' selected' : ''}>${esc(f.title)}</option>`).join('')}</select>
      <input class="field pts-new" id="addPts" inputmode="decimal" placeholder="Pts" aria-label="Points">
      <button class="btn primary" type="button" id="addGo">Add</button><button class="btn ghost" type="button" id="addX">Esc</button>
      <span class="xs muted hint">Goes in ${where}</span></div></td>`;
    return tr;
  }
  function placeItem(list, place) {
    const after = place.afterKey ? items.findIndex(i => i.key === place.afterKey) : -1;
    if (after >= 0) { const s = items[after].sprint; list.forEach(n => { n.sprint = s; }); items.splice(after + 1, 0, ...list); return; }
    list.forEach(n => { n.sprint = place.sprint ?? null; });
    if (place.bottom) { items.push(...list); return; }
    if (host.mode === 'list' && !place.sprint) { items.unshift(...list); return; }
    const first = items.findIndex(i => i.sprint === (place.sprint ?? null));
    items.splice(first < 0 ? items.length : first, 0, ...list);
  }
  function wireAdd() {
    const t = $('addTitle'); if (!t) return;
    const close = () => { const back = kit.adding?.afterKey; kit.adding = null; host.render(); if (back) setFocus(back); };
    const go = () => {
      const title = t.value.trim(); if (!title) return;
      const raw = $('addPts').value.trim();
      const it = { key: kit.nextKey(), type: $('addType').value, title, parent: $('addParent').value || null, state: 'New', points: raw === '' ? null : Number(raw), sprint: null, assignee: null, ac: [], tags: [], description: '', kids: 0 };
      kit.adding.type = it.type; kit.adding.parent = it.parent ?? '';
      placeItem([it], kit.adding);
      kit.adding.afterKey = it.key; // the next one lands below this one, so a typed list stays in order
      kit.focus = it.key; host.render(); $('addTitle')?.focus();
      SL.toast(`${it.key} added`);
    };
    const keys = e => { e.stopPropagation(); if (e.key === 'Enter') { e.preventDefault(); go(); } if (e.key === 'Escape') close(); };
    ['addTitle', 'addType', 'addParent', 'addPts'].forEach(id => { $(id).onkeydown = keys; });
    $('addGo').onclick = go; $('addX').onclick = close;
    t.onpaste = e => {
      const text = (e.clipboardData || window.clipboardData).getData('text');
      if (!/\r?\n/.test(text.trim())) return;
      e.preventDefault(); kit.openPaste(text, { ...kit.adding });
    };
  }

  /* Paste review */
  const SAMPLE = `- Show saved cards at checkout
- Remove a saved card
    Add the delete endpoint
    Confirm dialog on the card row
- bug: Session expires during checkout (2 pts)
- Gift card balance on the receipt (3 pts)
- Apple Pay on the order page`;
  function parse(text) {
    const rows = [];
    const lines = text.replace(/\r/g, '').split('\n').filter(l => l.trim());
    let lastTop = null;
    lines.forEach((line, n) => {
      const indent = /^(\s{2,}|\t)/.test(line) && lastTop;
      const cells = line.trim().split('\t');
      if (n === 0 && cells.length > 1 && /^(title|summary|name)$/i.test(cells[0].trim())) return;
      let title = cells[0].replace(/^([-*•]|\d+[.)]|\[[ x]\]|- \[[ x]\])\s+/i, '').replace(/^\[[ x]\]\s+/i, '').trim();
      let type = indent ? 'task' : 'story', points = null;
      if (/^bug:\s*/i.test(title)) { type = 'bug'; title = title.replace(/^bug:\s*/i, ''); }
      const inline = title.match(/\((\d+(?:\.\d+)?)\s*pts?\)\s*$/i);
      if (inline) { points = Number(inline[1]); title = title.slice(0, inline.index).trim(); }
      cells.slice(1).forEach(c => { const v = c.trim(); if (/^\d+(\.\d+)?$/.test(v)) points = Number(v); else if (/^bug$/i.test(v)) type = 'bug'; });
      if (!title) return;
      const row = { title, type, points, kid: !!indent, dupe: indent ? null : items.find(o => similar(title, o.title))?.key ?? null };
      rows.push(row); if (!indent) lastTop = row;
    });
    return rows;
  }
  kit.openPaste = function (text = SAMPLE, place = {}) {
    document.querySelector('.backdrop.pst')?.remove();
    const focusKey = place.afterKey || kit.focus;
    const places = [['top', 'Top of the backlog'], ['bottom', 'Bottom of the backlog'], ...(focusKey ? [['after', `Below ${focusKey}`]] : []), ...sprints.filter(s => s.status === 'future').map(s => ['s:' + s.id, s.name])];
    const cur = place.afterKey ? 'after' : place.sprint ? 's:' + place.sprint : 'top';
    const d = document.createElement('div'); d.className = 'backdrop pst';
    d.innerHTML = `<div class="dialog wide" role="dialog" aria-modal="true" aria-label="Add many work items">
      <header><div><h2>Add many</h2><span class="sm muted">Nothing is created until you press Add.</span></div><span class="spacer"></span><button class="ib" type="button" data-x aria-label="Close">✕</button></header>
      <div class="body">
        <div class="paste">
          <div class="src"><span class="overline">Pasted text</span><textarea class="field" id="pSrc" spellcheck="false" aria-label="Pasted text">${esc(text)}</textarea>
            <p class="rules" style="margin:0">One line, one item. Bullets and numbers are dropped. An indented line becomes a task under the line above. <code>bug:</code> makes a bug. <code>(3 pts)</code> or a number column from a spreadsheet sets points.</p></div>
          <div class="grid"><table><thead><tr><th style="width:124px">Type</th><th>Title</th><th style="width:64px">Pts</th><th style="width:26px"></th></tr></thead><tbody id="pRows"></tbody></table></div>
        </div>
        <div class="opts">
          <label>Parent for all of them<select class="field" id="pParent"><option value="">No parent</option>${features.map(f => `<option${place.parent === f.title ? ' selected' : ''}>${esc(f.title)}</option>`).join('')}</select></label>
          <label>Put them<select class="field" id="pPlace">${places.map(([v, l]) => `<option value="${v}"${v === cur ? ' selected' : ''}>${esc(l)}</option>`).join('')}</select></label>
          <span class="xs muted" style="align-self:center;max-width:320px">They keep the order you pasted, whichever place you pick.</span>
        </div>
      </div>
      <footer><button class="btn primary" type="button" id="pGo"></button><button class="btn ghost" type="button" id="pOne">Keep as one item</button><span class="spacer"></span><button class="btn ghost" type="button" data-x>Cancel</button></footer>
    </div>`;
    document.body.append(d);
    let rows = parse(text);
    const draw = () => {
      const typeOpts = r => r.kid ? `<option>Task</option>` : `<option value="story"${r.type === 'story' ? ' selected' : ''}>User Story</option><option value="bug"${r.type === 'bug' ? ' selected' : ''}>Bug</option>`;
      $('pRows').innerHTML = rows.length ? rows.map((r, i) => `<tr class="${r.kid ? 'kid' : ''}"><td><select class="field" data-i="${i}" data-f="type" aria-label="Type"${r.kid ? ' disabled' : ''}>${typeOpts(r)}</select></td>
        <td class="t"><div class="wrap">${tIcon(r.kid ? 'task' : r.type)}<span style="flex:1;min-width:0"><input class="field" data-i="${i}" data-f="title" value="${esc(r.title)}" aria-label="Title">${r.dupe ? `<span class="dupe">Looks like ${r.dupe}: ${esc(item(r.dupe)?.title ?? '')}</span>` : ''}</span></div></td>
        <td><input class="field" data-i="${i}" data-f="points" inputmode="decimal" value="${r.points ?? ''}" placeholder="?" aria-label="Points"></td>
        <td><button class="ib" type="button" data-del="${i}" aria-label="Leave out">✕</button></td></tr>`).join('') : `<tr><td colspan="4" class="muted sm" style="text-align:center;height:80px">Paste some lines on the left</td></tr>`;
      const tops = rows.filter(r => !r.kid).length, kids = rows.length - tops;
      $('pGo').textContent = `Add ${tops} item${tops === 1 ? '' : 's'}${kids ? ` and ${kids} task${kids === 1 ? '' : 's'}` : ''}`;
      $('pGo').disabled = !rows.length;
    };
    draw();
    const close = () => { d.remove(); $('addTitle')?.focus(); };
    d.addEventListener('click', e => { if (e.target.closest('[data-x]')) close(); const del = e.target.closest('[data-del]'); if (del) { rows.splice(+del.dataset.del, 1); draw(); } });
    d.addEventListener('keydown', e => { e.stopPropagation(); if (e.key === 'Escape') close(); });
    $('pSrc').addEventListener('input', e => { rows = parse(e.target.value); draw(); });
    $('pRows').addEventListener('change', e => { const { i, f } = e.target.dataset; if (i == null) return; rows[i][f] = f === 'points' ? (e.target.value.trim() === '' ? null : Number(e.target.value)) : e.target.value; if (f === 'type') draw(); });
    $('pOne').onclick = () => { const body = $('pSrc').value.trim(); const first = rows[0]?.title ?? 'New item'; d.remove(); kit.act('1 item added', () => { placeItem([{ key: kit.nextKey(), type: 'story', title: first, parent: null, state: 'New', points: null, sprint: null, assignee: null, ac: [], tags: [], description: body, kids: 0 }], {}); }, { keep: true }); };
    $('pGo').onclick = () => {
      const v = $('pPlace').value, parent = $('pParent').value || null;
      const target = v === 'after' ? { afterKey: focusKey } : v === 'bottom' ? { bottom: true } : v.startsWith('s:') ? { sprint: v.slice(2) } : {};
      const made = [];
      rows.forEach(r => {
        if (r.kid) { if (made.length) made[made.length - 1].kids++; return; }
        made.push({ key: kit.nextKey(), type: r.type, title: r.title, parent, state: 'New', points: r.points, sprint: null, assignee: null, ac: [], tags: [], description: '', kids: 0 });
      });
      d.remove(); kit.adding = null;
      kit.act(`${made.length} item${made.length === 1 ? '' : 's'} added`, () => placeItem(made, target), { keep: true });
    };
  };

  /* Readiness strip */
  function drawStrip() {
    const s = $('strip'); if (!s) return;
    const pool = unplannedOrFuture();
    const count = c => pool.filter(c.test).length;
    const chips = CHECKS.map(c => { const n = count(c); return `<button class="qchip${n ? '' : ' zero'}" type="button" data-q="${c.id}" aria-pressed="${kit.queue?.id === c.id}"><b>${n}</b> ${c.label}</button>`; }).join('');
    const q = kit.queue;
    s.innerHTML = `<span class="lead">Before you plan</span>${chips}${q ? `<span class="spacer"></span><button class="btn" type="button" data-qall>Select these ${visibleKeys().length}</button><button class="btn ghost" type="button" data-qx>Clear</button>` : ''}`;
  }

  /* Key sheet */
  function keySheet() {
    if (document.querySelector('.backdrop.ks')) return;
    const row = (keys, label) => `<div><span>${label}</span><span class="k">${keys.map(k => `<kbd>${k}</kbd>`).join('')}</span></div>`;
    const d = document.createElement('div'); d.className = 'backdrop ks';
    d.innerHTML = `<div class="dialog" role="dialog" aria-modal="true" aria-label="Keyboard shortcuts"><header><h2>Keyboard, on the list</h2><span class="spacer"></span><button class="ib" type="button" data-x aria-label="Close">✕</button></header>
      <div class="body keys">
        ${row(['J', 'K'], 'Next, previous row')}${row(['Shift', 'J'], 'Extend the selection')}${row(['X'], 'Select this row')}${row(['Ctrl', 'A'], 'Select all shown')}
        ${row(['Esc'], 'Clear, or close')}${row(['Enter'], 'Open the item')}${row(['0-9'], 'Start typing points')}${row(['N'], 'Add below this row')}
        ${row(['M', '1'], 'Move to a sprint')}${row(['M', 'T'], 'Move to the top')}${row(['M', 'B'], 'Move to the bottom')}${row(['/'], 'Search this list')}
      </div><footer><span class="xs muted">These only fire when the list has focus, never while you type in a field.</span></footer></div>`;
    d.addEventListener('click', e => { if (e.target.closest('[data-x]') || e.target === d) d.remove(); });
    document.body.append(d);
  }

  /* Wiring */
  kit.init = function (h) {
    host = h;
    host.body.addEventListener('mousedown', e => { if (e.shiftKey && e.target.closest('tr.item')) e.preventDefault(); });
    host.body.addEventListener('click', e => {
      const tr = e.target.closest('tr.item'); if (!tr) return;
      if (e.target.closest('.grip-btn')) return;
      const k = tr.dataset.key, mod = e.ctrlKey || e.metaKey;
      const toggle = () => { kit.selected.has(k) ? kit.selected.delete(k) : kit.selected.add(k); anchor = k; kit.focus = k; paintRows(); };
      const extend = () => { const from = anchor ?? kit.focus ?? k; range(from, k).forEach(x => kit.selected.add(x)); kit.focus = k; paintRows(); };
      if (e.target.closest('[data-chk]')) { e.target.checked = !e.target.checked; if (e.shiftKey && anchor) extend(); else toggle(); return; }
      if (e.target.closest('[data-ttl]')) {
        e.preventDefault();
        if (kit.selected.size && e.shiftKey) { extend(); return; }
        if (kit.selected.size && mod) { toggle(); return; }
        if (mod || e.shiftKey) SL.toast('Opens the full item page in a new tab, as today'); else openItem(k);
        return;
      }
      if (e.target.closest('[data-pts]') && !mod && !e.shiftKey) { editPts(k); return; }
      if (e.shiftKey) extend(); else if (mod) toggle(); else { anchor = k; setFocus(k, false); }
    });
    document.addEventListener('click', e => {
      const t = e.target;
      const a = t.closest('[data-to]'); if (a) { toSprint(a.dataset.to); return; }
      const end = t.closest('[data-end]'); if (end) { toEnd(end.dataset.end); return; }
      const clear = t.closest('[data-clear]'); if (clear) { kit.selected.clear(); pop = null; conflictMsg = ''; kit.moveMode = false; host.render(); return; }
      const pb = t.closest('[data-pop]'); if (pb) { pop = pop === pb.dataset.pop ? null : pb.dataset.pop; drawBar(); return; }
      if (t.closest('[data-setpts]')) { const v = t.closest('[data-setpts]').dataset.setpts; pop = null; setField('points', v === '' ? null : Number(v), v === '' ? 'Estimate cleared' : `${v} pts`); return; }
      if (t.closest('[data-setstate]')) { const v = t.closest('[data-setstate]').dataset.setstate; pop = null; setField('state', v, v); return; }
      if (t.closest('[data-setparent]')) { const v = t.closest('[data-setparent]').dataset.setparent; pop = null; setField('parent', v || null, v ? `Parent ${esc(v)}` : 'Parent cleared'); return; }
      if (t.closest('[data-tagadd]')) { const v = $('tagNew').value.trim(); if (v) { pop = null; setField('tag', v, `Tag ${esc(v)}`); } return; }
      if (t.closest('[data-remove]')) { removeSel(); return; }
      const q = t.closest('[data-q]');
      if (q) {
        if (kit.queue?.id === q.dataset.q) kit.queue = null;
        else { const c = CHECKS.find(x => x.id === q.dataset.q); kit.queue = { id: c.id, keys: new Set(unplannedOrFuture().filter(c.test).map(i => i.key)) }; }
        host.render(); return;
      }
      if (t.closest('[data-qx]')) { kit.queue = null; host.render(); return; }
      if (t.closest('[data-qall]')) { visibleKeys().forEach(k => kit.selected.add(k)); paintRows(); return; }
      const c = t.closest('[data-c]');
      if (c) {
        const p = pending; conflictMsg = ''; pending = null;
        if (c.dataset.c === 'skip') { kit.selected.clear(); host.render(); return; }
        if (p) kit.act(p.msg, p.fn, { keep: p.keep, rows: p.rows }); else host.render();
        return;
      }
      if (t.closest('[data-pclose]')) { closeItem(); return; }
      if (t.closest('[data-pfull]')) { e.preventDefault(); SL.toast('Opens the full item page'); return; }
      if (t.id === 'allChk') { const v = visibleKeys(); if (t.checked) v.forEach(k => kit.selected.add(k)); else v.forEach(k => kit.selected.delete(k)); paintRows(); return; }
      if (pop && !t.closest('.upop')) { pop = null; drawBar(); }
    });
    document.addEventListener('change', e => { if (e.target.dataset?.pf) savePanelField(e); });
    document.addEventListener('keydown', e => {
      if (e.target.id === 'pAc' && e.key === 'Enter') { const it = item(peekKey), v = e.target.value.trim(); if (it && v) { it.ac.push(v); e.target.value = ''; e.target.insertAdjacentHTML('beforebegin', `<label><input type="checkbox"> ${esc(v)}</label>`); if ($('pSaved')) $('pSaved').textContent = 'Saved'; host.render(); } }
      if (e.target.id === 'tagNew' && e.key === 'Enter') { const v = e.target.value.trim(); if (v) { pop = null; setField('tag', v, `Tag ${esc(v)}`); } return; }
      if (e.target.matches('input:not([type=checkbox]), select, textarea')) { if (e.key === 'Escape') e.target.blur(); return; }
      if (document.querySelector('.backdrop')) { if (e.key === 'Escape') { document.querySelector('.backdrop').remove(); peekKey = null; } return; }
      const v = visibleKeys(), i = v.indexOf(kit.focus), key = e.key.toLowerCase();
      if (kit.moveMode) {
        e.preventDefault(); kit.moveMode = false;
        const s = openSprints()[Number(key) - 1];
        if (s) toSprint(s.id); else if (key === 't') toEnd('top'); else if (key === 'b') toEnd('bottom'); else drawBar();
        return;
      }
      if ((e.ctrlKey || e.metaKey) && key === 'a') { e.preventDefault(); v.forEach(k => kit.selected.add(k)); paintRows(); return; }
      if (e.ctrlKey || e.metaKey || e.altKey) return;
      const step = key === 'j' || e.key === 'ArrowDown' ? 1 : key === 'k' || e.key === 'ArrowUp' ? -1 : 0;
      if (step) {
        e.preventDefault();
        const n = v[i < 0 ? 0 : i + step]; if (!n) return;
        if (e.shiftKey) { anchor = anchor ?? kit.focus ?? n; kit.selected.clear(); range(anchor, n).forEach(x => kit.selected.add(x)); } else anchor = n;
        setFocus(n); return;
      }
      if (key === 'x' && kit.focus) { kit.selected.has(kit.focus) ? kit.selected.delete(kit.focus) : kit.selected.add(kit.focus); anchor = kit.focus; paintRows(); return; }
      if (key === 'escape') { if (pop) { pop = null; drawBar(); } else if (kit.selected.size) { kit.selected.clear(); conflictMsg = ''; host.render(); } return; }
      if (e.key === 'Enter' && kit.focus && !e.target.closest('button, a')) { e.preventDefault(); openItem(kit.focus); return; }
      if (/^[0-9]$/.test(e.key) && kit.focus) { e.preventDefault(); editPts(kit.focus, e.key); return; }
      if (key === 'n') { e.preventDefault(); kit.openAdd(kit.focus ? { afterKey: kit.focus } : {}); return; }
      if (key === 'm' && (kit.selected.size || kit.focus)) { e.preventDefault(); if (!kit.selected.size) kit.selected.add(kit.focus); kit.moveMode = true; paintRows(); return; }
      if (e.key === '/') { e.preventDefault(); $('find')?.focus(); return; }
      if (e.key === '?') { keySheet(); }
    });
    const find = $('find');
    if (find) find.addEventListener('input', () => { kit.q = find.value.trim().toLowerCase(); host.render(); });
  };

  kit.afterRender = function () {
    if (kit.adding) {
      const a = kit.adding, tr = addRowEl();
      const anchorRow = a.afterKey ? host.body.querySelector(`tr.item[data-key="${a.afterKey}"]`)
        : host.mode === 'planning' ? host.body.querySelector(`tr.sec[data-sec="${a.sprint ?? ''}"]`) : null;
      if (anchorRow) anchorRow.after(tr); else host.body.prepend(tr);
      wireAdd();
    }
    const c = $('bulkConflict'); if (c) { c.hidden = !conflictMsg; c.innerHTML = conflictMsg; }
    paintRows(); drawStrip();
  };

  /* Drag badge and group helpers */
  kit.dragKeys = k => (kit.selected.has(k) ? rankSorted(kit.selected) : [k]);
  kit.dragBadge = function (e, keys) {
    if (keys.length < 2) return;
    const list = keys.map(item), b = document.createElement('div');
    b.className = 'dragbadge'; b.textContent = `${keys.length} items · ${pts(list)} pts`;
    document.body.append(b); e.dataTransfer.setDragImage(b, 12, 14); setTimeout(() => b.remove(), 0);
  };
  kit.moveBlock = function (keys, beforeKey) {
    const set = new Set(keys), block = items.filter(i => set.has(i.key)), rest = items.filter(i => !set.has(i.key));
    rest.splice(rest.findIndex(i => i.key === beforeKey), 0, ...block);
    items.length = 0; items.push(...rest);
  };
  kit.previewFor = function (keys, sprintId) {
    const s = sprints.find(x => x.id === sprintId); if (!s) return null;
    const adding = pts(keys.map(item).filter(i => i.sprint !== sprintId)), after = committedIn(sprintId) + adding, cap = capOf(s);
    return { text: `+${adding} → ${after} / ${cap}${after > cap ? ` · +${after - cap} over` : ''}`, over: after > cap };
  };
  kit.flagsOf = flagsOf;
  kit.committedIn = committedIn; kit.capOf = capOf;

  /* Opening states for the review page */
  kit.boot = function () {
    const st = params.get('state');
    const pick = (host.mode === 'planning') ? ['SHIP-160', 'SHIP-158', 'SHIP-163', 'SHIP-156'] : ['SHIP-160', 'SHIP-158', 'SHIP-184', 'SHIP-163'];
    if (st === 'selected') { pick.forEach(k => kit.selected.add(k)); kit.focus = pick[pick.length - 1]; anchor = pick[0]; }
    if (st === 'queue') { const c = CHECKS[0]; kit.queue = { id: c.id, keys: new Set(unplannedOrFuture().filter(c.test).map(i => i.key)) }; }
    if (st === 'add') { kit.focus = 'SHIP-158'; kit.adding = { type: 'story', parent: '', afterKey: 'SHIP-158' }; }
    host.render();
    if (st === 'conflict') { pick.forEach(k => kit.selected.add(k)); armed = true; toSprint('s25'); }
    if (st === 'modal') openItem('SHIP-184');
    if (st === 'paste') kit.openPaste();
    if (st === 'add') $('addTitle')?.focus();
    if (st === 'keys') keySheet();
    return kit;
  };
  kit.arm = () => { armed = true; SL.toast('Sam will change one of the rows during your next bulk write'); };
  kit.keySheet = keySheet;

  SL.kit = kit;
})();
