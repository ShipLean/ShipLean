import type { PromptAgentDto, PromptDto } from '@shiplean/core/api/prompts';
import { promptsTestIds as ids } from '../testIds';

// What the agent gets: the layers top to bottom and the exact text each agent receives.
export function AssembledTab(props: {
  projectId: string;
  prompt: PromptDto;
  agents: PromptAgentDto[];
}) {
  // TODO: the agent picker, the layers and the assembled text from usePromptAssembled
  return (
    <section
      data-testid={ids.panelAssembled}
      aria-label={`What the agent gets for ${props.prompt.name}`}
      data-agents={props.agents.length}
    />
  );
}
