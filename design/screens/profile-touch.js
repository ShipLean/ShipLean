/*
 * Your profile in the touch shell (SHIP-733); styles in profile-touch.css. Load at the end of profile.html, after its
 * own script, which exposes its state as window.PROF and calls PROF.after hooks after each render. At 900px and below:
 *   - one column of panels in the app's order (Profile, Password, Appearance, Your projects, Sessions and saved views,
 *     Connected agents); the top bar names the page, so the heading goes and the one-line intro stays
 *   - labels above full-width 36px fields at 16px; 32px colour targets five to a row
 *   - Save and Discard for the profile move to the shared Save bar, fixed at the bottom while something is unsaved
 *   - Password is one row with Change password until tapped; Cancel closes it and clears the fields
 *   - Your projects and Connected agents are two- and three-line rows with Leave and Revoke always showing; both ask
 *     first in the row
 *   - the Claude Code line wraps, with Copy
 * Above 900px nothing changes; ?notouch shows the desktop page at any width.
 * Review states: ?dirty (name edited) | ?dirty=colour, ?pw (Password open) | ?pw=typed, ?leave=PAY, ?revoke=cx,
 * ?agents=0, ?photo, ?at=password|appearance|projects|sessions|agents (scrolled to that panel), and the decision
 * alternatives ?alt=index|jump|pwopen|leavesheet|scroll|nodensity|heading (combine with +).
 */
(() => {
  const q = new URLSearchParams(location.search);
  if (q.has('notouch')) { document.querySelector('link[href="profile-touch.css"]')?.remove(); return; }
  const mq = matchMedia('(max-width: 900px)');
  mq.addEventListener('change', () => location.reload());
  if (!mq.matches) return;

  const { S, P0, mine, agents, render, after } = PROF;
  const { esc, people, ME } = SL;
  const root = document.documentElement;
  const $ = s => document.querySelector(s);
  const el = (tag, cls, html) => { const e = document.createElement(tag); if (cls) e.className = cls; if (html != null) e.innerHTML = html; return e; };
  const alt = (q.get('alt') ?? '').replace(/\+/g, ' ').trim();
  if (alt) root.dataset.alt = alt;
  const hasAlt = a => alt.split(/\s+/).includes(a);

  /* Review states */
  if (q.has('dirty')) { if (q.get('dirty') === 'colour') S.p.color = '#16a089'; else S.p.name = 'Priya S.'; }
  if (q.has('pw')) { S.pwOpen = true; if (q.get('pw') === 'typed') { S.cur = 'old-password'; S.nw = 'a-longer-one'; S.cf = 'a-longer'; } }

  /* The shared Save bar (SHIP-805 rule 15): message · Discard · Save. It presses the panel's own buttons. */
  const bar = el('div', 'savebar', '<span class="what"><span class="dot"></span><span>Unsaved profile changes</span></span><button class="btn" type="button" data-sb="pDiscard">Discard</button><button class="btn primary" type="button" data-sb="pSave">Save</button>');
  bar.hidden = true;
  bar.setAttribute('role', 'region'); bar.setAttribute('aria-label', 'Unsaved changes');
  document.body.append(bar);
  bar.addEventListener('click', e => { const b = e.target.closest('[data-sb]'); if (b) document.getElementById(b.dataset.sb)?.click(); });

  /* D3 alternative: Leave asks in a bottom sheet */
  const scrim = el('div', 'pf-scrim'); scrim.hidden = true;
  const sheet = el('div', 'pf-sheet'); sheet.hidden = true;
  sheet.setAttribute('role', 'dialog'); sheet.setAttribute('aria-modal', 'true');
  document.body.append(scrim, sheet);
  scrim.addEventListener('click', () => $('[data-leave-cancel]')?.click());
  sheet.addEventListener('click', e => {
    const b = e.target.closest('button'); if (!b) return;
    if (b.dataset.go) $(`[data-leave-go="${b.dataset.go}"]`)?.click(); else $('[data-leave-cancel]')?.click();
  });

  after.push(({ dirty, nameOk }) => {
    bar.hidden = !dirty;
    document.body.classList.toggle('has-savebar', dirty);
    bar.querySelector('.what').className = 'what' + (nameOk ? '' : ' err');
    bar.querySelector('.what span:last-child').textContent = nameOk ? 'Unsaved profile changes' : 'Enter a name to save';
    bar.querySelector('[data-sb="pSave"]').disabled = !nameOk;
    const leaving = hasAlt('leavesheet') && S.leaving && mine.find(m => m.key === S.leaving);
    sheet.hidden = scrim.hidden = !leaving;
    if (leaving) {
      const p = SL.projects.find(x => x.key === leaving.key);
      sheet.innerHTML = `<header class="sheet-head"><b>Leave ${leaving.key}?</b><button class="btn ghost sheet-x" type="button">Close</button></header>
        <p>You lose access to <b>${esc(p.name)}</b> straight away. Items assigned to you stay assigned.</p>
        <button class="btn danger" type="button" data-go="${leaving.key}">Leave ${leaving.key}</button>`;
    }
  });

  /* D1 alternatives: section links under the intro (jump), or an index of sections (index) */
  const SECS = [['profile', 'Profile'], ['password', 'Password'], ['appearance', 'Appearance'], ['projects', 'Your projects'], ['sessions', 'Sessions'], ['agents', 'Connected agents']];
  const goTo = sec => { const t = $(`[data-sec="${sec}"]`); if (t) window.scrollTo(0, t.getBoundingClientRect().top + scrollY - (parseFloat(getComputedStyle(root).getPropertyValue('--mobile-bar-height')) || 48) - 12); };
  if (hasAlt('jump')) {
    const j = el('nav', 'pf-jump', SECS.map(([k, l]) => `<a href="#${k}" data-go="${k}">${l}</a>`).join(''));
    j.setAttribute('aria-label', 'Sections');
    $('.head').after(j);
    j.addEventListener('click', e => { const a = e.target.closest('[data-go]'); if (a) { e.preventDefault(); goTo(a.dataset.go); } });
  }
  if (hasAlt('index')) {
    const me = people[ME];
    const sum = {
      profile: `${esc(me.name)} · ${esc(me.email)}`,
      password: 'Change your password',
      appearance: `${{ os: 'Match my OS', light: 'Light', dark: 'Dark' }[S.theme]} · ${S.density === 'compact' ? 'Compact' : 'Comfortable'}`,
      projects: `${mine.length} projects`,
      sessions: 'Sign out everywhere else, reset saved views, sign out',
      agents: agents.length ? `${agents.length} connected` : 'None connected',
    };
    const ix = el('nav', 'pf-index', SECS.map(([k, l]) => `<a href="#${k}"><span class="tx"><b>${l}</b><span>${sum[k]}</span></span><span class="chev" aria-hidden="true">›</span></a>`).join(''));
    ix.setAttribute('aria-label', 'Your profile');
    $('.head').after(ix);
  }

  render();
  const at = q.get('at');
  if (at) requestAnimationFrame(() => goTo(at));
})();
