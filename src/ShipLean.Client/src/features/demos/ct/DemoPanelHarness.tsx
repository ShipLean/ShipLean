import type { ProjectDto } from '@shiplean/core/api/projects';
import type { SprintDto } from '@shiplean/core/api/sprints';
import { ToastHost } from '../../../components/ToastHost';
import { LiveDot, useConnectionState, useProjectRealtime } from '../../../realtime';
import { SprintDemoPanel } from '../SprintDemoPanel';

// The tab as the shell hosts it: joined to the project's hub, so a pushed event reaches the panel's hooks.
export function DemoPanelHarness({ project, sprint }: { project: ProjectDto; sprint: SprintDto }) {
  useProjectRealtime(project.key);
  const connection = useConnectionState();
  return (
    <div className="p-4">
      <LiveDot state={connection} />
      <SprintDemoPanel project={project} sprint={sprint} />
      <ToastHost />
    </div>
  );
}
