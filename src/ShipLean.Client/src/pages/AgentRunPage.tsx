import { useParams } from 'react-router-dom';
import { AgentRunNarrow } from '../features/inspection/AgentRunNarrow';
import { RunPage } from '../features/inspection/RunPage';
import { useDesktopWidth } from '../features/inspection/useDesktopWidth';
import { pagesTestIds } from '../features/pages/testIds';
import { pageRouteIds } from './pageRouteIds';
import { ProjectScreen } from './ProjectScreen';

// One agent run of a work item, full width. Under 900px it is one line and a link back (D15).
export function AgentRunPage() {
  const { itemKey = '', runId = '' } = useParams();
  const desktop = useDesktopWidth();
  return (
    <ProjectScreen routeId={pageRouteIds.agentRun} testId={pagesTestIds.agentRunPage}>
      {(project) =>
        desktop ? (
          <RunPage project={project} itemKey={itemKey} runId={runId} />
        ) : (
          <AgentRunNarrow project={project.key} itemKey={itemKey} />
        )
      }
    </ProjectScreen>
  );
}
