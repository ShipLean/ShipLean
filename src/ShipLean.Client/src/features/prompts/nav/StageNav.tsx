import type { PromptDto, PromptKey, PromptStageDto } from '@shiplean/core/api/prompts';
import { promptsTestIds as ids } from '../testIds';
import { StageStrip } from './StageStrip';

const dotClass = (prompt: PromptDto) =>
  prompt.status === 'custom'
    ? 'bg-label-purple'
    : prompt.status === 'builtInUpdated'
      ? 'bg-warn'
      : null;

// The only stage picker (D1): the strip, then for a stage with two or more prompts a chip per prompt (D4), then when the
// stage happens. Clicking a stage opens its first prompt; the stage follows the open prompt, so there is no stage state.
// TODO: plain chips and no phone stage select; the styled strip, chips and the select under 900px (D7) replace them.
export function StageNav({
  stages,
  selectedKey,
  onSelect,
}: {
  stages: PromptStageDto[];
  selectedKey: PromptKey;
  onSelect: (key: PromptKey) => void;
}) {
  const current = stages.find((stage) => stage.prompts.some((p) => p.key === selectedKey));
  return (
    <div data-testid={ids.stageNav} className="grid min-w-0 gap-2">
      <StageStrip
        stages={stages}
        selected={current?.id ?? null}
        onSelect={(id) => {
          const first = stages.find((stage) => stage.id === id)?.prompts[0];
          if (first !== undefined) onSelect(first.key);
        }}
      />
      {current === undefined ? null : (
        <div className="flex min-w-0 flex-wrap items-center gap-x-3 gap-y-2">
          {current.prompts.length > 1 ? (
            <div className="flex flex-wrap gap-1.5" aria-label={`${current.name} prompts`}>
              {current.prompts.map((prompt) => {
                const dot = dotClass(prompt);
                return (
                  <button
                    key={prompt.key}
                    type="button"
                    data-testid={ids.stageChip}
                    data-prompt={prompt.key}
                    data-status={prompt.status}
                    aria-current={prompt.key === selectedKey}
                    onClick={() => onSelect(prompt.key)}
                    className={`inline-flex items-center gap-1.5 rounded-full border border-edge px-2.5 py-1 text-[length:var(--text-sm)] touch:min-h-[var(--touch-target)] ${
                      prompt.key === selectedKey ? 'bg-signal-soft font-semibold' : 'bg-panel'
                    }`}
                  >
                    {prompt.name}
                    {dot === null ? null : (
                      <span aria-hidden="true" className={`size-1.5 rounded-full ${dot}`} />
                    )}
                  </button>
                );
              })}
            </div>
          ) : null}
          <span data-testid={ids.stageWhen} className="text-[length:var(--text-sm)] text-ink-muted">
            {current.prompts.length > 1 ? null : <b className="text-ink">{current.name}. </b>}
            {current.when}
          </span>
        </div>
      )}
    </div>
  );
}
