import { useState } from 'react';
import type { PromptAgentDto, PromptDto } from '@shiplean/core/api/prompts';
import { promptsTestIds as ids } from '../testIds';
import { usePromptAssembled } from '../usePrompts';
import { SourceBlock } from './source/SourceBlock';
import { AgentSegment } from './assembled/AgentSegment';
import { LayerCard } from './assembled/LayerCard';

// What the agent gets: the layer cards top to bottom, then the exact text each agent receives below the stack.
export function AssembledTab(props: {
  projectId: string;
  prompt: PromptDto;
  agents: PromptAgentDto[];
}) {
  const { projectId, prompt, agents } = props;
  const [chosen, setChosen] = useState<string | null>(null);
  const agent = chosen ?? agents[0]?.id ?? '';
  const assembled = usePromptAssembled(projectId, prompt.key, agent, agent !== '');

  return (
    <section
      data-testid={ids.panelAssembled}
      aria-label={`What the agent gets for ${prompt.name}`}
      className="grid gap-3"
    >
      <p
        data-testid={ids.assembledIntro}
        className="m-0 text-[length:var(--text-sm)] text-ink-muted"
      >
        What an agent receives for this step, top to bottom. Your custom version replaces only the
        highlighted layer.
      </p>
      <AgentSegment agents={agents} selected={agent} onSelect={setChosen} />
      {assembled.isError ? (
        <p role="alert" data-testid={ids.assembledError} className="m-0 text-alert">
          What this agent gets could not be loaded.
        </p>
      ) : !assembled.data ? (
        <p data-testid={ids.assembledLoading} className="m-0 text-ink-muted">
          Loading...
        </p>
      ) : (
        <>
          <ol
            data-testid={ids.assembledLayers}
            className="m-0 grid max-w-[760px] list-none gap-1.5 p-0"
          >
            {assembled.data.layers.map((layer) => (
              <LayerCard key={layer.kind} layer={layer} prompt={prompt} />
            ))}
          </ol>
          <div className="grid gap-2">
            <div className="flex items-center gap-2">
              <strong>What this agent receives</strong>
              <button
                type="button"
                data-testid={ids.assembledCopy}
                className="btn touch:min-h-[var(--touch-target)]"
                onClick={() =>
                  void navigator.clipboard.writeText(assembled.data.text).catch(() => undefined)
                }
              >
                Copy
              </button>
            </div>
            <SourceBlock
              text={assembled.data.text}
              label="What this agent receives"
              testId={ids.assembledText}
            />
          </div>
        </>
      )}
    </section>
  );
}
