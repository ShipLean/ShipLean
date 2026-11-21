import { useState } from 'react';
import type { PromptAgentDto, PromptDto } from '@shiplean/core/api/prompts';
import { promptsTestIds as ids } from '../testIds';
import { usePromptAssembled } from '../usePrompts';
import { SourceBlock } from './source/SourceBlock';

// What the agent gets: the layers top to bottom and the exact text each agent receives.
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
      <div
        role="group"
        aria-label="Preview as"
        data-testid={ids.agentPicker}
        className="flex flex-wrap items-center gap-1"
      >
        <span className="text-[length:var(--text-sm)] text-ink-muted">Preview as</span>
        {agents.map((a) => (
          <button
            key={a.id}
            type="button"
            data-testid={ids.agentOption}
            data-agent={a.id}
            aria-pressed={a.id === agent}
            className={
              a.id === agent
                ? 'btn-signal touch:min-h-[var(--touch-target)]'
                : 'btn touch:min-h-[var(--touch-target)]'
            }
            onClick={() => setChosen(a.id)}
          >
            {a.label}
          </button>
        ))}
      </div>
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
          <ol data-testid={ids.assembledLayers} className="m-0 grid list-none gap-2 p-0">
            {assembled.data.layers.map((layer, i) => (
              <li
                key={layer.kind}
                data-testid={
                  layer.planned
                    ? ids.assembledLayerPlanned
                    : layer.kind === 'prompt'
                      ? ids.assembledLayerThis
                      : ids.assembledLayer
                }
                data-kind={layer.kind}
                className={`grid gap-2 rounded-control border px-3 py-2 text-[length:var(--text-sm)] ${
                  layer.planned
                    ? 'border-dashed border-edge opacity-60'
                    : layer.kind === 'prompt'
                      ? 'border-signal'
                      : 'border-edge'
                }`}
              >
                <div className="flex flex-wrap items-baseline gap-2">
                  <span className="text-ink-muted">{i + 1}</span>
                  <strong>{layer.title}</strong>
                  {layer.planned ? <span className="chip">Planned</span> : null}
                </div>
                {layer.note === null ? null : (
                  <div className="text-[length:var(--text-xs)] text-ink-muted">{layer.note}</div>
                )}
                {!layer.planned &&
                layer.text !== null &&
                (layer.kind === 'prompt' || layer.kind === 'agentSections') ? (
                  <SourceBlock
                    text={layer.text}
                    label={layer.title}
                    testId={`${ids.assembledLayer}-source`}
                  />
                ) : null}
              </li>
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
