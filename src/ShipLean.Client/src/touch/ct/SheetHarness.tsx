import { useState } from 'react';
import { Dialog } from '../../components/Dialog';
import { ActionSheet } from '../ActionSheet';
import { MoreButton } from '../MoreButton';
import { Sheet } from '../Sheet';
import { sheetHarnessIds } from './sheetHarnessIds';

// CT harness for Sheet.phone.spec.tsx and ActionSheet.phone.spec.tsx. Each variant starts with its overlay open (except
// `dialog` and `more`), shows whether it is open, and has a button to open it again.
export type SheetHarnessVariant = 'basic' | 'search' | 'long' | 'dialog' | 'actions' | 'more';

const rows = ['Assign to me', 'Move to sprint', 'Copy link', 'Change state', 'Add a label'];

export function SheetHarness({ variant }: { variant: SheetHarnessVariant }) {
  const [open, setOpen] = useState(variant !== 'dialog' && variant !== 'more');
  const [lastAction, setLastAction] = useState('none');
  const [dialogOpen, setDialogOpen] = useState(true);
  const close = () => setOpen(false);
  const actions = [
    { id: 'copy', label: 'Copy link', onSelect: () => setLastAction('copy') },
    { id: 'move', label: 'Move to sprint', onSelect: () => setLastAction('move') },
    { id: 'archive', label: 'Archive', onSelect: () => setLastAction('archive'), disabled: true },
    {
      id: 'delete',
      label: 'Delete',
      onSelect: () => setLastAction('delete'),
      tone: 'danger' as const,
    },
  ];

  const sheet = (
    <Sheet
      open={open}
      title={
        variant === 'long' ? 'A sheet with a long list of options to choose from' : 'Work item'
      }
      onClose={close}
      footer={
        variant === 'basic' ? (
          <button type="button" className="h-10 flex-1 rounded-control bg-signal text-signal-ink">
            Done
          </button>
        ) : undefined
      }
    >
      {variant === 'search' ? (
        <input
          data-testid={sheetHarnessIds.ctSheetSearch}
          type="search"
          placeholder="Search people"
          className="h-10 rounded-control border border-edge bg-canvas px-2"
        />
      ) : null}
      {(variant === 'long' ? Array.from({ length: 40 }, (_, i) => `Option ${i + 1}`) : rows).map(
        (row) => (
          <button
            key={row}
            type="button"
            className="flex min-h-[var(--touch-target)] items-center rounded-control px-2 text-left text-ink"
          >
            {row}
          </button>
        ),
      )}
    </Sheet>
  );

  return (
    <div className="grid gap-2 p-4 text-ink">
      <p>
        Sheet: <span data-testid={sheetHarnessIds.ctSheetState}>{open ? 'open' : 'closed'}</span> ·
        last action: <span data-testid={sheetHarnessIds.ctSheetLastAction}>{lastAction}</span>
      </p>
      <button type="button" data-testid={sheetHarnessIds.ctSheetOpen} onClick={() => setOpen(true)}>
        Open
      </button>
      {variant === 'more' ? <MoreButton actions={actions} label="Item actions" /> : null}
      {variant === 'actions' ? (
        <ActionSheet open={open} title="Work item" actions={actions} onClose={close} />
      ) : null}
      {variant === 'dialog' && dialogOpen ? (
        <Dialog title="Edit work item" onClose={() => setDialogOpen(false)}>
          <button
            type="button"
            data-testid={sheetHarnessIds.ctSheetDialogOpen}
            onClick={() => setOpen(true)}
          >
            Pick a person
          </button>
          {sheet}
        </Dialog>
      ) : null}
      {variant !== 'dialog' && variant !== 'actions' && variant !== 'more' ? sheet : null}
    </div>
  );
}
