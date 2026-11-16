/*
 * SHIP-1398 Inspection: sample history and the view model.
 * INS.build(opts) returns the same object the proposed MCP tool `inspection_metrics` returns, so the page, the drill-down
 * and an agent read the same numbers (as SHIP-1465's TL.build does for the timeline).
 * Decided 15 Nov (Joel, Q1–Q5 on SHIP-1398):
 *   Q1 a task's lead time ends at its last commit, from a new optional committed_at on report_task's commits. Existing
 *      installs get a one-off backfill. With no commit time the end falls back to the first move to Resolved.
 *   Q2 an escaping bug is every Bug unless it is marked Found before release (a new optional field).
 *   Q3 the period is a sprint; weeks are for when the project turns sprints off (a future project setting).
 *   Q4 the headline is the average (mean) lead time.
 *   Q5 one Feature: lead time is wave 1, escaping bugs wave 2, cost wave 3 (after SHIP-1463 agent runs).
 * Sample data is fictional. "Now" is Sat 24 Oct 2026, 14:00 (shared.js's today), midway through Sprint 24.
 */
(() => {
  const H = 36e5, DAY = 24 * H;
  const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
  const DOW = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
  const MON = Object.fromEntries(MONTHS.map((m, i) => [m, i]));
  const t = s => { const [d, m, hm = '00:00'] = s.split(' '); const [h, mi] = hm.split(':'); return Date.UTC(2026, MON[m], +d, +h, +mi); };
  const NOW = t('24 Oct 14:00');
  const pad = n => String(n).padStart(2, '0');
  const dt = ms => new Date(ms);
  const day = ms => `${DOW[dt(ms).getUTCDay()]} ${dt(ms).getUTCDate()} ${MONTHS[dt(ms).getUTCMonth()]}`;
  const dm = ms => `${dt(ms).getUTCDate()} ${MONTHS[dt(ms).getUTCMonth()]}`;
  const when = ms => `${day(ms)}, ${pad(dt(ms).getUTCHours())}:${pad(dt(ms).getUTCMinutes())}`;
  const iso = ms => (ms == null ? null : new Date(ms).toISOString().replace('.000', ''));
  const r1 = n => Math.round(n * 10) / 10;
  // Lead time reads in hours under a day and in days above it, always one decimal.
  const dur = h => (h == null ? '—' : h < 24 ? `${r1(h)} h` : `${(h / 24).toFixed(1)} d`);
  const mean = a => (a.length ? a.reduce((x, y) => x + y, 0) / a.length : null);

  // Agents are people too (Person.agent). They get a robot avatar and their connection name.
  const agents = {
    claude: { name: 'Claude Code', initials: 'CC' },
    cursor: { name: 'Cursor', initials: 'Cu' },
    codex: { name: 'Codex', initials: 'Cx' },
  };
  const isAgent = w => w in agents;

  // Sprints 16–24, two weeks each. A sprint belongs to the calendar quarter its end date falls in (Q4 assumption).
  const SPRINTS = [];
  for (let n = 16, s = t('29 Jun'); n <= 26; n++, s += 14 * DAY) {
    const end = s + 11 * DAY + 23 * H;
    const q = `Q${Math.floor(dt(end).getUTCMonth() / 3) + 1} ${dt(end).getUTCFullYear()}`;
    SPRINTS.push({ id: 's' + n, name: 'Sprint ' + n, start: s, end, q, current: s <= NOW && NOW <= end, future: s > NOW });
  }
  const sprintOf = ms => SPRINTS.find(s => s.start <= ms && ms <= s.end + 2 * DAY); // weekend after a sprint still counts

  const FEATURES = {
    'SHIP-99': 'Coupons v2', 'SHIP-101': 'Checkout v2', 'SHIP-102': 'Mobile sign-in', 'SHIP-103': 'Reporting',
    'SHIP-108': 'Dashboard widgets', 'SHIP-104': 'Ops hygiene', 'SHIP-96': 'Search relevance', 'SHIP-95': 'Order history',
  };
  // Which Features were running in which sprints, so generated tasks land on plausible ones.
  const running = {
    s16: ['SHIP-95', 'SHIP-96'], s17: ['SHIP-95', 'SHIP-96'], s18: ['SHIP-96', 'SHIP-103'], s19: ['SHIP-96', 'SHIP-103', 'SHIP-104'],
    s20: ['SHIP-103', 'SHIP-102', 'SHIP-104'], s21: ['SHIP-103', 'SHIP-102', 'SHIP-101'], s22: ['SHIP-101', 'SHIP-102', 'SHIP-99'],
    s23: ['SHIP-99', 'SHIP-101', 'SHIP-102'], s24: ['SHIP-101', 'SHIP-102', 'SHIP-108'],
  };
  const TITLES = ['API endpoint and contract test', 'Migration and backfill', 'UI: form and validation', 'E2E happy path', 'Feature flag and rollout', 'Error states and retries', 'Telemetry events', 'Copy and empty states', 'Accessibility pass', 'Load test and index', 'Contract tests against the gateway', 'Settings page section', 'Background job and alerting', 'Docs and runbook', 'Refactor the shared client'];

  // Deterministic sample: the same numbers on every load and in every screenshot.
  let seed = 1398;
  const rnd = () => { seed = (seed * 1664525 + 1013904223) % 4294967296; return seed / 4294967296; };
  const pick = a => a[Math.floor(rnd() * a.length)];

  /* A task's record. Everything here is state history ShipLean already keeps, plus commit times (new, Q1).
       start      first move to an Active-category state, or pick_up, whichever came first
       commits    [[sha, committed_at|null, subject]]: committed_at comes from report_task (reported) or the backfill
       resolved   first move to a Resolved-category state
       blocked    hours with the Blocked tag inside start → end
       rework     hours after the first resolve (or a changes-requested review) that still produced commits
     The lead time is start → last commit; with no commit time it is start → resolved, and the row says so. */
  const TASKS = [];
  let keyNo = 400;
  const add = o => TASKS.push({ reviews: 0, runs: 0, blocked: 0, rework: 0, blockedWhy: null, ...o });

  // Hand-written: SHIP-99 Coupons v2's tasks, the same history as feature-timeline.html (SHIP-1465).
  [
    ['SHIP-191', 'SHIP-185', 'Rules schema and migration', 'claude', '5 Oct 09:10', '5 Oct 11:32', '5 Oct 11:40', 0, 0, 1, 2],
    ['SHIP-192', 'SHIP-185', 'Stacking rules (percent + fixed)', 'claude', '6 Oct 10:30', '7 Oct 11:05', '6 Oct 14:00', 0, 21.1, 2, 4],
    ['SHIP-193', 'SHIP-185', 'Expiry and usage limits', 'claude', '6 Oct 10:40', '6 Oct 13:02', '6 Oct 13:10', 0, 0, 1, 1],
    ['SHIP-197', 'SHIP-188', 'Admin list and create form', 'ana', '6 Oct 13:00', '8 Oct 10:40', '8 Oct 11:00', 0, 0, 1, 0],
    ['SHIP-194', 'SHIP-186', 'API: validate and apply', 'priya', '8 Oct 09:00', '12 Oct 15:20', '12 Oct 16:00', 91, 0, 1, 1, 'Pricing sandbox down (acme/pricing#77)'],
    ['SHIP-195', 'SHIP-186', 'UI: coupon field and errors', 'mei', '14 Oct 13:00', '15 Oct 11:40', '15 Oct 12:00', 0, 0, 1, 0],
    ['SHIP-196', 'SHIP-186', 'E2E: coupon checkout', 'claude', '19 Oct 11:00', '20 Oct 15:48', '20 Oct 16:00', 25.2, 0, 1, 3, 'Bug SHIP-189, the coupon applied twice'],
    ['SHIP-199', 'SHIP-189', 'Debounce apply and make it idempotent', 'claude', '20 Oct 09:00', '20 Oct 10:12', '20 Oct 10:20', 0, 0, 1, 1],
  ].forEach(([key, parent, title, who, s, c, r, blocked, rework, reviews, runs, blockedWhy]) => add({
    key, parent, parentType: parent === 'SHIP-189' ? 'bug' : 'story', title, feature: 'SHIP-99', who,
    start: t(s), commits: [['9f3c2a1', t(c), title], ...(rework ? [['b71e0d4', t(c) - 4 * H, 'First pass']] : [])], resolved: t(r),
    blocked, rework, reviews, runs, blockedWhy, src: 'reported',
  }));
  // SHIP-198 was closed in a bulk edit with no start recorded: it has no lead time and is listed as left out.
  const LEFT_OUT = [{ key: 'SHIP-198', title: 'Bulk code import', feature: 'SHIP-99', who: 'ana', at: t('16 Oct 17:30'), why: 'Closed in one bulk edit with no start recorded' }];

  // Generated: the rest of each sprint. Mean lead time drifts down as the agents take more of the work, with a bad
  // Sprint 23 (the pricing sandbox outage) and a Sprint 24 that is only half done.
  const shape = { s16: [11, 34], s17: [12, 31], s18: [12, 33], s19: [13, 27], s20: [14, 29], s21: [15, 24], s22: [16, 21], s23: [13, 20], s24: [9, 15] };
  SPRINTS.filter(s => shape[s.id]).forEach(s => {
    const [n, target] = shape[s.id];
    const lastDay = s.current ? NOW - 2 * H : s.end - 8 * H;
    for (let i = 0; i < n; i++) {
      const agent = rnd() < (+s.id.slice(1) - 10) / 14;
      const who = agent ? pick(['claude', 'claude', 'cursor', 'codex']) : pick(['priya', 'tom', 'mei', 'sam', 'ana']);
      // Agents finish in hours, people in a day or two; a few long tails.
      let lead = agent ? 1.5 + rnd() * target * 0.6 : target * (0.6 + rnd() * 1.4);
      let blocked = 0, rework = 0, blockedWhy = null;
      if (rnd() < 0.12) { blocked = 8 + rnd() * 30; lead += blocked; blockedWhy = pick(['Waiting on a staging environment', 'Waiting on a decision from the owner', 'Upstream API change not deployed']); }
      if (rnd() < 0.25) { rework = 2 + rnd() * 14; lead += rework; }
      const end = s.start + 6 * H + rnd() * (lastDay - s.start - 6 * H);
      const start = end - lead * H;
      const commitSrc = +s.id.slice(1) >= 22 ? (rnd() < 0.9 ? 'reported' : 'resolved') : (rnd() < 0.8 ? 'backfilled' : 'resolved');
      const tail = 0.2 + rnd() * (agent ? 3 : 10);
      const feature = pick(running[s.id]);
      add({
        key: 'SHIP-' + keyNo++, parent: null, parentType: rnd() < 0.15 ? 'bug' : 'story', title: pick(TITLES), feature, who,
        start, commits: commitSrc === 'resolved' ? [['', null, '']] : [['', end, '']], resolved: commitSrc === 'resolved' ? end : end + tail * H,
        blocked, rework, blockedWhy, reviews: agent ? 1 + (rework ? 1 : 0) : rnd() < 0.7 ? 1 : 0, runs: agent ? 1 + Math.floor(rnd() * 3) + (rework ? 1 : 0) : rnd() < 0.4 ? 1 : 0, src: commitSrc,
      });
    }
  });
  // Commit shas for the generated rows.
  TASKS.forEach(k => k.commits.forEach(c => { if (!c[0]) { c[0] = Math.floor(rnd() * 0xfffffff).toString(16).padStart(7, '0'); c[2] = k.title; } }));

  // ?coverage=low: an install where the commit-time backfill has not run, so older tasks end at Resolved.
  if (new URLSearchParams(location.search).get('coverage') === 'low') TASKS.forEach(k => { if (k.src === 'backfilled') { k.src = 'resolved'; k.commits = k.commits.map(c => [c[0], null, c[2]]); } });

  // Derived per task: end, lead, the split. Active is what is left after blocked and rework.
  TASKS.forEach(k => {
    const last = Math.max(...k.commits.map(c => c[1] ?? -Infinity));
    k.lastCommit = Number.isFinite(last) ? last : null;
    k.end = k.lastCommit ?? k.resolved;
    k.lead = (k.end - k.start) / H;
    k.active = Math.max(0.2, k.lead - k.blocked - k.rework);
    k.tail = k.lastCommit != null ? Math.max(0, (k.resolved - k.lastCommit) / H) : 0;
    k.sprint = sprintOf(k.end)?.id;
    k.parentKey = k.parent || 'SHIP-' + (300 + (parseInt(k.key.slice(5), 10) % 60));
    // Tokens per task (wave 3, from SHIP-1463 runs). Agents' own runs, plus a little for people who used an agent.
    k.tokens = isAgent(k.who) ? Math.round((180 + k.active * 95 + k.rework * 140) * (0.8 + rnd() * 0.5)) * 1000 : k.runs ? Math.round((60 + rnd() * 200)) * 1000 : 0;
    k.model = isAgent(k.who) ? pick(['strong', 'strong', 'fast']) : null;
  });

  /* Escaping bugs (wave 2). Every Bug counts unless it is marked Found before release (Q2). Bugs are counted in the
     period they were filed. causedBy is the later Caused by link (bug idea 1), shown where someone set it. */
  const BUGS = [
    // [key, title, severity, filed, feature, foundBeforeRelease, causedBy, fixedHours]
    ['SHIP-212', 'Search returns archived products', 2, '8 Jul 10:00', 'SHIP-96', false, null, 30],
    ['SHIP-218', 'Order history paging skips a page', 3, '16 Jul 14:00', 'SHIP-95', false, 'SHIP-205', 12],
    ['SHIP-221', 'Typo in empty state', 4, '21 Jul 09:00', 'SHIP-95', true, null, 1],
    ['SHIP-227', 'Ranking ignores stock level', 2, '29 Jul 11:00', 'SHIP-96', false, null, 44],
    ['SHIP-233', 'Report totals off by one day (time zones)', 2, '5 Aug 15:00', 'SHIP-103', false, 'SHIP-229', 20],
    ['SHIP-236', 'Sort order lost after refresh', 3, '12 Aug 10:00', 'SHIP-96', false, null, 6],
    ['SHIP-240', 'CSV export breaks on commas', 3, '20 Aug 13:00', 'SHIP-103', true, null, 3],
    ['SHIP-244', 'Audit log missing the actor', 3, '27 Aug 09:30', 'SHIP-104', false, null, 9],
    ['SHIP-248', 'Login loops on Safari private mode', 1, '2 Sep 08:10', 'SHIP-102', false, 'SHIP-241', 15],
    ['SHIP-251', 'Chart legend overlaps on small screens', 4, '3 Sep 16:00', 'SHIP-103', false, null, 2],
    ['SHIP-256', 'Report filter resets on back', 3, '10 Sep 11:00', 'SHIP-103', true, null, 4],
    ['SHIP-259', 'Password reset link expires too soon', 2, '16 Sep 10:00', 'SHIP-102', false, null, 8],
    ['SHIP-138', 'Totals wrong when coupon removed', 2, '22 Sep 13:00', 'SHIP-101', false, 'SHIP-122', 7],
    ['SHIP-263', 'Guest checkout loses the address', 2, '29 Sep 09:00', 'SHIP-101', false, null, 11],
    ['SHIP-189', 'Coupon applied twice on double-click', 2, '19 Oct 13:30', 'SHIP-99', true, null, 1.2],
    ['SHIP-266', 'Coupon expiry uses server time zone', 3, '9 Oct 10:00', 'SHIP-99', false, 'SHIP-193', 5],
    ['SHIP-268', 'Apple Pay button shows on Android', 3, '13 Oct 15:00', 'SHIP-101', false, 'SHIP-127', 3],
    ['SHIP-153', 'Crash when biometric prompt is cancelled', 1, '15 Oct 18:00', 'SHIP-102', false, 'SHIP-177', null],
    ['SHIP-160', 'Session expires during checkout', 2, '16 Oct 11:00', 'SHIP-101', false, null, null],
    ['SHIP-270', 'Saved card shows the wrong expiry', 3, '21 Oct 10:00', 'SHIP-101', false, 'SHIP-171', null],
  ].map(([key, title, sev, filed, feature, fbr, causedBy, fixedHours]) => ({ key, title, sev, filed: t(filed), feature, fbr, causedBy, fixedHours, sprint: sprintOf(t(filed))?.id }));
  // Merged pull requests linked to an item in the project, by the sprint they merged in (link_state.merged_at).
  const MERGED_PRS = { s16: 17, s17: 19, s18: 18, s19: 22, s20: 24, s21: 27, s22: 31, s23: 30, s24: 16 };
  const SEV_WEIGHT = { 1: 8, 2: 4, 3: 2, 4: 1 }; // a project setting

  /* Periods. Sprints by default; weeks when the project has sprints turned off (the future setting, Q3). */
  function periods(cadence) {
    if (cadence !== 'weeks') return SPRINTS.filter(s => !s.future && shape[s.id]).map(s => ({ id: s.id, name: s.name, short: s.name.replace('Sprint ', 'S'), start: s.start, end: s.end, q: s.q, current: s.current }));
    const out = [];
    for (let w = t('19 Oct'); w >= t('3 Aug'); w -= 7 * DAY) {
      const end = w + 6 * DAY + 23 * H;
      out.unshift({ id: 'w' + dm(w).replace(' ', ''), name: `Week of ${dm(w)}`, short: dm(w), start: w, end, q: `Q${Math.floor(dt(end).getUTCMonth() / 3) + 1} 2026`, current: w <= NOW && NOW <= end });
    }
    return out;
  }
  const inPeriod = (p, ms) => p.start <= ms && ms <= (p.id.startsWith('s') ? p.end + 2 * DAY : p.end);

  // One period's numbers: the mean lead time (Q4) and the mean of each part, so the parts add up to the headline.
  function stats(list) {
    const n = list.length;
    return {
      tasks: n,
      lead_hours: n ? r1(mean(list.map(k => k.lead))) : null,
      active_hours: n ? r1(mean(list.map(k => k.active))) : null,
      blocked_hours: n ? r1(mean(list.map(k => k.blocked))) : null,
      rework_hours: n ? r1(mean(list.map(k => k.rework))) : null,
      unresolved_tail_hours: n ? r1(mean(list.map(k => k.tail))) : null,
      ended_at_resolved: list.filter(k => k.lastCommit == null).length,
    };
  }
  const bugStats = (p, list) => {
    const esc = list.filter(b => !b.fbr), prs = MERGED_PRS[p.id] ?? null;
    return { escaping: esc.length, weighted: esc.reduce((a, b) => a + SEV_WEIGHT[b.sev], 0), found_before_release: list.length - esc.length, merged_prs: prs, per_100_prs: prs ? r1((esc.length / prs) * 100) : null };
  };
  const costStats = list => { const w = list.filter(k => k.tokens); return { tasks_with_runs: w.length, tokens_per_task: w.length ? Math.round(mean(w.map(k => k.tokens))) : null, tokens_total: w.reduce((a, k) => a + k.tokens, 0) }; };

  /* The view model: what the page draws and what inspection_metrics returns.
       range   '6' (last 6 periods, default) | 'q' this quarter | 'lq' last quarter | '4q' last 4 quarters, by quarter
       cadence 'sprints' (default) | 'weeks'
       cut     'none' | 'feature' | 'who'   */
  function build({ range = '6', cadence = 'sprints', cut = 'none' } = {}) {
    const all = periods(cadence);
    const curQ = all[all.length - 1].q, lastQ = 'Q3 2026';
    const shown = range === 'q' ? all.filter(p => p.q === curQ) : range === 'lq' ? all.filter(p => p.q === lastQ) : range === '4q' ? all : all.slice(-6);
    const tasksIn = p => TASKS.filter(k => inPeriod(p, k.end));
    const rows = shown.map((p, i) => {
      const list = tasksIn(p);
      const prev = all[all.indexOf(p) - 1];
      const s = stats(list), ps = prev ? stats(tasksIn(prev)) : null;
      return { period: p, ...s, change_pct: ps?.lead_hours ? Math.round(((s.lead_hours - ps.lead_hours) / ps.lead_hours) * 100) : null, list, bugs: bugStats(p, BUGS.filter(b => inPeriod(p, b.filed))), bugList: BUGS.filter(b => inPeriod(p, b.filed)), cost: costStats(list) };
    });
    // Quarter roll-ups over every period in the quarter, not only the shown ones.
    const quarters = [...new Set(all.map(p => p.q))].map(q => {
      const ps = all.filter(p => p.q === q), list = ps.flatMap(tasksIn);
      return { q, periods: ps.map(p => p.id), to_date: q === curQ, ...stats(list), bugs: bugStats({ id: 'q' }, BUGS.filter(b => ps.some(p => inPeriod(p, b.filed)))), cost: costStats(list) };
    });
    const range_tasks = rows.flatMap(r => r.list);
    let cuts = null;
    if (cut !== 'none') {
      const keyOf = k => (cut === 'feature' ? k.feature : isAgent(k.who) ? k.who : 'people');
      const keys = [...new Set(range_tasks.map(keyOf))];
      cuts = keys.map(key => ({ key, label: cut === 'feature' ? FEATURES[key] : isAgent(key) ? agents[key].name : 'People', cells: rows.map(r => stats(r.list.filter(k => keyOf(k) === key))), all: stats(range_tasks.filter(k => keyOf(k) === key)) }))
        .sort((a, b) => b.all.tasks - a.all.tasks);
    }
    return { cadence, range, rows, quarters, cuts, range_stats: stats(range_tasks), slowest: [...range_tasks].sort((a, b) => b.lead - a.lead).slice(0, 6), left_out: LEFT_OUT.filter(x => shown.some(p => inPeriod(p, x.at))) };
  }

  // The JSON an agent gets from inspection_metrics for the same view (trimmed to what the page uses).
  function toJson(vm) {
    const task = k => ({ key: k.key, title: k.title, feature: k.feature, who: isAgent(k.who) ? agents[k.who].name : k.who, started_at: iso(k.start), ended_at: iso(k.end), ended_by: k.lastCommit != null ? 'last_commit' : 'resolved', lead_hours: r1(k.lead), active_hours: r1(k.active), blocked_hours: r1(k.blocked), rework_hours: r1(k.rework), unresolved_tail_hours: r1(k.tail) });
    return {
      project: 'SHIP', cadence: vm.cadence, range: vm.range, aggregate: 'mean', generated_at: iso(NOW),
      caveat: 'These numbers tell you where to look, not what caused a change.',
      periods: vm.rows.map(r => ({ id: r.period.id, name: r.period.name, start: iso(r.period.start), end: iso(r.period.end), quarter: r.period.q, in_progress: !!r.period.current, lead_time: { tasks: r.tasks, mean_hours: r.lead_hours, mean_active_hours: r.active_hours, mean_blocked_hours: r.blocked_hours, mean_rework_hours: r.rework_hours, change_pct_vs_previous: r.change_pct, ended_at_resolved: r.ended_at_resolved }, escaping_bugs: r.bugs, cost: { tokens_per_task: r.cost.tokens_per_task } })),
      quarters: vm.quarters.map(q => ({ quarter: q.q, to_date: q.to_date, tasks: q.tasks, mean_lead_hours: q.lead_hours, escaping_bugs: q.bugs.escaping })),
      slowest_tasks: vm.slowest.map(task),
      left_out: vm.left_out.map(x => ({ key: x.key, reason: x.why })),
    };
  }

  window.INS = { NOW, H, DAY, SPRINTS, TASKS, BUGS, FEATURES, agents, isAgent, SEV_WEIGHT, MERGED_PRS, build, toJson, periods, dur, r1, mean, when, day, dm, iso };
})();
