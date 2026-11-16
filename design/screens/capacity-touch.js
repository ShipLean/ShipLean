/*
 * The sprint Capacity tab in the touch shell (SHIP-1626 Q4, for SHIP-726); styles in capacity-touch.css. Loaded after
 * capacity.html's own script and drives it through its own controls (days, reset, ×, + Add member, Copy, Clear), so the
 * figures are the desktop's. At 900px and below:
 *   - SHIP-724's shared sprint header: picker · Current · ⋯, the dates and time left, the goal (two lines), the tabs
 *   - one toolbar row: Copy from Sprint 23 and ⋯ More (Add member, Clear) (P5)
 *   - "Sprint 24 as a whole" first: three compact tiles, the bar, the room or over line and the velocity check (P1)
 *   - Team velocity on one row with its field at 16px; the calendar line under it
 *   - People as two-line rows: name and points, then a bar of days available out of the working days and the note.
 *     The bar is availability only: nobody's number is compared with work assigned to them (desktop rule) (P2, P6)
 *   - tapping a person opens their days sheet: − / + in half days around a 16px field, Use default, the note and
 *     Remove from Sprint 24 (P3)
 *   - edits are a draft until Save on the shared Save bar (message · Discard · Save, SHIP-805 rule 15) (P4)
 * Above 900px nothing changes; ?notouch shows the desktop page at any width.
 * Review states (add ?shot for screenshots):
 *   ?at=people              with ?shot, hides what is above the People list (Chrome's --screenshot ignores scrolling)
 *   ?sheet=days&p=ana       a person's days sheet
 *   ?sheet=more|add         the toolbar ⋯ More, and the Add member sheet (?rm=mei first takes Mei out of the table)
 *   ?sheet=sprint|sprintmore the sprint picker and the header's ⋯ (SHIP-724's shared header)
 *   ?dirty                  Ana's days 5 → 8, unsaved: the Save bar, and the sprint back under capacity
 *   ?empty                  nobody in the table: the sprint plans against velocity
 * The page's own data is over capacity (20 pts against 21 committed), so the plain page is the over-capacity state.
 */
