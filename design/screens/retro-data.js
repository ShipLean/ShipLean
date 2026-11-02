/*
 * Sample retrospectives for the SHIP-82 mockups. Fictional, like shared.js.
 * A retrospective belongs to one work item (story, bug or feature). Agents log one when they finish a story;
 * the conductor logs the feature's one when its last story closes, compiling the story retros.
 * Each point has a category (well | bad | improve | unplanned) and a theme from the project's theme list.
 * Sub-agents also send a raw failure log (blockers, assumptions disproved, friction), kept verbatim.
 */
(() => {
  // Themes are clusters ShipLean builds from the point text (D5). People can rename, merge, or move a point (D8).
  const themes = {
    contracts: { name: 'Contract missing at handoff', tone: 'bad' },
    shots: { name: 'Screenshot baselines need CI', tone: 'bad' },
    merge: { name: 'Shared-branch merge conflicts', tone: 'bad' },
    brief: { name: 'Brief too vague', tone: 'bad' },
    env: { name: 'Local test env: native libs', tone: 'bad' },
    perms: { name: 'Agent tool permissions', tone: 'bad' },
    missed: { name: 'Plan missed a dependency', tone: 'bad' },
    contractsFirst: { name: 'Contracts first worked', tone: 'good' },
    sized: { name: 'Slices sized well', tone: 'good' },
    repro: { name: 'Clear repro steps', tone: 'good' },
    ciShots: { name: 'CI screenshots', tone: 'bad' }, // SHIP-497: a drifted name; the agent proposes merging it into shots.
    none: { name: 'Unthemed', tone: 'none' },
  };

  // Mentions per sprint (Sprint 19 … 24) in went-badly and not-in-the-plan points, for the trend strips.
  const sprintNames = ['S19', 'S20', 'S21', 'S22', 'S23', 'S24'];
  const trend = {
    contracts: [3, 4, 2, 0, 1, 0],
    shots: [1, 2, 2, 3, 2, 3],
    merge: [0, 1, 0, 2, 2, 3],
    brief: [2, 1, 2, 1, 1, 2],
    env: [2, 1, 0, 0, 0, 0],
    perms: [0, 0, 1, 0, 0, 1],
    missed: [1, 0, 1, 0, 0, 2],
    ciShots: [0, 0, 0, 0, 1, 1],
  };

  // Mentions on retros with no sprint (SHIP-497): shown in their own column, never counted towards Recurring or a verdict.
  const noSprint = { contracts: 0, shots: 1, merge: 0, brief: 4, env: 0, perms: 0, missed: 1, ciShots: 0 };
  // Mentions with no theme, per sprint then no sprint: the Unthemed row.
  const unthemed = { trend: [0, 0, 1, 0, 2, 3], noSprint: 6, weeks: [0, 0, 0, 0, 1, 0, 0, 0, 1, 1, 2, 7] };

  // The same mentions by week, Monday to Sunday UTC, by when the point was logged, so no-sprint points land in a week too.
  const weekNames = ['10 Aug', '17 Aug', '24 Aug', '31 Aug', '7 Sep', '14 Sep', '21 Sep', '28 Sep', '5 Oct', '12 Oct', '19 Oct', '26 Oct'];
  const weeks = {
    contracts: [2, 1, 3, 1, 1, 1, 0, 0, 1, 0, 0, 0],
    shots: [1, 0, 1, 1, 2, 0, 1, 2, 1, 1, 2, 2],
    merge: [0, 0, 1, 0, 0, 0, 1, 1, 2, 0, 1, 2],
    brief: [1, 1, 0, 1, 1, 1, 1, 0, 0, 1, 2, 4],
    env: [1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0],
    perms: [0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 1],
    missed: [1, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1, 2],
    ciShots: [0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 1],
  };

  // Action items are a work item type at story/bug level (D6): pulled into a sprint like a story, linked to a theme.
  // closedIn = index into sprintNames when the change shipped; impact compares mentions before and after.
  const actions = [
    { key: 'SHIP-112', type: 'action', fix: 'guide', closedInWeek: 5, title: 'Planning guide: Agent 0 ships every contract before wave 1', theme: 'contracts', raised: 'S20', state: 'Closed', owner: 'priya', closedIn: 2, sprint: 'Sprint 21', points: 2 },
    { key: 'SHIP-109', type: 'action', fix: 'test', closedInWeek: 2, title: 'Add native Playwright libs to the dev host setup script', theme: 'env', raised: 'S19', state: 'Closed', owner: 'tom', closedIn: 1, sprint: 'Sprint 20', points: 1 },
    { key: 'SHIP-133', type: 'action', fix: 'product', closedInWeek: 9, title: 'CI job uploads CT screenshots as an artifact', theme: 'shots', raised: 'S22', state: 'Closed', owner: 'mei', closedIn: 4, sprint: 'Sprint 23', points: 3 },
    { key: 'SHIP-146', type: 'action', fix: 'guide', title: 'Brief template: name the test double for each external service', theme: 'brief', raised: 'S23', state: 'Active', owner: 'ana', sprint: 'Sprint 24', points: 2 },
    { key: 'SHIP-188', type: 'action', fix: 'guide', title: 'Common brief: screenshot baselines come from the CI report', theme: 'shots', raised: 'S24', state: 'New', owner: null, sprint: null, points: 1 },
    { key: 'SHIP-189', type: 'action', fix: 'guide', title: 'Plan gives each lane its own migration range', theme: 'merge', raised: 'S24', state: 'New', owner: null, sprint: 'Sprint 25', points: 2 },
  ];

  // [category, text, theme, root cause (optional), action key (optional), not needed (optional)]
  // A point's decision (SHIP-497): an action key means actioned, none = { reason, by, agent } means not needed, neither means undecided.
  const P = (cat, text, theme, cause, action, none) => ({ cat, text, theme, cause, action, none });

  const retros = [
    {
      id: 'R-305', subject: 'SHIP-101', type: 'feature', title: 'Checkout v2', sprint: 'S24', at: 'Thu 29 Oct 16:40',
      by: 'priya', agent: 'Claude Code · conductor', role: 'conductor', compiledFrom: ['R-301', 'R-302', 'R-304'],
      points: [
        P('well', 'Contracts-first wave 0 held: none of the 3 lanes waited on another.', 'contractsFirst'),
        P('well', 'Every slice fit one context window; no lane needed a handoff mid-task.', 'sized'),
        P('bad', '2 of 3 lanes burned retries on local CT screenshots that cannot run on this host.', 'shots', 'The common brief did not say that baselines come from the CI report, so each lane rediscovered it.', 'SHIP-188'),
        P('bad', 'Lanes A and C both added a migration named 20261027_*; lane C rebased 3 times.', 'merge', 'One shared branch and no per-lane migration range in the plan.', 'SHIP-189'),
        P('improve', 'Common brief: screenshot baselines come from the CI report; do not retry locally.', 'shots', null, 'SHIP-188'),
        P('improve', 'Plan: give each lane its own migration number range.', 'merge', null, 'SHIP-189'),
        P('unplanned', 'Saved card tokens can expire mid-checkout; nothing in the plan re-authenticates the card.', 'missed'),
        P('unplanned', 'The address provider sandbox rate-limits at 50 calls; the plan assumed unlimited test calls.', 'brief', null, null, { reason: 'Covered by SHIP-146: briefs now name the test double.', by: 'priya', agent: 'Claude Code · conductor' }),
      ],
    },
    {
      id: 'R-301', subject: 'SHIP-141', type: 'story', title: 'Pay with saved card', sprint: 'S24', at: 'Wed 28 Oct 11:12',
      by: 'priya', agent: 'Claude Code · lane A', role: 'sub-agent',
      points: [
        P('well', 'The card token contract was on the branch before I started.', 'contractsFirst'),
        P('bad', 'Could not take the saved-card picker screenshot locally; left the baseline for CI.', 'shots'),
        P('improve', 'Say in the brief which baselines must come from CI.', 'shots', null, 'SHIP-188'),
        P('unplanned', 'Tokens expire mid-checkout; no story covers re-auth.', 'missed'),
      ],
      log: {
        blockers: ['CT screenshot run: "Executable doesn\'t exist at …/chrome-headless-shell". Stopped after 3 attempts and reported.'],
        disproved: ['Assumed the card API returns last4. It returns a masked PAN; mapped it in the client.'],
        friction: ['Rebased twice on the shared branch after lane C pushed a migration (about 25 min).'],
      },
    },
    {
      id: 'R-302', subject: 'SHIP-144', type: 'story', title: 'Address autocomplete', sprint: 'S24', at: 'Wed 28 Oct 15:03',
      by: 'mei', agent: 'Claude Code · lane B', role: 'sub-agent',
      points: [
        P('well', 'The provider spike as its own task kept the build slice small.', 'sized'),
        P('bad', 'Provider sandbox returned 429 after 50 calls; 40 minutes lost.', 'brief', 'The brief named the provider but no test double.'),
        P('improve', 'Briefs that touch a third-party API should name the test double.', 'brief', null, 'SHIP-146'),
        P('unplanned', 'The provider rate limit; the plan assumed unlimited test calls.', 'brief'),
      ],
      log: {
        blockers: ['Provider sandbox: HTTP 429 after 50 calls. Halted and reported instead of retrying.'],
        disproved: [],
        friction: ['The address form has two field components; picked the deprecated one first.'],
      },
    },
    {
      id: 'R-304', subject: 'SHIP-150', type: 'story', title: 'Order confirmation email', sprint: 'S24', at: 'Thu 29 Oct 10:21',
      by: 'ana', agent: 'Claude Code · lane C', role: 'sub-agent',
      points: [
        P('well', 'The template preview test made review fast.', 'sized'),
        P('bad', 'Migration name clashed with lane A; 3 rebases on the shared branch.', 'merge'),
        P('improve', 'Give each lane its own migration range.', 'merge', null, 'SHIP-189'),
      ],
      log: {
        blockers: [],
        disproved: ['Assumed migrations are named by lane. They are named by date, so two lanes on the same day collide.'],
        friction: ['3 rebases; the third needed a manual merge of the model snapshot.'],
      },
    },
    {
      id: 'R-303', subject: 'SHIP-138', type: 'bug', title: 'Totals wrong when coupon removed', sprint: 'S24', at: 'Tue 27 Oct 09:30',
      by: 'tom', agent: null, role: 'person',
      points: [P('well', 'The repro steps were exact; fixed in one pass.', 'repro')],
    },
    {
      id: 'R-306', subject: 'SHIP-129', type: 'story', title: 'Sign in with passkey', sprint: 'S24', at: 'Fri 30 Oct 14:55',
      by: 'sam', agent: 'Cursor · composer', role: 'sub-agent', partial: true,
      points: [
        P('bad', 'Password fallback blocked: the CI sandbox does not allow the virtual authenticator.', 'perms', 'The agent has no permission to enable CDP WebAuthn in CI.'),
        P('unplanned', 'Needs a CI permission change before the fallback can be tested.', 'missed'),
      ],
      log: {
        blockers: ['Playwright: "WebAuthn.enable is not allowed". Halted; story rolled over to Sprint 25.'],
        disproved: ['Assumed the CI image allows CDP domains. It blocks WebAuthn.'],
        friction: [],
      },
    },
    {
      id: 'R-307', subject: 'SHIP-101', type: 'feature', title: 'Checkout v2', sprint: 'S24', at: 'Fri 30 Oct 09:40',
      by: 'ana', agent: null, role: 'person',
      points: [
        P('well', 'Pairing on the review checklist caught the masked PAN early.', 'sized'),
        P('improve', 'Show the review checklist in the Feature template.', 'none'),
      ],
    },
    // Retros with no sprint (SHIP-497): most of what agents log, so they appear on no sprint's Retro tab.
    {
      id: 'R-312', subject: 'SHIP-104', type: 'feature', title: 'Ops hygiene', sprint: null, at: 'Mon 2 Nov 09:12',
      by: 'priya', agent: 'Claude Code · conductor', role: 'conductor', compiledFrom: ['R-313'],
      points: [
        P('well', 'Audit log retention shipped behind the existing settings page; no new screen needed.', 'none'),
        P('unplanned', 'Rotating a key logs out every open session; nothing in the plan said so.', 'missed'),
        P('bad', 'Two agents picked different retention defaults because the brief named neither.', 'brief', 'The brief left the default open.'),
        P('improve', 'State in the brief whether retention is per project or per install.', 'none'),
        P('improve', 'State in the brief which default applies when the setting is empty.', 'none'),
        P('improve', 'State in the brief who clears a stale key after rotation.', 'none'),
        P('improve', 'State in the brief whether a rotation event reaches open tabs.', 'none'),
      ],
      log: { blockers: [], disproved: ['Assumed retention was per project. It is per install.'], friction: ['Reworked the settings form once.'] },
    },
    {
      id: 'R-313', subject: 'SHIP-163', type: 'story', title: 'Audit log retention setting', sprint: null, at: 'Sun 1 Nov 18:20',
      by: 'mei', agent: 'Claude Code · lane A', role: 'sub-agent',
      points: [
        P('well', 'The settings page contract was on the branch first.', 'contractsFirst'),
        P('bad', 'Picked a 30-day default; the other lane picked 90.', 'brief', 'The brief named no default.'),
        P('improve', 'Name the default value in the brief.', 'brief'),
      ],
      log: { blockers: [], disproved: [], friction: ['Reworked the default after review.'] },
    },
    {
      id: 'R-314', subject: 'SHIP-126', type: 'bug', title: 'Wrong currency on receipts', sprint: null, at: 'Sat 31 Oct 11:05',
      by: 'ana', agent: 'Cursor · composer', role: 'sub-agent',
      points: [
        P('well', 'Repro steps named the exact order.', 'repro'),
        P('improve', 'Add a receipts test for a non-default currency.', 'none'),
      ],
    },
    {
      id: 'R-315', subject: 'SHIP-199', type: 'bug', title: 'Favicon 404 on sign-in', sprint: null, at: 'Thu 29 Oct 08:15',
      by: 'tom', agent: null, role: 'person',
      points: [P('well', 'Found by the new access log dashboard.', 'none')],
    },
    // Earlier sprints, for search and insights.
    {
      id: 'R-284', subject: 'SHIP-124', type: 'story', title: 'Apple Pay button', sprint: 'S23', at: 'Wed 14 Oct 17:02',
      by: 'tom', agent: 'Claude Code · lane B', role: 'sub-agent',
      points: [
        P('bad', 'Button screenshot differs by 1px between local and CI; baseline rejected twice.', 'shots'),
        P('bad', 'Merge conflict in the checkout route table with lane A.', 'merge'),
        P('improve', 'Take baselines from CI only.', 'shots', null, 'SHIP-133'),
      ],
      log: { blockers: [], disproved: ['Assumed local screenshots match CI.'], friction: ['Baseline rejected twice.'] },
    },
    {
      id: 'R-281', subject: 'SHIP-131', type: 'story', title: 'Rate limiting on login', sprint: 'S23', at: 'Tue 13 Oct 12:40',
      by: 'mei', agent: 'Claude Code · lane A', role: 'sub-agent',
      points: [
        P('well', 'The limiter contract from wave 0 was exactly what I needed.', 'contractsFirst'),
        P('bad', 'The brief did not say which endpoints count as login.', 'brief'),
        P('bad', 'Rebased after lane B changed the route table.', 'merge'),
      ],
      log: { blockers: [], disproved: [], friction: ['Asked the conductor which endpoints count; 20 minutes waiting.'] },
    },
    {
      id: 'R-262', subject: 'SHIP-120', type: 'story', title: 'Guest checkout', sprint: 'S22', at: 'Thu 1 Oct 15:15',
      by: 'priya', agent: 'Claude Code · lane A', role: 'sub-agent',
      points: [
        P('bad', 'Screenshot run failed locally (missing libnss3).', 'shots'),
        P('bad', 'Two lanes edited CheckoutForm at once.', 'merge'),
        P('improve', 'Upload CT screenshots from CI so agents can fetch baselines.', 'shots', null, 'SHIP-133'),
      ],
      log: { blockers: ['libnss3.so: cannot open shared object file'], disproved: [], friction: [] },
    },
    {
      id: 'R-241', subject: 'SHIP-117', type: 'story', title: 'Coupon codes at checkout', sprint: 'S21', at: 'Thu 17 Sep 11:00',
      by: 'sam', agent: 'Claude Code · lane C', role: 'sub-agent',
      points: [
        P('bad', 'Coupon DTO was not on the branch; built against a guess and redid it.', 'contracts', 'Agent 0 did not ship the DTO before wave 1.'),
        P('improve', 'Agent 0 should ship every DTO before wave 1 starts.', 'contracts', null, 'SHIP-112'),
      ],
      log: { blockers: [], disproved: ['Assumed the coupon DTO had a percent field. It has amount + kind.'], friction: ['Redid the form mapping.'] },
    },
  ];

  const catLabel = { well: 'Went well', bad: 'Went badly', improve: 'Can be improved', unplanned: 'Not in the plan' };
  const roleLabel = { conductor: 'Conductor', 'sub-agent': 'Sub-agent', person: 'Person' };
  // SHIP-497 decision 13: the same order everywhere.
  const catOrder = ['well', 'unplanned', 'bad', 'improve'];
  // Which Feature each sampled item sits under, for the Features list and its story rows. null: no Feature.
  const featureOf = { 'SHIP-141': 'SHIP-101', 'SHIP-144': 'SHIP-101', 'SHIP-150': 'SHIP-101', 'SHIP-138': 'SHIP-101', 'SHIP-124': 'SHIP-101', 'SHIP-120': 'SHIP-101', 'SHIP-117': 'SHIP-101', 'SHIP-129': 'SHIP-102', 'SHIP-131': 'SHIP-104', 'SHIP-163': 'SHIP-104', 'SHIP-126': 'SHIP-103', 'SHIP-199': null };

  // Sprint mode by default; week mode swaps the series and the window (SHIP-497 decision 4). No sprint never counts towards either rule.
  const SERIES = {
    sprints: { names: sprintNames, counts: trend, close: 'closedIn', window: 4, need: 3, unit: 'sprint' },
    weeks: { names: weekNames, counts: weeks, close: 'closedInWeek', window: 6, need: 3, unit: 'week' },
  };
  let series = SERIES.sprints;
  const setSeries = id => { series = SERIES[id]; };
  const getSeries = () => series;

  // Recurring = mentioned in 3 or more of the last 4 sprints with no action that helped.
  function verdict(themeId) {
    const t = series.counts[themeId]; if (!t) return null;
    const done = actions.filter(a => a.theme === themeId && a.state === 'Closed' && a[series.close] !== undefined);
    if (!done.length) return null;
    const at = Math.max(...done.map(a => a[series.close]));
    const before = t.slice(0, at + 1), after = t.slice(at + 1);
    const avg = xs => xs.reduce((a, v) => a + v, 0) / xs.length;
    if (!after.length) return { at, before: avg(before), after: null, label: 'Too early', tone: '' };
    const b = avg(before), a = avg(after);
    return { at, before: b, after: a, label: a <= b * 0.5 ? 'Helped' : a < b ? 'Some effect' : 'No effect', tone: a <= b * 0.5 ? 'ok' : a < b ? 'warn' : 'danger' };
  }
  function recurring(themeId) {
    const t = series.counts[themeId]; if (!t) return false;
    const recent = t.slice(-series.window).filter(n => n > 0).length >= series.need;
    const v = verdict(themeId);
    return recent && (!v || v.tone !== 'ok');
  }

  // Small trend strip: one bar per sprint, a marker where an action shipped.
  function strip(themeId, { w = 96, h = 22 } = {}) {
    const t = series.counts[themeId]; if (!t) return '';
    const max = 4, bw = w / t.length;
    const v = verdict(themeId);
    const bars = t.map((n, i) => {
      const bh = Math.max(n ? (n / max) * (h - 4) : 1, 1);
      return `<rect x="${i * bw + 1.5}" y="${h - bh}" width="${bw - 3}" height="${bh}" rx="1.5" fill="${n ? 'var(--color-warn)' : 'var(--color-border)'}"><title>${series.names[i]}: ${n} mention${n === 1 ? '' : 's'}</title></rect>`;
    }).join('');
    const mark = v ? `<line x1="${(v.at + 1) * bw}" x2="${(v.at + 1) * bw}" y1="0" y2="${h}" stroke="var(--color-ok)" stroke-width="1.5" stroke-dasharray="2 2"/>` : '';
    return `<svg class="strip" viewBox="0 0 ${w} ${h}" width="${w}" height="${h}" role="img" aria-label="${themes[themeId].name}: mentions per ${series.unit} ${t.join(', ')}">${bars}${mark}</svg>`;
  }

  window.RETRO = { themes, trend, sprintNames, weeks, weekNames, noSprint, unthemed, actions, retros, catLabel, roleLabel, catOrder, featureOf, setSeries, getSeries, verdict, recurring, strip };
})();
