import type { ProjectDto } from '@shiplean/core/api/projects';
import { useAgentRuns } from './inspectionApi';
import { inspectionTestIds as ids } from './testIds';

export interface AgentRunsSectionProps {
  project: ProjectDto;
  itemKey: string;
}

// The Agent runs section of the item page. It reads its own data (one request).
// TODO: the table, totals, footer and the empty and store-down states replace this stub.
export function AgentRunsSection({ project, itemKey }: AgentRunsSectionProps) {
  useAgentRuns(project.key, itemKey);
  return <section data-testid={ids.agentRunsSection} aria-label="Agent runs" />;
}
