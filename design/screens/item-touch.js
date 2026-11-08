/*
 * The work item and Feature forms in the touch shell (SHIP-714); styles in item-touch.css. Load after the page's own
 * script. At 900px and below it adds: section chips under the shell's top bar, a wrapping title, the shell's sheet
 * header and scrim on field menus, the Save bar, and "Show all" under History. Above 900px all of it is hidden.
 * Review states: ?at=<section> scrolls to a section (e.g. ?at=Tasks), ?open=<menu> opens a field sheet (state,
 * assignee, parent), ?dirty makes two unsaved edits, ?conflict runs the 409, ?mention starts an @mention, ?peek shows
 * the page as the full-screen item modal (?item=KEY in the app). With ?shot, ?at hides what is above the section instead
 * of scrolling (Chrome's --screenshot ignores scrolling), and ?at=Conflict starts at the 409 banner.
 */
(() => {
  const q = new URLSearchParams(location.search);
  const root = document.documentElement;
  const $ = s => document.querySelector(s);
  const touch = () => matchMedia('(max-width: 900px)').matches;
  const SHORT = { 'Acceptance criteria': 'Criteria', 'Repro steps': 'Repro', Retrospectives: 'Retros', 'Why this exists': 'Why', 'Imported from Jira': 'Jira', 'Decisions and questions': 'Decisions' };
  const svg = d => `<svg viewBox="0 0 16 16" width="16" height="16" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="${d}"/></svg>`;

  /* The item modal: over the shell, with its own bar (Close · the item · Open full page). */
  const ident = $('.wi-title .ident');
  const key = ident?.querySelector('.key')?.textContent || '';
  let topBar = $('.appbar');
  if (q.has('peek')) {
    root.dataset.peek = '';
    const bar = document.createElement('header');
    bar.className = 'pbar';
    bar.innerHTML = `<button class="tbtn" type="button" id="pbClose" aria-label="Close">${svg('m4 4 8 8M12 4l-8 8')}</button>
      <span class="pb-id">${ident?.querySelector('.ti')?.outerHTML || ''}<span class="key">${key}</span><span class="tn">${ident?.querySelector('.sm.muted')?.textContent || ''}</span></span>
      <button class="tbtn" type="button" id="pbOpen" aria-label="Open full page" title="Open full page">${svg('M6 3h7v7M13 3 4 12')}</button>`;
    (topBar || document.body.firstElementChild).before(bar);
    bar.addEventListener('click', e => {
      if (e.target.closest('#pbClose')) SL.toast('Closes the item and goes back to the page under it');
      if (e.target.closest('#pbOpen')) SL.toast(`Opens ${key} as a full page`);
    });
    topBar = bar;
  }

  /* Section chips, sticky under the top bar */
  const jump = document.createElement('nav');
  jump.className = 'jump';
  jump.setAttribute('aria-label', 'Sections');
  if (topBar) topBar.after(jump); else document.body.prepend(jump);
  const sections = () => [...document.querySelectorAll('.goal, #main .sec, #main > section, .wi-side > section')].map(el => {
    const h = el.querySelector(':scope > .sec-h h2, :scope > header.sec-h h2, :scope > .sec-h2 .overline, :scope > .lbl');
    if (!h) return null;
    const label = h.textContent.trim();
    const n = el.querySelector(':scope > .sec-h .cnt, :scope > .sec-h .chip, :scope > header.sec-h .cnt, :scope > header.sec-h .chip, :scope > .sec-h2 .cnt')?.textContent.replace(/[^\d/]/g, '').trim() || '';
    return { el, label, short: SHORT[label] || label, n };
  }).filter((s, i, a) => s && a.findIndex(x => x && x.label === s.label) === i);
  const offset = () => (topBar?.offsetHeight || 0) + jump.offsetHeight;
  const goTo = (label, smooth) => {
    const s = sections().find(x => x.label === label || x.short === label);
    if (s) window.scrollTo({ top: s.el.getBoundingClientRect().top + window.scrollY - offset() + 1, behavior: smooth ? 'smooth' : 'instant' });
  };
  let current = '';
  const renderJump = () => {
    const html = sections().map(s => `<button type="button" data-jump="${s.label}" aria-current="${s.label === current}">${s.short}${s.n ? ` <span class="n">${s.n}</span>` : ''}</button>`).join('');
    if (jump.innerHTML !== html) jump.innerHTML = html;
  };
  const markCurrent = () => {
    const list = sections().filter(s => s.el.getClientRects().length), top = offset() + 8;
    let cur = list[0]?.label || '';
    list.forEach(s => { if (s.el.getBoundingClientRect().top <= top) cur = s.label; });
    if (window.innerHeight + window.scrollY >= document.documentElement.scrollHeight - 2) cur = list[list.length - 1]?.label || cur;
    if (cur !== current) {
      current = cur;
      jump.querySelectorAll('button').forEach(b => b.setAttribute('aria-current', b.dataset.jump === cur));
      jump.querySelector('[aria-current="true"]')?.scrollIntoView({ inline: 'nearest', block: 'nearest' });
    }
  };
  jump.addEventListener('click', e => { const b = e.target.closest('[data-jump]'); if (b) goTo(b.dataset.jump, true); });
  window.addEventListener('scroll', markCurrent, { passive: true });

  /* Title: a textarea that wraps, kept in step with the page's own title input. */
  const titleIn = $('#title');
  const ta = document.createElement('textarea');
  ta.className = 'title-ta';
  ta.rows = 1;
  ta.setAttribute('aria-label', 'Title');
  titleIn?.after(ta);
  const fit = () => { ta.style.height = 'auto'; ta.style.height = ta.scrollHeight + 'px'; };
  ta.addEventListener('input', () => { titleIn.value = ta.value; titleIn.dispatchEvent(new Event('input', { bubbles: true })); fit(); });
  ta.addEventListener('keydown', e => { if (e.key === 'Enter') { e.preventDefault(); ta.blur(); } });
  const syncTitle = () => { if (titleIn && document.activeElement !== ta && ta.value !== titleIn.value) { ta.value = titleIn.value; fit(); } };

  /* Save bar: mirrors the header's save state; Discard and Save press the header's buttons. */
  const saveState = $('#saveState');
  const sb = document.createElement('div');
  sb.className = 'savebar';
  sb.hidden = true;
  sb.setAttribute('role', 'region');
  sb.setAttribute('aria-label', 'Unsaved changes');
  sb.innerHTML = '<span class="what"><span class="dot"></span><span id="sbWhat"></span></span><button class="btn" type="button" id="sbDiscard">Discard</button><button class="btn primary" type="button" id="sbSave">Save</button>';
  document.body.append(sb);
  sb.addEventListener('click', e => { if (e.target.closest('#sbSave')) $('#save').click(); if (e.target.closest('#sbDiscard')) $('#discard').click(); });
  const syncSave = () => {
    const dirty = !!saveState?.classList.contains('dirty');
    sb.hidden = !dirty;
    document.body.classList.toggle('has-savebar', dirty && touch());
    const what = (saveState?.textContent || '').replace(/^Unsaved:\s*/, '');
    if (dirty && $('#sbWhat').textContent !== what) $('#sbWhat').textContent = what;
  };

  /* Field sheets: the shell's scrim (<html data-sheet="field">) and sheet header with the field's name and Done.
     Tapping the scrim or Done is a click outside the menu, which the page already treats as close. */
  const syncSheets = () => {
    const m = $('.menu:not(.mention-menu)');
    if (m && touch()) root.dataset.sheet = 'field';
    else if (root.dataset.sheet === 'field') delete root.dataset.sheet;
    if (m && !m.querySelector('.sheet-head')) {
      const opener = $('[aria-expanded="true"]');
      const name = opener?.closest('.fcell')?.querySelector('.lbl')?.textContent || opener?.dataset.sheetTitle || 'Choose';
      m.insertAdjacentHTML('afterbegin', `<header class="sheet-head m-only"><b>${name}</b><button class="btn ghost" type="button" data-sheet-done>Done</button></header>`);
      m.querySelector('[data-sheet-done]').addEventListener('click', e => { e.stopPropagation(); document.body.click(); });
    }
  };

  /* History: the newest three revisions, then Show all. */
  let allRevs = false;
  const syncRevs = () => {
    document.querySelectorAll('.revs').forEach(ol => {
      ol.classList.toggle('all', allRevs);
      const n = ol.children.length;
      let more = ol.nextElementSibling?.classList.contains('rev-more') ? ol.nextElementSibling : null;
      if (n <= 3 || allRevs) { more?.remove(); return; }
      if (!more) {
        more = document.createElement('button');
        more.type = 'button';
        more.className = 'rev-more';
        more.addEventListener('click', () => { allRevs = true; syncRevs(); });
        ol.after(more);
      }
      // With SHIP-922's decision and question lines in it, the list isn't only revisions.
      const label = ol.querySelector('.revno.ask') ? `Show all ${n}` : `Show all ${n} revisions`;
      if (more.textContent !== label) more.textContent = label;
    });
  };

  /* A refused save (409): bring the banner into view, since the header is far above. */
  let hadBanner = false;
  const syncBanner = () => {
    const b = $('#banner .conflict-b');
    if (b && !hadBanner && touch() && !shotAt) window.scrollTo({ top: b.getBoundingClientRect().top + window.scrollY - offset() - 8 });
    hadBanner = !!b;
  };

  /* ?shot&at=<section>: the page as if scrolled there, for screenshots.sh. */
  const shotAt = q.has('shot') && q.get('at');
  const hideAbove = () => {
    if (!shotAt || !touch()) return;
    const hide = el => { if (el && el.style.getPropertyValue('display') !== 'none') el.style.setProperty('display', 'none', 'important'); };
    ['.wi-head', '.fields', '.band'].forEach(sel => hide($(sel)));
    if (shotAt === 'Conflict') return;
    hide($('#banner'));
    const list = sections(), i = list.findIndex(x => x.label === shotAt || x.short === shotAt);
    if (i < 0) return;
    const target = list[i].el, inRail = !!target.closest('.wi-side');
    if (inRail) hide($('#main'));
    list.slice(0, i).forEach(x => { if (x.el.closest('.wi-side') || !inRail) hide(x.el); });
  };

  const sync = () => { hideAbove(); syncBanner(); renderJump(); markCurrent(); syncTitle(); syncSave(); syncSheets(); syncRevs(); };
  let queued = false;
  new MutationObserver(() => { if (!queued) { queued = true; requestAnimationFrame(() => { queued = false; sync(); }); } })
    .observe(document.body, { childList: true, subtree: true, attributes: true, attributeFilter: ['class', 'aria-expanded'] });
  setInterval(syncTitle, 400);
  sync();

  /* Review states */
  const press = label => [...document.querySelectorAll('.preview button')].find(b => b.textContent.trim() === label)?.click();
  requestAnimationFrame(() => {
    if (q.has('dirty')) { $('[data-prio="3"]')?.click(); const p = $('#fPoints'); if (p) { p.value = '8'; p.dispatchEvent(new Event('input', { bubbles: true })); } }
    if (q.has('conflict')) press('Teammate saved first');
    if (q.get('open')) { const b = $(`[data-menu="${q.get('open')}"]`) || (q.get('open') === 'state' ? $('#stateBtn') : null); b?.click(); }
    if (q.has('mention')) {
      const c = $('#cmText');
      if (c) { c.value = 'Thanks! @M'; c.focus(); c.selectionStart = c.selectionEnd = c.value.length; c.dispatchEvent(new Event('input', { bubbles: true })); }
    }
    if (q.get('at') && !shotAt) requestAnimationFrame(() => { goTo(q.get('at'), false); markCurrent(); });
  });
})();
