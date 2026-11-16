/*
 * The sprint Demo tab in the touch shell (SHIP-1626 Q4, for SHIP-889); styles in demo-touch.css. Loaded at the end of
 * sprint-demo.html, after its own script, and re-applied after every render of the tab. At 900px and below:
 *   - the breadcrumb reads SHIP › Sprints › Sprint 23 › Demo (SHIP-1626 K2; shared.js has no entry for this page) (P1)
 *   - SHIP-724's sprint header: picker · state · ⋯ (All sprints moves into the ⋯), dates, the goal, three number tiles
 *   - the sprint tabs on their own row; no toolbar row, the tab has no other controls
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

  const sheet = el('div', 'dm-sheet');
  sheet.hidden = true;
  sheet.setAttribute('role', 'dialog'); sheet.setAttribute('aria-modal', 'true');
  sheet.innerHTML = '<header class="sheet-head"><b></b><button class="btn ghost sheet-x" type="button">Close</button></header><div class="sb"></div>';
  document.body.append(sheet);
  function openSprint() {
    const open = $('#sprintChip').textContent !== 'Ended';
    sheet.querySelector('.sheet-head b').textContent = 'Sprint 23';
    sheet.querySelector('.sb').innerHTML = `
      <button class="srow" type="button">Edit goal</button>
      <button class="srow" type="button">Edit sprint<span class="sub">name, dates, goal</span></button>
      <a class="srow" href="sprints.html">All sprints<span class="sub">›</span></a>
      ${open ? '<button class="srow" type="button">Close sprint</button>' : ''}
      <p class="hint">The sprint header's ⋯, the same on every sprint tab (SHIP-724). Close sprint shows only while the sprint is open.</p>`;
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
