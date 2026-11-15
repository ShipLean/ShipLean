import type { ProjectDto } from '@shiplean/core/api/projects';
import { inspectionTestIds as ids } from './testIds';

export interface AgentSetupProps {
  project: ProjectDto;
}

// The Agent setup sub-tab: the files to give each agent, with copy buttons.
// TODO: the agent picker, files and notes replace this stub.
export function AgentSetup({ project }: AgentSetupProps) {
  return <div data-testid={ids.inspectionAgentSetup} data-project={project.key} />;
}
