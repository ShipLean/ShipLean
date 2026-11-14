import { useState } from 'react';
import { demosTestIds as ids } from '../../../demos/testIds';

/** Disconnect, asking first. The confirming button keeps the test id: the second press is the one that calls. */
export function DisconnectControl({
  channelTitle,
  busy,
  onConfirm,
}: {
  channelTitle: string;
  busy: boolean;
  onConfirm(): void;
}) {
  const [asking, setAsking] = useState(false);
  return (
    <span className="flex flex-wrap items-center gap-2 touch:flex-col touch:items-stretch">
      {asking ? (
        <span className="text-ink">
          Disconnect {channelTitle}? ShipLean asks Google to revoke its access and stops publishing
          demos there.
        </span>
      ) : null}
      <button
        type="button"
        data-testid={ids.youtubeDisconnect}
        className="btn-ghost text-alert touch:min-h-[var(--touch-target)]"
        disabled={busy}
        onClick={() => {
          if (!asking) return setAsking(true);
          setAsking(false);
          onConfirm();
        }}
      >
        {asking ? 'Yes, disconnect' : 'Disconnect'}
      </button>
      {asking ? (
        <button
          type="button"
          className="btn-ghost touch:min-h-[var(--touch-target)]"
          onClick={() => setAsking(false)}
        >
          Keep it
        </button>
      ) : null}
    </span>
  );
}
