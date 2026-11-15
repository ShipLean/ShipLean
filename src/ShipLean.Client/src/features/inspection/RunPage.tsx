import type { ProjectDto } from '@shiplean/core/api/projects';
import { EventTimeline } from './EventTimeline';
import { OverviewStrip } from './OverviewStrip';
import { RowPanel } from './RowPanel';
import { RunBanners } from './RunBanners';
import { RunHeader } from './RunHeader';
import { Waterfall } from './Waterfall';
import { useRunPage } from './inspectionApi';
import { inspectionTestIds as ids } from './testIds';
import { useRunView } from './useRunView';

export interface RunPageProps {
  project: ProjectDto;
  itemKey: string;
  runId: string;
}

// The run page: the run is fetched once, and the view state (zoom, errors only, selected row, find) is the address.
// TODO: the loading, failed and expired states and the real layout replace this scaffold.
export function RunPage({ project, itemKey, runId }: RunPageProps) {
  const view = useRunView();
  const run = useRunPage(project.key, itemKey, runId, view.errorsOnly);
  const page = run.data;
  if (page === undefined) return <div data-testid={ids.runPage} />;
  return (
    <div data-testid={ids.runPage}>
      <RunHeader
        project={project.key}
        itemKey={itemKey}
        header={page.header}
        errorsOnly={view.errorsOnly}
        onErrorsOnly={view.setErrorsOnly}
        query={view.query}
        onQuery={view.setQuery}
      />
      <RunBanners page={page} />
      <OverviewStrip
        overview={page.overview}
        axis={page.axis}
        zoomTurn={view.zoomTurn}
        onZoom={view.setZoomTurn}
      />
      {page.logOnly ? (
        <EventTimeline
          turns={page.turns}
          axis={page.axis}
          selectedRowId={view.selectedRowId}
          onSelect={view.selectRow}
        />
      ) : (
        <Waterfall
          turns={page.turns}
          axis={page.axis}
          selectedRowId={view.selectedRowId}
          onSelect={view.selectRow}
          contentShown={page.contentShown}
        />
      )}
      {view.selectedRowId === null ? null : (
        <RowPanel
          project={project.key}
          itemKey={itemKey}
          runId={runId}
          rowId={view.selectedRowId}
        />
      )}
    </div>
  );
}
