import type { PromptDto } from '@shiplean/core/api/prompts';
import { promptsTestIds as ids } from '../../testIds';
import { textInUse } from '../filterPrompts';
import { firstMatchingLine, lineExcerpt } from './firstMatchingLine';
import { PromptStatusChip } from './PromptStatusChip';

// One row of the header search's results menu (D5): the prompt's name, its stage and file, its status chip and the first
// line of its text in use that holds the search, with the match marked. A row matched only by name, file or a placeholder
// the text does not hold shows no line.
export function SearchHit({
  id,
  prompt,
  stageLabel,
  query,
  active,
  onPick,
}: {
  id: string;
  prompt: PromptDto;
  stageLabel: string;
  query: string;
  active: boolean;
  onPick: () => void;
}) {
  const matched = firstMatchingLine(textInUse(prompt), query);
  const excerpt = matched === null ? null : lineExcerpt(matched);
  return (
    <button
      type="button"
      id={id}
      role="option"
      aria-selected={active}
      tabIndex={-1}
      data-testid={ids.searchResult}
      data-prompt={prompt.key}
      className={`grid w-full grid-cols-[minmax(0,1fr)_auto] gap-x-2 gap-y-0.5 rounded-control px-[9px] py-[7px] text-left text-ink hover:bg-sunken touch:min-h-[var(--touch-target)] ${active ? 'bg-sunken' : ''}`}
      // Keeps the focus in the search box, so the menu does not close before the click lands.
      onMouseDown={(e) => e.preventDefault()}
      onClick={onPick}
    >
      <span
        data-testid={ids.searchResultName}
        className="col-start-1 text-[length:var(--text-sm)] font-semibold"
      >
        {prompt.name}
      </span>
      <span className="col-start-2 row-span-2 row-start-1">
        <PromptStatusChip prompt={prompt} />
      </span>
      <span
        data-testid={ids.searchResultStage}
        className="col-start-1 text-[length:var(--text-xs)] text-ink-muted"
      >
        {stageLabel} · <span className="font-mono">{prompt.file}</span>
      </span>
      {excerpt === null ? null : (
        <span
          data-testid={ids.searchResultLine}
          className="col-span-2 truncate font-mono text-[length:var(--text-2xs)] text-ink-muted"
        >
          {excerpt.before}
          <mark data-testid={ids.searchMatch} className="rounded-[2px] bg-caution/30 text-inherit">
            {excerpt.match}
          </mark>
          {excerpt.after}
        </span>
      )}
    </button>
  );
}
