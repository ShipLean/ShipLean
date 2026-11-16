import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ApiRequestError } from '@shiplean/core/api/client';
import { Dialog } from '../../components/Dialog';
import { showToast } from '../../components/toastStore';
import { routePath } from '../../routes';
import { taskChangesTestIds as ids } from './testIds';
import type { ChangesData, ReviewActions } from './types';

export interface ReviewBarProps {
  data: ChangesData;
  projectKey: string;
  itemKey: string;
  /** ChangesScreen adds the head commit id it showed to Approve (D21). */
  review: ReviewActions;
}

type Open = 'approve' | 'request' | null;

// Approve and Request changes sit straight in the bar. Request changes needs an open comment to send back.
export function ReviewBar({ data, projectKey, itemKey, review }: ReviewBarProps) {
  const [open, setOpen] = useState<Open>(null);
  const open_ = data.openComments;
  return (
    <div data-testid={ids.reviewBar} className="ml-auto flex items-center gap-2">
      <button
        type="button"
        data-testid={ids.approve}
        className="btn"
        onClick={() => setOpen('approve')}
      >
        Approve
      </button>
      <button
        type="button"
        data-testid={ids.requestChanges}
        className="btn-signal"
        disabled={open_ === 0}
        onClick={() => setOpen('request')}
      >
        {open_ > 0 ? `Request changes · ${open_}` : 'Request changes'}
      </button>
      {open === 'approve' ? (
        <ApproveDialog
          data={data}
          itemKey={itemKey}
          review={review}
          onCancel={() => setOpen(null)}
          projectKey={projectKey}
        />
      ) : null}
      {open === 'request' ? (
        <RequestDialog
          data={data}
          itemKey={itemKey}
          projectKey={projectKey}
          review={review}
          onCancel={() => setOpen(null)}
        />
      ) : null}
    </div>
  );
}

interface DialogProps extends ReviewBarProps {
  onCancel: () => void;
}

function useBackToItem(projectKey: string, itemKey: string) {
  const navigate = useNavigate();
  return () => void navigate(routePath('workItem', { key: projectKey, itemKey }));
}

function errorText(error: unknown): string {
  if (error instanceof ApiRequestError && error.status === 409 && error.code === 'conflict')
    return 'A new commit arrived. Reload to review it.';
  return error instanceof Error ? error.message : 'Something went wrong.';
}

function Footer({
  onCancel,
  confirmId,
  label,
  busy,
  onConfirm,
}: {
  onCancel: () => void;
  confirmId: string;
  label: string;
  busy: boolean;
  onConfirm: () => void;
}) {
  return (
    <>
      <span className="flex-1" />
      <button type="button" className="btn-ghost" onClick={onCancel}>
        Cancel
      </button>
      <button
        type="button"
        data-testid={confirmId}
        className="btn-signal"
        disabled={busy}
        onClick={onConfirm}
      >
        {label}
      </button>
    </>
  );
}

function ErrorLine({ error }: { error: string | null }) {
  return (
    <p
      data-testid={ids.reviewError}
      role="alert"
      hidden={error === null}
      className="m-0 text-[length:var(--text-sm)] text-alert"
    >
      {error}
    </p>
  );
}

function ApproveDialog({ data, itemKey, projectKey, review, onCancel }: DialogProps) {
  const [note, setNote] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const back = useBackToItem(projectKey, itemKey);
  const shas = data.commits.map((c) => c.shortSha);
  const range = shas.length > 1 ? `${shas[0]} … ${shas[shas.length - 1]}` : (shas[0] ?? '');
  const confirm = async () => {
    setBusy(true);
    setError(null);
    try {
      await review.approve(note.trim() === '' ? null : note.trim());
      showToast(`Approved ${itemKey}`, { tone: 'ok' });
      back();
    } catch (e) {
      setError(errorText(e));
      setBusy(false);
    }
  };
  return (
    <Dialog
      title={`Approve and close ${itemKey}?`}
      onClose={onCancel}
      footer={
        <Footer
          onCancel={onCancel}
          confirmId={ids.approveConfirm}
          label="Approve and close"
          busy={busy}
          onConfirm={() => void confirm()}
        />
      }
    >
      <div data-testid={ids.approveDialog} className="grid gap-3">
        <p className="m-0 text-ink-muted">
          {`It moves to Closed, and History records that you approved commits ${range}.`}
        </p>
        {data.openComments > 0 ? (
          <p data-testid={ids.approveWarning} className="m-0">
            <b>{`${data.openComments} comment${data.openComments === 1 ? ' is' : 's are'} still open.`}</b>{' '}
            They stay open on the task and show on its page. Request changes instead if they need
            doing.
          </p>
        ) : null}
        <label className="grid gap-1 text-[length:var(--text-sm)]">
          Note (optional)
          <textarea
            data-testid={ids.approveNote}
            className="field"
            value={note}
            onChange={(e) => setNote(e.target.value)}
          />
        </label>
        <ErrorLine error={error} />
      </div>
    </Dialog>
  );
}

function RequestDialog({ data, itemKey, projectKey, review, onCancel }: DialogProps) {
  const [note, setNote] = useState('');
  const [keep, setKeep] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const back = useBackToItem(projectKey, itemKey);
  const confirm = async () => {
    setBusy(true);
    setError(null);
    try {
      await review.requestChanges(note.trim() === '' ? null : note.trim(), keep);
      showToast(`Sent ${itemKey} back`, { tone: 'ok' });
      back();
    } catch (e) {
      setError(errorText(e));
      setBusy(false);
    }
  };
  return (
    <Dialog
      title={`Send ${itemKey} back with ${data.openComments} comment${data.openComments === 1 ? '' : 's'}`}
      onClose={onCancel}
      footer={
        <Footer
          onCancel={onCancel}
          confirmId={ids.requestConfirm}
          label="Send back"
          busy={busy}
          onConfirm={() => void confirm()}
        />
      }
    >
      <div data-testid={ids.requestDialog} className="grid gap-3">
        <p className="m-0 text-ink-muted">
          It goes back to New and is unassigned, so the next agent or person who picks it up gets
          these comments in its brief and answers them when it reports.
        </p>
        <label className="grid gap-1 text-[length:var(--text-sm)]">
          Anything else for whoever picks it up (optional, goes on the task as a comment)
          <textarea
            data-testid={ids.requestNote}
            className="field"
            value={note}
            onChange={(e) => setNote(e.target.value)}
          />
        </label>
        <label className="flex items-center gap-2 text-[length:var(--text-sm)]">
          <input
            type="checkbox"
            data-testid={ids.requestKeepAssignee}
            checked={keep}
            onChange={(e) => setKeep(e.target.checked)}
          />
          Keep the assignee and move to Active
        </label>
        <ErrorLine error={error} />
      </div>
    </Dialog>
  );
}
