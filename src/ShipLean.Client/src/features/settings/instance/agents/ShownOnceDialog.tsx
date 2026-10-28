import { useState } from 'react';
import { Dialog } from '../../../../components/Dialog';
import { settingsTestIds as ids } from '../../testIds';
import type { ShownOnce } from './model';

const code =
  'rounded-control bg-sunken px-2 py-1 font-mono text-[length:var(--text-sm)] text-ink select-all break-all';

/** Shows a new signing secret and/or callback token once. Done (or closing) drops them: they cannot be shown again. */
export function ShownOnceDialog({ shown, onDone }: { shown: ShownOnce; onDone: () => void }) {
  const [copy, setCopy] = useState<'idle' | 'copied' | 'failed'>('idle');
  const copyAll = async () => {
    const lines = [
      shown.signingSecret === null ? null : `Signing secret: ${shown.signingSecret}`,
      shown.callbackToken === null ? null : `Callback token: ${shown.callbackToken}`,
    ].filter((line) => line !== null);
    try {
      await navigator.clipboard.writeText(lines.join('\n'));
      setCopy('copied');
    } catch {
      setCopy('failed');
    }
  };
  return (
    <Dialog
      title={`Keys for ${shown.name}`}
      onClose={onDone}
      testId={ids.agentsShownOnce}
      closeTestId={ids.agentsShownOnceClose}
      footer={
        <>
          <button
            type="button"
            data-testid={ids.agentsShownCopy}
            className="btn-ghost"
            onClick={() => void copyAll()}
          >
            {copy === 'copied' ? 'Copied' : 'Copy'}
          </button>
          <span className="flex-1" />
          <button
            type="button"
            data-testid={ids.agentsShownDone}
            className="btn-signal"
            onClick={onDone}
          >
            Done
          </button>
        </>
      }
    >
      <p className="m-0 text-[length:var(--text-sm)] text-ink-muted">
        Copy these now. They are shown once and can't be shown again; to get a new one, regenerate
        it.
      </p>
      {shown.signingSecret === null ? null : (
        <div className="grid gap-1 text-[length:var(--text-sm)] font-medium text-ink">
          Signing secret
          <code data-testid={ids.agentsShownSecret} className={code}>
            {shown.signingSecret}
          </code>
          <span className="font-normal text-ink-muted">
            The agent checks the Linear-Signature header of each webhook with it.
          </span>
        </div>
      )}
      {shown.callbackToken === null ? null : (
        <div className="grid gap-1 text-[length:var(--text-sm)] font-medium text-ink">
          Callback token
          <code data-testid={ids.agentsShownToken} className={code}>
            {shown.callbackToken}
          </code>
          <span className="font-normal text-ink-muted">
            The agent sends it as a bearer token to /linear/graphql to report back.
          </span>
        </div>
      )}
      {copy === 'failed' ? (
        <p role="alert" className="m-0 text-[length:var(--text-sm)] text-alert">
          Couldn't copy. Select the text and copy it by hand.
        </p>
      ) : null}
    </Dialog>
  );
}
