import { useEffect, useRef } from 'react';
import { Dialog } from '../../../../components/Dialog';
import { Sheet } from '../../../../touch/Sheet';
import { useTouchShell } from '../../../../touch/useTouchShell';
import { boardTestIds as ids } from '../../testIds';
import { OpenChildRow } from './OpenChildRow';
import { boardOfChildren } from './openText';
import type { Level } from '../../../../routes';
import type { OpenBelow } from './openBelow';
import { useOpenChildren } from './useOpenChildren';

const SHOWN = 6;

/** Asks before a card with open children goes into Done: lists what is still open; Cancel moves nothing. */
export function DoneConfirm({
  projectId,
  level,
  asking,
  onCancel,
  onMove,
}: {
  projectId: string;
  level: Level;
  asking: OpenBelow;
  onCancel: () => void;
  onMove: () => void;
}) {
  const { card, text } = asking;
  // Enter confirms; the dialog leaves focus alone when something inside already has it.
  const confirmButton = useRef<HTMLButtonElement>(null);
  const { touch } = useTouchShell();
  useEffect(() => {
    if (touch) return;
    const frame = requestAnimationFrame(() => confirmButton.current?.focus());
    return () => cancelAnimationFrame(frame);
  }, [touch]);
  const children = useOpenChildren(projectId, card.id);
  const listed = children.data ?? [];
  const content = (
    <div data-testid={ids.doneConfirm} data-key={card.key} className="grid gap-3">
      <p className="m-0 text-[length:var(--text-sm)] text-ink">
        <b>{card.title}</b> still has {text} open. They stay as they are; close or move them from
        the {boardOfChildren(level)} board.
      </p>
      {children.isError ? (
        <p role="alert" className="m-0 text-[length:var(--text-sm)] text-caution">
          The open children could not be loaded.
        </p>
      ) : (
        <ul aria-label={`Open under ${card.key}`} className="m-0 grid list-none gap-1.5 p-0">
          {listed.slice(0, SHOWN).map((child) => (
            <OpenChildRow key={child.id} child={child} />
          ))}
          {listed.length > SHOWN ? (
            <li
              data-testid={ids.doneConfirmMore}
              className="text-[length:var(--text-sm)] text-ink-muted"
            >
              and {listed.length - SHOWN} more
            </li>
          ) : null}
        </ul>
      )}
    </div>
  );
  if (touch) {
    return (
      <Sheet
        open
        title={`${text} still open`}
        onClose={onCancel}
        footer={
          <div className="flex w-full flex-col gap-2">
            <button
              type="button"
              data-testid={ids.doneConfirmMove}
              className="min-h-[var(--touch-target)] w-full rounded-control bg-signal px-3 text-[length:var(--text-base)] font-medium text-signal-ink"
              onClick={onMove}
            >
              Move to Done anyway
            </button>
            <button
              type="button"
              data-testid={ids.doneConfirmCancel}
              className="min-h-[var(--touch-target)] w-full rounded-control border border-edge px-3 text-[length:var(--text-base)] text-ink"
              onClick={onCancel}
            >
              Cancel
            </button>
          </div>
        }
      >
        {content}
      </Sheet>
    );
  }
  return (
    <Dialog
      title={`Move ${card.key} to Done?`}
      onClose={onCancel}
      footer={
        <>
          <span className="flex-1" />
          <button
            type="button"
            data-testid={ids.doneConfirmCancel}
            className="rounded-control border border-edge px-3 py-1 text-[length:var(--text-sm)] text-ink"
            onClick={onCancel}
          >
            Cancel
          </button>
          <button
            type="button"
            ref={confirmButton}
            data-testid={ids.doneConfirmMove}
            className="rounded-control bg-accent px-3 py-1 text-[length:var(--text-sm)] font-medium text-signal-ink"
            onClick={onMove}
          >
            Move to Done anyway
          </button>
        </>
      }
    >
      {content}
    </Dialog>
  );
}
