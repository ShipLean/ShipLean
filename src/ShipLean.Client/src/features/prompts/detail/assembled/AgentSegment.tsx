import type { PromptAgentDto } from '@shiplean/core/api/prompts';
import { promptsTestIds as ids } from '../../testIds';

// "Preview as": one pressed button of a joined group.
export function AgentSegment(props: {
  agents: PromptAgentDto[];
  selected: string;
  onSelect: (id: string) => void;
}) {
  return (
    <div className="flex flex-wrap items-center gap-2">
      <strong className="text-[length:var(--text-sm)]">Preview as</strong>
      <div
        role="group"
        aria-label="Preview as"
        data-testid={ids.agentPicker}
        className="inline-flex overflow-hidden rounded-control border border-edge bg-panel"
      >
        {props.agents.map((a) => (
          <button
            key={a.id}
            type="button"
            data-testid={ids.agentOption}
            data-agent={a.id}
            aria-pressed={a.id === props.selected}
            className={`cursor-pointer border-0 border-l border-edge px-3 text-[length:var(--text-sm)] first:border-l-0 h-7 touch:h-[var(--touch-target)] ${
              a.id === props.selected
                ? 'bg-signal-soft font-semibold text-ink'
                : 'bg-panel text-ink-muted hover:bg-sunken'
            }`}
            onClick={() => props.onSelect(a.id)}
          >
            {a.label}
          </button>
        ))}
      </div>
    </div>
  );
}
