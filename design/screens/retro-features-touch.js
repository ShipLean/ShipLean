/*
 * Retrospectives › Features in the touch shell (SHIP-728); styles in retro-features-touch.css. Load at the end of
 * retros-features.html, after its own script. From 600 to 900px the page is the desktop table at touch sizes, all in
 * CSS (D5). Under 600px it:
 *   - puts the Show filter and a search button on one toolbar row; the search button turns the row into the search
 *     field with Cancel beside it (Cancel clears the search and closes the field)
 *   - after every render of the table, builds one list from its rows: key, state and last retro; the title and the
 *     "No Feature retro yet" chip; the four category counts, what is left to decide and the Decided bar; the retros
 *     and action items. A legend names the four colours once, in the list's head, with the count of Features.
 * The table, the filter and the search are still the page's own code. Above 900px nothing changes; ?notouch shows the
 * desktop page at any width.
 * Review states: ?show=open|waiting (the page's own), ?q=<text> (search open with that text). The options Joel didn't
 * choose on 7 Nov stay viewable: ?alt=cards|table (D1 b, c), ?alt=filter (+ ?sheet=filter) and ?alt=tworow (D2 b, c),
 * ?alt=nometa (D3 b). Several alts: ?alt=cards+filter.
 */
(() => {
  const q = new URLSearchParams(location.search);
  if (q.has('notouch')) { document.querySelector('link[href="retro-features-touch.css"]')?.remove(); return; }
  const mq = matchMedia('(max-width: 599.98px)');
  mq.addEventListener('change', () => location.reload());
  if (!mq.matches) return;

  const root = document.documentElement;
  const alt = (q.get('alt') ?? '').split(/[ +,]/).filter(Boolean);
  if (alt.length) root.dataset.alt = alt.join(' ');
  const $ = s => document.querySelector(s);
  const el = (tag, cls, html) => { const e = document.createElement(tag); if (cls) e.className = cls; if (html != null) e.innerHTML = html; return e; };
  const searchIcon = '<svg viewBox="0 0 16 16" width="16" height="16" aria-hidden="true"><circle cx="7" cy="7" r="4.5" fill="none" stroke="currentColor" stroke-width="1.6"/><path d="M10.5 10.5 14 14" stroke="currentColor" stroke-width="1.6" stroke-linecap="round"/></svg>';

  const toolbar = $('.toolbar'), seg = $('[aria-label="Show"]'), field = $('#q'), count = $('#count'), rowsEl = $('#rows');
  const page = $('.rc-page'), box = page.querySelector('.rc-box');
  const SHOW = { all: 'All', open: 'To decide', waiting: 'No Feature retro yet' };
  const shown = () => seg.querySelector('[aria-pressed="true"]')?.dataset.show ?? 'all';

  /* Toolbar row. Recommended: Show · search button. D2 b: search field · Filter (n). D2 c: Show, then the field. */
  const bar = el('div', 'rf-bar');
  const searchBtn = el('button', 'btn rf-find', `${searchIcon}Search`);
  searchBtn.type = 'button'; searchBtn.setAttribute('aria-label', 'Search Features');
  const cancel = el('button', 'btn rf-cancel', 'Cancel'); cancel.type = 'button';
  const filterBtn = el('button', 'btn rt-filter', 'Filter'); filterBtn.type = 'button'; filterBtn.setAttribute('aria-haspopup', 'dialog');
  field.style.width = ''; field.placeholder = 'Search Features';
  if (alt.includes('filter')) { bar.append(field, filterBtn); bar.dataset.mode = 'search'; field.after(el('span', 'spacer')); }
  else bar.append(seg, el('span', 'spacer'), searchBtn, field, cancel);
  toolbar.append(bar);

  function openSearch(on) {
    if (alt.includes('filter') || alt.includes('tworow')) return;
    if (on) { bar.dataset.mode = 'search'; field.focus(); }
    else { delete bar.dataset.mode; if (field.value) { field.value = ''; field.dispatchEvent(new Event('input')); } }
  }
  searchBtn.addEventListener('click', () => openSearch(true));
  cancel.addEventListener('click', () => openSearch(false));
  if (q.get('q')) { field.value = q.get('q'); field.dispatchEvent(new Event('input')); if (!alt.includes('filter') && !alt.includes('tworow')) { bar.dataset.mode = 'search'; } }

  /* D2 b: the Filter sheet. Picking a value applies it and closes (SHIP-805 C4 recommendation). */
  const sheet = el('div', 'rf-sheet');
  sheet.hidden = true; sheet.setAttribute('role', 'dialog'); sheet.setAttribute('aria-modal', 'true');
  sheet.innerHTML = '<header class="sheet-head"><b>Filter</b><button class="btn ghost" type="button" data-x>Close</button></header><div class="sb"></div>';
  document.body.append(sheet);
  const sb = sheet.querySelector('.sb');
  const tally = { all: 0, open: 0, waiting: 0 };
  function openFilter() {
    sb.innerHTML = '<div class="grp">Show</div>' + Object.entries(SHOW).map(([k, l]) => `<button class="srow" type="button" role="menuitemradio" data-pick="${k}" aria-checked="${k === shown()}"><span class="ck">${k === shown() ? '✓' : ''}</span>${l}<span class="sub">${tally[k]}</span></button>`).join('')
      + '<p class="hint">To decide keeps the Features with a point no one has decided. No Feature retro yet keeps those whose own retro isn’t logged.</p>';
    sheet.hidden = false; root.dataset.sheet = 'rf';
  }
  function closeSheet() { sheet.hidden = true; if (root.dataset.sheet === 'rf') delete root.dataset.sheet; }
  sheet.addEventListener('click', e => {
    if (e.target.closest('[data-x]')) closeSheet();
    const p = e.target.closest('[data-pick]');
    if (p) { seg.querySelector(`[data-show="${p.dataset.pick}"]`).click(); closeSheet(); }
  });
  document.querySelector('.scrim')?.addEventListener('click', closeSheet);
  filterBtn.addEventListener('click', openFilter);

  /* How many rows each Show value keeps (for the sheet), counted once from the page's own filter. */
  const firstShow = shown();
  for (const k of Object.keys(SHOW)) { seg.querySelector(`[data-show="${k}"]`).click(); tally[k] = rowsEl.querySelectorAll('tr[data-href]').length; }
  seg.querySelector(`[data-show="${firstShow}"]`).click();

  /* The list, rebuilt from the table after every render. */
  const list = el('div', 'rf-list');
  list.setAttribute('role', 'list');
  box.after(list);
  const cellHtml = (tr, i) => tr.children[i]?.innerHTML ?? '';
  function build() {
    const s = shown();
    const n = s !== 'all';
    filterBtn.innerHTML = n ? 'Filter <span class="n">1</span>' : 'Filter';
    if (n) filterBtn.dataset.n = 1; else delete filterBtn.dataset.n;
    if (field.value) searchBtn.dataset.q = ''; else delete searchBtn.dataset.q;

    const parts = [`<div class="rf-head"><span class="count">${count.textContent}</span><span class="rf-legend">${RETRO.catOrder.map(RC.catHead).join('')}</span></div>`];
    let any = false;
    for (const tr of rowsEl.children) {
      if (tr.classList.contains('group')) { parts.push(`<div class="rf-grp">${tr.textContent}</div>`); continue; }
      if (!tr.dataset.href) continue;
      any = true;
      const t = tr.children[0];
      const icon = t.querySelector('.ti').outerHTML, key = t.querySelector('.key').textContent, title = t.querySelector('a').textContent;
      const chip = t.querySelector('.chip') ? '<span class="chip">No Feature retro yet</span>' : '';
      const nums = [3, 4, 5, 6].map(i => cellHtml(tr, i)).join('');
      const todo = tr.children[7].querySelector('.rc-todo') ? `<span class="rc-todo">${tr.children[7].textContent} to decide</span>` : '';
      const retros = +tr.children[2].textContent, acts = +tr.children[9].textContent || 0;
      parts.push(`<a class="rf-row" role="listitem" href="${tr.dataset.href}">
        <span class="rf-id">${icon}<span class="key">${key}</span>${cellHtml(tr, 1)}</span>
        <span class="rf-when">${tr.children[10].textContent}</span>
        <span class="rf-title"><b>${SL.esc(title)}</b>${chip}</span>
        <span class="rf-nums"><span class="rc-counts">${nums}</span><span class="rf-right">${todo}${cellHtml(tr, 8)}</span></span>
        <span class="rf-meta">${retros} retro${retros === 1 ? '' : 's'} · ${acts} action item${acts === 1 ? '' : 's'}</span></a>`);
    }
    if (!any) parts.push(field.value
      ? `<div class="rf-empty">No Feature matches “${SL.esc(field.value)}”.<br><button class="btn" type="button" data-clear>Clear search</button></div>`
      : `<div class="rf-empty">${s === 'open' ? 'Nothing left to decide.' : 'Nothing here.'}</div>`);
    list.innerHTML = parts.join('');
    obs.takeRecords(); // our own changes are not a render
  }
  list.addEventListener('click', e => { if (e.target.closest('[data-clear]')) { field.value = ''; field.dispatchEvent(new Event('input')); } });
  const obs = new MutationObserver(build);
  obs.observe(rowsEl, { childList: true });
  build();

  if (q.get('sheet') === 'filter') openFilter();
})();
