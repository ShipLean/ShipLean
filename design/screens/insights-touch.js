/*
 * Retrospective insights in the touch shell (SHIP-731); styles in insights-touch.css. Load at the end of
 * retro-insights.html, after its own script, which exposes its state and rules as window.INS. At 900px and below,
 * after every render of the page it:
 *   - puts the three views on one row of equal tabs (Themes | Action items | Impact), then Range ▾ and ⋯ on one row;
 *     Range opens a sheet with its two groups; ⋯ holds Merge themes…, Give unthemed points themes and What the
 *     statuses mean
 *   - draws Recurring themes as one card per theme: name and status, a heat strip of mentions per sprint or week with
 *     the total, then its action items and the last change. Tapping a card opens everything about the theme in a sheet
 *     (the counts with their labels, what the status means, action items, the last change, the latest mentions, Rename)
 *   - draws Action items as one card per item, open ones first, then Done
 *   - shows Changes & impact one card a row, with the explanation in the What these mean sheet; tapping a bar reads it out
 *   - opens Merge themes… and + Action item full screen
 * The rules (status, verdict, recurring) and the data are the page's own. Above 900px nothing changes; ?notouch shows
 * the desktop page at any width.
 * Review states: ?sheet=range|more|defs|defs:<status or verdict>|theme:<theme>|rename:<theme>, ?merge (Merge themes),
 * ?dialog=<theme> (New action item on that theme), ?alt=table|list (the D1 alternatives), plus the page's own
 * ?view=themes|actions|impact and ?range=6|3|all|w6|w12.
 */
