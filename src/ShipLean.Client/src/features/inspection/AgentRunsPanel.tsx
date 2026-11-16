import type { ProjectDto } from '@shiplean/core/api/projects';
import { AgentRunsSection } from './AgentRunsSection';
import { useDesktopWidth } from './useDesktopWidth';

// Shows the Agent runs section when the item's project has Inspection on and the screen is desktop width (D15), and
// otherwise nothing, so no inspection request goes out. `enabled` is the work item's `inspectionEnabled`.
export function AgentRunsPanel({
  project,
  itemKey,
  enabled,
}: {
  project: ProjectDto;
  itemKey: string;
  enabled: boolean;
}) {
  const desktop = useDesktopWidth();
  if (!enabled || !desktop) return null;
  return <AgentRunsSection project={project} itemKey={itemKey} />;
}
