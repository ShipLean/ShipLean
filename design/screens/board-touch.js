/*
 * The Stories, Features and Epics boards in the touch shell (SHIP-719); styles in board-touch.css. Load after the
 * board's own script. At 900px and below, after every render of the board it moves the board's column headers, lane
 * headers and cells into a pager (one page per column) and adds:
 *   - column chips above the pager, with the count against the WIP limit; tapping one turns to that column
 *   - a meta line under the toolbar (open cards, points, lanes, card size, filter)
 *   - a ⋯ on every card opening the card's actions: Open, Move to (each column, with its WIP), Move to top or bottom,
 *     lane, Assign or Owner, and on the Features and Epics boards what's under it
 *   - touch drag: a long press lifts the card; drag it to a column or lane (hold at the left or right edge to turn the
 *     page) and let go; let go without moving and the card's actions open
 *   - Filter and ⋯ More as bottom sheets; the board's own ⓘ, Assign/Owner menus and Move to Done check as sheets
 * Moves go through the board's own drop handler, so the state, pull rule, WIP, lane and Done check behave exactly as
 * a drop on the desktop board does. Above 900px nothing changes; ?notouch shows the desktop board at any width.
 * Review states: ?col=<column id> turns to a column, ?sheet=filter|more|card:KEY|assign:KEY|dod:<column id> opens a
 * sheet, ?q=<text> and ?who=<person> fill in the filter, ?compact for compact cards, ?nolanes turns swimlanes off,
 * ?drag=KEY&to=<column id> shows a card lifted and held over that column.
 */
