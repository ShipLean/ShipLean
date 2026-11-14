/*
 * SHIP-1465 Feature timeline: sample history and the view model.
 * TL.build(key) returns the same object the MCP tool `feature_timeline` returns (D11). The chart, the rail and the
 * Feature page's key dates all draw from it, so a person and an agent read the same numbers.
 * Decided 14 Nov (Joel): a task bar runs from Active to Resolved, from state history, never from pull requests (D2);
 * one solid bar per task (D5); a story's bar runs from its first task Active to its last task Resolved (D3); just the
 * chart, no computed findings (D8). Reviews are events the review agent logs on a task (D15, proposed), drawn as
 * markers after the bar.
 * Sample data is fictional. "Now" is Sat 24 Oct 2026, 14:00 (shared.js's today).
 *   SHIP-99  Coupons v2    finished Wed 21 Oct: the after-the-fact case (the default on feature-timeline.html)
 *   SHIP-101 Checkout v2   still running: open bars run to now, no critical path yet
 */
(() => {
  const MON = { Aug: 7, Sep: 8, Oct: 9, Nov: 10 };
  const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
  const DOW = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
  const t = s => { const [d, m, hm] = s.split(' '); const [h, mi] = hm.split(':'); return Date.UTC(2026, MON[m], +d, +h, +mi); };
  const NOW = t('24 Oct 14:00');
  const H = 36e5;
  const pad = n => String(n).padStart(2, '0');
  const iso = ms => (ms == null ? null : new Date(ms).toISOString().replace('.000', ''));
  const d = ms => new Date(ms);
  const day = ms => `${DOW[d(ms).getUTCDay()]} ${d(ms).getUTCDate()} ${MONTHS[d(ms).getUTCMonth()]}`;
  const when = ms => `${day(ms)}, ${pad(d(ms).getUTCHours())}:${pad(d(ms).getUTCMinutes())}`;
  const r1 = n => Math.round(n * 10) / 10;
  const dur = h => (h < 1 ? `${Math.round(h * 60)} min` : h < 24 ? `${r1(h)} h` : r1(h / 24) === 1 ? '1 day' : `${r1(h / 24)} days`);

  /* One Feature's history. Task transitions are state history ShipLean already records (a move or pick_up):
     ['active'|'blocked'|'resolved'|'closed', at, reason?]. Reviews are review events [at, 'approved'|'changes', by].
     edges: [from, to, kind]. depends = a blocked_by/predecessor link or a later wave in the captured plan;
     blocked = the item was blocked part-way by another one (a bug found while testing). */
  const DATA = {
    'SHIP-99': {
      title: 'Coupons v2', state: 'Done', epic: 'Checkout conversion',
      added: { at: '18 Aug 10:20', by: 'ana', source: 'Feature created' },
      planned: { at: '1 Oct 16:45', by: 'agent', source: 'Plan captured: 4 stories and bugs, 9 tasks' },
      finished: { at: '21 Oct 10:05', by: 'priya', source: 'Feature moved to Done' },
      stories: [
        ['SHIP-185', 'story', 'Coupon rules engine', 'tom'],
        ['SHIP-188', 'story', 'Coupon admin page', 'ana'],
        ['SHIP-186', 'story', 'Apply coupon at checkout', 'priya'],
        ['SHIP-189', 'bug', 'Coupon applied twice on double-click', 'agent'],
      ],
      tasks: [
        ['SHIP-191', 'SHIP-185', 'Rules schema and migration', 'agent', [['active', '5 Oct 09:10'], ['resolved', '5 Oct 11:40']], [['6 Oct 10:05', 'approved', 'agent']]],
        ['SHIP-192', 'SHIP-185', 'Stacking rules (percent + fixed)', 'agent', [['active', '6 Oct 10:30'], ['resolved', '6 Oct 14:00'], ['active', '7 Oct 09:30', 'Review asked for changes'], ['resolved', '7 Oct 11:15']], [['7 Oct 09:30', 'changes', 'agent'], ['7 Oct 16:20', 'approved', 'agent']]],
        ['SHIP-193', 'SHIP-185', 'Expiry and usage limits', 'agent', [['active', '6 Oct 10:40'], ['resolved', '6 Oct 13:10']], [['6 Oct 17:00', 'approved', 'agent']]],
        ['SHIP-197', 'SHIP-188', 'Admin list and create form', 'ana', [['active', '6 Oct 13:00'], ['resolved', '8 Oct 11:00']], [['9 Oct 11:00', 'approved', 'tom']]],
        ['SHIP-198', 'SHIP-188', 'Bulk code import', 'ana', [['closed', '16 Oct 17:30']], [], 'Closed in one bulk edit with 11 other items'],
        ['SHIP-194', 'SHIP-186', 'API: validate and apply', 'priya', [['active', '8 Oct 09:00'], ['blocked', '8 Oct 15:00', 'Pricing sandbox down (acme/pricing#77)'], ['active', '12 Oct 10:00'], ['resolved', '12 Oct 16:00']], [['14 Oct 11:30', 'approved', 'agent']]],
        ['SHIP-195', 'SHIP-186', 'UI: coupon field and errors', 'mei', [['active', '14 Oct 13:00'], ['resolved', '15 Oct 12:00']], [['19 Oct 10:15', 'approved', 'agent']]],
        ['SHIP-196', 'SHIP-186', 'E2E: coupon checkout', 'agent', [['active', '19 Oct 11:00'], ['blocked', '19 Oct 13:30', 'Bug SHIP-189, the coupon applied twice'], ['active', '20 Oct 14:40'], ['resolved', '20 Oct 16:00']], [['21 Oct 09:40', 'approved', 'agent']]],
        ['SHIP-199', 'SHIP-189', 'Debounce apply and make it idempotent', 'agent', [['active', '20 Oct 09:00'], ['resolved', '20 Oct 10:20']], [['20 Oct 14:30', 'approved', 'agent']]],
      ],
      edges: [['SHIP-191', 'SHIP-192', 'depends'], ['SHIP-191', 'SHIP-193', 'depends'], ['SHIP-191', 'SHIP-197', 'depends'], ['SHIP-192', 'SHIP-194', 'depends'], ['SHIP-193', 'SHIP-194', 'depends'], ['SHIP-194', 'SHIP-195', 'depends'], ['SHIP-195', 'SHIP-196', 'depends'], ['SHIP-199', 'SHIP-196', 'blocked']],
      retros: { 'SHIP-194': [['Went badly', 'The pricing sandbox was down from Thursday to Monday and nobody noticed until the agent hit it.', 'priya']], 'SHIP-195': [['Can be improved', 'The UI could have started on day one against the API contract instead of waiting for the API task.', 'mei']] },
    },
    'SHIP-101': {
      title: 'Checkout v2', state: 'Active', epic: 'Checkout conversion',
      added: { at: '21 Sep 10:00', by: 'ana', source: 'Feature created' },
      planned: { at: '21 Sep 15:30', by: 'priya', source: 'First stories created (no captured plan)' },
      finished: null,
      stories: [
        ['SHIP-120', 'story', 'Guest checkout', 'priya'],
        ['SHIP-124', 'story', 'Apple Pay button', 'tom'],
        ['SHIP-138', 'bug', 'Totals wrong when coupon removed', 'tom'],
        ['SHIP-144', 'story', 'Address autocomplete', 'mei'],
        ['SHIP-150', 'story', 'Order confirmation email', 'ana'],
        ['SHIP-141', 'story', 'Pay with saved card', 'priya'],
      ],
      notStarted: [['SHIP-158', 'story', 'Gift card balance check'], ['SHIP-160', 'bug', 'Session expires during checkout']],
      tasks: [
        ['SHIP-121', 'SHIP-120', 'Guest session and cart merge', 'priya', [['active', '22 Sep 10:00'], ['resolved', '24 Sep 14:00']], [['25 Sep 16:00', 'approved', 'tom']]],
        ['SHIP-122', 'SHIP-120', 'Guest order lookup page', 'agent', [['active', '28 Sep 09:30'], ['resolved', '28 Sep 12:00']], [['30 Sep 11:00', 'approved', 'agent']]],
        ['SHIP-125', 'SHIP-124', 'Apple Pay merchant validation', 'tom', [['active', '5 Oct 09:30'], ['blocked', '6 Oct 11:00', 'Waiting for the Apple merchant certificate'], ['active', '8 Oct 14:00'], ['resolved', '9 Oct 10:00']], [['12 Oct 11:00', 'approved', 'priya']]],
        ['SHIP-127', 'SHIP-124', 'Apple Pay button on payment step', 'agent', [['active', '12 Oct 13:00'], ['resolved', '12 Oct 15:10']], [['13 Oct 15:00', 'approved', 'agent']]],
        ['SHIP-174', 'SHIP-138', 'Fix and regression test', 'tom', [['active', '19 Oct 09:00'], ['resolved', '19 Oct 16:00']], [['20 Oct 11:00', 'approved', 'agent']]],
        ['SHIP-175', 'SHIP-144', 'Provider spike', 'mei', [['active', '19 Oct 09:00'], ['resolved', '20 Oct 09:30']], []],
        ['SHIP-176', 'SHIP-144', 'Wire into the address form', null, [], []],
        ['SHIP-181', 'SHIP-150', 'Email template', 'ana', [['active', '19 Oct 13:00'], ['resolved', '20 Oct 15:00']], [['21 Oct 10:00', 'approved', 'agent']]],
        ['SHIP-171', 'SHIP-141', 'API: tokenised card lookup', 'priya', [['active', '20 Oct 09:00'], ['resolved', '22 Oct 15:00']], []],
        ['SHIP-172', 'SHIP-141', 'UI: saved card picker', 'mei', [['active', '21 Oct 10:00']], []],
        ['SHIP-173', 'SHIP-141', 'E2E test', null, [], []],
      ],
      edges: [['SHIP-121', 'SHIP-122', 'depends'], ['SHIP-125', 'SHIP-127', 'depends'], ['SHIP-171', 'SHIP-172', 'depends'], ['SHIP-172', 'SHIP-173', 'depends'], ['SHIP-175', 'SHIP-176', 'depends']],
      retros: { 'SHIP-125': [['Went badly', 'We found out on day two that the merchant certificate needs an Apple account admin; asking on day one would have saved two days.', 'tom']] },
    },
  };

  const PEOPLE_NAMES = { priya: 'Priya Shah', tom: 'Tom Okafor', mei: 'Mei Lin', sam: 'Sam Rivera', ana: 'Ana Costa', agent: 'Claude Code' };
  const personJson = p => (p ? { name: PEOPLE_NAMES[p], agent: p === 'agent' } : null);
  const STATE = { active: 'Active', blocked: 'Blocked', resolved: 'Resolved', closed: 'Closed' };

  function build(key) {
    const F = DATA[key];
    if (!F) return null;
    const bars = F.tasks.map(([k, parent, title, who, trans, reviews, note]) => {
      // history: each state with how long the task stayed in it (the last one runs to now if the task isn't done)
      const history = trans.map(([state, at, reason], i) => {
        const from = t(at), done = state === 'resolved' || state === 'closed';
        const to = i + 1 < trans.length ? t(trans[i + 1][1]) : done ? null : NOW;
        return { state, from, to, hours: to == null ? null : r1((to - from) / H), ...(reason ? { reason } : {}) };
      });
      const firstActive = history.find(h => h.state === 'active');
      const last = history[history.length - 1];
      const start = firstActive ? firstActive.from : null;
      const ended = last && (last.state === 'resolved' || last.state === 'closed');
      const end = ended ? last.from : null;
      const state = !trans.length ? 'not_started' : ended ? last.state : 'in_flight';
      return { key: k, type: 'task', title, parent, who, start, end, state,
        inferred: !!(ended && start == null), note: note || null, history,
        reviews: reviews.map(([at, verdict, by]) => ({ at: t(at), verdict, by })),
        hours: start != null ? r1(((end ?? NOW) - start) / H) : null };
    });
    const byKey = Object.fromEntries(bars.map(b => [b.key, b]));
    const stories = F.stories.map(([k, type, title, who]) => {
      const kids = bars.filter(b => b.parent === k), started = kids.filter(b => b.start != null);
      const start = started.length ? Math.min(...started.map(b => b.start)) : null;
      const done = kids.length && kids.every(b => b.end != null);
      const end = done ? Math.max(...kids.map(b => b.end)) : null;
      return { key: k, type, title, who, start, end, tasks: kids.map(b => b.key), hours: start != null ? r1(((end ?? NOW) - start) / H) : null };
    });
    const edges = F.edges.map(([from, to, kind]) => {
      const a = byKey[from], b = byKey[to];
      const wait = kind === 'depends' && a.end != null && b.start != null ? r1((b.start - a.end) / H) : null;
      // blocked part-way: the moment the blocked task went back to Active
      const unblocked = kind === 'blocked' ? b.history.find((h, i) => h.state === 'blocked' && b.history[i + 1])?.to ?? null : null;
      return { from, to, kind, wait_hours: wait, ...(kind === 'blocked' ? { unblocked_at: unblocked } : {}) };
    });

    /* Critical path, once the Feature is Done (D7): from the last task to resolve, walk back through the predecessor
       that released each task (the latest one to resolve before it went Active), plus anything that blocked a task on
       that chain part-way. */
    const finished = F.finished ? t(F.finished.at) : null;
    const critical = [];
    if (finished) {
      let cur = bars.filter(b => b.end != null && !b.inferred).sort((x, y) => y.end - x.end)[0];
      while (cur) {
        critical.unshift(cur.key);
        const c = cur;
        const preds = edges.filter(e => e.to === c.key && e.kind === 'depends').map(e => byKey[e.from]).filter(p => p.end != null && p.end <= c.start + H);
        cur = preds.sort((x, y) => y.end - x.end)[0];
      }
      edges.filter(e => e.kind === 'blocked' && critical.includes(e.to)).forEach(e => critical.push(e.from));
    }
    edges.forEach(e => { e.on_critical_path = critical.includes(e.from) && critical.includes(e.to); });
    bars.forEach(b => { b.on_critical_path = critical.includes(b.key); });

    const recorded = bars.filter(b => b.start != null);
    const execStart = Math.min(...recorded.map(b => b.start));
    const execStartBar = recorded.find(b => b.start === execStart);
    const keyDates = [
      { point: 'added', at: t(F.added.at), by: F.added.by, source: F.added.source },
      { point: 'planned', at: t(F.planned.at), by: F.planned.by, source: F.planned.source },
      { point: 'execution_started', at: execStart, by: execStartBar.who, source: `${execStartBar.key} moved to Active` },
      { point: 'finished', at: finished, by: F.finished?.by ?? null, source: F.finished?.source ?? null },
    ];
    const gaps = keyDates.slice(1).map((k, i) => ({ from: keyDates[i].point, to: k.point, days: r1(((k.at ?? NOW) - keyDates[i].at) / (24 * H)), so_far: k.at == null }));

    return {
      key, title: F.title, state: F.state, epic: F.epic, now: NOW, finished: !!finished,
      key_dates: keyDates, gaps,
      execution: { start: execStart, end: finished, calendar_days: r1(((finished ?? NOW) - execStart) / (24 * H)) },
      stories, bars, edges, critical_path: critical,
      not_started: (F.notStarted || []).map(([k, type, title]) => ({ key: k, type, title })),
      retros: F.retros || {},
    };
  }

  /* The MCP twin: the same view model with times as ISO UTC and people named. No findings (D8). */
  function toJson(m) {
    return {
      feature: { key: m.key, title: m.title, state: m.state },
      as_of: iso(m.now),
      key_dates: m.key_dates.map(k => ({ point: k.point, at: iso(k.at), by: personJson(k.by), source: k.source })),
      gaps: m.gaps,
      execution: { start: iso(m.execution.start), end: iso(m.execution.end), calendar_days: m.execution.calendar_days },
      critical_path: m.critical_path,
      stories: m.stories.map(s => ({ key: s.key, type: s.type, title: s.title, start: iso(s.start), end: iso(s.end), hours: s.hours, tasks: s.tasks })),
      bars: m.bars.map(b => ({ key: b.key, type: b.type, title: b.title, parent: b.parent, assignee: personJson(b.who), state: b.state, start: iso(b.start), end: iso(b.end), hours: b.hours, inferred: b.inferred, ...(b.note ? { note: b.note } : {}), on_critical_path: b.on_critical_path,
        history: b.history.map(h => ({ state: h.state, from: iso(h.from), ...(h.to != null ? { to: iso(h.to), hours: h.hours } : {}), ...(h.reason ? { reason: h.reason } : {}) })),
        reviews: b.reviews.map(r => ({ at: iso(r.at), verdict: r.verdict === 'changes' ? 'changes_requested' : 'approved', by: personJson(r.by) })) })),
      edges: m.edges.map(e => ({ from: e.from, to: e.to, kind: e.kind, wait_hours: e.wait_hours, ...(e.kind === 'blocked' ? { unblocked_at: iso(e.unblocked_at) } : {}), on_critical_path: e.on_critical_path })),
      not_started: m.not_started,
      links: { ui: `/p/SHIP/items/${m.key}/timeline`, agent_runs: 'comes with SHIP-1463' },
    };
  }

  /* The key dates strip: four points, gaps labelled but not drawn to scale (awareness, not detail). The last gap is the
     execution the Gantt chart shows. This is all the Feature page shows (D1, Joel 14 Nov). */
  const POINT = { added: 'Added', planned: 'Planned', execution_started: 'Execution started', finished: 'Finished' };
  function keyDatesHtml(m, compact) {
    const pts = m.key_dates.map((k, i) => {
      const pending = k.at == null;
      return `<div class="kd-pt${pending ? ' pending' : ''}${i >= 2 ? ' exec' : ''}"><span class="kd-dot"></span>
        <span class="kd-l">${POINT[k.point]}</span>
        <span class="kd-d">${pending ? 'Not yet' : day(k.at)}</span>
        <span class="kd-s">${pending ? `${m.gaps[2].days} days in execution so far` : compact ? (k.by ? PEOPLE_NAMES[k.by] : '') : `${k.source}${k.by ? ` · ${PEOPLE_NAMES[k.by]}` : ''}`}</span></div>`;
    });
    const gap = (g, i) => `<div class="kd-gap${i === 2 ? ' exec' : ''}${g.so_far ? ' open' : ''}"><span>${g.days >= 10 ? `${Math.round(g.days)} days` : g.days < 1 ? `${Math.round(g.days * 24)} hours` : `${g.days} days`}${g.so_far ? ' so far' : ''}</span></div>`;
    return `<div class="kd${compact ? ' compact' : ''}" role="list" aria-label="Key dates">${pts[0]}${gap(m.gaps[0], 0)}${pts[1]}${gap(m.gaps[1], 1)}${pts[2]}${gap(m.gaps[2], 2)}${pts[3]}</div>`;
  }

  window.TL = { build, toJson, keyDatesHtml, DATA, NOW, H, day, when, dur, iso, PEOPLE_NAMES, POINT, STATE };
})();
