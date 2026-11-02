import { useState } from 'react';
import type { RetroPointDto } from '../../../api/retros';
import type { RetroActionScope } from '../actionDialog/RetroActionScope';
import { useRetroActionDialog } from '../actionDialog/useRetroActionDialog';
import { retroSharedTestIds as ids } from '../testIds';
import { NotNeededBlock } from './NotNeededBlock';
import { PinnedAction } from './PinnedAction';
import type { RetroNoteWrites } from './useRetroNoteWrites';
import { WhyEditor } from './WhyEditor';

/** What a point offers by its decision: nothing for Went well, the buttons while undecided, the pin once actioned, the reason once not needed. */
export function NoteDecision({
  point,
  scope,
  writes,
}: {
  point: RetroPointDto;
  scope: RetroActionScope;
  writes: RetroNoteWrites;
}) {
  const dialog = useRetroActionDialog();
  const [asking, setAsking] = useState(false);
  if (point.decision === null) return null;
  if (point.decision === 'actioned' && point.action) return <PinnedAction action={point.action} />;
  if (point.decision === 'notNeeded' && point.notNeeded) {
    return <NotNeededBlock pointId={point.id} notNeeded={point.notNeeded} writes={writes} />;
  }
  if (asking) {
    return (
      <WhyEditor
        saving={writes.notNeeded.isPending}
        onCancel={() => setAsking(false)}
        onSave={(reason) => {
          setAsking(false);
          writes.notNeeded.mutate({ pointId: point.id, reason });
        }}
      />
    );
  }
  // TODO: the finished button and link styling replaces these plain controls.
  const emphasised = point.category === 'improve';
  return (
    <div className="flex items-center gap-2 text-[length:var(--text-xs)]">
      <button
        type="button"
        data-testid={ids.retroNoteAddAction}
        data-variant={emphasised ? 'button' : 'link'}
        className={
          emphasised
            ? 'cursor-pointer rounded-control bg-accent px-2 py-0.5 text-signal-ink'
            : 'cursor-pointer border-0 bg-transparent p-0 text-accent underline'
        }
        onClick={() => dialog.open({ pointIds: [point.id], scope })}
      >
        + Action item
      </button>
      <button
        type="button"
        data-testid={ids.retroNoteNotNeeded}
        className="cursor-pointer border-0 bg-transparent p-0 text-ink-muted underline"
        onClick={() => setAsking(true)}
      >
        Not needed
      </button>
    </div>
  );
}
