import { useState } from 'react';
import type { WorkItemDto } from '@shiplean/core/api/workItems';
import { StateDot } from '../../../components/StateDot';
import { Sheet } from '../../../touch/Sheet';
import { CATEGORY_LABELS } from '../../settings/states/categories';
import { sprintsTestIds as ids } from '../testIds';
import type { StateChanger } from './RowStateMenu';

/**
 * A row's state at touch (SHIP-724 D3): a 28px pill inside a 32px hit area that opens the States sheet. The sheet lists
 * the item type's states with each one's category; picking another state applies it and closes the sheet.
 */
export function StatesSheet({ item, changer }: { item: WorkItemDto; changer: StateChanger }) {
  const [open, setOpen] = useState(false);
  const options = changer.states
    .filter((state) => state.type === item.type.id && state.category !== 'removed')
    .sort((a, b) => a.order - b.order);
  return (
    <>
      <button
        type="button"
        data-testid={ids.stateButton}
        aria-label={`State of ${item.key}: ${item.state.name}`}
        aria-haspopup="dialog"
        aria-expanded={open}
        disabled={changer.busy}
        className="relative inline-flex h-[var(--touch-target)] cursor-pointer items-center border-0 bg-transparent p-0 text-[length:var(--text-sm)] text-ink disabled:cursor-default disabled:opacity-60"
        onClick={() => setOpen(true)}
      >
        <span
          className={`inline-flex h-7 items-center gap-1.5 rounded-full border px-2 ${open ? 'border-signal bg-signal-soft' : 'border-edge bg-panel'}`}
        >
          <StateDot category={item.state.category} name={item.state.name} />
          <span aria-hidden="true" className="text-[9px] text-ink-muted">
            ▾
          </span>
        </span>
      </button>
      <Sheet open={open} title="States" onClose={() => setOpen(false)}>
        <ul className="m-0 list-none p-0">
          {options.map((state) => {
            const current = state.id === item.state.id;
            return (
              <li key={state.id}>
                <button
                  type="button"
                  data-testid={ids.statesSheetOption}
                  data-category={state.category}
                  aria-current={current}
                  className="flex min-h-12 w-full cursor-pointer items-center gap-3 border-0 border-b border-edge bg-transparent px-3 text-left text-[length:var(--text-base)] text-ink"
                  onClick={() => {
                    setOpen(false);
                    if (!current) changer.onChange(item, state.id);
                  }}
                >
                  <StateDot category={state.category} name={state.name} />
                  <span
                    data-testid={ids.statesSheetCategory}
                    className="ml-auto text-[length:var(--text-sm)] text-ink-muted"
                  >
                    {CATEGORY_LABELS[state.category]}
                  </span>
                  <span aria-hidden="true" className="w-4 text-signal">
                    {current ? '✓' : ''}
                  </span>
                </button>
              </li>
            );
          })}
        </ul>
      </Sheet>
    </>
  );
}
