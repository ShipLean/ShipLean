/*
 * SHIP-497 retro cards, shared by every retro screen: the post-it note and its actions, the column with Add under the
 * last card, the aggregate rows, the New action item dialog and the tooltips. Load after shared.js and retro-data.js.
 * The mock changes RETRO's sample data in memory, so counts move as you decide points. Nothing is saved.
 */
(() => {
  const { esc, tIcon, av, people, toast, ME } = SL;
  const { themes, actions, retros, catLabel, catOrder, featureOf } = RETRO;

  const FIXES = [
    ['skill', 'Skill', 'A repeatable way to do a kind of task'],
    ['guide', 'Guide', 'The planning guide, a brief, or agent md'],
    ['test', 'Test', 'Automation that fails when it happens again'],
    ['analyzer', 'Analyzer', 'A static check that stops it at build time'],
    ['product', 'Product', 'Change ShipLean itself'],
    ['other', 'Other', 'None of these; say what in the description'],
  ];
  const fixLabel = id => FIXES.find(f => f[0] === id)?.[1] ?? '';

  // Which points need a decision. Went well never does (open question on the review page).
  const decidable = p => p.cat !== 'well';
  const isOpen = p => decidable(p) && !p.action && !p.none;
  const actionOf = key => actions.find(a => a.key === key);
  const pointsOfAction = key => retros.flatMap(r => r.points.filter(p => p.action === key).map(p => ({ p, r })));
  const idOf = (r, p) => `${r.id}.${r.points.indexOf(p)}`;
  const pointById = id => {
    const [rid, i] = id.split('.');
    const r = retros.find(x => x.id === rid);
    return { r, p: r.points[+i] };
  };
  const firstName = id => people[id].name.split(' ')[0];

  const retrosOf = key => retros.filter(r => r.subject === key);
  const scopeOfFeature = key => retros.filter(r => r.subject === key || featureOf[r.subject] === key);

  function counts(rs) {
    const c = { well: 0, unplanned: 0, bad: 0, improve: 0, open: 0, decided: 0, decidable: 0, actions: new Set() };
    rs.forEach(r => r.points.forEach(p => {
      c[p.cat]++;
      if (decidable(p)) c.decidable++;
      if (isOpen(p)) c.open++;
      else if (decidable(p)) c.decided++;
      if (p.action) c.actions.add(p.action);
    }));
    return c;
  }

  const catHead = cat => `<span class="rc-th ${cat}">${catLabel[cat]}</span>`;
  const countCell = (cat, n) => `<span class="rc-count ${cat}${n ? '' : ' zero'}" title="${catLabel[cat]}: ${n}">${n}</span>`;
  const countStrip = c => `<span class="rc-counts">${catOrder.map(cat => countCell(cat, c[cat])).join('')}</span>`;
  const progress = c => c.decidable
    ? `<span class="rc-prog" title="${c.decided} of ${c.decidable} points decided"><i><b style="width:${Math.round(c.decided / c.decidable * 100)}%"></b></i>${c.decided}/${c.decidable}</span>`
    : '<span class="muted xs">nothing to decide</span>';
  const todo = n => n ? `<span class="rc-todo">${n} to decide</span>` : '';

  // An agent's chip already says who wrote it; the person it ran for is on the avatar.
  const whoHtml = r => (r.agent
    ? `<span title="${esc(people[r.by].name)}">${av(r.by)}</span><span class="agent rc-trunc" title="${esc(r.agent)}">${esc(r.agent)}</span>`
    : `${av(r.by)}<span>${firstName(r.by)}</span>`);

  /* The note. opts: showItem (the point's item and author, for views over many retros), showTheme, hit (highlight). */
  function note(p, r, opts = {}) {
    const { showItem = false, showAuthor = true, showTheme = true, hit = false } = opts;
    const id = idOf(r, p);
    const editingWhy = RC.ui.why === id;
    const a = p.action && actionOf(p.action);
    const others = a ? pointsOfAction(p.action).length - 1 : 0;
    const theme = themes[p.theme] ?? themes.none;
    const canArchive = r.role === 'person';

    let acts = '';
    if (a) {
      acts = `<a class="rc-pin" href="work-item.html?key=${a.key}" title="${esc(a.title)}${a.fix ? ` · ${fixLabel(a.fix)}` : ''}">${tIcon('action')}<span class="key">${a.key}</span>${SL.st(a.state)}${others ? `<span class="also">+${others} more point${others === 1 ? '' : 's'}</span>` : ''}</a>`;
    } else if (p.none) {
      acts = `<div class="rc-none"><b>Not needed</b><span>${esc(p.none.reason)}</span><span>${firstName(p.none.by)}${p.none.agent ? ` · ${esc(p.none.agent)}` : ''} · <button class="rc-link quiet" type="button" data-rc-undo="${id}">Undo</button></span></div>`;
    } else if (editingWhy) {
      acts = `<div class="rc-why"><textarea rows="2" data-rc-why-text placeholder="Why isn't this needed? (required)"></textarea>
        <div class="row"><button class="rc-mini solid" type="button" data-rc-why-save="${id}">Save</button><button class="rc-mini" type="button" data-rc-why-cancel>Cancel</button></div></div>`;
    } else if (decidable(p)) {
      acts = `<div class="rc-acts">${p.cat === 'improve'
        ? `<button class="rc-cta" type="button" data-rc-action="${id}">+ Action item</button>`
        : `<button class="rc-link" type="button" data-rc-action="${id}">+ Action item</button>`}
        <button class="rc-link quiet" type="button" data-rc-why="${id}">Not needed</button></div>`;
    }

    const showMeta = showItem || (showAuthor && r.role !== undefined);
    return `<article class="rc-note${p.none ? ' is-none' : ''}${hit ? ' is-hit' : ''}${canArchive ? ' has-more' : ''}" data-rc-point="${id}" data-category="${p.cat}">
      ${canArchive ? `<button class="ib rc-more" type="button" data-rc-more="${id}" title="Archive this point (people's points only)" aria-label="More">⋯</button>` : ''}
      <p>${esc(p.text)}</p>
      ${p.cause ? `<p class="cause"><b>Root cause:</b> ${esc(p.cause)}</p>` : ''}
      ${showTheme ? `<button class="rc-theme${p.theme === 'none' ? ' unset' : ''}" type="button" data-rc-theme="${id}" title="Move to another theme"># ${esc(theme.name)} <span class="car">▾</span></button>` : ''}
      ${showMeta ? `<div class="meta">${showItem ? `${tIcon(r.type)}<a class="key" href="retro-item.html?key=${r.subject}">${r.subject}</a>` : ''}${showAuthor ? whoHtml(r) : ''}</div>` : ''}
      ${acts}
    </article>`;
  }

  /* One category's column: its notes, then Add under the last card. ctx says what Add writes to. */
  function column(cat, notesHtml, n, open, ctx) {
    const adding = RC.ui.adding === `${ctx.id}:${cat}`;
    return `<section class="rc-col ${cat}" aria-label="${catLabel[cat]}">
      <header><span>${catLabel[cat]}</span><span class="n">${n}</span><span class="spacer"></span>${todo(open)}</header>
      ${notesHtml}
      ${adding ? editor(cat, ctx) : `<button class="rc-add" type="button" data-rc-add="${ctx.id}:${cat}">+ Add</button>`}
    </section>`;
  }

  function editor(cat, ctx) {
    return `<article class="rc-note rc-editor" data-rc-editor="${ctx.id}:${cat}">
      <textarea rows="3" data-rc-add-text placeholder="${catLabel[cat]}: what happened?"></textarea>
      ${cat === 'bad' ? '<label>Root cause<textarea rows="2" data-rc-add-cause placeholder="Optional"></textarea></label>' : ''}
      ${ctx.about ? `<label>About<select data-rc-add-about>${ctx.about.map(([v, l]) => `<option value="${esc(v)}">${esc(l)}</option>`).join('')}</select></label>` : ''}
      <div class="row"><button class="rc-mini solid" type="button" data-rc-add-save="${ctx.id}:${cat}">Add</button><button class="rc-mini" type="button" data-rc-add-cancel>Cancel</button><span class="spacer"></span><span class="xs muted">Goes on your retro</span></div>
    </article>`;
  }

  /* The four columns over a set of retros. Agent text stays read-only; Add appends to the person's own retro on the item. */
  function board(rs, ctx, opts = {}) {
    const filter = opts.filter ?? (() => true);
    const cols = catOrder.map(cat => {
      const rows = rs.flatMap(r => r.points.filter(p => p.cat === cat && filter(p, r)).map(p => ({ p, r })));
      const open = rs.flatMap(r => r.points.filter(p => p.cat === cat && isOpen(p))).length;
      let html;
      if (opts.groupByTheme) {
        const groups = {};
        rows.forEach(x => (groups[x.p.theme] ??= []).push(x));
        const order = Object.keys(groups).sort((a, b) => (a === 'none') - (b === 'none') || groups[b].length - groups[a].length);
        html = order.map(k => `<div class="rc-grp-h"><span>${esc(themes[k].name)}</span>${RETRO.recurring(k) ? '<span class="chip warn" style="height:16px" data-rc-tip="recurring">Recurring</span>' : ''}</div>${groups[k].map(({ p, r }) => note(p, r, { ...opts.note, showTheme: false })).join('')}`).join('');
      } else {
        html = rows.map(({ p, r }) => note(p, r, opts.note)).join('');
      }
      return column(cat, html, rows.length, open, ctx);
    }).join('');
    return `<div class="rc-board">${cols}</div>`;
  }

  /* The raw failure log, collapsed, under the retro it came from. */
  const rawLog = r => r.log ? `<details class="rawlog"><summary>Raw failure log <span class="muted">· ${esc(r.agent || firstName(r.by))} · verbatim from the agent</span></summary>
    <dl>${[['blockers', 'Blockers'], ['disproved', 'Assumptions disproved'], ['friction', 'Friction']].map(([k, l]) => `<dt>${l}</dt><dd>${r.log[k].length ? r.log[k].map(esc).join('<br>') : '<span class="muted">None reported.</span>'}</dd>`).join('')}</dl></details>` : '';

  /* The work item page's section: one row per retro, with counts. A row opens that item's page in Retrospectives. */
  function workItemRows(key) {
    const rs = retrosOf(key);
    if (!rs.length) return '';
    return `<div class="rc-box"><table class="rc-rows">
      <thead><tr><th>Retro</th><th>Logged</th>${catOrder.map(c => `<th class="n">${catHead(c)}</th>`).join('')}<th>Decided</th></tr></thead>
      <tbody>${rs.map(r => {
        const c = counts([r]);
        const href = `retro-item.html?key=${key}&retro=${r.id}`;
        return `<tr data-href="${href}"><td><div class="row" style="gap:6px;flex-wrap:nowrap;white-space:nowrap">${av(r.by)}<a class="rowlink" href="${href}">${esc(r.agent || people[r.by].name)}</a></div></td>
          <td class="muted xs" style="white-space:nowrap">${r.at.replace(/ \d\d:\d\d$/, '')}</td>
          ${catOrder.map(cat => `<td class="n">${countCell(cat, c[cat])}</td>`).join('')}
          <td>${progress(c)}</td></tr>`;
      }).join('')}</tbody></table></div>`;
  }

  /* New action item. seed: point ids already picked. scope: the retros whose open points it offers. */
  function openActionDialog(seed = [], scope = [], opts = {}) {
    const seeded = seed.map(pointById);
    const picks = new Set(seed);
    const first = seeded[0]?.p;
    const offered = cat => scope.flatMap(r => r.points.filter(p => p.cat === cat && isOpen(p) && !picks.has(idOf(r, p))).map(p => ({ p, r })));
    const pickRow = ({ p, r }) => {
      const id = idOf(r, p);
      return `<label class="rc-pick"><input type="checkbox" value="${id}"${picks.has(id) ? ' checked' : ''}><span>${esc(p.text)}<small>${catLabel[p.cat]} · ${r.subject} · # ${esc((themes[p.theme] ?? themes.none).name)}</small></span></label>`;
    };
    const group = (label, cat) => {
      const rows = offered(cat);
      return rows.length ? `<div class="overline">${label}</div>${rows.map(pickRow).join('')}` : '';
    };
    const open = actions.filter(a => a.state !== 'Closed');
    const el = document.createElement('div');
    el.className = 'rc-overlay';
    el.innerHTML = `<div class="dialog rc-dialog" role="dialog" aria-modal="true" aria-label="New action item">
      <header>${tIcon('action')}<h2>Action item</h2><span class="spacer"></span>
        <div class="seg" role="group" aria-label="New or existing"><button type="button" data-mode="new" aria-pressed="true">New</button><button type="button" data-mode="existing" aria-pressed="false">Add to existing</button></div>
        <button class="ib" type="button" data-close aria-label="Close">✕</button></header>
      <div class="body">
        <div class="rc-field" data-only="new"><label for="rcTitle">Title</label><input class="field" id="rcTitle" value="${esc(first ? first.text.slice(0, 80) : '')}" placeholder="What will change"></div>
        <div class="rc-field" data-only="existing" hidden><label for="rcExisting">Action item</label><select class="field" id="rcExisting" style="width:100%">${open.map(a => `<option value="${a.key}">${a.key} · ${esc(a.title)}</option>`).join('')}</select></div>
        <div class="rc-field" data-only="new"><span class="lbl">Type of fix</span>
          <div class="rc-fix" role="radiogroup" aria-label="Type of fix">${FIXES.map(([id, label, hint]) => `<label><input type="radio" name="rcFix" value="${id}"${id === (opts.fix ?? 'guide') ? ' checked' : ''}>${label}<span>${hint}</span></label>`).join('')}</div></div>
        <div class="rc-field" data-only="new"><label for="rcTheme">Theme</label><select class="field" id="rcTheme" style="width:100%">${Object.entries(themes).filter(([, t]) => t.tone !== 'good').map(([k, t]) => `<option value="${k}">${esc(t.name)}</option>`).join('')}</select><span class="xs muted" data-theme-hint></span></div>
        <div class="rc-field"><span class="lbl">Points it covers <span class="muted" data-pick-count></span></span>
          <div class="rc-picks">${seeded.length ? `<div class="overline">Selected</div>${seeded.map(pickRow).join('')}` : ''}${group('Can be improved, no action item yet', 'improve')}${group('Went badly, no action item yet', 'bad')}${group('Not in the plan, no action item yet', 'unplanned')}</div>
          <span class="xs muted">Quoted on the action item with a link back. Each one then counts as decided.</span></div>
      </div>
      <footer><span class="xs muted" data-only="new">Starts in New with no sprint. Pulling it into a sprint is the approval.</span><span class="spacer"></span><button class="btn ghost" type="button" data-close>Cancel</button><button class="btn primary" type="button" data-create>Create action item</button></footer>
    </div>`;
    document.body.append(el);

    const dlg = el.querySelector('.rc-dialog');
    let mode = 'new';
    const close = () => el.remove();
    // The theme follows the commonest among the picked points until someone chooses one. Verdicts judge this one theme.
    let themeChosen = false;
    const commonest = () => {
      const tally = {};
      [...dlg.querySelectorAll('.rc-picks input:checked')].forEach(i => { const t = pointById(i.value).p.theme; tally[t] = (tally[t] ?? 0) + 1; });
      const named = Object.keys(tally).filter(t => t !== 'none');
      return (named.length ? named : Object.keys(tally)).sort((a, b) => tally[b] - tally[a])[0] ?? 'none';
    };
    const setCount = () => {
      dlg.querySelector('[data-pick-count]').textContent = `· ${dlg.querySelectorAll('.rc-picks input:checked').length} selected`;
      if (!themeChosen) dlg.querySelector('#rcTheme').value = commonest();
      dlg.querySelector('[data-theme-hint]').textContent = themeChosen ? 'Verdicts judge this theme.' : 'The commonest among its points. Verdicts judge this theme.';
    };
    setCount();
    dlg.addEventListener('change', e => { if (e.target.id === 'rcTheme') themeChosen = true; setCount(); });
    dlg.querySelector('.seg').addEventListener('click', e => {
      const b = e.target.closest('[data-mode]'); if (!b) return;
      mode = b.dataset.mode;
      dlg.querySelectorAll('[data-mode]').forEach(x => x.setAttribute('aria-pressed', x === b));
      dlg.querySelectorAll('[data-only]').forEach(x => { x.hidden = x.dataset.only !== mode; });
      dlg.querySelector('[data-create]').textContent = mode === 'new' ? 'Create action item' : 'Add points';
    });
    el.addEventListener('click', e => {
      if (e.target === el || e.target.closest('[data-close]')) { close(); return; }
      if (!e.target.closest('[data-create]')) return;
      const chosen = [...dlg.querySelectorAll('.rc-picks input:checked')].map(i => pointById(i.value));
      if (!chosen.length) { toast('Pick at least one point.'); return; }
      let key;
      if (mode === 'new') {
        key = `SHIP-${200 + actions.length}`;
        const theme = dlg.querySelector('#rcTheme').value;
        actions.push({ key, type: 'action', fix: dlg.querySelector('input[name=rcFix]:checked').value, title: dlg.querySelector('#rcTitle').value || chosen[0].p.text, theme, raised: chosen[0].r.sprint ?? '—', state: 'New', owner: null, sprint: null, points: null });
      } else {
        key = dlg.querySelector('#rcExisting').value;
      }
      chosen.forEach(({ p }) => { p.action = key; p.none = undefined; });
      close();
      toast(`${tIcon('action')} ${key} covers ${chosen.length} point${chosen.length === 1 ? '' : 's'}. In New, no sprint.`);
      RC.render();
    });
    return el;
  }

  /* Tooltips: one box, filled from a named entry or the attribute's own text. */
  let tipEl;
  const showTip = target => {
    const name = target.dataset.rcTip;
    const html = RC.tips[name] ?? esc(name);
    if (!tipEl) { tipEl = document.createElement('div'); tipEl.className = 'rc-tip'; tipEl.setAttribute('role', 'tooltip'); document.body.append(tipEl); }
    tipEl.innerHTML = typeof html === 'function' ? html() : html;
    tipEl.hidden = false;
    const box = target.getBoundingClientRect();
    const tb = tipEl.getBoundingClientRect();
    tipEl.style.left = `${Math.max(8, Math.min(box.left, innerWidth - tb.width - 8))}px`;
    tipEl.style.top = `${box.bottom + tb.height + 12 > innerHeight ? box.top - tb.height - 6 : box.bottom + 6}px`;
  };
  document.addEventListener('mouseover', e => { const t = e.target.closest?.('[data-rc-tip]'); if (t) showTip(t); });
  document.addEventListener('mouseout', e => { if (e.target.closest?.('[data-rc-tip]') && tipEl) tipEl.hidden = true; });
  document.addEventListener('focusin', e => { const t = e.target.closest?.('[data-rc-tip]'); if (t) showTip(t); });
  document.addEventListener('focusout', e => { if (e.target.closest?.('[data-rc-tip]') && tipEl) tipEl.hidden = true; });

  // A row anywhere opens what its link opens, including on the work item forms, which don't call wire().
  document.addEventListener('click', e => {
    const row = e.target.closest?.('tr[data-href]');
    if (row && !e.target.closest('a, button')) location.href = row.dataset.href;
  });

  /* One listener for everything on a note, so every screen behaves the same. */
  function wire(root, scope) {
    root.addEventListener('click', e => {
      const t = e.target;
      const action = t.closest('[data-rc-action]');
      if (action) { openActionDialog([action.dataset.rcAction], scope()); return; }

      const why = t.closest('[data-rc-why]');
      if (why) { RC.ui.why = why.dataset.rcWhy; RC.render(); document.querySelector('[data-rc-why-text]')?.focus(); return; }
      if (t.closest('[data-rc-why-cancel]')) { RC.ui.why = null; RC.render(); return; }
      const whySave = t.closest('[data-rc-why-save]');
      if (whySave) {
        const text = whySave.closest('.rc-why').querySelector('textarea').value.trim();
        if (!text) { toast('Say why it is not needed.'); return; }
        pointById(whySave.dataset.rcWhySave).p.none = { reason: text, by: ME };
        RC.ui.why = null; RC.render(); return;
      }
      const undo = t.closest('[data-rc-undo]');
      if (undo) { pointById(undo.dataset.rcUndo).p.none = undefined; RC.render(); return; }

      const add = t.closest('[data-rc-add]');
      if (add) { RC.ui.adding = add.dataset.rcAdd; RC.render(); document.querySelector('[data-rc-add-text]')?.focus(); return; }
      if (t.closest('[data-rc-add-cancel]')) { RC.ui.adding = null; RC.render(); return; }
      const addSave = t.closest('[data-rc-add-save]');
      if (addSave) {
        const box = addSave.closest('[data-rc-editor]');
        const text = box.querySelector('[data-rc-add-text]').value.trim();
        if (!text) { toast('Write the point first.'); return; }
        const cat = addSave.dataset.rcAddSave.split(':').pop();
        const cause = box.querySelector('[data-rc-add-cause]')?.value.trim() || undefined;
        RC.addPoint(cat, text, cause, box.querySelector('[data-rc-add-about]')?.value);
        RC.ui.adding = null; RC.render(); return;
      }

      const theme = t.closest('[data-rc-theme]');
      if (theme) { toast('Opens the theme picker: pick a theme or add one. Moves any point, agent or person.'); return; }
      const more = t.closest('[data-rc-more]');
      if (more) { toast('Archive: a person\'s own points only. An agent\'s points are never edited or archived.'); }
    });
  }

  const RC = {
    FIXES, fixLabel, decidable, isOpen, idOf, pointById, retrosOf, scopeOfFeature, counts, catHead, countCell, countStrip, progress, todo,
    note, column, board, rawLog, workItemRows, openActionDialog, wire,
    ui: { why: null, adding: null },
    render: () => {},
    addPoint: () => {},
    tips: {},
  };

  /* A person's point goes on their own retro for that target, which is made on first use (RetroPointWriter). */
  RC.addPointToItem = (key, cat, text, cause) => {
    let r = retros.find(x => x.subject === key && x.role === 'person' && x.by === ME);
    if (!r) {
      const any = retrosOf(key)[0];
      r = { id: `R-9${retros.length}`, subject: key, type: any?.type ?? 'story', title: any?.title ?? key, sprint: any?.sprint ?? null, at: 'Just now', by: ME, agent: null, role: 'person', points: [] };
      retros.push(r);
    }
    r.points.push({ cat, text, theme: 'none', cause });
  };

  // ?why=<point id> and ?adding=<ctx:cat> open those states, for screenshots.
  const params = new URLSearchParams(location.search);
  if (params.get('why')) RC.ui.why = params.get('why');
  if (params.get('adding')) RC.ui.adding = params.get('adding');

  window.RC = RC;
})();
