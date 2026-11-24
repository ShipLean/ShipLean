import type { PromptStage, PromptStageDto } from '@shiplean/core/api/prompts';
import { promptsTestIds as ids } from '../testIds';
import { isCustomised } from '../header/filterPrompts';

const customCount = (stage: PromptStageDto) => stage.prompts.filter(isCustomised);

// The seven stages across the top, each with its prompt and custom counts, joined by chevrons; the stage of the open
// prompt is shaded, and a click picks the stage. Under 900px StageSelect replaces it (D7), so nothing scrolls sideways.
export function StageStrip({
  stages,
  selected,
  onSelect,
}: {
  stages: PromptStageDto[];
  selected: PromptStage | null;
  onSelect: (stage: PromptStage) => void;
}) {
  return (
    <nav
      data-testid={ids.stageStrip}
      aria-label="Workflow stages"
      hidden={stages.length === 0}
      className="flex min-w-0 items-stretch overflow-x-auto touch:hidden"
    >
      {stages.map((stage, index) => {
        const customs = customCount(stage).length;
        const count = stage.prompts.length;
        return (
          <button
            key={stage.id}
            type="button"
            data-testid={ids.stage}
            data-stage={stage.id}
            aria-current={selected === stage.id}
            onClick={() => onSelect(stage.id)}
            className={`relative grid min-h-[var(--touch-target,32px)] min-w-[104px] flex-1 content-center justify-items-start gap-0.5 border border-edge py-1.5 pr-2.5 text-left text-ink hover:bg-sunken focus-visible:outline-2 focus-visible:outline-offset-[-2px] focus-visible:outline-signal ${
              index === 0 ? 'rounded-l-control pl-3' : 'pl-[18px]'
            } ${index === stages.length - 1 ? 'rounded-r-control' : 'border-r-0'} ${
              selected === stage.id ? 'bg-signal-soft' : 'bg-panel'
            }`}
          >
            {index === stages.length - 1 ? null : (
              <span
                aria-hidden="true"
                data-testid={ids.stageChevron}
                className="absolute top-1/2 -right-[7px] z-[1] size-3 -translate-y-1/2 rotate-45 border-t border-r border-edge bg-inherit"
              />
            )}
            <span className="text-[length:var(--text-2xs)] font-semibold tracking-wider text-ink-muted uppercase">
              {index + 1}
            </span>
            <span className="text-[length:var(--text-sm)] font-semibold whitespace-nowrap">
              {stage.name}
            </span>
            <span className="text-[length:var(--text-xs)] whitespace-nowrap text-ink-muted">
              <span data-testid={ids.stageStripCount}>
                {count} {count === 1 ? 'prompt' : 'prompts'}
              </span>
              {customs > 0 ? (
                <>
                  {' · '}
                  <b data-testid={ids.stageStripCustom} className="font-semibold text-label-purple">
                    {customs} custom
                  </b>
                </>
              ) : null}
            </span>
          </button>
        );
      })}
    </nav>
  );
}
