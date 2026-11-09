import { useState } from 'react';
import type { RetroPointDto } from '@shiplean/core/api/retros';
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
  const emphasised = point.category === 'improve';
  return (
    <div className="flex min-w-0 flex-wrap items-center gap-1.5 pt-0.5 text-[length:var(--text-xs)]">
      <button
        type="button"
        data-testid={ids.retroNoteAddAction}
        data-variant={emphasised ? 'button' : 'link'}
        className={
          emphasised
            ? 'inline-flex h-6 cursor-pointer items-center gap-1 rounded-control border border-signal bg-signal px-2 font-semibold whitespace-nowrap text-signal-ink hover:brightness-[1.08]'
            : 'cursor-pointer border-0 bg-transparent p-0 font-medium whitespace-nowrap text-signal hover:underline'
        }
        onClick={() => dialog.open({ pointIds: [point.id], scope })}
      >
        + Action item
      </button>
      <button
        type="button"
        data-testid={ids.retroNoteNotNeeded}
        className="cursor-pointer border-0 bg-transparent p-0 whitespace-nowrap text-ink-muted hover:underline"
        onClick={() => setAsking(true)}
      >
        Not needed
      </button>
    </div>
  );
}
