import { useState } from 'react';
import { Dialog } from '../../components/Dialog';
import { ActionSheet } from '../ActionSheet';
import { MoreButton } from '../MoreButton';
import { Sheet } from '../Sheet';
import { StateDot } from '../../components/StateDot';
import { sheetHarnessIds } from './sheetHarnessIds';

// CT harness for Sheet.phone.spec.tsx and ActionSheet.phone.spec.tsx. Each variant starts with its overlay open (except
// `dialog` and the `more*` ones), shows whether it is open, and has a button to open it again. `rich` is the K1 anatomy:
// icons, hints, details, a checked row, a chevron, groups, a toggle, a subtitle and a footnote.
export type SheetHarnessVariant =
  | 'basic'
  | 'search'
  | 'long'
  | 'dialog'
  | 'actions'
  | 'rich'
  | 'more'
  | 'moreSlot'
  | 'moreBordered';

const rows = ['Assign to me', 'Move to sprint', 'Copy link', 'Change state', 'Add a label'];

export function SheetHarness({ variant }: { variant: SheetHarnessVariant }) {
  const [open, setOpen] = useState(variant !== 'dialog' && !variant.startsWith('more'));
  const [showTasks, setShowTasks] = useState(false);
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

  const glyph = (text: string) => <span className="text-ink-muted">{text}</span>;
  const richActions = [
    {
      id: 'open',
      label: 'Open',
      icon: glyph('↗'),
      detail: 'full screen',
      onSelect: () => setLastAction('open'),
    },
    {
      id: 'assign',
      label: 'Assign',
      detail: 'Sam Rivera',
      trailing: 'chevron' as const,
      onSelect: () => setLastAction('assign'),
    },
    {
      id: 'download',
      label: 'Download',
      icon: glyph('↓'),
      hint: '14 KB',
      onSelect: () => setLastAction('download'),
    },
    {
      id: 'new',
      label: 'New',
      icon: <StateDot category="proposed" />,
      detail: '5',
      group: 'Move to',
      onSelect: () => setLastAction('new'),
    },
    {
      id: 'progress',
      label: 'In progress',
      icon: <StateDot category="inProgress" />,
      detail: 'here · 4/5',
      checked: true,
      group: 'Move to',
      onSelect: () => setLastAction('progress'),
    },
    {
      id: 'done',
      label: 'Done',
      icon: <StateDot category="completed" />,
      detail: '7',
      group: 'Move to',
      onSelect: () => setLastAction('done'),
    },
    {
      id: 'tasks',
      label: 'Show tasks',
      hint: 'Under each story',
      toggle: { on: showTasks },
      group: 'View',
      onSelect: () => {
        setShowTasks((was) => !was);
        setLastAction('tasks');
      },
    },
    {
      id: 'about',
      label: 'About this file',
      icon: glyph('ⓘ'),
      group: '',
      onSelect: () => setLastAction('about'),
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
          // Like a real search field: Esc clears it first, and marks that Esc handled so the sheet stays open.
          onKeyDown={(event) => {
            if (event.key === 'Escape' && event.currentTarget.value !== '') {
              event.currentTarget.value = '';
              event.preventDefault();
            }
          }}
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
      {variant === 'moreSlot' ? (
        <MoreButton actions={actions} label="Item actions">
          {(close) => (
            <button type="button" data-testid={sheetHarnessIds.ctSheetSlot} onClick={close}>
              Slot content
            </button>
          )}
        </MoreButton>
      ) : null}
      {variant === 'moreBordered' ? (
        <MoreButton
          actions={actions}
          label="Sprint 24"
          subtitle="19 Oct – 30 Oct"
          footer="Closing a sprint asks where unfinished work goes."
          variant="bordered"
        />
      ) : null}
      {variant === 'rich' ? (
        <ActionSheet
          open={open}
          title="SHIP-129"
          subtitle="Sign in with passkey"
          actions={richActions}
          footer="States for each type are set per project in Settings."
          onClose={close}
        />
      ) : null}
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
      {variant === 'basic' || variant === 'search' || variant === 'long' ? sheet : null}
    </div>
  );
}
