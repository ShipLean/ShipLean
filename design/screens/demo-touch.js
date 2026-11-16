/*
 * The sprint Demo tab in the touch shell (SHIP-1626 Q4, for SHIP-889); styles in demo-touch.css. Loaded at the end of
 * sprint-demo.html, after its own script, and re-applied after every render of the tab. At 900px and below:
 *   - the breadcrumb reads SHIP › Sprints › Sprint 23 › Demo (SHIP-1626 K2; shared.js has no entry for this page) (P1)
 *   - SHIP-724's sprint header: picker · state · ⋯ (All sprints moves into the ⋯), dates, the goal
 *   - the sprint tabs on their own row, the current one underlined; no toolbar row, the tab has no other controls
 *   - the three numbers as one row of tiles under the tabs, as on the Sprint backlog (P7)
 *   - the status band stacks: chip, message, the line under it, the BAU item as its own row, the action full width (P2)
 *   - Ready: player, YouTube line or notice, facts in two columns, Chapters, Features covered, Script (P3, P4)
 * Above 900px nothing changes; ?notouch shows the desktop page at any width.
 * Review states (add ?shot for screenshots), on top of the page's own ?state=:
 *   ?state=failed&said     failed after the agent session ended, with what the agent said (SHIP-1709, #147) (P5)
 *   ?at=chapters|script    with ?shot, hides what is above that section (Chrome's --screenshot ignores scrolling)
 *   ?sheet=sprint          the sprint header's ⋯
 */
(() => {
  const q = new URLSearchParams(location.search);
  if (q.has('notouch')) { document.querySelector('link[href="demo-touch.css"]')?.remove(); return; }
  const mq = matchMedia('(max-width: 900px)');
  mq.addEventListener('change', () => location.reload());
  if (!mq.matches) return;

  const root = document.documentElement;
  const $ = s => document.querySelector(s);
  const el = (tag, cls, html) => { const e = document.createElement(tag); if (cls) e.className = cls; if (html != null) e.innerHTML = html; return e; };
  const dots = '<svg viewBox="0 0 16 16" width="16" height="16" aria-hidden="true"><circle cx="3.5" cy="8" r="1.4" fill="currentColor"/><circle cx="8" cy="8" r="1.4" fill="currentColor"/><circle cx="12.5" cy="8" r="1.4" fill="currentColor"/></svg>';
  if (q.has('shot') && ['chapters', 'script'].includes(q.get('at'))) root.dataset.at = q.get('at');

  /* Breadcrumb: <project> › Sprints › <sprint> › <tab> (K2) */
  const crumbs = $('.appbar .crumbs');
  if (crumbs) crumbs.innerHTML = `<span>${crumbs.firstElementChild.textContent}</span><span>›</span><span>Sprints</span><span>›</span><span>Sprint 23</span><span>›</span><b>Demo</b>`;

  /* The sprint header's ⋯ (SHIP-724 D10): the same sheet on every sprint tab */
  const hmore = el('button', 'dm-hmore', dots);
  hmore.type = 'button'; hmore.setAttribute('aria-label', 'Sprint actions'); hmore.setAttribute('aria-haspopup', 'dialog');
  $('.sprint-head .row').append(hmore);

  /* The numbers move under the tabs (P7) */
  const kpiBox = el('div', 'dm-kpis');
  kpiBox.append($('.sprint-head .kpis'));
  $('.toolbar').after(kpiBox);

  const sheet = el('div', 'dm-sheet');
  sheet.hidden = true;
  sheet.setAttribute('role', 'dialog'); sheet.setAttribute('aria-modal', 'true');
  sheet.innerHTML = '<header class="sheet-head"><b></b><button class="btn ghost sheet-x" type="button">Close</button></header><div class="sb"></div>';
  document.body.append(sheet);
  function openSprint() {
    const open = $('#sprintChip').textContent !== 'Ended';
    sheet.querySelector('.sheet-head b').textContent = 'Sprint 23';
    const ic = d => `<span class="g"><svg viewBox="0 0 16 16" aria-hidden="true"><path d="${d}" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/></svg></span>`;
    const flag = 'M3.5 14V2.5M3.5 3h8l-1.6 3 1.6 3h-8', edit = 'M10.5 2.5 13.5 5.5 6 13H3v-3z', list = 'M5.5 4h8M5.5 8h8M5.5 12h8M2.5 4h.01M2.5 8h.01M2.5 12h.01', check = 'm3.5 8.5 3 3 6-7';
    // The same sheet as the Sprint backlog's ⋯ (sprint-backlog-more): Close sprint only while the sprint is open.
    sheet.querySelector('.sb').innerHTML = `<ul>
      <li><button class="srow" type="button">${ic(flag)}<span class="tx">Edit goal</span></button></li>
      <li><button class="srow" type="button">${ic(edit)}<span class="tx"><span>Edit sprint</span><small>Name, dates and goal</small></span></button></li>
      <li><a class="srow" href="sprints.html">${ic(list)}<span class="tx">All sprints</span><span class="r">›</span></a></li>
      ${open ? `<li><hr></li><li><a class="srow" href="sprint-end.html">${ic(check)}<span class="tx"><span>Close sprint</span><small>Choose where unfinished work goes</small></span></a></li>` : ''}</ul>`;
    sheet.hidden = false; root.dataset.sheet = 'demo';
  }
  function closeSheet() { sheet.hidden = true; if (root.dataset.sheet === 'demo') delete root.dataset.sheet; }
  hmore.addEventListener('click', openSprint);
  sheet.querySelector('.sheet-x').addEventListener('click', closeSheet);
  $('.scrim')?.addEventListener('click', () => { if (!sheet.hidden) closeSheet(); });
  document.addEventListener('keydown', e => { if (e.key === 'Escape' && !sheet.hidden) closeSheet(); });

  /* After every render: the BAU item onto its own row; Failed after a session ended gets what the agent said (P5).
     The words are the server's (SprintDemoStatusRule): a session that failed, was canceled or went stale. */
  const page = $('#page');
  function apply() {
    const band = page.querySelector('.band');
    if (!band || band.dataset.touch) return;
    band.dataset.touch = '1';
    const link = band.querySelector('[data-testid="demo-item-link"]');
    if (link) {
      const sub = link.parentElement;
      link.classList.add('dm-item');
      band.querySelector('.msg').after(link);
      sub.textContent = sub.textContent.replace(/\s*·\s*$/, '').trim();
    }
    if (q.has('said') && band.classList.contains('danger')) {
      band.querySelector('[data-testid="demo-status-message"]').textContent = 'The agent session ended as failed before a video came back.';
      const said = el('blockquote', 'dm-said', '<span>It said</span>');
      said.append('Playwright could not reach http://localhost:5173: the seeded copy of Storefront did not start within 5 minutes, so there was nothing to record. I stopped before making slides.');
      band.querySelector('.msg').after(said);
    }
  }
  apply();
  new MutationObserver(apply).observe(page, { childList: true });

  if (q.get('sheet') === 'sprint') openSprint();
})();
