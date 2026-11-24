import type { PromptDto, PromptKey, PromptStage, PromptStageDto } from '@shiplean/core/api/prompts';
import { promptsTestIds as ids } from '../testIds';
import { StageSelect } from './StageSelect';
import { StageStrip } from './StageStrip';

const dotOf = (prompt: PromptDto) =>
  prompt.status === 'custom'
    ? { kind: 'custom', className: 'bg-label-purple' }
    : prompt.status === 'builtInUpdated'
      ? { kind: 'updated', className: 'bg-caution' }
      : null;

// The only stage picker (D1): the strip, then for a stage with two or more prompts a chip per prompt (D4), then when the
// stage happens. Clicking a stage opens its first prompt; the stage follows the open prompt, so there is no stage state.
// Under 900px the strip is a stage select and the chips wrap under it (D7).
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
  const openStage = (id: PromptStage) => {
    const first = stages.find((stage) => stage.id === id)?.prompts[0];
    if (first !== undefined) onSelect(first.key);
  };
  return (
    <div data-testid={ids.stageNav} className="grid min-w-0 gap-2">
      <StageStrip stages={stages} selected={current?.id ?? null} onSelect={openStage} />
      <StageSelect stages={stages} selected={current?.id ?? null} onSelect={openStage} />
      {current === undefined ? null : (
        <div className="flex min-w-0 flex-wrap items-center gap-x-3 gap-y-2">
          {current.prompts.length > 1 ? (
            <div
              className="flex flex-wrap gap-1.5"
              role="group"
              aria-label={`${current.name} prompts`}
            >
              {current.prompts.map((prompt) => {
                const dot = dotOf(prompt);
                return (
                  <button
                    key={prompt.key}
                    type="button"
                    data-testid={ids.stageChip}
                    data-prompt={prompt.key}
                    data-status={prompt.status}
                    aria-current={prompt.key === selectedKey}
                    onClick={() => onSelect(prompt.key)}
                    className={`inline-flex min-h-[var(--control-height,32px)] items-center gap-1.5 rounded-full border px-2.5 text-[length:var(--text-sm)] touch:min-h-[var(--touch-target)] ${
                      prompt.key === selectedKey
                        ? 'border-signal/45 bg-signal-soft font-semibold'
                        : 'border-edge bg-panel font-medium hover:bg-sunken'
                    }`}
                  >
                    {prompt.name}
                    {dot === null ? null : (
                      <span
                        aria-label={dot.kind === 'custom' ? 'custom' : 'built-in updated'}
                        role="img"
                        data-testid={ids.stageChipDot}
                        data-dot={dot.kind}
                        className={`size-[7px] rounded-full ${dot.className}`}
                      />
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
