import { useQuery } from '@tanstack/react-query';
import { useEffect, type ComponentType } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { ApiRequestError } from '@shiplean/core/api/client';
import { getInspectionMetrics, inspectionMetricsKeys } from '@shiplean/core/api/inspectionMetrics';
import type { ProjectDto } from '@shiplean/core/api/projects';
import { postInspectionMetricsViewed } from '@shiplean/core/api/usageApi';
import { InspectionHeader } from '../features/inspectionMetrics/InspectionHeader';
import type { InspectionViewProps } from '../features/inspectionMetrics/InspectionViewProps';
import {
  apiQueryOf,
  queryFromSearch,
  queryToSearch,
  type InspectionView,
} from '../features/inspectionMetrics/inspectionMetricsQuery';
import { JsonView } from '../features/inspectionMetrics/json/JsonView';
import { LeadTimeView } from '../features/inspectionMetrics/leadtime/LeadTimeView';
import { OverviewView } from '../features/inspectionMetrics/overview/OverviewView';
import { PeriodView } from '../features/inspectionMetrics/period/PeriodView';
import { inspectionMetricsTestIds as ids } from '../features/inspectionMetrics/testIds';
import { inspectionMetricsSlots } from './inspectionMetricsSlots';
import { routePath } from '../routes';
import { pageRouteIds } from './pageRouteIds';
import { ProjectScreen } from './ProjectScreen';
import type { SetSearchParams } from './settingsDialogProps';

const VIEWS: Record<InspectionView, ComponentType<InspectionViewProps>> = {
  overview: OverviewView,
  leadtime: LeadTimeView,
  period: PeriodView,
  json: JsonView,
};

// Inspection (desktop only): the header over the one view the address names. The screen reads its view model once, for
// the range, split and sprint in the address.
export function InspectionMetricsPage() {
  const [params, setParams] = useSearchParams();
  return (
    <ProjectScreen routeId={pageRouteIds.inspectionMetrics} testId={ids.page}>
      {(project) => (
        <InspectionMetricsScreen project={project} params={params} setParams={setParams} />
      )}
    </ProjectScreen>
  );
}

function InspectionMetricsScreen({
  project,
  params,
  setParams,
}: {
  project: ProjectDto;
  params: URLSearchParams;
  setParams: SetSearchParams;
}) {
  const query = queryFromSearch(params);
  const apiQuery = apiQueryOf(query);
  const metrics = useQuery({
    queryKey: inspectionMetricsKeys.view(project.id, apiQuery),
    queryFn: () => getInspectionMetrics(project.id, apiQuery),
    // While the sprint stays the same (a new task selected, range or split), the view keeps showing the last answer
    // instead of unmounting into the loading state.
    placeholderData: (previous, previousQuery) =>
      (previousQuery?.queryKey[3] as InspectionMetricsQuery | undefined)?.period === apiQuery.period
        ? previous
        : undefined,
  });
  // One inspection_metrics.viewed per view opened; the usage call never blocks or fails the page.
  useEffect(() => {
    postInspectionMetricsViewed(project.id, query.view).catch(() => undefined);
  }, [project.id, query.view]);

  const view = metrics.data;
  let body;
  if (view !== undefined) {
    const slots = inspectionMetricsSlots(project, view, params, setParams);
    body = (Object.keys(VIEWS) as InspectionView[]).map((name) => {
      const props = slots[name];
      const View = VIEWS[name];
      return props === undefined ? null : <View key={name} {...props} />;
    });
  } else if (
    metrics.error instanceof ApiRequestError &&
    metrics.error.code === 'validation' &&
    apiQuery.period !== undefined
  ) {
    // A sprint id the project doesn't have (an old or edited address): retrying can't help, so offer Lead time.
    const leadTime = queryToSearch({ ...query, view: 'leadtime', period: null, select: null });
    body = (
      <section data-testid={ids.error} className="grid justify-items-start gap-2 p-6">
        <h2 className="text-[length:var(--text-lg)] font-semibold">
          This sprint is not in the project
        </h2>
        <p className="text-ink-muted">The address names a sprint Inspection can't show.</p>
        <Link
          data-testid={ids.periodBack}
          to={routePath(
            'inspectionMetrics',
            { key: project.key },
            Object.fromEntries(leadTime.entries()),
          )}
        >
          ← Lead time
        </Link>
      </section>
    );
  } else if (metrics.isError) {
    body = (
      <section data-testid={ids.error} className="grid justify-items-start gap-2 p-6">
        <h2 className="text-[length:var(--text-lg)] font-semibold">Could not load Inspection</h2>
        <p className="text-ink-muted">The numbers did not load. Try again in a moment.</p>
        <button
          type="button"
          data-testid={ids.errorRetry}
          className="btn-ghost"
          onClick={() => void metrics.refetch()}
        >
          Retry
        </button>
      </section>
    );
  } else {
    body = (
      <p data-testid={ids.loading} className="p-4 text-ink-muted">
        Loading Inspection…
      </p>
    );
  }
  return (
    <>
      <InspectionHeader projectKey={project.key} query={query} caveat={view?.caveat ?? null} />
      {body}
    </>
  );
}