(() => {
  const q = new URLSearchParams(location.search);
  if (q.has('notouch')) { document.querySelector('link[href="capacity-touch.css"]')?.remove(); return; }
  const mq = matchMedia('(max-width: 900px)');
  mq.addEventListener('change', () => location.reload());
  if (!mq.matches) return;

  const { people, sprints, esc, av, toast } = SL;
  const root = document.documentElement;
  const $ = s => document.querySelector(s);
  const el = (tag, cls, html) => { const e = document.createElement(tag); if (cls) e.className = cls; if (html != null) e.innerHTML = html; return e; };
  const dots = '<svg viewBox="0 0 16 16" width="16" height="16" aria-hidden="true"><circle cx="3.5" cy="8" r="1.4" fill="currentColor"/><circle cx="8" cy="8" r="1.4" fill="currentColor"/><circle cx="12.5" cy="8" r="1.4" fill="currentColor"/></svg>';
  const chev = '<svg viewBox="0 0 16 16" width="16" height="16" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="m6 3 5 5-5 5"/></svg>';
  const s24 = sprints.find(x => x.id === 's24');
  const [, W, H] = ($('#cal').textContent.match(/(\d+) working days · (\d+)/) || [0, 10, 0]).map(Number);
  const DEF = W - H;
  const vel = $('#vel'), rows = $('#rows'), add = $('#add');
  const fmt = n => String(Math.round(n * 100) / 100);

  /* Reading and driving the desktop page */
  const notes = {};
  const read = () => [...rows.querySelectorAll('tr')].map(tr => {
    const inp = tr.querySelector('.days-in'), p = inp.dataset.p;
    if (!(p in notes)) notes[p] = tr.querySelector('.note-in').value;
    const days = +inp.value, typed = !!tr.querySelector('[data-reset]') || days !== DEF;
    return { p, days, typed, note: notes[p], pts: tr.querySelector(`[data-pts="${p}"]`).textContent.replace(/\s*pts$/, '') };
  });
  const setDays = (p, v) => {
    const inp = rows.querySelector(`.days-in[data-p="${p}"]`); if (!inp) return;
    inp.value = v; inp.dispatchEvent(new Event('input', { bubbles: true }));
  };
  const useDefault = p => {
    const r = rows.querySelector(`[data-reset="${p}"]`);
    if (r) r.click(); else { setDays(p, ''); rows.querySelector(`.days-in[data-p="${p}"]`)?.dispatchEvent(new Event('change', { bubbles: true })); }
  };
  const remove = p => rows.querySelector(`[data-rm="${p}"]`)?.click();
  const addPerson = p => { add.value = p; add.dispatchEvent(new Event('change')); };
  const absent = () => [...add.options].filter(o => o.value).map(o => o.value);
  const state = () => JSON.stringify({ v: vel.value, r: read().map(r => [r.p, r.days, r.typed, r.note]) });

  /* Sprint header: SHIP-724's shared phone header (its D5, D6, D10) */
  const head = $('.head');
  head.classList.add('cpt-head');
  head.querySelector('.sm.muted').innerHTML = `${s24.start} – ${s24.end} · <b>5 days left</b>`;
  const hmore = el('button', 'btn cpt-hmore', dots);
  const goal = el('button', 'cpt-goal', `<span class="overline">Goal</span><span class="txt">${esc(s24.goal)}</span>`);
  hmore.type = goal.type = 'button';
  hmore.setAttribute('aria-label', 'More sprint actions');
  goal.setAttribute('aria-expanded', 'false');
  head.querySelector('.chip').after(hmore);
  head.querySelector('.sm.muted').after(goal);
  hmore.addEventListener('click', () => openSheet('sprintmore'));
  goal.addEventListener('click', () => goal.setAttribute('aria-expanded', goal.getAttribute('aria-expanded') !== 'true'));
  head.querySelector('.picker').addEventListener('click', () => openSheet('sprint'));
  const tabs = head.querySelector('.tabs'), cur = tabs?.querySelector('[aria-current="page"]');
  if (cur) tabs.scrollLeft = cur.offsetLeft - (tabs.clientWidth - cur.offsetWidth) / 2;

  /* Toolbar row (rule 8): the primary action and ⋯ More */
  const bar = el('div', 'cpt-bar', `<button class="btn" type="button" data-copy>Copy from Sprint 23</button><span class="spacer"></span><button class="btn cpt-more" type="button" aria-label="More capacity actions">${dots}</button>`);
  head.after(bar);
  bar.querySelector('[data-copy]').addEventListener('click', () => $('#copyBtn').click());
  bar.querySelector('.cpt-more').addEventListener('click', () => openSheet('more'));

  /* People: two-line rows in place of the table (P2) */
  const card = $('.layout > .card');
  const list = el('section', 'cpt-people');
  list.setAttribute('aria-label', 'People');
  card.append(list);
  function drawPeople() {
    const rs = read(), total = $('#total').textContent;
    const was = new Map(saved ? JSON.parse(saved).r.map(r => [r[0], JSON.stringify(r)]) : []);
    const changed = r => saved && was.get(r.p) !== JSON.stringify([r.p, r.days, r.typed, r.note]);
    list.innerHTML = `<header><b>People</b><span class="muted">${rs.length}</span><span class="spacer"></span>${rs.length ? `<span class="tot">Team capacity <b>${esc(total)}</b></span>` : ''}</header>
      ${rs.map(r => `<button class="cpt-row" type="button" data-person="${r.p}" aria-label="${people[r.p].name}: ${r.days} days, ${r.pts} points">
        ${av(r.p)}<span class="tx"><span class="l1"><b>${people[r.p].name}</b>${changed(r) ? '<span class="chg">Unsaved</span>' : ''}<span class="pts">${r.pts} <small>pts</small></span></span>
        <span class="l2"><span class="cap"><i style="width:${Math.min(100, r.days / W * 100)}%"></i></span><span class="d">${fmt(r.days)} / ${W} days${r.typed ? '' : ' · default'}</span></span>
        ${r.note ? `<span class="note">${esc(r.note)}</span>` : ''}</span><span class="go">${chev}</span></button>`).join('')}
      ${rs.length ? '' : `<div class="cpt-empty"><b>No capacity entered</b><span>Sprint 24 plans against velocity (${SL.VELOCITY}). Add the people who are in, or copy Sprint 23's table.</span></div>`}
      ${absent().length ? '<button class="cpt-add" type="button" data-add>+ Add member</button>' : ''}
      ${rs.length ? '<p class="foot">Capacity is the velocity shared out by days available. Only the team total is compared with what the sprint has committed.</p>' : ''}`;
  }
  list.addEventListener('click', e => {
    const r = e.target.closest('[data-person]'); if (r) openSheet('days', r.dataset.person);
    if (e.target.closest('[data-add]')) openSheet('add');
  });

  /* The shared Save bar (SHIP-805 rule 15): message · Discard · Save (P4) */
  const save = el('div', 'savebar', '<span class="what"><span class="dot"></span><span>Unsaved capacity changes</span></span><button class="btn" type="button" data-sb="discard">Discard</button><button class="btn primary" type="button" data-sb="save">Save</button>');
  save.setAttribute('role', 'region'); save.setAttribute('aria-label', 'Unsaved changes');
  save.hidden = true;
  document.body.append(save);
  let saved;
  save.addEventListener('click', e => {
    const b = e.target.closest('[data-sb]'); if (!b) return;
    if (b.dataset.sb === 'save') { saved = state(); toast('Saved capacity for Sprint 24'); render(); return; }
    const s = JSON.parse(saved), want = new Map(s.r.map(r => [r[0], r]));
    read().forEach(r => { if (!want.has(r.p)) remove(r.p); });
    s.r.forEach(([p, days, typed, note]) => { if (absent().includes(p)) addPerson(p); notes[p] = note; if (typed) setDays(p, days); else useDefault(p); });
    vel.value = s.v; vel.dispatchEvent(new Event('input'));
    render();
  });

  /* Sheets (SHIP-805 rules 3, 8 and 805 D1): grab bar, title, Close */
  const scrim = el('div', 'cpt-scrim'); scrim.hidden = true;
  const sheet = el('div', 'cpt-sheet'); sheet.hidden = true;
  sheet.setAttribute('role', 'dialog'); sheet.setAttribute('aria-modal', 'true');
  document.body.append(scrim, sheet);
  let open = null;
  const shHead = title => `<header class="sheet-head">${title}<button class="btn ghost sheet-x" type="button" data-close>Close</button></header>`;
  function openSheet(kind, p) { open = { kind, p }; drawSheet(); }
  function closeSheet() { open = null; sheet.hidden = scrim.hidden = true; }
  scrim.addEventListener('click', closeSheet);
  function drawSheet() {
    if (!open) return;
    sheet.hidden = scrim.hidden = false;
    const { kind, p } = open;
    if (kind === 'days') {
      const r = read().find(x => x.p === p); if (!r) { closeSheet(); return; }
      const n = read().length;
      sheet.setAttribute('aria-label', `Days available for ${people[p].name}`);
      sheet.innerHTML = `${shHead(`<span class="who">${av(p)}<b>${people[p].name}</b></span>`)}<div class="body">
        <label class="lbl" for="cptDays">Days available</label>
        <div class="stepper"><button class="btn" type="button" data-step="-0.5" aria-label="Half a day less"${r.days <= 0 ? ' disabled' : ''}>−</button>
          <input class="field" id="cptDays" type="number" inputmode="decimal" min="0" max="${W}" step="0.5" value="${fmt(r.days)}">
          <button class="btn" type="button" data-step="0.5" aria-label="Half a day more"${r.days >= W ? ' disabled' : ''}>+</button>
          <span class="of">of ${W} working days</span></div>
        <div class="dflt">${r.typed ? `<span>Default ${DEF}: ${W} working days less ${H} public holiday</span><button class="btn ghost" type="button" data-default>Use default</button>` : `<span>The default: ${W} working days less ${H} public holiday</span>`}</div>
        <div class="calc"><span>Capacity</span><b>${r.pts} pts</b><small>${fmt(+vel.value || 0)} velocity ÷ ${n} people × ${fmt(r.days)} ÷ ${W} days</small></div>
        <label class="lbl" for="cptNote">Note</label>
        <input class="field" id="cptNote" value="${esc(r.note)}" placeholder="Optional, e.g. Off Thu–Fri">
        <hr><button class="opt danger" type="button" data-remove><span class="tx"><span>Remove from Sprint 24</span><small>Their share goes to everyone else in the table</small></span></button>
      </div>`;
    } else if (kind === 'add') {
      const a = absent();
      sheet.setAttribute('aria-label', 'Add member');
      sheet.innerHTML = `${shHead('<b>Add member</b>')}<div class="body">
        ${a.map(x => `<button class="opt" type="button" data-addp="${x}">${av(x)}<span class="tx"><span>${people[x].name}</span><small>Starts at the default, ${DEF} days</small></span></button>`).join('') || '<p class="note">Everyone on the project is in the table.</p>'}
      </div>`;
    } else if (kind === 'more') {
      const a = absent().length, n = read().length;
      sheet.setAttribute('aria-label', 'Capacity');
      sheet.innerHTML = `${shHead('<b>Capacity</b>')}<div class="body">
        <button class="opt" type="button" data-more="add"${a ? '' : ' disabled'}><span class="tx"><span>Add member</span><small>${a ? `${a} on the project not in the table` : 'Everyone on the project is in the table'}</small></span><span class="go">${chev}</span></button>
        <button class="opt danger" type="button" data-more="clear"${n ? '' : ' disabled'}><span class="tx"><span>Clear</span><small>Remove everyone; Sprint 24 plans against velocity (${SL.VELOCITY})</small></span></button>
      </div>`;
    } else if (kind === 'sprint') {
      const label = { past: 'Past', current: 'Current', future: 'Future' };
      sheet.setAttribute('aria-label', 'Sprints');
      sheet.innerHTML = `${shHead('<b>Sprints</b>')}<p class="note sub">Opens the same tab (Capacity) of the sprint you pick.</p><div class="body">
        ${sprints.map(x => `<button class="opt" type="button" role="menuitemradio" aria-checked="${x.id === 's24'}"><span class="chk">${x.id === 's24' ? '✓' : ''}</span><span class="tx"><span>${x.name}</span><small>${x.start} – ${x.end}</small></span><span class="chip${x.status === 'current' ? ' accent' : ''}">${label[x.status]}</span></button>`).join('')}
        <hr><a class="opt" href="sprints.html"><span class="tx"><span>All sprints</span></span><span class="go">${chev}</span></a>
      </div>`;
    } else if (kind === 'sprintmore') {
      sheet.setAttribute('aria-label', 'Sprint 24');
      // Icons as the Sprint backlog's ⋯ (sprint-backlog-more): flag, pencil, list, check.
      const ic = d => `<span class="g"><svg viewBox="0 0 16 16" aria-hidden="true"><path d="${d}" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/></svg></span>`;
      sheet.innerHTML = `${shHead('<b>Sprint 24</b>')}<div class="body">
        <button class="opt" type="button">${ic('M3.5 14V2.5M3.5 3h8l-1.6 3 1.6 3h-8')}<span class="tx"><span>Edit goal</span></span></button>
        <button class="opt" type="button">${ic('M10.5 2.5 13.5 5.5 6 13H3v-3z')}<span class="tx"><span>Edit sprint</span><small>Name, dates and goal</small></span></button>
        <a class="opt" href="sprints.html">${ic('M5.5 4h8M5.5 8h8M5.5 12h8M2.5 4h.01M2.5 8h.01M2.5 12h.01')}<span class="tx"><span>All sprints</span></span><span class="go">${chev}</span></a>
        <hr><a class="opt" href="sprint-end.html">${ic('m3.5 8.5 3 3 6-7')}<span class="tx"><span>Close sprint</span><small>Choose where unfinished work goes</small></span><span class="go">${chev}</span></a>
      </div>`;
    }
  }
  sheet.addEventListener('click', e => {
    if (e.target.closest('[data-close]')) { closeSheet(); return; }
    const p = open?.p;
    const st = e.target.closest('[data-step]');
    if (st) { const r = read().find(x => x.p === p); setDays(p, Math.max(0, Math.min(W, r.days + +st.dataset.step))); render(); }
    if (e.target.closest('[data-default]')) { useDefault(p); render(); }
    if (e.target.closest('[data-remove]')) { remove(p); closeSheet(); render(); }
    const ap = e.target.closest('[data-addp]');
    if (ap) { addPerson(ap.dataset.addp); openSheet('days', ap.dataset.addp); render(); }
    const m = e.target.closest('[data-more]');
    if (m?.dataset.more === 'add') openSheet('add');
    if (m?.dataset.more === 'clear') { $('#clearBtn').click(); closeSheet(); render(); }
  });
  sheet.addEventListener('change', e => {
    if (e.target.id === 'cptDays') { setDays(open.p, e.target.value === '' ? DEF : e.target.value); render(); }
    if (e.target.id === 'cptNote') { notes[open.p] = e.target.value; render(); }
  });

  function render() {
    drawPeople();
    const dirty = state() !== saved;
    save.hidden = !dirty;
    document.body.classList.toggle('has-savebar', dirty);
    if (open) drawSheet();
  }
  vel.addEventListener('input', () => render());
  $('#copyBtn').addEventListener('click', () => render());
  $('#clearBtn').addEventListener('click', () => render());
  add.addEventListener('change', () => render());

  /* Review states */
  if (q.has('empty')) read().forEach(r => remove(r.p));
  if (q.get('rm')) remove(q.get('rm'));
  saved = state();
  if (q.has('dirty')) setDays('ana', 8);
  render();
  if (q.get('sheet')) openSheet(q.get('sheet'), q.get('p'));
  if (q.get('at') === 'people' && q.has('shot')) document.head.append(el('style', null, '.cpt-head, .cpt-bar, .layout > .sum, .layout > .card > .top { display: none !important; }'));
})();
