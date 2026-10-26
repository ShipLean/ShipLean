import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { ApiRequestError } from '../../../api/client';
import { conflictOf } from '../../../api/workItems';
import { StateDot } from '../../../components/StateDot';
import { cacheIfNewer } from '../../workItem/form/cacheIfNewer';
import { workItemHistoryKey, workItemKey } from '../../workItem/workItemApi';
import type { FeatureRegionProps } from '../FeatureRegionProps';
import { featureTestIds as ids } from '../testIds';
import { confirmStatus } from './statusApi';
import { StatusEditor } from './StatusEditor';
import { VerifiedChip } from './VerifiedChip';

const buttonClass =
  'h-7 rounded-control border border-edge bg-panel px-3 text-[length:var(--text-sm)] text-ink hover:bg-signal-soft focus-visible:outline-2 focus-visible:outline-signal disabled:cursor-not-allowed disabled:opacity-50';

// The Feature's status band: the state, the status line, when it was verified and by whom. Still true confirms it at once
// (its own revision, so only while the form is clean); Edit status opens the editor, whose changes the form's Save writes.
export function StatusBand({ item, draft, dirty, setField, saveError }: FeatureRegionProps) {
  const queryClient = useQueryClient();
  const [editing, setEditing] = useState(false);
  const [confirmError, setConfirmError] = useState<string | null>(null);

  const stillTrue = useMutation({
    mutationFn: () => confirmStatus(item.id, item.revision),
    onSuccess: (fresh) => {
      setConfirmError(null);
      cacheIfNewer(queryClient, fresh);
      void queryClient.invalidateQueries({
        queryKey: workItemHistoryKey(item.projectId, item.key),
      });
    },
    onError: (failure) => {
      const stale = conflictOf(failure);
      if (stale) cacheIfNewer(queryClient, stale.current);
      setConfirmError(
        stale || (failure instanceof ApiRequestError && failure.status === 409)
          ? 'Someone changed this first. It is reloaded; confirm again if the status still holds.'
          : failure.message,
      );
      void queryClient.invalidateQueries({ queryKey: workItemKey(item.projectId, item.key) });
    },
  });

  const cancel = () => {
    setField({ state: item.state, statusLine: item.statusLine });
    setEditing(false);
  };
  const statusError = saveError?.field === 'statusLine' ? saveError.message : confirmError;

  return (
    <section
      data-testid={ids.statusBand}
      aria-label="Status"
      className="border-b border-l-[3px] border-b-edge border-l-signal bg-panel px-4 py-3"
    >
      {editing ? (
        <StatusEditor
          projectId={item.projectId}
          draft={draft}
          setField={setField}
          onDone={() => setEditing(false)}
          onCancel={cancel}
        />
      ) : (
        <div className="flex items-start justify-between gap-4">
          <div className="flex min-w-0 items-start gap-3">
            <span
              data-testid={ids.statusState}
              className="rounded-full border border-edge bg-signal-soft px-2.5 py-0.5"
            >
              <StateDot category={draft.state.category} name={draft.state.name} />
            </span>
            <div className="grid min-w-0 gap-1">
              <p data-testid={ids.statusLine} className="text-[length:var(--text-lg)] text-ink">
                {draft.statusLine === '' ? 'No status yet.' : draft.statusLine}
              </p>
              <div className="flex flex-wrap items-center gap-2">
                <VerifiedChip verified={item.statusVerified} />
              </div>
            </div>
          </div>
          <div className="flex flex-none gap-2">
            <button
              type="button"
              data-testid={ids.statusStillTrue}
              disabled={dirty || stillTrue.isPending}
              title={dirty ? 'Save or discard your changes first' : undefined}
              className={buttonClass}
              onClick={() => stillTrue.mutate()}
            >
              ✓ Still true
            </button>
            <button
              type="button"
              data-testid={ids.statusEdit}
              className={buttonClass}
              onClick={() => setEditing(true)}
            >
              Edit status
            </button>
          </div>
        </div>
      )}
      {statusError === null ? null : (
        <p
          data-testid={ids.statusError}
          role="alert"
          className="mt-2 text-[length:var(--text-sm)] text-alert"
        >
          {statusError}
        </p>
      )}
    </section>
  );
}
