export const timelineTestIds = {
  loading: 'timeline-loading',
  loadFailed: 'timeline-load-failed',
  header: 'timeline-header',
  headerBack: 'timeline-header-back',
  headerTitle: 'timeline-header-title',
  headerState: 'timeline-header-state',
  headerEpic: 'timeline-header-epic',
  keyDates: 'timeline-key-dates',
  keyDatePoint: 'timeline-key-date-point',
  keyDateGap: 'timeline-key-date-gap',
  toolbar: 'timeline-toolbar',
  toolbarGroup: 'timeline-toolbar-group',
  toolbarZoom: 'timeline-toolbar-zoom',
  toolbarDeps: 'timeline-toolbar-deps',
  toolbarCp: 'timeline-toolbar-cp',
  toolbarView: 'timeline-toolbar-view',
  toolbarLegend: 'timeline-toolbar-legend',
  chart: 'timeline-chart',
  chartScroll: 'timeline-chart-scroll',
  chartLeft: 'timeline-chart-left',
  chartRow: 'timeline-chart-row',
  chartRowStatus: 'timeline-chart-row-status',
  chartBar: 'timeline-chart-bar',
  chartBracket: 'timeline-chart-bracket',
  chartDiamond: 'timeline-chart-diamond',
  chartReviewDot: 'timeline-chart-review-dot',
  chartCommitTick: 'timeline-chart-commit-tick',
  chartNowLine: 'timeline-chart-now-line',
  chartWeekend: 'timeline-chart-weekend',
  chartSprintBand: 'timeline-chart-sprint-band',
  chartDayLabel: 'timeline-chart-day-label',
  edges: 'timeline-edges',
  edge: 'timeline-edge',
  edgeWaitLabel: 'timeline-edge-wait-label',
  edgeCritical: 'timeline-edge-critical',
  rail: 'timeline-rail',
  railOverview: 'timeline-rail-overview',
  railNotStarted: 'timeline-rail-not-started',
  railSelected: 'timeline-rail-selected',
  railHistoryRow: 'timeline-rail-history-row',
  railReviewRow: 'timeline-rail-review-row',
  railWaitedOn: 'timeline-rail-waited-on',
  railHeldUp: 'timeline-rail-held-up',
  railClose: 'timeline-rail-close',
  json: 'timeline-json',
  jsonCopy: 'timeline-json-copy',
  jsonFieldGuide: 'timeline-json-field-guide',
  typeBadge: 'timeline-type-badge',
  personAvatar: 'timeline-person-avatar',
  // Wave 1 lanes: append your ids inside your own block only.
  // SHIP-2246 (T1a): header KPIs, caption, key dates and the Feature page card
  kpis: 'timeline-kpis',
  kpi: 'timeline-kpi',
  headerCaption: 'timeline-header-caption',
  // end SHIP-2246
  // SHIP-2247 (T1b): row labels column
  chartLeftHead: 'timeline-chart-left-head',
  chartRowTook: 'timeline-chart-row-took',
  chartRowCp: 'timeline-chart-row-cp',
  chartRowAccent: 'timeline-chart-row-accent',
  // end SHIP-2247
  // SHIP-2248 (T1c): chart marks
  chartDurationLabel: 'timeline-chart-duration-label',
  chartOpenEnd: 'timeline-chart-open-end',
  chartInferredNote: 'timeline-chart-inferred-note',
  // end SHIP-2248
  // SHIP-2249 (T1d): dependency arrows and wait labels
  // end SHIP-2249
  // SHIP-2250 (T1e): legend, Reading the chart and the selected-item card
  toolbarSwatch: 'timeline-toolbar-swatch',
  railGlyph: 'timeline-rail-glyph',
  railHint: 'timeline-rail-hint',
  railFacts: 'timeline-rail-facts',
  railAgentChip: 'timeline-rail-agent-chip',
  railCriticalPill: 'timeline-rail-critical-pill',
  railNoStart: 'timeline-rail-no-start',
  railOpen: 'timeline-rail-open',
  railBack: 'timeline-rail-back',
  // end SHIP-2250
  // SHIP-2251 (T1f): JSON view
  // end SHIP-2251
  // SHIP-2252 (T1g): chart axis, Now line and the selected band
  chartWeekTick: 'timeline-chart-week-tick',
  chartNowLabel: 'timeline-chart-now-label',
  chartSelectedBand: 'timeline-chart-selected-band',
  // end SHIP-2252
} as const;
