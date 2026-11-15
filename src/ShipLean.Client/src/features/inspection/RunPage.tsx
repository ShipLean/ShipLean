import { useEffect, useMemo } from 'react';
import { Link } from 'react-router-dom';
import type { ProjectDto } from '@shiplean/core/api/projects';
import { ApiRequestError } from '@shiplean/core/api/client';
import { routePath } from '../../routes';
import { EventTimeline } from './EventTimeline';
import { OverviewStrip } from './OverviewStrip';
import { RowPanel } from './RowPanel';
import { RunBanners } from './RunBanners';
import { RunHeader } from './RunHeader';
import { Waterfall } from './Waterfall';
import { useRunPage } from './inspectionApi';
import { useInspectionUsage } from './page/useInspectionUsage';
import { visibleTurns, zoomedAxis } from './page/runView';
import { inspectionTestIds as ids } from './testIds';
import { useRunView } from './useRunView';
import './page/runpage.css';

export interface RunPageProps {
  project: ProjectDto;
  itemKey: string;
  runId: string;
}

// The run page: the run is fetched once, and the view state (zoom, errors only, selected row, find) is the address.
export function RunPage({ project, itemKey, runId }: RunPageProps) {
  const view = useRunView();
  const run = useRunPage(project.key, itemKey, runId, view.errorsOnly);
  const usage = useInspectionUsage(project.id, itemKey);
  const page = run.data;

  // One runOpened per open of a run.
  useEffect(() => usage('runOpened'), [usage, runId]);

  const axis = useMemo(
    () => (page === undefined ? undefined : zoomedAxis(page.axis, page.overview, view.zoomTurn)),
    [page, view.zoomTurn],
  );
  const turns = useMemo(
    () => (page === undefined ? [] : visibleTurns(page.turns, view.zoomTurn, view.query)),
    [page, view.zoomTurn, view.query],
  );

  if (page === undefined || axis === undefined) {
    return (
      <div data-testid={ids.runPage}>
        {run.error instanceof ApiRequestError && run.error.status === 409 ? (
          <div className="rv-state" data-testid={ids.runLoadFailed} data-reason="store-unreachable">
            <h2>Store unreachable</h2>
            <p>{run.error.message}</p>
          </div>
        ) : run.isError ? (
          <div className="rv-state" data-testid={ids.runLoadFailed}>
            <h2>Couldn&apos;t load this run</h2>
            <p>Reload the page to try again.</p>
          </div>
        ) : (
          <div className="rv-state" data-testid={ids.runLoading}>
            Loading…
          </div>
        )}
      </div>
    );
  }

  if (page.expired) {
    return (
      <div data-testid={ids.runPage}>
        <div className="rv-state" data-testid={ids.runExpired}>
          <h2>This run is no longer available</h2>
          <p>The store no longer keeps its traces and logs.</p>
          <Link to={routePath('workItem', { key: project.key, itemKey })}>
            Back to {itemKey}&apos;s runs
          </Link>
        </div>
      </div>
    );
  }

  const zoom = (turn: number | null) => {
    view.setZoomTurn(turn);
    if (turn !== null) usage('zoomed');
  };
  const errorsOnly = (on: boolean) => {
    view.setErrorsOnly(on);
    if (on) usage('errorsOnly');
  };
  return (
    <div data-testid={ids.runPage}>
      <RunHeader
        project={project.key}
        itemKey={itemKey}
        header={page.header}
        errorsOnly={view.errorsOnly}
        onErrorsOnly={errorsOnly}
        query={view.query}
        onQuery={view.setQuery}
      />
      <RunBanners page={page} />
      <OverviewStrip
        overview={page.overview}
        axis={page.axis}
        zoomTurn={view.zoomTurn}
        onZoom={zoom}
      />
      <div className="rv" data-has-panel={view.selectedRowId !== null}>
        <div className="rv-main">
          {page.logOnly ? (
            <EventTimeline
              turns={turns}
              axis={axis}
              selectedRowId={view.selectedRowId}
              onSelect={view.selectRow}
            />
          ) : (
            <Waterfall
              turns={turns}
              axis={axis}
              selectedRowId={view.selectedRowId}
              onSelect={view.selectRow}
              contentShown={page.contentShown}
            />
          )}
        </div>
        {view.selectedRowId === null ? null : (
          <RowPanel
            project={project.key}
            itemKey={itemKey}
            runId={runId}
            rowId={view.selectedRowId}
          />
        )}
      </div>
    </div>
  );
}
