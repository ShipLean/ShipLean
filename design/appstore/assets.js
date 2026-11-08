// ShipLean phone app store assets (SHIP-930). asset.html?id=<id> draws one asset at its exact pixel size;
// asset.html?list prints every asset as APPASSETS:<json>:END for export.sh. Nothing here redraws the logo:
// GLYPH is the boat from design/logo/shiplean-mark-*.svg, copied unchanged (viewBox 0 0 26 26).
(() => {
  const GLYPH = '<rect x="13.5" y="5.5" width="4.5" height="2.4" rx="1.2"/><rect x="11" y="9.3" width="7" height="2.4" rx="1.2"/><rect x="8.5" y="13.1" width="9.5" height="2.4" rx="1.2"/><path d="M5 17.6h16.5a1 1 0 0 1 .8 1.6l-1.6 2.1a2 2 0 0 1-1.6.8H7.4a2 2 0 0 1-1.6-.8l-1.6-2.1A1 1 0 0 1 5 17.6Z"/>';
  const LIGHT = ['#16a089', '#2563c9']; // brand gradient, light
  const DARK = ['#3dd6b5', '#5cc8f0'];  // brand gradient, dark
  const NAVY = '#06192a';               // glyph colour on the dark mark
  const grad = (id, [a, b], user) => `<linearGradient id="${id}" ${user ? 'gradientUnits="userSpaceOnUse" x1="4.2" y1="5.5" x2="22.3" y2="22.1"' : 'x1="0" y1="0" x2="1" y2="1"'}><stop offset="0" stop-color="${a}"/><stop offset="1" stop-color="${b}"/></linearGradient>`;
  const svg = (w, h, vb, inner) => `<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="${h}" viewBox="${vb}">${inner}</svg>`;

  // The mark's own proportions on a full-bleed square: the store or launcher applies its own corner mask.
  const square = (size, { bg, glyph, defs = '' }) => svg(size, size, '0 0 26 26', `<defs>${defs}</defs><rect width="26" height="26" fill="${bg}"/><g fill="${glyph}">${GLYPH}</g>`);
  // The mark exactly as in design/logo (rounded tile), for splash images.
  const tile = (size, dark) => svg(size, size, '0 0 26 26', `<defs>${grad('g', dark ? DARK : LIGHT)}</defs><rect width="26" height="26" rx="7" fill="url(#g)"/><g fill="${dark ? NAVY : '#fff'}">${GLYPH}</g>`);
  // Android adaptive layers are 108 dp with a 66 dp safe circle: the glyph's farthest point sits 31 dp from the centre.
  const adaptive = (size, fill) => svg(size, size, '0 0 108 108', `<g fill="${fill}" transform="translate(54 54) scale(2.9) translate(-13.25 -13.8)">${GLYPH}</g>`);

  const ICONS = {
    'ios-icon':        { file: 'ios/icon-1024.png', w: 1024, h: 1024, alpha: false, note: 'App icon, iOS light. Opaque, square; iOS rounds it.', draw: () => square(1024, { defs: grad('g', LIGHT), bg: 'url(#g)', glyph: '#fff' }) },
    'ios-icon-dark':   { file: 'ios/icon-1024-dark.png', w: 1024, h: 1024, alpha: false, note: 'iOS 18+ dark appearance: the dark brand gradient on the glyph, navy behind.', draw: () => square(1024, { defs: grad('d', ['#171e2d', NAVY]) + grad('gg', DARK, true), bg: 'url(#d)', glyph: 'url(#gg)' }) },
    'ios-icon-tinted': { file: 'ios/icon-1024-tinted.png', w: 1024, h: 1024, alpha: false, note: 'iOS 18+ tinted: grayscale, iOS applies the tint.', draw: () => square(1024, { defs: grad('t', ['#ffffff', '#c9c9c9'], true), bg: '#000', glyph: 'url(#t)' }) },
    'android-fg':      { file: 'android/adaptive-foreground.png', w: 1024, h: 1024, alpha: true, note: 'Adaptive icon foreground: white glyph inside the 66 dp safe zone.', draw: () => adaptive(1024, '#fff') },
    'android-bg':      { file: 'android/adaptive-background.png', w: 1024, h: 1024, alpha: false, note: 'Adaptive icon background: the brand gradient.', draw: () => svg(1024, 1024, '0 0 1 1', `<defs>${grad('g', LIGHT)}</defs><rect width="1" height="1" fill="url(#g)"/>`) },
    'android-mono':    { file: 'android/monochrome.png', w: 1024, h: 1024, alpha: true, note: 'Android 13+ themed icon: only the alpha is used.', draw: () => adaptive(1024, '#fff') },
    'play-icon':       { file: 'play/icon-512.png', w: 512, h: 512, alpha: true, note: 'Google Play icon: 32-bit PNG, full square; Play applies the mask.', draw: () => square(512, { defs: grad('g', LIGHT), bg: 'url(#g)', glyph: '#fff' }) },
    'splash':          { file: 'expo/splash-icon.png', w: 1024, h: 1024, alpha: true, note: 'Splash image, light: the mark on #f7f9fd.', draw: () => tile(1024, false) },
    'splash-dark':     { file: 'expo/splash-icon-dark.png', w: 1024, h: 1024, alpha: true, note: 'Splash image, dark: the dark mark on #171e2d.', draw: () => tile(1024, true) },
  };

  // Store screenshots: one caption over a real app screen (a mock-up state until the app is built).
  const SHOTS = [
    { n: 1, page: 'inbox', state: 'list', h: 'Everything waiting on you', s: 'Questions, decisions and approvals from people and agents, in one list.' },
    { n: 2, page: 'backlog', state: 'listening', h: 'Hold to talk', s: 'Say what to change. Keys and names on screen are matched as you speak.' },
    { n: 3, page: 'work-item', state: 'preview', h: 'Read it before it changes', s: 'Every voice command shows its changes first. Nothing applies until you say yes.' },
    { n: 4, page: 'feature-board', state: 'board', h: 'See how every Feature is going', s: 'On track, at risk or blocked, and what needs you.' },
    { n: 5, page: 'work-item', state: 'ask', h: 'Ask, then read the answer', s: '“What’s left on this?” answered on screen, from the item as it is now.' },
    { n: 6, page: 'retro', state: 'retro', h: 'Run the retro from your phone', s: 'Add notes by voice and decide which become action items.' },
    { n: 7, page: 'search', state: 'results', h: 'Find any work item', s: 'The same search as ShipLean on the web, typed or spoken.' },
    { n: 8, page: 'setup', state: 'models', h: 'Speech recognised on your phone', s: 'Voice models download once and run on the phone.' },
  ];
  const DEVICES = {
    'ios-69': { w: 1320, h: 2868, dir: 'ios/screenshots-6.9' },
    'ios-65': { w: 1284, h: 2778, dir: 'ios/screenshots-6.5' },
    'ios-63': { w: 1206, h: 2622, dir: 'ios/screenshots-6.3' },
    'play':   { w: 1080, h: 1920, dir: 'play/phone-screenshots' },
  };

  function shot(dev, n, { style = 'brand', theme = 'light' } = {}) {
    const { w, h } = DEVICES[dev];
    const sh = SHOTS.find((x) => x.n === n);
    const capH = Math.round(h * 0.2);          // Google Play: a caption takes at most 20% of the screenshot
    const bottom = Math.round(h * 0.035);
    const k = Math.min((w * 0.84) / 390, (h - capH - bottom) / 844);
    const sw = Math.round(390 * k), shH = Math.round(844 * k);
    const fs = Math.round(w * 0.066), ss = Math.round(w * 0.034);
    return `<div class="shot ${style}" style="width:${w}px;height:${h}px">
      <div class="cap" style="height:${capH}px;padding:0 ${Math.round(w * 0.07)}px">
        <h1 style="font-size:${fs}px">${sh.h}</h1><p style="font-size:${ss}px">${sh.s}</p>
      </div>
      <div class="screen" style="width:${sw}px;height:${shH}px;border-radius:${Math.round(sw * 0.075)}px">
        <iframe src="../mobileapp/${sh.page}.html?state=${sh.state}&theme=${theme}" width="390" height="844" scrolling="no" style="transform:scale(${k})"></iframe>
      </div>
    </div>`;
  }

  // Google Play feature graphic: no alpha, no store badges, no claims; key content away from the edges.
  function feature() {
    const k = 0.62;
    const phone = (page, state, x, y) => `<div class="screen fg" style="left:${x}px;top:${y}px;width:${390 * k}px;height:${844 * k}px;border-radius:${Math.round(390 * k * 0.075)}px"><iframe src="../mobileapp/${page}.html?state=${state}&theme=light" width="390" height="844" scrolling="no" style="transform:scale(${k})"></iframe></div>`;
    return `<div class="feature" style="width:1024px;height:500px">
      <div class="brand">${svg(76, 76, '0 0 26 26', `<rect width="26" height="26" rx="7" fill="#fff"/><g fill="#2563c9">${GLYPH}</g>`)}<span>ShipLean</span></div>
      <h1>Your backlog,<br>by voice.</h1>
      <p>Speak to change it. Read every change before it applies.</p>
      ${phone('backlog', 'preview', 600, 70)}${phone('inbox', 'list', 790, 34)}
    </div>`;
  }

  const ASSETS = [];
  for (const [id, a] of Object.entries(ICONS)) ASSETS.push({ id, file: a.file, w: a.w, h: a.h, alpha: a.alpha, note: a.note });
  ASSETS.push({ id: 'feature', file: 'play/feature-graphic.png', w: 1024, h: 500, alpha: false, note: 'Google Play feature graphic (required).' });
  for (const [dev, d] of Object.entries(DEVICES))
    for (const s of SHOTS) ASSETS.push({ id: `${dev}-${s.n}`, file: `${d.dir}/${s.n}-${s.page}-${s.state}.png`, w: d.w, h: d.h, alpha: false });
  // Alternatives for the review only (D2 background, D3 theme), Play size, shots 1-3.
  for (const n of [1, 2, 3]) {
    ASSETS.push({ id: `alt-soft-${n}`, file: `_alternatives/soft-${n}.png`, w: 1080, h: 1920, alpha: false, alt: true });
    ASSETS.push({ id: `alt-dark-${n}`, file: `_alternatives/dark-${n}.png`, w: 1080, h: 1920, alpha: false, alt: true });
  }

  function render(id) {
    if (ICONS[id]) return ICONS[id].draw();
    if (id === 'feature') return feature();
    let m = id.match(/^alt-(soft|dark)-(\d)$/);
    if (m) return shot('play', +m[2], m[1] === 'soft' ? { style: 'soft' } : { theme: 'dark' });
    m = id.match(/^(ios-69|ios-65|ios-63|play)-(\d)$/);
    if (m) return shot(m[1], +m[2]);
    return `Unknown asset ${id}`;
  }

  const q = new URLSearchParams(location.search);
  if (q.has('list')) { document.body.textContent = 'APPASSETS:' + JSON.stringify(ASSETS) + ':END'; return; }
  const a = ASSETS.find((x) => x.id === q.get('id'));
  if (!a) { document.body.innerHTML = `<pre>${ASSETS.map((x) => `${x.id}\t${x.w}×${x.h}\t${x.file}`).join('\n')}</pre>`; return; }
  document.documentElement.style.cssText = `width:${a.w}px;height:${a.h}px`;
  if (a.alpha) document.documentElement.classList.add('clear');
  document.body.innerHTML = render(a.id);
  window.STORE = { SHOTS, DEVICES, ASSETS };
})();
