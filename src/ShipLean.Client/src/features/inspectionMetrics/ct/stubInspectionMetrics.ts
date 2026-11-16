// CT data for Inspection (SHIP-1398). The view model is IMPORTED from the pinned fixture (ViewDtoTests pins it to the C#
// DTO), never copied; each variant is the fixture with only what it needs changed. A view task builds further variants in
// its own spec from stubView, without editing this file.
import type { Page } from 'playwright';
import type {
  InspectionCut,
  InspectionMetricsViewDto,
  LeadTimeEndedBy,
  LeadTimeStatsDto,
} from '@shiplean/core/api/views';
import fixtureJson from '../../../../../ShipLean.TestSupport/DtoFixtures/views/inspection-metrics-view.json' with { type: 'json' };

const fixture = fixtureJson as InspectionMetricsViewDto;

/** The pinned view model with `patch` over its top-level fields (a fresh copy each call). */
export function stubView(patch: Partial<InspectionMetricsViewDto> = {}): InspectionMetricsViewDto {
  return { ...(structuredClone(fixture) as InspectionMetricsViewDto), ...patch };
}

const noTasks: LeadTimeStatsDto = {
  tasks: 0,
  meanHours: null,
  meanLabel: null,
  meanActiveHours: null,
  meanActiveLabel: null,
  meanBlockedHours: null,
  meanBlockedLabel: null,
  meanReworkHours: null,
  meanReworkLabel: null,
  meanUnresolvedTailHours: null,
  meanUnresolvedTailLabel: null,
  endedAtResolved: 0,
  changePctVsPrevious: null,
};

/** One task in five ended at a commit time: the coverage banner shows. */
export function stubLowCoverage(): InspectionMetricsViewDto {
  return stubView({ coverage: { tasks: 5, withCommitTime: 1, percent: 20 } });
}

/** Only the running sprint has started: no finished headline, one period, its tasks only. */
export function stubNoFinishedSprint(): InspectionMetricsViewDto {
  const view = stubView();
  const running = view.periods.filter((period) => period.inProgress);
  const ids = new Set(running.map((period) => period.id));
  return {
    ...view,
    headline: { ...view.headline, finished: null, lastQuarter: null },
    periods: running,
    longest: view.longest.filter((task) => task.periodId !== null && ids.has(task.periodId)),
    periodDetail: null,
  };
}

/** Sprints with no task ending in them: every stat empty, no dots, no longest, nothing left out. */
export function stubNoTasks(): InspectionMetricsViewDto {
  const view = stubView();
  const empty = (headline: { leadTime: LeadTimeStatsDto } | null) =>
    headline === null ? null : { ...headline, leadTime: noTasks };
  return {
    ...view,
    cut: 'none',
    cuts: null,
    coverage: { tasks: 0, withCommitTime: 0, percent: null },
    headline: {
      finished: empty(view.headline.finished),
      soFar: empty(view.headline.soFar),
      quarterToDate: empty(view.headline.quarterToDate),
      lastQuarter: empty(view.headline.lastQuarter),
    },
    periods: view.periods.map((period) => ({ ...period, leadTime: noTasks, dots: [] })),
    quarters: view.quarters.map((quarter) => ({ ...quarter, leadTime: noTasks })),
    longest: [],
    leftOut: [],
    periodDetail: null,
  };
}

/** The split table by Feature (the fixture's rows) or by who (the same cells, one person and one agent). */
export function stubCut(cut: Exclude<InspectionCut, 'none'>): InspectionMetricsViewDto {
  const view = stubView();
  const rows = view.cuts ?? [];
  if (cut === 'feature') return { ...view, cut, cuts: rows };
  const who = [
    { key: 'Priya Shah', label: 'Priya Shah', agent: false },
    { key: 'Claude Code', label: 'Claude Code', agent: true },
  ];
  return {
    ...view,
    cut,
    cuts: rows.map((row, i) => ({ ...row, ...who[i % who.length] })),
  };
}

/** One sprint open with a task selected, every task (and the selected one) ended by `endedBy`. */
export function stubPeriod({ endedBy }: { endedBy: LeadTimeEndedBy }): InspectionMetricsViewDto {
  const view = stubView();
  const detail = view.periodDetail;
  if (detail === null) throw new Error('the pinned fixture has no periodDetail');
  return {
    ...view,
    periodDetail: {
      ...detail,
      tasks: detail.tasks.map((task) => ({ ...task, endedBy })),
      selected:
        detail.selected === null
          ? null
          : { ...detail.selected, task: { ...detail.selected.task, endedBy } },
    },
  };
}

/** One sprint open with nothing selected, for its Features tab (`?tab=features`). */
export function stubFeaturesTab(): InspectionMetricsViewDto {
  const view = stubView();
  const detail = view.periodDetail;
  if (detail === null || detail.features.length === 0)
    throw new Error('the pinned fixture has no period Features');
  return { ...view, periodDetail: { ...detail, selected: null } };
}

/** What the Inspection routes saw: each read's path and query, and each usage POST's path (after usage/) and body. */
export interface InspectionMetricsRequests {
  reads: string[];
  usage: { path: string; body: unknown }[];
}

/**
 * Answers GET …/inspection-metrics with `answer` (a view model, or an error status) and the usage POSTs with 204.
 * Register it after stubProjectApi: Playwright tries the newest route first.
 */
export async function routeInspectionMetrics(
  page: Page,
  answer: InspectionMetricsViewDto | { status: 404 | 500 } = stubView(),
): Promise<InspectionMetricsRequests> {
  const seen: InspectionMetricsRequests = { reads: [], usage: [] };
  await page.route('**/api/projects/*/inspection-metrics*', (route) => {
    const url = new URL(route.request().url());
    seen.reads.push(url.pathname + url.search);
    if ('status' in answer) {
      const code = answer.status === 404 ? 'not_found' : 'internal_error';
      return route.fulfill({ status: answer.status, json: { error: 'x', code } });
    }
    return route.fulfill({ json: answer });
  });
  await page.route('**/api/projects/*/usage/inspection-metrics-*', (route) => {
    const request = route.request();
    const path = new URL(request.url()).pathname.replace(/^.*\/usage\//, '');
    seen.usage.push({ path, body: request.postDataJSON() as unknown });
    return route.fulfill({ status: 204 });
  });
  return seen;
}
