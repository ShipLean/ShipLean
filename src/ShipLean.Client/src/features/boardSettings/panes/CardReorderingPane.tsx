import type { BoardSettingsJson, CardReordering } from '../../../api/persisted';
import type { PaneProps } from '../../../components/SettingsDialog/types';
import type { BoardSettingsContext } from '../panes';
import { boardSettingsTestIds as ids } from '../testIds';

const options: { value: CardReordering; testId: string; title: string; text: string }[] = [
  {
    value: 'changesBacklogOrder',
    testId: ids.reorderingChangesBacklog,
    title: 'Reordering cards changes backlog order',
    text: 'Dragging a card above another also moves it above that item in the Stories backlog, so the board and the backlog always agree.',
  },
  {
    value: 'keepsBacklogOrder',
    testId: ids.reorderingKeepsBacklog,
    title: 'Cards keep backlog order (no reordering on the board)',
    text: 'Cards sort by backlog order inside each column. Dragging only changes the column or lane; to change the order, use the backlog.',
  },
];

/** What dragging a card up or down inside a column does. No saved choice behaves as keeping backlog order. */
export function CardReorderingPane({
  draft,
  onChange,
  readOnly,
}: PaneProps<BoardSettingsJson, BoardSettingsContext>) {
  const current = draft.cardReordering ?? 'keepsBacklogOrder';
  return (
    <div data-testid={ids.reorderingPane} className="grid max-w-[620px] content-start gap-2">
      <p className="m-0 text-[length:var(--text-xs)] text-ink-muted">
        What happens when someone drags a card up or down inside a column.
      </p>
      {options.map((option) => (
        <label
          key={option.value}
          className="flex items-start gap-2 rounded-panel border border-edge bg-panel p-3"
        >
          <input
            type="radio"
            name="card-reordering"
            data-testid={option.testId}
            checked={current === option.value}
            disabled={readOnly}
            onChange={() => onChange({ ...draft, cardReordering: option.value })}
          />
          <span className="grid gap-0.5">
            <b className="text-[length:var(--text-sm)]">{option.title}</b>
            <span className="text-[length:var(--text-xs)] text-ink-muted">{option.text}</span>
          </span>
        </label>
      ))}
      <p className="m-0 text-[length:var(--text-xs)] text-ink-muted">
        Either way, moving a card to another column sets its state, and moving it to another lane
        sets its lane.
      </p>
    </div>
  );
}