(() => {
  const q = new URLSearchParams(location.search);
  if (q.has('notouch')) { document.querySelector('link[href="board-touch.css"]')?.remove(); return; }
  const mq = matchMedia('(max-width: 900px)');
  mq.addEventListener('change', () => location.reload());
  if (!mq.matches) return;

  const root = document.documentElement;
  const $ = s => document.querySelector(s);
  const el = (tag, cls, html) => { const e = document.createElement(tag); if (cls) e.className = cls; if (html != null) e.innerHTML = html; return e; };
  const board = $('#board'), bwrap = $('#bwrap'), toolbar = $('.toolbar'), fbar = $('#fbar'), btnFilter = $('#btnFilter');
  const isStories = !!$('#optLanes');
  const dots = '<svg viewBox="0 0 16 16" width="16" height="16" aria-hidden="true"><circle cx="3.5" cy="8" r="1.4" fill="currentColor"/><circle cx="8" cy="8" r="1.4" fill="currentColor"/><circle cx="12.5" cy="8" r="1.4" fill="currentColor"/></svg>';

  /* Chrome around the pager: ⋯ More in the toolbar, the meta line, the column chips */
  const more = el('button', 'bt-more', dots);
  more.type = 'button'; more.setAttribute('aria-label', 'More board options'); more.setAttribute('aria-haspopup', 'dialog');
  toolbar.append(more);
  const meta = el('div', 'bt-meta');
  toolbar.after(meta);
  const strip = el('nav', 'bt-strip');
  strip.setAttribute('aria-label', 'Columns');
  board.before(strip);
  const pager = el('div', 'bt-pager');
  pager.style.position = 'relative';
  board.append(pager);

  const colOfTrack = (track, pages) => pages.find(p => track === p.dataset.col || track.startsWith(p.dataset.col + '-'));
  let pages = [], current = 0;

  /* After each render: move the board into the pager */
  function restructure() {
    if (!bwrap.children.length) return;
    const left = pager.scrollLeft;
    pager.classList.toggle('compact', bwrap.classList.contains('compact'));
    const heads = [...bwrap.querySelectorAll('.bhead > .colh')];
    const lanes = [...bwrap.querySelectorAll(':scope > .lane')];
    pager.innerHTML = '';
    pages = heads.map(h => { const p = el('section', 'bt-page'); p.dataset.col = h.dataset.col; p.setAttribute('aria-label', h.querySelector('.nm').textContent); p.append(h); pager.append(p); return p; });
    lanes.forEach(lane => {
      const head = lane.querySelector(':scope > .lane-h');
      const collapsed = !lane.querySelector(':scope > .bgrid');
      const cells = [...lane.querySelectorAll(':scope > .bgrid > .cell')];
      pages.forEach((p, i) => {
        const mine = cells.filter(c => colOfTrack(c.dataset.track, pages) === p);
        const n = mine.reduce((a, c) => a + c.querySelectorAll(':scope > .card').length, 0);
        const hasAdd = mine.some(c => c.querySelector('[data-add], .add-field'));
        if (head && !collapsed && !n && !hasAdd) return; // an empty lane in this column: leave it out
        const box = el('div', 'bt-lane' + (collapsed ? ' collapsed' : ''));
        if (head) {
          const h = i === pages.length - 1 ? head : head.cloneNode(true);
          const notes = [...h.querySelectorAll(':scope > .xs.muted')], note = notes.pop();
          notes.forEach(x => x.remove());
          if (note) note.textContent = collapsed ? 'collapsed' : `${n} ${n === 1 ? 'card' : 'cards'}`;
          box.append(h);
        }
        mine.forEach(c => {
          if (c.getAttribute('aria-label')?.includes(' · ')) {
            const sub = c.getAttribute('aria-label').split(' · ')[1];
            const k = c.querySelectorAll(':scope > .card').length;
            box.append(el('div', 'bt-sub', `${sub} <b>${k}</b> <span>${sub === 'Doing' ? 'being worked on' : 'ready to pull'}</span>`));
          }
          box.append(c);
        });
        p.append(box);
      });
    });
    pager.querySelectorAll('.card').forEach(card => {
      card.querySelectorAll('.r1 .bt-cm').forEach(b => b.remove());
      const b = el('button', 'bt-cm', dots);
      b.type = 'button'; b.dataset.cm = card.dataset.key; b.setAttribute('aria-label', `Actions for ${card.dataset.key}`); b.setAttribute('aria-haspopup', 'dialog');
      card.querySelector('.r1').append(b);
      card.removeAttribute('draggable');
      card.getAnimations().forEach(a => { if (!(a instanceof CSSAnimation)) a.cancel(); }); // the page's FLIP measured the hidden board
    });
    pager.scrollLeft = left;
    $('#addTitle')?.focus();
    renderStrip();
    renderMeta();
    if (sheet.dataset.kind === 'more') openMore();
  }
  new MutationObserver(restructure).observe(bwrap, { childList: true });

  function renderStrip() {
    strip.innerHTML = pages.map((p, i) => {
      const h = p.querySelector('.colh');
      const wip = h.querySelector('.wip').textContent;
      return `<button type="button" data-go="${i}" class="${h.classList.contains('over') ? 'over' : ''}" aria-current="${i === current}">${h.querySelector('.nm').textContent} <span class="n">${wip}</span></button>`;
    }).join('');
  }
  function renderMeta() {
    const lanes = isStories ? ($('#optLanes').checked ? 'Swimlanes' : '') : ($('[data-lanes="epic"][aria-pressed="true"]') && !$('#lanesCtl').hidden ? 'Lanes by Epic' : '');
    const compact = bwrap.classList.contains('compact');
    const n = (btnFilter.textContent.match(/\((\d+)\)/) || [])[1];
    if (n) btnFilter.dataset.n = n; else delete btnFilter.dataset.n;
    const shown = $('#fcount').textContent.match(/Showing (\d+) of (\d+)/);
    const [open, pts] = $('#summary').textContent.split(' · ');
    meta.innerHTML = [`<b>${open}</b>`, pts, lanes, compact ? 'Compact cards' : '', shown ? `<span class="on">Filtered: ${shown[1]} of ${shown[2]} cards</span>` : '']
      .filter(Boolean).join('<span class="dot"></span>');
  }

  /* Turning pages */
  const pageWidth = () => (pages[1] ? pages[1].offsetLeft - pages[0].offsetLeft : pager.clientWidth);
  function goTo(i, smooth) {
    const p = pages[Math.max(0, Math.min(i, pages.length - 1))];
    if (p) pager.scrollTo({ left: p.offsetLeft - parseFloat(getComputedStyle(pager).paddingLeft), behavior: smooth ? 'smooth' : 'instant' });
  }
  pager.addEventListener('scroll', () => {
    const i = Math.round(pager.scrollLeft / pageWidth());
    if (i === current) return;
    current = i;
    strip.querySelectorAll('[data-go]').forEach(b => b.setAttribute('aria-current', +b.dataset.go === i));
    const b = strip.querySelector(`[data-go="${i}"]`);
    if (b) strip.scrollTo({ left: b.offsetLeft - 12, behavior: 'smooth' });
  }, { passive: true });
  strip.addEventListener('click', e => { const b = e.target.closest('[data-go]'); if (b) goTo(+b.dataset.go, true); });

  /* One bottom sheet for Filter, ⋯ More and the card's actions */
  const sheet = el('div', 'bt-sheet');
  sheet.hidden = true;
  sheet.setAttribute('role', 'dialog'); sheet.setAttribute('aria-modal', 'true');
  sheet.innerHTML = '<header class="sheet-head"><b></b><button class="btn ghost sheet-x" type="button">Close</button></header><div class="sb"></div><div class="foot" hidden></div>';
  document.body.append(sheet);
  const sb = sheet.querySelector('.sb'), foot = sheet.querySelector('.foot');
  let fbarHome = null;
  function openSheet(kind, title, html, footHtml) {
    sheet.dataset.kind = kind;
    sheet.querySelector('.sheet-head b').textContent = title;
    if (html != null) sb.innerHTML = html;
    foot.hidden = !footHtml; foot.innerHTML = footHtml || '';
    sheet.hidden = false; root.dataset.sheet = 'board';
  }
  function closeSheet() {
    if (sheet.hidden) return;
    const kind = sheet.dataset.kind;
    sheet.hidden = true; delete sheet.dataset.kind;
    if (root.dataset.sheet === 'board') delete root.dataset.sheet;
    pager.querySelectorAll('.card.lifted').forEach(c => c.classList.remove('lifted'));
    if (kind === 'filter') { fbarHome.after(fbar); if (!fbar.hidden) btnFilter.click(); }
  }
  sheet.querySelector('.sheet-x').addEventListener('click', closeSheet);
  document.querySelector('.scrim')?.addEventListener('click', () => { if (!sheet.hidden) closeSheet(); });
  document.addEventListener('keydown', e => { if (e.key === 'Escape' && !sheet.hidden) closeSheet(); });

  /* Filter: the board's own filter bar, moved into the sheet while it's open */
  new MutationObserver(() => {
    if (fbar.hidden || sheet.dataset.kind === 'filter') return;
    fbarHome = fbarHome || fbar.previousElementSibling;
    openSheet('filter', 'Filter', '', '<button class="btn" type="button" data-clear>Clear</button><button class="btn primary" type="button" data-done>Show cards</button>');
    sb.append(fbar);
    document.activeElement?.blur(); // the keyboard waits for a tap on the field (SHIP-805 C4)
  }).observe(fbar, { attributes: true, attributeFilter: ['hidden'] });
  foot.addEventListener('click', e => {
    if (e.target.closest('[data-clear]')) $('#fclear').click();
    if (e.target.closest('[data-done]')) closeSheet();
  });

  /* ⋯ More: the toolbar controls that don't fit on one row */
  function openMore() {
    const lanesRow = isStories
      ? `<label class="opt-row">Swimlanes <span class="xs muted">Expedite above Default</span><input type="checkbox" data-m="lanes"${$('#optLanes').checked ? ' checked' : ''}></label>`
      : $('#lanesCtl').hidden ? '' : `<div class="opt-row">Lanes<span class="seg" role="group" aria-label="Lanes">${['none', 'epic'].map(v => `<button type="button" data-m="lanes:${v}" aria-pressed="${$(`[data-lanes="${v}"]`).getAttribute('aria-pressed') === 'true'}">${v === 'none' ? 'None' : 'By Epic'}</button>`).join('')}</span></div>`;
    const compact = bwrap.classList.contains('compact');
    openSheet('more', 'Board', `${lanesRow}
      <div class="opt-row">Cards<span class="seg" role="group" aria-label="Cards"><button type="button" data-m="cards:full" aria-pressed="${!compact}">Full</button><button type="button" data-m="cards:compact" aria-pressed="${compact}">Compact</button></span></div>
      <div class="grp">Settings</div>
      <a class="row" href="${$('.toolbar a[href^="board-settings"]')?.getAttribute('href') || 'board-settings.html'}">⚙ Board settings<span class="sub">columns, WIP, lanes, styles ›</span></a>
      <p class="hint">Lanes and card size are yours alone; Board settings change the board for everyone.</p>`);
  }
  more.addEventListener('click', openMore);
  sb.addEventListener('click', e => {
    const m = e.target.closest('[data-m]');
    if (!m) return;
    const [k, v] = m.dataset.m.split(':');
    if (k === 'lanes' && isStories) { $('#optLanes').click(); return; }
    if (k === 'lanes') $(`[data-lanes="${v}"]`).click();
    if (k === 'cards') $(`[data-cards="${v}"]`).click();
  });

  /* The card's actions */
  const cardEl = key => pager.querySelector(`.card[data-key="${key}"]`);
  const firstCard = cell => cell.querySelector(':scope > .card');
  function drop(key, cell, y) {
    const card = cardEl(key);
    const dt = new DataTransfer();
    card.dispatchEvent(new DragEvent('dragstart', { bubbles: true, dataTransfer: dt }));
    cell.dispatchEvent(new DragEvent('drop', { bubbles: true, cancelable: true, dataTransfer: dt, clientY: y }));
    card.dispatchEvent(new DragEvent('dragend', { bubbles: true, dataTransfer: dt }));
  }
  function openCard(key) {
    const card = cardEl(key);
    if (!card) return;
    card.classList.add('lifted');
    const cell = card.closest('.cell');
    const here = cell.dataset.track, lane = cell.dataset.lane;
    const tracks = [];
    pager.querySelectorAll('.cell').forEach(c => { if (!tracks.some(t => t.id === c.dataset.track)) tracks.push({ id: c.dataset.track, label: c.getAttribute('aria-label') }); });
    const moveRows = tracks.map(t => {
      const h = colOfTrack(t.id, pages).querySelector('.colh');
      const wip = h.querySelector('.wip').textContent, over = h.classList.contains('over');
      const isHere = t.id === here;
      return `<button class="row" type="button" data-to="${t.id}"${isHere ? ' disabled aria-checked="true"' : ''}><span class="ck">${isHere ? '✓' : ''}</span>${t.label}<span class="sub${over ? ' over' : ''}">${isHere ? 'here · ' : ''}${wip}</span></button>`;
    }).join('');
    const laneCells = [...pager.querySelectorAll(`.cell[data-track="${here}"]`)].filter(c => c.dataset.lane !== 'all');
    const laneRows = laneCells.length > 1 ? `<div class="grp">Lane</div>` + [...new Set(laneCells.map(c => c.dataset.lane))].map(id => {
      const name = [...pager.querySelectorAll('.bt-lane')].find(b => b.querySelector(`[data-lane-toggle="${id}"]`))?.querySelector('.lane-h b')?.textContent.trim() || id;
      const what = id === 'expedite' ? 'sets Priority 1' : id === 'default' ? 'Priority 2' : 'makes it the parent';
      return `<button class="row" type="button" data-lane="${id}"${id === lane ? ' disabled aria-checked="true"' : ''}><span class="ck">${id === lane ? '✓' : ''}</span>${name}<span class="sub">${id === lane ? 'here' : what}</span></button>`;
    }).join('') : '';
    const isTop = firstCard(cell) === card, isBottom = [...cell.querySelectorAll(':scope > .card')].pop() === card;
    const who = card.querySelector('[data-assign]');
    const kids = card.querySelector('[data-kids]');
    const state = (card.getAttribute('title') || '').replace(/^.*State /, '');
    openSheet('card', key, `
      <div class="ident"><span class="r">${card.querySelector('.ti')?.outerHTML || ''}${key} · ${state}</span><span class="t">${card.querySelector('.ttl').textContent}</span></div>
      <button class="row" type="button" data-act="open"><span class="ck">↗</span>Open<span class="sub">full screen</span></button>
      ${who ? `<button class="row" type="button" data-act="assign"><span class="ck"></span>${isStories ? 'Assign' : 'Owner'}<span class="sub">${who.getAttribute('title').split(' · ')[0].replace(/^Owner /, '')} ›</span></button>` : ''}
      ${kids ? `<button class="row" type="button" data-act="kids"><span class="ck"></span>${kids.getAttribute('aria-expanded') === 'true' ? 'Hide' : 'Show'} what's under it<span class="sub">${[...kids.querySelectorAll('.an[title]')].map(a => a.title.replace(/^(\d+) of (\d+) (.*) closed$/, '$1/$2 $3')).join(' · ')}</span></button>` : ''}
      <div class="grp">Move to</div>${moveRows}
      ${laneRows}
      <div class="grp">Order in ${cell.getAttribute('aria-label')}</div>
      <button class="row" type="button" data-act="top"${isTop ? ' disabled' : ''}><span class="ck">↑</span>Move to top</button>
      <button class="row" type="button" data-act="bottom"${isBottom ? ' disabled' : ''}><span class="ck">↓</span>Move to bottom</button>`);
    sheet.dataset.key = key;
  }
  sb.addEventListener('click', e => {
    if (sheet.dataset.kind !== 'card') return;
    const key = sheet.dataset.key, card = cardEl(key), cell = card?.closest('.cell');
    const to = e.target.closest('[data-to]:not([disabled])'), ln = e.target.closest('[data-lane]:not([disabled])'), act = e.target.closest('[data-act]:not([disabled])');
    if (!to && !ln && !act) return;
    closeSheet();
    if (to) drop(key, pager.querySelector(`.cell[data-track="${to.dataset.to}"][data-lane="${cell.dataset.lane}"]`) || pager.querySelector(`.cell[data-track="${to.dataset.to}"]`), 1e6);
    else if (ln) drop(key, pager.querySelector(`.cell[data-track="${cell.dataset.track}"][data-lane="${ln.dataset.lane}"]`), 1e6);
    else if (act.dataset.act === 'top') drop(key, cell, -1e6);
    else if (act.dataset.act === 'bottom') drop(key, cell, 1e6);
    else if (act.dataset.act === 'assign') card.querySelector('[data-assign]').click();
    else if (act.dataset.act === 'kids') card.querySelector('[data-kids]').click();
    else SL.toast(`Opens <b>${key}</b> full screen over the board (the item modal, SHIP-714); ✕ comes back here`);
  });

  /* Tapping, ⋯, long press and touch drag on cards (D2). A long press lifts the card: drag it to another column or lane
     and let go to drop it there; hold it at the screen's left or right edge to turn the page; near the top or bottom
     the page scrolls. Let go without moving and the card's actions open instead. The drop goes through the board's own
     drop handler, as a mouse drop does on desktop. */
  let suppressClick = false, press = null, drag = null;
  const edges = ['left', 'right'].map(side => { const e = el('div', `bt-edge ${side}`, side === 'left' ? '‹' : '›'); e.setAttribute('aria-hidden', 'true'); document.body.append(e); return e; });
  board.addEventListener('click', e => {
    if (suppressClick) { suppressClick = false; e.stopPropagation(); e.preventDefault(); return; }
    const cm = e.target.closest('[data-cm]');
    if (cm) { e.stopPropagation(); openCard(cm.dataset.cm); return; }
    const card = e.target.closest('.bt-page .card');
    if (card && !e.target.closest('button, a, input, .kids')) { e.stopPropagation(); SL.toast(`Opens <b>${card.dataset.key}</b> full screen over the board (the item modal, SHIP-714); ✕ comes back here`); }
  }, true);
  board.addEventListener('pointerdown', e => {
    const card = e.target.closest('.bt-page .card');
    if (!card || e.target.closest('button, a, input, .kids') || e.button > 0) return;
    press = { card, x: e.clientX, y: e.clientY, timer: setTimeout(() => lift(card, press.x, press.y), 450) };
  });
  const cancelPress = () => { if (press) clearTimeout(press.timer); press = null; };
  const clearDrop = () => pager.querySelectorAll('.drop-before, .drop-end, .cell.over').forEach(x => x.classList.remove('drop-before', 'drop-end', 'over'));
  function target(x, y) {
    const hit = document.elementFromPoint(x, y);
    const cell = hit?.closest('.bt-page .cell');
    if (cell) return cell;
    // Over a column or lane header, or the gap between columns: the card's own lane in the nearest column
    const page = hit?.closest('.bt-page') || (hit?.closest('.bt-pager') && pages.reduce((a, pg) => {
      const r = pg.getBoundingClientRect(), d = x < r.left ? r.left - x : x > r.right ? x - r.right : 0;
      return !a || d < a.d ? { pg, d } : a;
    }, null)?.pg);
    return page ? page.querySelector(`.cell[data-lane="${drag.lane}"]`) || page.querySelector('.cell') : null;
  }
  function lift(card, x, y) {
    if (!press) return;
    navigator.vibrate?.(10);
    const r = card.getBoundingClientRect();
    const ghost = card.cloneNode(true);
    ghost.classList.add('bt-ghost');
    ghost.classList.remove('flash');
    ghost.style.width = r.width + 'px';
    document.body.append(ghost);
    card.classList.add('dragging');
    drag = { key: card.dataset.key, card, ghost, lane: card.closest('.cell').dataset.lane, dx: x - r.left, dy: y - r.top, x, y, sx: x, sy: y, moved: false, edgeDir: 0, edgeSince: 0, raf: 0 };
    root.dataset.dragging = '';
    place(x, y);
    drag.raf = requestAnimationFrame(tick);
  }
  function place(x, y) {
    drag.x = x; drag.y = y;
    drag.moved = drag.moved || Math.hypot(x - drag.sx, y - drag.sy) > 8;
    drag.ghost.style.transform = `translate(${x - drag.dx}px, ${y - drag.dy}px) rotate(-2deg)`;
    clearDrop();
    const cell = target(x, y);
    if (!cell) return;
    cell.classList.add('over');
    const b = [...cell.querySelectorAll(':scope > .card:not(.dragging)')].find(c => { const r = c.getBoundingClientRect(); return y < r.top + r.height / 2; });
    if (b) b.classList.add('drop-before'); else cell.classList.add('drop-end');
  }
  function tick(t) {
    if (!drag) return;
    const dir = drag.x > innerWidth - 32 ? 1 : drag.x < 32 ? -1 : 0;
    edges.forEach((e, i) => e.classList.toggle('hot', dir === (i ? 1 : -1)));
    if (dir !== drag.edgeDir) { drag.edgeDir = dir; drag.edgeSince = t; }
    else if (dir && t - drag.edgeSince > 500) { goTo(current + dir, true); drag.edgeSince = t + 300; }
    const top = ($('.appbar')?.getBoundingClientRect().bottom || 0) + 40;
    if (drag.y < top) window.scrollBy(0, -10); else if (drag.y > innerHeight - 48) window.scrollBy(0, 10);
    place(drag.x, drag.y);
    drag.raf = requestAnimationFrame(tick);
  }
  function endDrag() {
    const d = drag;
    cancelAnimationFrame(d.raf);
    d.ghost.remove();
    d.card.classList.remove('dragging');
    edges.forEach(e => e.classList.remove('hot'));
    clearDrop();
    delete root.dataset.dragging;
    drag = null;
    cancelPress();
    return d;
  }
  document.addEventListener('pointermove', e => {
    if (drag) place(e.clientX, e.clientY);
    else if (press && Math.hypot(e.clientX - press.x, e.clientY - press.y) > 8) cancelPress(); // a scroll, not a press
  });
  document.addEventListener('touchmove', e => { if (drag) e.preventDefault(); }, { passive: false });
  document.addEventListener('pointerup', e => {
    if (!drag) { cancelPress(); return; }
    const cell = target(e.clientX, e.clientY), d = endDrag();
    suppressClick = true;
    setTimeout(() => { suppressClick = false; }, 300);
    if (!d.moved) openCard(d.key);
    else if (cell) drop(d.key, cell, e.clientY);
  });
  document.addEventListener('pointercancel', () => { if (drag) endDrag(); cancelPress(); });
  board.addEventListener('contextmenu', e => { if (e.target.closest('.bt-page .card')) e.preventDefault(); });

  /* The board's own popovers (ⓘ and Assign/Owner) as bottom sheets, with the shell's scrim and a Close button */
  const pop = $('.bpop');
  const syncPop = () => {
    if (!pop) return;
    if (pop.hidden) { if (root.dataset.sheet === 'board' && sheet.hidden) delete root.dataset.sheet; return; }
    root.dataset.sheet = 'board';
    if (!pop.querySelector(':scope > .sheet-head')) {
      const title = pop.querySelector(':scope > h3, :scope > .overline')?.textContent || '';
      const head = el('header', 'sheet-head', `<b></b><button class="btn ghost sheet-x" type="button">Close</button>`);
      head.querySelector('b').textContent = title;
      head.querySelector('.sheet-x').addEventListener('click', () => document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' })));
      pop.prepend(head);
    }
  };
  if (pop) new MutationObserver(syncPop).observe(pop, { attributes: true, attributeFilter: ['hidden'], childList: true });

  /* Review states */
  restructure();
  const col = q.get('col');
  if (col) { current = -1; goTo(pages.findIndex(p => p.dataset.col === col), false); }
  if (q.has('compact')) $('[data-cards="compact"]').click();
  if (q.has('nolanes') && isStories) $('#optLanes').click();
  if (q.get('q')) { $('#fq').value = q.get('q'); $('#fq').dispatchEvent(new Event('input')); }
  if (q.get('who')) $('#fwho').querySelector(`[data-who="${q.get('who')}"]`)?.click();
  const s = q.get('sheet') || '';
  requestAnimationFrame(() => {
    if (s === 'filter') btnFilter.click();
    else if (s === 'more') openMore();
    else if (s.startsWith('card:')) openCard(s.slice(5));
    else if (s.startsWith('assign:')) pager.querySelector(`[data-assign="${s.slice(7)}"]`)?.click();
    else if (s.startsWith('dod:')) pager.querySelector(`[data-dod="${s.slice(4)}"]`)?.click();
    // ?drag=KEY&to=<column id>: the card lifted and held over the first card of that column, for a screenshot
    const card = q.get('drag') && cardEl(q.get('drag'));
    if (!card) return;
    const r = card.getBoundingClientRect();
    press = { card, x: r.left + r.width / 2, y: r.top + 20 };
    lift(card, press.x, press.y);
    cancelAnimationFrame(drag.raf);
    const page = pages.find(p => p.dataset.col === q.get('to'));
    if (!page) return;
    goTo(pages.indexOf(page), false);
    requestAnimationFrame(() => {
      const c = page.querySelector(`.cell[data-lane="${drag.lane}"] > .card`) || page.querySelector('.cell');
      const cr = c.getBoundingClientRect();
      place(cr.left + cr.width * .45, cr.top + 14);
    });
  });
})();
