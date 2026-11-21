import type { PromptStage, PromptStageDto } from '@shiplean/core/api/prompts';
import { promptsTestIds as ids } from '../testIds';

export const stageAnchor = (stage: PromptStage) => `stage-${stage}`;

const customCount = (stage: PromptStageDto) => stage.prompts.filter((p) => p.status !== 'builtIn');

// The seven stages across the top, each with its prompt and custom counts; a click marks the stage and jumps the list to
// it. It scrolls sideways inside its own box, so the page never does (SHIP-805 rule 7).
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
      className="flex min-w-0 items-stretch overflow-x-auto"
    >
      {stages.map((stage, index) => {
        const customs = customCount(stage).length;
        const count = stage.prompts.length;
        return (
          <button
            key={stage.id}
            type="button"
            data-testid={ids.stageStripItem}
            data-stage={stage.id}
            aria-current={selected === stage.id}
            onClick={() => {
              onSelect(stage.id);
              document.getElementById(stageAnchor(stage.id))?.scrollIntoView({ block: 'start' });
            }}
            className={`grid min-h-[var(--touch-target,32px)] min-w-[104px] flex-1 content-center justify-items-start gap-0.5 border border-edge py-1.5 pr-2.5 pl-3 text-left text-ink hover:bg-sunken focus-visible:outline-2 focus-visible:outline-offset-[-2px] focus-visible:outline-signal ${
              index === 0 ? 'rounded-l-control' : 'border-l-0'
            } ${index === stages.length - 1 ? 'rounded-r-control' : ''} ${
              selected === stage.id ? 'bg-signal-soft' : 'bg-panel'
            }`}
          >
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
