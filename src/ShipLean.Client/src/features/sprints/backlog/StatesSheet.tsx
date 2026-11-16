import { useState } from 'react';
import type { WorkItemDto } from '@shiplean/core/api/workItems';
import { StateDot } from '../../../components/StateDot';
import { StateMenu } from '../../../components/StateMenu';
import { Sheet } from '../../../touch/Sheet';
import { sprintsTestIds as ids } from '../testIds';
import type { StateChanger } from './RowStateMenu';

/**
 * A row's state at touch (SHIP-724 D3): a 28px pill inside a 32px hit area that opens the States sheet, "State of {key}"
 * with the item's title under it. The sheet is the shared `StateMenu` (K1b); picking another state applies it and closes
 * the sheet.
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
      <Sheet
        open={open}
        title={`State of ${item.key}`}
        subtitle={item.title}
        onClose={() => setOpen(false)}
      >
        {/* K1b: the states under their category headings, the current one ticked and tinted. */}
        <StateMenu
          states={options}
          currentId={item.state.id}
          title={`State of ${item.key}`}
          onClose={() => setOpen(false)}
          onSelect={(stateId) => {
            setOpen(false);
            if (stateId !== item.state.id) changer.onChange(item, stateId);
          }}
        />
      </Sheet>
    </>
  );
}
