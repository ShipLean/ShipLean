/*
 * SHIP-1465 Feature timeline: sample history and the view model.
 * TL.build(key) returns the same object the proposed MCP tool `feature_timeline` returns. The chart, the rail's
 * findings and the Feature page summary all draw from it, so a person and an agent read the same numbers.
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

  /* One Feature's history. Task transitions are what ShipLean already records (state history, pick_up, the linked PR):
     ['active'|'review'|'blocked', at, reason?] then ['merged'|'closed', at] when it finished. review = PR open, not merged.
     edges: [from, to, kind]. depends = a blocked_by/predecessor link or a later wave in the captured plan;
     blocked = the item was blocked part-way by another one (a bug found while testing). */
  const DATA = {
    'SHIP-99': {
      title: 'Coupons v2', state: 'Done', epic: 'Checkout conversion', repo: 'acme/storefront',
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
        ['SHIP-191', 'SHIP-185', 'Rules schema and migration', 'agent', '#402', [['active', '5 Oct 09:10'], ['review', '5 Oct 11:40'], ['merged', '6 Oct 10:05']]],
        ['SHIP-192', 'SHIP-185', 'Stacking rules (percent + fixed)', 'agent', '#405', [['active', '6 Oct 10:30'], ['review', '6 Oct 14:00'], ['active', '7 Oct 09:30', 'Changes requested'], ['review', '7 Oct 11:15'], ['merged', '7 Oct 16:20']]],
        ['SHIP-193', 'SHIP-185', 'Expiry and usage limits', 'agent', '#406', [['active', '6 Oct 10:40'], ['review', '6 Oct 13:10'], ['merged', '6 Oct 17:00']]],
        ['SHIP-197', 'SHIP-188', 'Admin list and create form', 'ana', '#404', [['active', '6 Oct 13:00'], ['review', '8 Oct 11:00'], ['merged', '9 Oct 11:00']]],
        ['SHIP-198', 'SHIP-188', 'Bulk code import', 'ana', null, [['closed', '16 Oct 17:30']], 'Closed in one bulk edit with 11 other items'],
        ['SHIP-194', 'SHIP-186', 'API: validate and apply', 'priya', '#409', [['active', '8 Oct 09:00'], ['blocked', '8 Oct 15:00', 'Pricing sandbox down (acme/pricing#77)'], ['active', '12 Oct 10:00'], ['review', '12 Oct 16:00'], ['merged', '14 Oct 11:30']]],
        ['SHIP-195', 'SHIP-186', 'UI: coupon field and errors', 'mei', '#411', [['active', '14 Oct 13:00'], ['review', '15 Oct 12:00'], ['merged', '19 Oct 10:15']]],
        ['SHIP-196', 'SHIP-186', 'E2E: coupon checkout', 'agent', '#413', [['active', '19 Oct 11:00'], ['review', '19 Oct 13:30'], ['blocked', '19 Oct 15:00', 'Bug SHIP-189, the coupon applied twice'], ['review', '20 Oct 14:40'], ['merged', '21 Oct 09:40']]],
        ['SHIP-199', 'SHIP-189', 'Debounce apply and make it idempotent', 'agent', '#414', [['active', '20 Oct 09:00'], ['review', '20 Oct 10:20'], ['merged', '20 Oct 14:30']]],
      ],
      edges: [['SHIP-191', 'SHIP-192', 'depends'], ['SHIP-191', 'SHIP-193', 'depends'], ['SHIP-191', 'SHIP-197', 'depends'], ['SHIP-192', 'SHIP-194', 'depends'], ['SHIP-193', 'SHIP-194', 'depends'], ['SHIP-194', 'SHIP-195', 'depends'], ['SHIP-195', 'SHIP-196', 'depends'], ['SHIP-199', 'SHIP-196', 'blocked']],
      retros: { 'SHIP-194': [['Went badly', 'The pricing sandbox was down from Thursday to Monday and nobody noticed until the agent hit it.', 'priya']], 'SHIP-195': [['Can be improved', 'The UI could have started on day one against the API contract instead of waiting for the merge.', 'mei']] },
    },
    'SHIP-101': {
      title: 'Checkout v2', state: 'Active', epic: 'Checkout conversion', repo: 'acme/storefront',
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
        ['SHIP-121', 'SHIP-120', 'Guest session and cart merge', 'priya', '#380', [['active', '22 Sep 10:00'], ['review', '24 Sep 14:00'], ['merged', '25 Sep 16:00']]],
        ['SHIP-122', 'SHIP-120', 'Guest order lookup page', 'agent', '#383', [['active', '28 Sep 09:30'], ['review', '28 Sep 12:00'], ['merged', '30 Sep 11:00']]],
        ['SHIP-125', 'SHIP-124', 'Apple Pay merchant validation', 'tom', '#396', [['active', '5 Oct 09:30'], ['blocked', '6 Oct 11:00', 'Waiting for the Apple merchant certificate'], ['active', '8 Oct 14:00'], ['review', '9 Oct 10:00'], ['merged', '12 Oct 11:00']]],
        ['SHIP-127', 'SHIP-124', 'Apple Pay button on payment step', 'agent', '#399', [['active', '12 Oct 13:00'], ['review', '12 Oct 15:10'], ['merged', '13 Oct 15:00']]],
        ['SHIP-174', 'SHIP-138', 'Fix and regression test', 'tom', '#415', [['active', '19 Oct 09:00'], ['review', '19 Oct 16:00'], ['merged', '20 Oct 11:00']]],
        ['SHIP-175', 'SHIP-144', 'Provider spike', 'mei', null, [['active', '19 Oct 09:00'], ['closed', '20 Oct 09:30']]],
        ['SHIP-176', 'SHIP-144', 'Wire into the address form', null, null, []],
        ['SHIP-181', 'SHIP-150', 'Email template', 'ana', '#416', [['active', '19 Oct 13:00'], ['review', '20 Oct 15:00'], ['merged', '21 Oct 10:00']]],
        ['SHIP-171', 'SHIP-141', 'API: tokenised card lookup', 'priya', '#417', [['active', '20 Oct 09:00'], ['review', '22 Oct 15:00']]],
        ['SHIP-172', 'SHIP-141', 'UI: saved card picker', 'mei', null, [['active', '21 Oct 10:00']]],
        ['SHIP-173', 'SHIP-141', 'E2E test', null, null, []],
      ],
      edges: [['SHIP-121', 'SHIP-122', 'depends'], ['SHIP-125', 'SHIP-127', 'depends'], ['SHIP-171', 'SHIP-172', 'depends'], ['SHIP-172', 'SHIP-173', 'depends'], ['SHIP-175', 'SHIP-176', 'depends']],
      retros: { 'SHIP-125': [['Went badly', 'We found out on day two that the merchant certificate needs an Apple account admin; asking on day one would have saved two days.', 'tom']] },
    },
  };

  const PEOPLE_NAMES = { priya: 'Priya Shah', tom: 'Tom Okafor', mei: 'Mei Lin', sam: 'Sam Rivera', ana: 'Ana Costa', agent: 'Claude Code' };
  const personJson = p => (p ? { name: PEOPLE_NAMES[p], agent: p === 'agent' } : null);

  function build(key) {
    const F = DATA[key];
    if (!F) return null;
    const bars = F.tasks.map(([k, parent, title, who, pr, trans, note]) => {
      const ends = trans.length && ['merged', 'closed'].includes(trans[trans.length - 1][0]) ? trans[trans.length - 1] : null;
      const steps = trans.filter(s => !['merged', 'closed'].includes(s[0]));
      const segments = steps.map(([kind, at, reason], i) => {
        const from = t(at), to = i + 1 < steps.length ? t(steps[i + 1][1]) : ends ? t(ends[1]) : NOW;
        return { kind: kind === 'review' ? 'in_review' : kind, from, to, hours: r1((to - from) / H), ...(reason ? { reason } : {}) };
      });
      const start = segments.length ? segments[0].from : null, end = ends ? t(ends[1]) : null;
      const totals = { active: 0, in_review: 0, blocked: 0 };
      segments.forEach(s => { totals[s.kind] += s.hours; });
      Object.keys(totals).forEach(x => { totals[x] = r1(totals[x]); });
      const state = !trans.length ? 'not_started' : ends ? 'finished' : 'in_flight';
      return { key: k, type: 'task', title, parent, who, pr: pr ? `${F.repo}${pr}` : null, start, end,
        end_source: ends ? (ends[0] === 'merged' ? 'pr_merged' : 'closed') : null, state,
        inferred: !!(ends && !segments.length), note: note || null, segments, totals,
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
      const wait = a.end != null && b.start != null ? r1((b.start - a.end) / H) : null;
      return { from, to, kind, wait_hours: kind === 'depends' ? wait : null };
    });

    /* Critical path, only once the Feature is finished: from the last task to merge, walk back through the
       predecessor that released each task (the latest one to finish before it started). Items that blocked a task on
       that chain part-way are on it too. */
    const finished = F.finished ? t(F.finished.at) : null;
    let critical = [];
    if (finished) {
      const last = bars.filter(b => b.end != null && !b.inferred).sort((x, y) => y.end - x.end)[0];
      let cur = last;
      while (cur) {
        critical.unshift(cur.key);
        const preds = edges.filter(e => e.to === cur.key && e.kind === 'depends').map(e => byKey[e.from]).filter(p => p.end != null && p.end <= cur.start + H);
        cur = preds.sort((x, y) => y.end - x.end)[0];
      }
      edges.filter(e => e.kind === 'blocked' && critical.includes(e.to)).forEach(e => critical.push(e.from));
    }
    const onCp = k => critical.includes(k);
    edges.forEach(e => { e.on_critical_path = onCp(e.from) && onCp(e.to); });
    bars.forEach(b => { b.on_critical_path = onCp(b.key); });

    const recorded = bars.filter(b => b.segments.length);
    const execStart = Math.min(...recorded.map(b => b.start));
    const execStartBar = recorded.find(b => b.start === execStart);
    const execEnd = finished ?? NOW;
    const totals = { active: 0, in_review: 0, blocked: 0 };
    recorded.forEach(b => Object.keys(totals).forEach(x => { totals[x] += b.totals[x]; }));
    Object.keys(totals).forEach(x => { totals[x] = r1(totals[x]); });

    /* Findings: what dragged it out, worked out from the bars above. Critical path first, then longest. */
    const findings = [];
    recorded.forEach(b => b.segments.filter(s => s.kind === 'blocked' && s.hours >= 12).forEach(s => findings.push({
      kind: 'blocked', item: b.key, hours: s.hours, from: s.from, to: s.to, reason: s.reason, on_critical_path: b.on_critical_path,
      summary: `${b.key} was blocked for ${dur(s.hours)}: ${s.reason}.` })));
    recorded.forEach(b => b.segments.filter(s => s.kind === 'in_review' && s.hours >= 24).forEach(s => {
      const weekend = [...Array(Math.ceil(s.hours / 24) + 1)].some((_, i) => [0, 6].includes(d(s.from + i * 24 * H).getUTCDay()));
      const open = b.end == null && s.to === NOW;
      findings.push({ kind: 'review_wait', item: b.key, hours: s.hours, from: s.from, to: s.to, spans_weekend: weekend, still_waiting: open, on_critical_path: b.on_critical_path,
        summary: open ? `${b.key}'s pull request has waited ${dur(s.hours)} for review so far${weekend ? ', over a weekend' : ''}.` : `${b.key}'s pull request waited ${dur(s.hours)} to merge${weekend ? ', over a weekend' : ''}.` });
    }));
    edges.filter(e => e.kind === 'depends').forEach(e => {
      const a = byKey[e.from], b = byKey[e.to];
      if (a.parent === b.parent && a.end != null && b.start != null && b.start >= a.end && a.hours >= 120) findings.push({
        kind: 'waited_for_dependency', item: b.key, waited_on: a.key, hours: r1((b.start - a.start) / H), on_critical_path: b.on_critical_path,
        summary: `${b.key} waited for ${a.key} to merge, so it started ${dur((b.start - a.start) / H)} after ${a.key} did.` });
    });
    // Nothing in flight for more than a day during execution.
    const spans = recorded.map(b => [b.start, b.end ?? NOW]).sort((x, y) => x[0] - y[0]);
    let reach = spans[0][1];
    spans.slice(1).forEach(([s, e]) => {
      if (s - reach >= 72 * H) findings.push({ kind: 'idle', item: null, hours: r1((s - reach) / H), from: reach, to: s, on_critical_path: false,
        summary: `Nothing was in flight for ${dur((s - reach) / H)} (${when(reach)} to ${when(s)}).` });
      reach = Math.max(reach, e);
    });
    bars.filter(b => b.inferred).forEach(b => findings.push({ kind: 'no_history', item: b.key, hours: null, on_critical_path: false,
      summary: `${b.key} has no recorded start and no pull request (${b.note}), so it shows as a marker, not a bar.` }));
    findings.sort((x, y) => (y.on_critical_path - x.on_critical_path) || ((y.hours ?? -1) - (x.hours ?? -1)));

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
      execution: { start: execStart, end: finished, calendar_days: r1((execEnd - execStart) / (24 * H)), task_hours: totals },
      stories, bars, edges, critical_path: critical, findings,
      not_started: (F.notStarted || []).map(([k, type, title]) => ({ key: k, type, title })),
      retros: F.retros || {},
    };
  }

  /* The MCP twin: the same view model with times as ISO UTC and people named. */
  function toJson(m) {
    return {
      feature: { key: m.key, title: m.title, state: m.state },
      as_of: iso(m.now),
      key_dates: m.key_dates.map(k => ({ point: k.point, at: iso(k.at), by: personJson(k.by), source: k.source })),
      gaps: m.gaps,
      execution: { start: iso(m.execution.start), end: iso(m.execution.end), calendar_days: m.execution.calendar_days, task_hours: m.execution.task_hours },
      critical_path: m.critical_path,
      findings: m.findings.map(f => ({ kind: f.kind, item: f.item, ...(f.waited_on ? { waited_on: f.waited_on } : {}), hours: f.hours, ...(f.from != null ? { from: iso(f.from), to: iso(f.to) } : {}), ...(f.reason ? { reason: f.reason } : {}), ...(f.spans_weekend != null ? { spans_weekend: f.spans_weekend } : {}), on_critical_path: f.on_critical_path, summary: f.summary })),
      stories: m.stories.map(s => ({ key: s.key, type: s.type, title: s.title, start: iso(s.start), end: iso(s.end), tasks: s.tasks })),
      bars: m.bars.map(b => ({ key: b.key, type: b.type, title: b.title, parent: b.parent, assignee: personJson(b.who), state: b.state, start: iso(b.start), end: iso(b.end), end_source: b.end_source, pull_request: b.pr, inferred: b.inferred, ...(b.note ? { note: b.note } : {}), on_critical_path: b.on_critical_path, hours: b.totals, segments: b.segments.map(s => ({ kind: s.kind, from: iso(s.from), to: iso(s.to), hours: s.hours, ...(s.reason ? { reason: s.reason } : {}) })) })),
      edges: m.edges,
      not_started: m.not_started,
      links: { ui: `/p/SHIP/items/${m.key}/timeline`, agent_runs: 'comes with SHIP-1463' },
    };
  }

  /* The key dates strip: four points, gaps labelled but not to scale (awareness, not detail). The last gap is the
     execution the Gantt chart shows. */
  const POINT = { added: 'Added', planned: 'Planned', execution_started: 'Execution started', finished: 'Finished' };
  function keyDatesHtml(m, compact) {
    const pts = m.key_dates.map((k, i) => {
      const pending = k.at == null;
      return `<div class="kd-pt${pending ? ' pending' : ''}${i >= 2 ? ' exec' : ''}"><span class="kd-dot"></span>
        <span class="kd-l">${POINT[k.point]}</span>
        <span class="kd-d">${pending ? 'Not yet' : day(k.at)}</span>
        ${compact ? '' : `<span class="kd-s">${pending ? `${m.gaps[2].days} days in execution so far` : `${k.source}${k.by ? ` · ${PEOPLE_NAMES[k.by]}` : ''}`}</span>`}</div>`;
    });
    const gap = (g, i) => `<div class="kd-gap${i === 2 ? ' exec' : ''}${g.so_far ? ' open' : ''}"><span>${g.days >= 10 ? Math.round(g.days) : g.days < 1 ? `${Math.round(g.days * 24)} hours` : g.days} days${g.so_far ? ' so far' : ''}</span></div>`.replace(' hours days', ' hours');
    return `<div class="kd${compact ? ' compact' : ''}" role="list" aria-label="Key dates">${pts[0]}${gap(m.gaps[0], 0)}${pts[1]}${gap(m.gaps[1], 1)}${pts[2]}${gap(m.gaps[2], 2)}${pts[3]}</div>`;
  }

  /* A thumbnail of the execution chart for the Feature page: one thin line per task, the same colours. */
  function miniHtml(m) {
    const rows = m.bars.filter(b => b.start != null).sort((a, b) => a.start - b.start), rh = 5, gap = 2, h = rows.length * (rh + gap);
    const t0 = m.execution.start, t1 = m.execution.end ?? m.now, x = ms => ((ms - t0) / (t1 - t0)) * 100;
    return `<svg class="tl-mini" width="100%" height="${h}" role="img" aria-label="Execution: ${rows.length} tasks over ${m.execution.calendar_days} days">
      ${rows.map((b, i) => b.segments.map(s => `<rect class="ms ${s.kind}" x="${x(s.from)}%" y="${i * (rh + gap)}" width="${Math.max(0.3, x(s.to) - x(s.from))}%" height="${rh}" rx="1"/>`).join('')).join('')}
      ${m.finished ? '' : `<line class="now" x1="99.8%" x2="99.8%" y1="0" y2="${h}"/>`}</svg>`;
  }

  window.TL = { build, toJson, keyDatesHtml, miniHtml, DATA, NOW, H, day, when, dur, iso, PEOPLE_NAMES, POINT };
})();
