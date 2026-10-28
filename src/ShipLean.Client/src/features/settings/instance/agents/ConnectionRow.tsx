import { useState } from 'react';
import { settingsTestIds as ids } from '../../testIds';
import { errorText } from '../model';
import {
  useArchiveConnection,
  useRegenerateSecret,
  useRegenerateToken,
  useUpdateConnection,
} from './agentsApi';
import { ConnectionForm } from './ConnectionForm';
import { shownOf, type AgentConnectionDto, type ShownOnce } from './model';

type Confirming = 'secret' | 'token' | 'archive' | null;

const confirmText: Record<Exclude<Confirming, null>, string> = {
  secret: 'Regenerate the signing secret? The agent must be given the new one.',
  token: 'Regenerate the callback token? The agent must be given the new one.',
  archive: 'Archive this connection? Projects can no longer use it.',
};

/** One connection: its name, receiver and key endings, with Edit, the two regenerates and Archive (each asks first). */
export function ConnectionRow({
  connection,
  onShown,
}: {
  connection: AgentConnectionDto;
  onShown: (shown: ShownOnce) => void;
}) {
  const [editing, setEditing] = useState(false);
  const [confirming, setConfirming] = useState<Confirming>(null);
  const update = useUpdateConnection();
  const secret = useRegenerateSecret();
  const token = useRegenerateToken();
  const archive = useArchiveConnection();
  const archived = connection.archivedAt !== null;
  const busy = update.isPending || secret.isPending || token.isPending || archive.isPending;
  const failure = secret.error ?? token.error ?? archive.error;

  const confirm = () => {
    const id = connection.id;
    const done = { onSuccess: () => setConfirming(null) };
    if (confirming === 'secret')
      secret.mutate(id, { onSuccess: (issued) => (setConfirming(null), onShown(shownOf(issued))) });
    else if (confirming === 'token')
      token.mutate(id, { onSuccess: (issued) => (setConfirming(null), onShown(shownOf(issued))) });
    else if (confirming === 'archive') archive.mutate(id, done);
  };

  const action = (label: string, testId: string, kind: Exclude<Confirming, null>) => (
    <button
      type="button"
      data-testid={testId}
      className="btn-ghost"
      disabled={busy}
      onClick={() => {
        secret.reset();
        token.reset();
        archive.reset();
        setConfirming(kind);
      }}
    >
      {label}
    </button>
  );

  return (
    <div
      data-testid={ids.agentsRow}
      className={`grid gap-2 border-b border-edge py-3 last:border-b-0 ${archived ? 'opacity-70' : ''}`}
    >
      {editing ? (
        <ConnectionForm
          initial={{ name: connection.name, receiverUrl: connection.receiverUrl }}
          submitLabel="Save"
          pending={update.isPending}
          error={update.error}
          onCancel={() => {
            update.reset();
            setEditing(false);
          }}
          onSubmit={(body) =>
            update.mutate({ id: connection.id, body }, { onSuccess: () => setEditing(false) })
          }
        />
      ) : (
        <>
          <div className="flex items-baseline gap-2 text-[length:var(--text-sm)]">
            <b data-testid={ids.agentsName} className="text-ink">
              {connection.name}
            </b>
            {archived ? (
              <span data-testid={ids.agentsArchivedBadge} className="text-ink-muted">
                Archived
              </span>
            ) : null}
            <span data-testid={ids.agentsUrl} className="truncate font-mono text-ink-muted">
              {connection.receiverUrl}
            </span>
          </div>
          <div className="flex flex-wrap items-center gap-x-4 gap-y-1 text-[length:var(--text-sm)] text-ink-muted">
            <span>
              Signing secret{' '}
              <span data-testid={ids.agentsSecretLast4} className="font-mono text-ink">
                {`•••• ${connection.signingSecretLast4}`}
              </span>
            </span>
            <span>
              Callback token{' '}
              <span data-testid={ids.agentsTokenLast4} className="font-mono text-ink">
                {`•••• ${connection.callbackTokenLast4}`}
              </span>
            </span>
          </div>
          {archived ? null : confirming === null ? (
            <div className="flex flex-wrap items-center gap-1">
              <button
                type="button"
                data-testid={ids.agentsEdit}
                className="btn-ghost"
                disabled={busy}
                onClick={() => setEditing(true)}
              >
                Edit
              </button>
              {action('Regenerate secret', ids.agentsRegenerateSecret, 'secret')}
              {action('Regenerate token', ids.agentsRegenerateToken, 'token')}
              {action('Archive', ids.agentsArchive, 'archive')}
            </div>
          ) : (
            <div className="flex flex-wrap items-center gap-2 text-[length:var(--text-sm)] text-ink">
              <span>{confirmText[confirming]}</span>
              <button
                type="button"
                data-testid={ids.agentsConfirm}
                className="btn-signal"
                disabled={busy}
                onClick={confirm}
              >
                Confirm
              </button>
              <button
                type="button"
                data-testid={ids.agentsConfirmCancel}
                className="btn-ghost"
                disabled={busy}
                onClick={() => setConfirming(null)}
              >
                Cancel
              </button>
            </div>
          )}
          {failure ? (
            <p
              role="alert"
              data-testid={ids.agentsActionError}
              className="m-0 text-[length:var(--text-sm)] text-alert"
            >
              {errorText(failure)}
            </p>
          ) : null}
        </>
      )}
    </div>
  );
}
