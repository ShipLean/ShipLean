import { useEffect, useRef } from 'react';
import { useSearchParams } from 'react-router-dom';
import type { WorkItemTypeId } from '../../../api/refs';
import type { BoardColumnDto } from '../../../api/views';
import { Markdown } from '../../../components/Markdown';
import { StateDot } from '../../../components/StateDot';
import { TypeIcon } from '../../../components/TypeIcon';
import { boardTestIds as ids } from '../testIds';

const TYPE_NAMES: Record<WorkItemTypeId, string> = {
  epic: 'Epic',
  feature: 'Feature',
  story: 'User Story',
  bug: 'Bug',
  task: 'Task',
};

// What a column means: the state each type takes there, the WIP rule, and its Definition of done as the server
// rendered it. Its opener closes it on Escape, a click outside, and a scroll.
export function DodPopover({
  column,
  anchor,
  onClose,
}: {
  column: BoardColumnDto;
  anchor: { left: number; top: number };
  onClose: () => void;
}) {
  const popover = useRef<HTMLDivElement>(null);
  const [params, setParams] = useSearchParams();
  useEffect(() => popover.current?.focus(), []);
  return (
    <div
      ref={popover}
      role="dialog"
      aria-label={`${column.name}: state per type and definition of done`}
      tabIndex={-1}
      data-testid={ids.dod}
      className="fixed z-45 grid w-[290px] gap-2 rounded-panel border border-edge bg-panel p-3 text-[length:var(--text-sm)] shadow-[0_12px_32px_rgb(0_0_0/0.18)]"
      style={{ left: anchor.left, top: anchor.top }}
    >
      <h3 className="m-0 text-[length:var(--text-md)] font-semibold">{column.name}</h3>
      <div className="text-[length:var(--text-2xs)] font-semibold tracking-wide text-ink-muted uppercase">
        State per type
      </div>
      <div className="grid grid-cols-[auto_1fr] items-center gap-x-2 gap-y-1">
        {column.states.map((mapping) => (
          <div key={mapping.type} className="contents">
            <span
              data-testid={ids.dodType}
              className="inline-flex items-center gap-1.5 text-ink-muted"
            >
              <TypeIcon type={mapping.type} /> {TYPE_NAMES[mapping.type]}
            </span>
            <span data-testid={ids.dodState}>
              <StateDot category={mapping.state.category} name={mapping.state.name} />
            </span>
          </div>
        ))}
      </div>
      {column.wipLimit === null ? null : (
        <div data-testid={ids.dodWip} className="text-[length:var(--text-xs)] text-ink-muted">
          WIP limit {column.wipLimit}
          {column.split ? ', counting Doing and Done' : ''}. Over the limit is allowed; the header
          turns red.
        </div>
      )}
      {column.split ? (
        <div data-testid={ids.dodSplit} className="text-[length:var(--text-xs)] text-ink-muted">
          Split: <b>Doing</b> is being worked on, <b>Done</b> is ready to be pulled into the next
          column. Both keep the same state.
        </div>
      ) : null}
      <div className="text-[length:var(--text-2xs)] font-semibold tracking-wide text-ink-muted uppercase">
        Definition of done
      </div>
      {column.definitionOfDoneHtml === '' ? (
        <div data-testid={ids.dodEmpty} className="text-ink-muted">
          No definition of done set for this column.
        </div>
      ) : (
        <div data-testid={ids.dodBody}>
          <Markdown html={column.definitionOfDoneHtml} />
        </div>
      )}
      <footer className="flex items-center border-t border-edge pt-2 text-[length:var(--text-xs)] text-ink-muted">
        Markdown
        <span className="flex-1" />
        <button
          type="button"
          data-testid={ids.dodSettings}
          className="cursor-pointer border-0 bg-transparent p-0 text-accent underline"
          onClick={() => {
            const next = new URLSearchParams(params);
            next.set('settings', 'columns');
            setParams(next);
            onClose();
          }}
        >
          Edit in Board settings
        </button>
      </footer>
    </div>
  );
}
