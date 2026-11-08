// ShipLean native app (SHIP-905) mock-up kit. Builders return HTML strings; a screen file declares its states
// with App.screen({ title, states: [{ id, title, note, render }] }) and the kit renders the one named by
// ?state=<id> (the first when absent). ?theme=light|dark forces a theme. See README.md in this folder.
(function () {
  const P = {
    mic: '<rect x="9" y="3" width="6" height="11" rx="3"/><path d="M5 11a7 7 0 0 0 14 0M12 18v3"/>',
    back: '<path d="M15 5l-7 7 7 7"/>',
    chevron: '<path d="M9 5l7 7-7 7"/>',
    down: '<path d="M6 9l6 6 6-6"/>',
    search: '<circle cx="11" cy="11" r="6.5"/><path d="M20 20l-4.2-4.2"/>',
    more: '<circle cx="5" cy="12" r="1.3"/><circle cx="12" cy="12" r="1.3"/><circle cx="19" cy="12" r="1.3"/>',
    check: '<path d="M5 12.5l4.5 4.5L19 7.5"/>',
    x: '<path d="M6 6l12 12M18 6L6 18"/>',
    undo: '<path d="M9 14L4 9l5-5"/><path d="M4 9h10a6 6 0 0 1 0 12h-3"/>',
    play: '<path d="M8 5.5v13l10-6.5z" fill="currentColor"/>',
    pause: '<path d="M8 5v14M16 5v14"/>',
    up: '<path d="M12 19V5M6 11l6-6 6 6"/>',
    arrowdown: '<path d="M12 5v14M6 13l6 6 6-6"/>',
    rank: '<path d="M7 4v16M4 7l3-3 3 3M17 20V4M14 17l3 3 3-3"/>',
    flag: '<path d="M5 21V4M5 4h11l-2 4 2 4H5"/>',
    alert: '<path d="M12 4l9 16H3z"/><path d="M12 10v4M12 17.2v.1"/>',
    note: '<path d="M5 4h14v12l-4 4H5z"/><path d="M15 20v-4h4M8 9h8M8 13h5"/>',
    message: '<path d="M4 5h16v11H9l-5 4z"/>',
    inbox: '<path d="M3 13l3-8h12l3 8v6H3z"/><path d="M3 13h5l1 3h6l1-3h5"/>',
    list: '<path d="M8 6h12M8 12h12M8 18h12"/><circle cx="4" cy="6" r="1"/><circle cx="4" cy="12" r="1"/><circle cx="4" cy="18" r="1"/>',
    board: '<rect x="3" y="4" width="5" height="16" rx="1.5"/><rect x="9.5" y="4" width="5" height="11" rx="1.5"/><rect x="16" y="4" width="5" height="7" rx="1.5"/>',
    retro: '<path d="M4 12a8 8 0 1 0 2.4-5.7"/><path d="M4 4v4h4"/><path d="M12 8v4l3 2"/>',
    user: '<circle cx="12" cy="8" r="4"/><path d="M4 20a8 8 0 0 1 16 0"/>',
    spark: '<path d="M12 3l1.8 5.2L19 10l-5.2 1.8L12 17l-1.8-5.2L5 10l5.2-1.8z"/>',
    filter: '<path d="M4 5h16l-6 7v6l-4 2v-8z"/>',
    plus: '<path d="M12 5v14M5 12h14"/>',
    keyboard: '<rect x="3" y="6" width="18" height="12" rx="2"/><path d="M7 10h.01M11 10h.01M15 10h.01M7 14h10"/>',
    thumb: '<path d="M7 11v9H4v-9zM7 11l4-7a2 2 0 0 1 3 2l-1 4h6a2 2 0 0 1 2 2.3l-1.2 6A2 2 0 0 1 17.8 20H7"/>',
    clock: '<circle cx="12" cy="12" r="8"/><path d="M12 8v4l3 2"/>',
    download: '<path d="M12 4v11M7 10l5 5 5-5M5 20h14"/>',
    eye: '<path d="M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7S2 12 2 12z"/><circle cx="12" cy="12" r="3"/>',
    link: '<path d="M10 14a4 4 0 0 0 5.7 0l3-3a4 4 0 0 0-5.7-5.7l-1 1M14 10a4 4 0 0 0-5.7 0l-3 3a4 4 0 0 0 5.7 5.7l1-1"/>',
    speaker: '<path d="M4 9h4l5-4v14l-5-4H4z"/><path d="M16 9a4 4 0 0 1 0 6M18.5 6.5a8 8 0 0 1 0 11"/>',
  };
  const esc = (s) => String(s).replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' })[c]);
  const icon = (name, cls = '') => `<svg class="i ${cls}" viewBox="0 0 24 24" aria-hidden="true">${P[name] || ''}</svg>`;

  const statusBar = () => `<div class="status"><span>9:41</span><span class="sys">
    <svg viewBox="0 0 17 12"><rect x="0" y="8" width="3" height="4" rx="1"/><rect x="4.5" y="5.5" width="3" height="6.5" rx="1"/><rect x="9" y="3" width="3" height="9" rx="1"/><rect x="13.5" y="0" width="3" height="12" rx="1"/></svg>
    <svg viewBox="0 0 25 12"><rect x=".5" y=".5" width="21" height="11" rx="3" fill="none" stroke="currentColor"/><rect x="2" y="2" width="16" height="8" rx="1.5"/><rect x="23" y="4" width="2" height="4" rx="1"/></svg></span></div>`;

  // topBar({ title, sub, back, left, right }): left/right are HTML (icon buttons); back adds a back button.
  const topBar = ({ title = '', sub = '', back = false, left = '', right = '' } = {}) =>
    `<div class="topbar">${back ? `<button class="icon-btn" aria-label="Back">${icon('back')}</button>` : left}
      <div class="title"><b>${title}</b>${sub ? `<small>${sub}</small>` : ''}</div>${right}</div>`;
  const iconBtn = (name, { on = false, dot = false, label = '' } = {}) =>
    `<button class="icon-btn${on ? ' on' : ''}" aria-label="${esc(label || name)}">${icon(name)}${dot ? '<span class="dot"></span>' : ''}</button>`;

  // tabBar(active, { inbox: 3, mic: 'idle'|'live'|'busy'|'off' }): Inbox · Backlog · [hold to talk] · Features · Retro.
  const tabBar = (active, { inbox = 0, mic = 'idle' } = {}) => {
    const t = (id, label, ic, badge = 0) => `<button class="tab${active === id ? ' on' : ''}">${icon(ic)}${label}${badge ? `<span class="badge">${badge}</span>` : ''}</button>`;
    const micLabel = { idle: 'Hold to talk', live: 'Listening', busy: 'Working…', off: 'Downloading' }[mic];
    return `<nav class="tabs">${t('inbox', 'Inbox', 'inbox', inbox)}${t('backlog', 'Backlog', 'list')}
      <div class="mic-slot"><button class="mic ${mic === 'idle' ? '' : mic}" aria-label="Hold to talk">${icon('mic')}</button><span class="mic-label">${micLabel}</span></div>
      ${t('features', 'Features', 'board')}${t('retro', 'Retro', 'retro')}</nav>`;
  };

  // dock({ mic, placeholder }): the detail-screen alternative to the tab bar.
  const dock = ({ mic = 'idle', placeholder = 'Type a note or command' } = {}) =>
    `<div class="dock"><div class="type-field">${esc(placeholder)}</div><button class="mic ${mic === 'idle' ? '' : mic}" aria-label="Hold to talk">${icon('mic')}</button></div>`;

  const type = (t) => `<span class="type ${t}" title="${t}">${{ epic: 'E', feature: 'F', story: 'S', bug: 'B', task: 'T', action: 'A' }[t] || ''}</span>`;
  // state(name, category): category is proposed | in-progress | resolved | completed | removed.
  const state = (name, cat) => `<span class="state ${cat}">${esc(name)}</span>`;
  const health = (h) => `<span class="health ${h}">${{ ok: 'On track', risk: 'At risk', blocked: 'Blocked' }[h]}</span>`;
  // progress({ done, cur, other }) in percent of points.
  const progress = ({ done = 0, cur = 0, other = 0 } = {}) =>
    `<div class="progress"><i class="done" style="width:${done}%"></i><i class="cur" style="width:${cur}%"></i><i class="other" style="width:${other}%"></i></div>`;
  const avatar = (initials, { agent = false, sm = false, color = '' } = {}) =>
    `<span class="avatar${agent ? ' agent' : ''}${sm ? ' sm' : ''}"${color ? ` style="background:${color}"` : ''}>${esc(initials)}</span>`;

  // Deterministic bar heights for waveforms (no randomness, so screenshots are stable).
  const bars = (n, max, seed = 3) => Array.from({ length: n }, (_, i) => {
    const v = Math.abs(Math.sin((i + 1) * seed * 0.7) * Math.cos(i * 0.45 + seed));
    return Math.max(3, Math.round(v * max));
  });
  const wave = (n = 34) => `<div class="wave">${bars(n, 38).map((h) => `<i style="height:${h}px"></i>`).join('')}</div>`;

  // askCard({ kind: 'approval'|'feedback', q, from, agent, when, opts: [labels], primary: index, say }):
  // something an agent or a person is waiting on a human for.
  const askCard = ({ kind = 'approval', q, from = '', agent = true, when = '', opts = [], primary = 0, say = '' }) =>
    `<div class="ask-card"><div class="kind">${icon(kind === 'approval' ? 'check' : 'message', 'sm')}${kind === 'approval' ? 'Decision to confirm' : 'Needs input'}</div>
      <div class="q">${q}</div>${from ? `<div class="from">${avatar(agent ? 'AI' : from.slice(0, 2).toUpperCase(), { agent, sm: true })}${esc(from)}${when ? `<span class="sep"></span>${esc(when)}` : ''}</div>` : ''}
      ${opts.length ? `<div class="opts">${opts.map((o, i) => `<button class="btn sm${i === primary ? ' primary' : ''}">${o}</button>`).join('')}</div>` : ''}
      ${say ? `<div class="say">${icon('mic', 'sm')}Or say “${say}”</div>` : ''}</div>`;

  // listening({ ctx, words, partial, tries }): the sheet while the mic is held. ctx = what the model will act on,
  // words = settled transcript (may include <span class="hot">…</span>), partial = still being recognised.
  const listening = ({ ctx = '', words = '', partial = '', tries = [] } = {}) =>
    `<div class="scrim"></div><div class="sheet listening"><div class="grab"></div>
      <div class="voice-ctx"><span class="live-dot"></span>Listening${ctx ? ` <span class="sep"></span> ${ctx}` : ''}</div>
      <div class="transcript">${words}${partial ? ` <span class="partial">${partial}</span>` : ''}${!words && !partial ? '<span class="partial">Say what to do…</span>' : ''}</div>
      ${tries.length ? `<div class="try">${tries.map((t) => `<span class="chip">“${t}”</span>`).join('')}</div>` : ''}
      ${wave()}<div class="voice-hint">Release to send <span class="sep"></span> Slide left to cancel</div></div>`;

  // act({ kind: 'state'|'note'|'rank'|'approve'|'field'|'ask', icon, title, detail, quote })
  const act = ({ kind = 'state', icon: ic, title, detail = '', quote = '' }) => {
    const def = { state: 'flag', note: 'note', rank: 'rank', approve: 'check', field: 'flag', ask: 'message' }[kind];
    return `<div class="act act-${kind}"><span class="ic">${icon(ic || def)}</span><div class="what"><b>${title}</b>${detail ? `<span>${detail}</span>` : ''}${quote ? `<div class="quote">“${quote}”</div>` : ''}</div><button class="x" aria-label="Remove">${icon('x', 'sm')}</button></div>`;
  };
  // preview({ ctx, heard, acts: [act opts], thinking }): what the command will do. Nothing changes until Apply or “yes”.
  const preview = ({ ctx = '', heard = '', acts = [], thinking = false } = {}) =>
    `<div class="scrim"></div><div class="sheet preview"><div class="grab"></div>
      <div class="voice-ctx">${icon('spark', 'sm')}${thinking ? 'Working it out' : `${acts.length} change${acts.length === 1 ? '' : 's'} to apply`}${ctx ? ` <span class="sep"></span> ${ctx}` : ''}</div>
      <div class="heard" style="margin-top:10px">“${heard}”</div>
      ${thinking ? '<div class="thinking"><span class="dots"><i></i><i></i><i></i></span>Reading your command on this phone…</div>'
        : `<div class="acts">${acts.map(act).join('')}</div>
      <div class="go"><button class="btn">${icon('mic', 'sm')}Fix</button><button class="btn primary">Apply</button></div>
      <div class="say-yes">Or say “yes” <span class="sep"></span> nothing changes until you apply</div>`}</div>`;

  // toast(text, { bottom }): after applying. bottom = px from the phone's bottom edge.
  const toast = (text, { bottom = 106, action = 'Undo' } = {}) =>
    `<div class="toast" style="bottom:${bottom}px">${icon('check', 'sm')}<span class="grow">${text}</span><button>${action}</button></div>`;

  // voiceNote({ who, initials, when, text, secs, agent }): a note (comment) left by voice. secs = clip length or 0 for none.
  const voiceNote = ({ who, initials, when = '', text, secs = 0, agent = false, color = '' }) =>
    `<div class="vnote">${avatar(initials, { agent, color })}<div class="body"><div class="who"><b>${esc(who)}</b>${when ? `<span>${esc(when)}</span>` : ''}${secs ? `<span class="via-voice">${icon('mic', 'sm')}voice</span>` : ''}</div>
      <div class="text">${text}</div>${secs ? `<span class="clip"><span class="play">${icon('play', 'sm')}</span><span class="bars">${bars(28, 18, initials.charCodeAt(0)).map((h) => `<i style="height:${h}px"></i>`).join('')}</span>0:${String(secs).padStart(2, '0')}</span>` : ''}</div></div>`;

  // frame({ top, sub, body, bottom, overlay }): the whole phone. bottom = tabBar(...) or dock(...).
  const frame = ({ top = '', sub = '', body = '', bottom = '', overlay = '' }) =>
    `<div class="phone">${statusBar()}${top}${sub}<div class="content"><div class="scroll">${body}</div></div>${bottom}${overlay}</div>`;

  let registered = null;
  function screen(def) {
    registered = def;
    window.APP_SCREEN = { title: def.title, states: def.states.map(({ id, title, note }) => ({ id, title, note })) };
    const q = new URLSearchParams(location.search);
    const theme = q.get('theme');
    if (theme === 'light' || theme === 'dark') document.documentElement.dataset.theme = theme;
    const mount0 = document.getElementById('app');
    if (q.has('meta')) { (mount0 || document.body).textContent = 'APPMETA' + JSON.stringify(window.APP_SCREEN) + 'ENDMETA'; return; }
    if (q.has('list')) { (mount0 || document.body).textContent = 'APPSTATES:' + window.APP_SCREEN.states.map((x) => x.id).join(',') + ':END'; return; }
    const s = def.states.find((x) => x.id === q.get('state')) || def.states[0];
    document.title = `ShipLean app · ${def.title} · ${s.title}`;
    const mount = document.getElementById('app') || document.body.appendChild(Object.assign(document.createElement('div'), { id: 'app' }));
    mount.innerHTML = s.render();
  }

  window.App = { icon, esc, statusBar, topBar, iconBtn, tabBar, dock, type, state, health, progress, avatar, wave, bars, askCard, listening, act, preview, toast, voiceNote, frame, screen };
})();