(() => {
  const q = new URLSearchParams(location.search);
  if (q.has('notouch')) { document.querySelector('link[href="insights-touch.css"]')?.remove(); return; }
  const mq = matchMedia('(max-width: 900px)');
  mq.addEventListener('change', () => location.reload());
  if (!mq.matches) return;

  const { esc, tIcon, av, people, toast } = SL;
  const { themes, actions, retros, noSprint, unthemed, verdict, recurring, getSeries, catLabel } = RETRO;
  const root = document.documentElement;
  const $ = s => document.querySelector(s);
  const el = (tag, cls, html) => { const e = document.createElement(tag); if (cls) e.className = cls; if (html != null) e.innerHTML = html; return e; };
  const dots = '<svg viewBox="0 0 16 16" width="16" height="16" aria-hidden="true"><circle cx="3.5" cy="8" r="1.4" fill="currentColor"/><circle cx="8" cy="8" r="1.4" fill="currentColor"/><circle cx="12.5" cy="8" r="1.4" fill="currentColor"/></svg>';
  const alt = q.get('alt') ?? '';
  if (alt) root.dataset.alt = alt;

  const RANGES = [['By sprint', [['6', 'Last 6 sprints'], ['3', 'Last 3 sprints'], ['all', 'All sprints']]], ['By week', [['w6', 'Last 6 weeks'], ['w12', 'Last 12 weeks']]]];
  const rangeLabel = r => RANGES.flatMap(([, rs]) => rs).find(([v]) => v === r)[1];
  const VIEWS = [['themes', 'Themes'], ['actions', 'Action items'], ['impact', 'Impact']];

  /* The toolbar: three tabs, then Range ▾ · ⋯ */
  const toolbar = $('.toolbar');
  const tabs = el('div', 'it-tabs');
  tabs.setAttribute('role', 'tablist'); tabs.setAttribute('aria-label', 'Insight');
  tabs.innerHTML = VIEWS.map(([v, l]) => `<button type="button" role="tab" data-it-view="${v}">${l}</button>`).join('');
  const bar = el('div', 'it-bar');
  bar.innerHTML = `<button class="btn it-range" type="button" aria-haspopup="dialog"></button><span class="spacer"></span><button class="btn it-more" type="button" aria-label="More" aria-haspopup="dialog">${dots}</button>`;
  toolbar.append(tabs, bar);
  tabs.addEventListener('click', e => { const b = e.target.closest('[data-it-view]'); if (b) INS.setView(b.dataset.itView); });
  bar.querySelector('.it-range').addEventListener('click', openRange);
  bar.querySelector('.it-more').addEventListener('click', openMore);

  /* One bottom sheet (SHIP-805 C4 recommendation: grab bar, title, Close on the right) */
  const sheet = el('div', 'it-sheet');
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
    sheet.hidden = false; root.dataset.sheet = 'insights';
  }
  function closeSheet() {
    if (sheet.hidden) return;
    sheet.hidden = true; delete sheet.dataset.kind; delete sheet.dataset.theme;
    if (root.dataset.sheet === 'insights') delete root.dataset.sheet;
  }
  sheet.querySelector('.sheet-x').addEventListener('click', closeSheet);
  $('.scrim')?.addEventListener('click', () => { if (!sheet.hidden) closeSheet(); });
  document.addEventListener('keydown', e => { if (e.key === 'Escape') { if (!full.hidden) closeFull(); else closeSheet(); } });

  /* Small pieces */
  const series = () => getSeries();
  const names = () => series().names.slice(-INS.shownCount());
  const counts = k => (k === 'none' ? (INS.weekMode() ? unthemed.weeks : unthemed.trend) : series().counts[k]).slice(-INS.shownCount());
  const noSprintOf = k => (INS.weekMode() ? 0 : k === 'none' ? unthemed.noSprint : noSprint[k] ?? 0);
  const total = k => counts(k).reduce((a, v) => a + v, 0) + noSprintOf(k);
  const badThemes = () => Object.keys(themes).filter(k => themes[k].tone === 'bad' && series().counts[k])
    .sort((a, b) => recurring(b) - recurring(a) || series().counts[b].slice(-3).reduce((x, y) => x + y) - series().counts[a].slice(-3).reduce((x, y) => x + y));
  const maxCount = () => Math.max(...badThemes().flatMap(counts), 1);
  const heatBg = (n, max) => (n ? `background:color-mix(in srgb, var(--color-warn) ${Math.round(12 + (n / max) * 48)}%, transparent)` : '');
  const chip = s => `<span class="chip ${s.tone}">${s.label}</span>`;
  const actChip = a => `<span class="act-chip">${tIcon(a.type)}<span class="key">${a.key}</span>${SL.st(a.state)}</span>`;
  const short = n => n.replace(/^(\d+) (\w{3})$/, '$1<br>$2');
  const pointsOf = k => retros.flatMap(r => r.points.filter(p => p.theme === k).map(p => ({ r, p })));
  const openPointIds = k => retros.flatMap(r => r.points.filter(p => p.theme === k && RC.isOpen(p)).map(p => RC.idOf(r, p)));
  const fixLabel = f => (RC.fixLabel ? RC.fixLabel(f) : f);
  const statusText = s => INS.STATUS_TEXT[s.id]();

  function strip(k, max, cls = '') {
    return `<div class="it-strip${cls}" style="--n:${counts(k).length}">${counts(k).map(n => `<span class="${n ? '' : 'zero'}" style="${heatBg(n, max)}">${n}</span>`).join('')}</div>`;
  }

  /* Recurring themes: one card per theme */
  function themesView() {
    const n = names(), max = maxCount(), week = INS.weekMode();
    const bad = badThemes(), recurringCount = bad.filter(recurring).length;
    const callout = $('#ins > .callout.warn');
    const proposal = `<section class="it-merge" aria-label="Proposed merges"><b>1 merge proposed by Decide what to do</b>
      <div class="it-pair"><span class="chip">CI screenshots</span><span class="to">into</span><span class="chip">Screenshot baselines need CI</span></div>
      <span class="muted xs">2 points, the same fix · Claude Code · conductor</span>
      <div class="it-two"><button class="btn" type="button" data-merge="keep">Keep apart</button><button class="btn primary" type="button" data-merge="yes">Merge</button></div></section>`;
    const card = k => {
      const s = INS.statusOf(k), v = verdict(k), acts = actions.filter(a => a.theme === k);
      return `<article class="it-theme" data-it-theme="${k}" role="button" tabindex="0" aria-haspopup="dialog">
        <header><b>${esc(themes[k].name)}</b>${chip(s)}</header>
        <div class="it-row">${strip(k, max)}<span class="it-total"><b>${total(k)}</b>${noSprintOf(k) ? `<small>${noSprintOf(k)} no sprint</small>` : ''}</span></div>
        <footer>${acts.length ? acts.map(actChip).join('') : `<button class="it-mini" type="button" data-make="${k}">+ Action item</button>`}
          <span class="spacer"></span>${v ? `<span class="it-last"><span class="muted xs">Last change</span><span class="chip ${v.tone}">${v.label}</span></span>` : ''}</footer>
      </article>`;
    };
    const un = `<article class="it-theme it-unthemed" data-it-theme="none" role="button" tabindex="0" aria-haspopup="dialog">
        <header><i>Unthemed</i><span class="muted xs">not judged</span></header>
        <div class="it-row">${strip('none', max)}<span class="it-total"><b>${total('none')}</b>${noSprintOf('none') ? `<small>${noSprintOf('none')} no sprint</small>` : ''}</span></div>
        <footer><a class="it-mini solid" href="retros.html?theme=none">Give them themes ›</a></footer></article>`;
    const good = Object.keys(themes).filter(k => themes[k].tone === 'good');
    return `${callout ? callout.outerHTML : ''}${proposal}
      <section class="it-sec"><header><h2>Went badly and not in the plan</h2><button class="it-link" type="button" data-defs>What these mean</button></header>
        <p class="muted xs it-sub">By theme, ${esc(n[0])} – ${esc(n[n.length - 1])}${week ? ', by the day each point was logged' : ''}. ${recurringCount} recurring.</p>
        <div class="it-axis" style="--n:${n.length}"><span>${esc(n[0])}</span><span>${esc(n[n.length - 1])}</span><span>Total</span></div>
        <div class="it-themes">${bad.map(card).join('')}${un}</div>
      </section>
      <section class="it-sec"><header><h2>What keeps working</h2></header><p class="muted xs it-sub">Went well, to keep in the planning guide.</p>
        <div class="it-good">${good.map(k => { const m = INS.mentions(k); return `<div class="it-good-row"><b>${esc(themes[k].name)}</b><span class="n">${m.length}</span><span class="q">“${esc(m[0]?.text || '')}”</span></div>`; }).join('')}</div>
      </section>`;
  }

  /* D1 (c): a short list; everything else in the theme sheet */
  function themesList() {
    const bad = badThemes();
    return `${$('#ins > .callout.warn')?.outerHTML ?? ''}<section class="it-sec"><header><h2>Went badly and not in the plan</h2></header>
      <div class="it-list">${bad.map(k => `<button type="button" class="it-li" data-it-theme="${k}"><b>${esc(themes[k].name)}</b>${chip(INS.statusOf(k))}<span class="n">${total(k)}</span><span class="chev">›</span></button>`).join('')}
      <button type="button" class="it-li" data-it-theme="none"><i>Unthemed</i><span class="muted xs">not judged</span><span class="n">${total('none')}</span><span class="chev">›</span></button></div></section>`;
  }

  /* Action items: one card each, open first */
  function actionsView() {
    const open = actions.filter(a => a.state !== 'Closed'), done = actions.filter(a => a.state === 'Closed');
    const card = a => {
      const v = a.state === 'Closed' ? verdict(a.theme) : null;
      const covers = retros.flatMap(r => r.points).filter(p => p.action === a.key).length;
      return `<article class="it-act" data-open-item="${a.key}">
        <header>${tIcon(a.type)}<span class="key">${a.key}</span>${SL.st(a.state)}<span class="spacer"></span><span class="pts">${a.points} pts</span></header>
        <b class="t">${esc(a.title)}</b>
        <div class="it-meta"><span># ${esc(themes[a.theme].name)}</span>${recurring(a.theme) ? '<span class="chip warn">↻ Recurring</span>' : ''}</div>
        <div class="it-meta muted">${a.fix ? `Fix: ${fixLabel(a.fix)} · ` : ''}covers ${covers} point${covers === 1 ? '' : 's'} · raised ${a.raised}</div>
        <footer>${a.sprint ? `<span class="sm">${a.sprint}</span>` : `<button class="it-mini solid" type="button" data-pull="${a.key}">Pull into Sprint 25</button>`}
          <span class="it-owner">${av(a.owner)}<span class="sm">${a.owner ? people[a.owner].name.split(' ')[0] : '<span class="muted">Unassigned</span>'}</span></span>
          <span class="spacer"></span>${v ? `<span class="chip ${v.tone}">${v.label}</span>` : a.state === 'Closed' ? '' : '<span class="muted xs">impact when done</span>'}</footer>
      </article>`;
    };
    return `<p class="muted sm it-intro">${tIcon('action')} An <b>action item</b> is a work item like a story: it's pulled into a sprint and links to the theme it addresses.</p>
      <div class="it-acthead"><b>${open.length} open · ${done.length} done</b><span class="spacer"></span><button class="btn" type="button" data-make="">+ Action item</button></div>
      <div class="it-acts">${open.map(card).join('')}</div>
      <h3 class="it-grp">Done</h3><div class="it-acts">${done.map(card).join('')}</div>`;
  }

  /* After every render of the page */
  function after() {
    tabs.querySelectorAll('[data-it-view]').forEach(b => b.setAttribute('aria-selected', b.dataset.itView === INS.view));
    bar.querySelector('.it-range').innerHTML = `${rangeLabel(INS.range)} <span class="caret">▾</span>`;
    const ins = $('#ins');
    ins.dataset.view = INS.view;
    if (INS.view === 'themes' && alt !== 'table') ins.innerHTML = alt === 'list' ? themesList() : themesView();
    if (INS.view === 'actions') ins.innerHTML = actionsView();
    if (INS.view === 'impact') {
      const intro = ins.querySelector(':scope > p');
      intro.outerHTML = `<p class="muted sm it-intro">How often each closed action item's theme came up before and after it shipped. Tap a bar to read it. <button class="it-link" type="button" data-defs="verdicts">How verdicts work</button></p>`;
      ins.querySelectorAll('.impact').forEach(c => c.querySelector('svg').after(el('div', 'it-read muted xs', 'Tap a bar to read it')));
      ins.querySelectorAll('.impact [data-rc-tip]').forEach(c => c.removeAttribute('data-rc-tip'));
    }
    ins.querySelectorAll('[data-rc-tip]').forEach(c => { c.dataset.itDefs = c.dataset.rcTip; c.removeAttribute('data-rc-tip'); });
  }
  INS.after = after;

  /* Sheets */
  function openRange() {
    openSheet('range', 'Range', RANGES.map(([g, rs]) => `<div class="grp">${g}</div>${rs.map(([v, l]) => `<button class="srow" type="button" role="menuitemradio" aria-checked="${v === INS.range}" data-range="${v}"><span class="ck">${v === INS.range ? '✓' : ''}</span>${l}</button>`).join('')}`).join('')
      + '<p class="hint">By week counts each point in the week it was logged, so retros with no sprint land in a week too. Recurring and the verdicts follow the range you pick.</p>');
  }
  function openMore() {
    openSheet('more', 'Insights', `
      <button class="srow" type="button" data-go="merge">Merge themes…<span class="sub">fold one theme into another</span></button>
      <a class="srow" href="retros.html?theme=none">Give unthemed points themes<span class="sub">${total('none')} ›</span></a>
      <button class="srow" type="button" data-defs>What the statuses and verdicts mean<span class="sub">›</span></button>
      <div class="grp">Go to</div>
      <a class="srow" href="retros.html">Search retrospectives<span class="sub">›</span></a>
      <a class="srow" href="retros-features.html">Retrospectives by Feature<span class="sub">›</span></a>`);
  }
  function openDefs(focus) {
    const u = INS.unit();
    const row = (id, c, text) => `<div class="it-def${id === focus ? ' on' : ''}">${c}<span>${text}</span></div>`;
    openSheet('defs', 'What these mean', `
      <div class="grp">Status</div><p class="hint">Worked out from every ${u}, not only the ones shown. No sprint points never count.</p>
      ${row('recurring', chip({ tone: 'warn', label: 'Recurring' }), INS.STATUS_TEXT.recurring())}
      ${row('addressed', chip({ tone: 'accent', label: 'Being addressed' }), INS.STATUS_TEXT.addressed())}
      ${row('resolved', chip({ tone: 'ok', label: 'Resolved' }), INS.STATUS_TEXT.resolved())}
      ${row('quiet', chip({ tone: '', label: 'Quiet' }), INS.STATUS_TEXT.quiet())}
      <div class="grp" id="it-verdicts">Last change</div><p class="hint">What happened to a theme after its last action item closed. Worked out automatically.</p>
      ${row('Helped', chip({ tone: 'ok', label: 'Helped' }), INS.VERDICT_TEXT.Helped())}
      ${row('Some effect', chip({ tone: 'warn', label: 'Some effect' }), INS.VERDICT_TEXT['Some effect']())}
      ${row('No effect', chip({ tone: 'danger', label: 'No effect' }), INS.VERDICT_TEXT['No effect']())}
      ${row('Too early', chip({ tone: '', label: 'Too early' }), INS.VERDICT_TEXT['Too early']())}
      ${INS.weekMode() ? '' : `<div class="grp">No sprint</div><p class="hint">Points on retros with no sprint. They count in Total, but not towards Recurring or a verdict, because they have no time order.</p>`}
      <div class="grp">Unthemed</div><p class="hint">Went badly and Not in the plan points with no theme. Give them one in Search, or run Decide what to do, which assigns themes as it goes.</p>`);
    if (focus === 'verdicts' || ['Helped', 'Some effect', 'No effect', 'Too early'].includes(focus)) requestAnimationFrame(() => { sb.scrollTop = sb.querySelector('#it-verdicts').offsetTop - 8; });
  }

  function openTheme(k, renaming = false) {
    const n = names(), max = Math.max(...counts(k), 1), un = k === 'none';
    const t = themes[k], s = un ? null : INS.statusOf(k), v = un ? null : verdict(k), acts = actions.filter(a => a.theme === k);
    const pts = pointsOf(k).filter(({ p }) => p.cat !== 'well');
    const latest = pts.slice(0, 3);
    const head = renaming
      ? `<label class="it-rename"><span>Theme name</span><input class="field" id="itName" value="${esc(t.name)}" autocomplete="off"></label><p class="hint">Renaming keeps every point, action item and count. Agents see the new name in their next brief.</p>`
      : un ? '<p class="hint">Went badly and Not in the plan points with no theme. They count in Total but are never judged.</p>' : '';
    openSheet('theme', un ? 'Unthemed' : t.name, `${head}
      ${s ? `<div class="it-def on">${chip(s)}<span>${statusText(s)}</span></div>` : ''}
      <div class="grp">Mentions per ${INS.unit()}</div>
      <div class="it-bars" style="--n:${n.length}">${counts(k).map((c, i) => `<div><span class="${c ? '' : 'zero'}" style="${heatBg(c, max)}">${c}</span><small>${short(esc(n[i]))}</small></div>`).join('')}</div>
      <div class="it-sums">${noSprintOf(k) ? `<span><b>${noSprintOf(k)}</b> on retros with no sprint</span>` : ''}<span><b>${total(k)}</b> in all</span></div>
      ${un ? `<a class="btn" href="retros.html?theme=none">Give them themes ›</a>` : `
      <div class="grp">Action items</div>
      ${acts.map(a => `<a class="srow it-arow" href="#" data-open-item="${a.key}">${actChip(a)}<span class="t">${esc(a.title)}</span></a>`).join('') || '<p class="hint">None yet.</p>'}
      <button class="btn it-wide" type="button" data-make="${k}">+ Action item${openPointIds(k).length ? ` on ${openPointIds(k).length} open point${openPointIds(k).length === 1 ? '' : 's'}` : ''}</button>
      <div class="grp">Last change</div>
      ${v ? `<div class="it-def on">${chip(v)}<span>${INS.VERDICT_TEXT[v.label]()} ${v.after === null ? '' : `${v.before.toFixed(1)} before, ${v.after.toFixed(1)} after.`}</span></div><button class="srow" type="button" data-impact="${k}">See it on Impact<span class="sub">›</span></button>` : '<p class="hint">No change shipped yet.</p>'}`}
      <div class="grp">Latest mentions</div>
      ${latest.map(({ r, p }) => `<div class="it-quote"><span class="catdot ${p.cat}">${catLabel[p.cat]}</span><span>${esc(p.text)}</span><small class="muted">${esc(r.subject)} · ${esc(r.sprint ?? 'no sprint')}</small></div>`).join('')}
      <a class="srow" href="retros.html?theme=${k}">All ${pts.length} in Search<span class="sub">›</span></a>
      ${un || renaming ? '' : `<div class="grp">Theme</div>
      <button class="srow" type="button" data-rename="${k}">Rename…</button>
      <button class="srow" type="button" data-merge-from="${k}">Merge into another theme…</button>`}`,
    renaming ? '<button class="btn" type="button" data-discard>Discard</button><button class="btn primary" type="button" data-save>Save</button>' : '');
    sheet.dataset.theme = k;
  }

  sheet.addEventListener('click', e => {
    const r = e.target.closest('[data-range]');
    if (r) { closeSheet(); INS.setRange(r.dataset.range); return; }
    const mf = e.target.closest('[data-merge-from]');
    if (mf) { closeSheet(); openMerge([mf.dataset.mergeFrom]); return; }
    const go = e.target.closest('[data-go]');
    if (go) { closeSheet(); openMerge(); return; }
    const d = e.target.closest('[data-defs]');
    if (d) { openDefs(d.dataset.defs); return; }
    const rn = e.target.closest('[data-rename]');
    if (rn) { openTheme(rn.dataset.rename, true); return; }
    if (e.target.closest('[data-discard]')) { openTheme(sheet.dataset.theme); return; }
    if (e.target.closest('[data-save]')) { const k = sheet.dataset.theme; themes[k].name = $('#itName').value.trim() || themes[k].name; closeSheet(); INS.render(); toast(`Renamed to ${esc(themes[k].name)}`); return; }
    const im = e.target.closest('[data-impact]');
    if (im) { closeSheet(); INS.setView('impact'); return; }
  });

  /* Merge themes: full screen (rule 4), ✕ at the top left, Merge at the top right */
  const full = el('div', 'it-full');
  full.hidden = true;
  full.setAttribute('role', 'dialog'); full.setAttribute('aria-modal', 'true'); full.setAttribute('aria-label', 'Merge themes');
  document.body.append(full);
  function openMerge(pre = []) {
    const opts = Object.keys(themes).filter(k => k !== 'none').map(k => [k, pointsOf(k).length]);
    full.innerHTML = `<header><button class="ib" type="button" data-x aria-label="Close">✕</button><h2>Merge themes</h2><button class="btn primary" type="button" data-do-merge disabled>Merge</button></header>
      <div class="body">
        <fieldset><legend>Themes to merge</legend>${opts.map(([k, c]) => `<label class="it-check"><input type="checkbox" value="${k}"${pre.includes(k) ? ' checked' : ''}><span>${esc(themes[k].name)}</span><small>${c}</small></label>`).join('')}</fieldset>
        <label class="it-into"><span>Into</span><select class="field" id="itInto"><option value="">Choose the theme to keep</option>${opts.map(([k]) => `<option value="${k}">${esc(themes[k].name)}</option>`).join('')}</select></label>
        <p class="hint" id="itPreview">Pick the themes to fold in and the one that stays.</p>
      </div>`;
    full.hidden = false; root.dataset.full = '';
    if (pre.length > 1) { full.querySelector('#itInto').value = pre[pre.length - 1]; full.querySelector(`input[value="${pre[pre.length - 1]}"]`).checked = false; }
    preview();
  }
  function preview() {
    const into = full.querySelector('#itInto').value;
    full.querySelectorAll('.it-check input').forEach(i => { i.disabled = i.value === into; if (i.disabled) i.checked = false; });
    const picked = [...full.querySelectorAll('.it-check input:checked')].map(i => i.value);
    const ready = into && picked.length;
    full.querySelector('[data-do-merge]').disabled = !ready;
    const sum = picked.concat(into ? [into] : []).reduce((a, k) => a + pointsOf(k).length, 0);
    full.querySelector('#itPreview').innerHTML = ready ? `${picked.map(k => `<b>${esc(themes[k].name)}</b>`).join(', ')} fold${picked.length === 1 ? 's' : ''} into <b>${esc(themes[into].name)}</b>: ${sum} points and their action items under one theme.` : 'Pick the themes to fold in and the one that stays.';
  }
  function closeFull() { full.hidden = true; delete root.dataset.full; }
  full.addEventListener('change', preview);
  full.addEventListener('click', e => {
    if (e.target.closest('[data-x]')) closeFull();
    if (e.target.closest('[data-do-merge]')) { closeFull(); toast('Themes merged. Their points and action items moved.'); }
  });

  /* Taps on the page */
  $('#ins').addEventListener('click', e => {
    const t = e.target;
    const make = t.closest('[data-make]');
    if (make) { e.stopImmediatePropagation(); makeAction(make.dataset.make); return; }
    if (t.closest('[data-pull], [data-merge], a')) return; // the page's own handlers
    const d = t.closest('[data-defs], [data-it-defs]');
    if (d) { e.stopImmediatePropagation(); openDefs(d.dataset.defs ?? d.dataset.itDefs); return; }
    const th = t.closest('[data-it-theme]');
    if (th) { openTheme(th.dataset.itTheme); return; }
    const item = t.closest('[data-open-item]');
    if (item) { toast(`Opens ${item.dataset.openItem} full screen (SHIP-714)`); return; }
    const bar = t.closest('.impact svg rect');
    if (bar) {
      const card = bar.closest('.impact'), label = bar.querySelector('title').textContent.split(': ');
      card.querySelectorAll('svg rect').forEach(r => r.classList.toggle('it-on', r === bar));
      card.querySelector('.it-read').innerHTML = `<b>${esc(label[0])}</b>: ${label[1]} mention${label[1] === '1' ? '' : 's'}`;
    }
  }, true);
  $('#ins').addEventListener('keydown', e => { if (e.key === 'Enter' && e.target.matches('[data-it-theme]')) openTheme(e.target.dataset.itTheme); });
  sb.addEventListener('click', e => { const m = e.target.closest('[data-make]'); if (m) { closeSheet(); makeAction(m.dataset.make); } const it = e.target.closest('[data-open-item]'); if (it) { e.preventDefault(); toast(`Opens ${it.dataset.openItem} full screen (SHIP-714)`); } });
  function makeAction(k) { RC.openActionDialog(k ? openPointIds(k) : [], retros); }

  /* New action item: full screen, as on the sprint Retro tab (SHIP-727 D5 and D8: the shared dialog). Create moves into
     the header; the footer note goes to the end of the body. */
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

  after();

  const s = q.get('sheet');
  if (s === 'range') openRange();
  else if (s === 'more') openMore();
  else if (s === 'defs') openDefs();
  else if (s?.startsWith('defs:')) openDefs(s.slice(5));
  else if (s?.startsWith('theme:')) openTheme(s.slice(6));
  else if (s?.startsWith('rename:')) openTheme(s.slice(7), true);
  if (q.has('merge')) openMerge(q.get('merge') ? q.get('merge').split(',') : []);
  if (q.get('dialog')) makeAction(q.get('dialog'));
})();
