import { useState } from 'react';
import { componentTestIds } from './testIds';

/** Shows a password once. Done removes it from the DOM; there is no way to show it again. */
export function OneTimePasswordReveal({
  password,
  onDone,
}: {
  password: string;
  onDone?: () => void;
}) {
  const [visible, setVisible] = useState(true);
  const [copy, setCopy] = useState<'idle' | 'copied' | 'failed'>('idle');

  if (!visible) return null;

  const copyPassword = async () => {
    try {
      await navigator.clipboard.writeText(password);
      setCopy('copied');
    } catch {
      setCopy('failed');
    }
  };

  return (
    <div
      data-testid={componentTestIds.otpReveal}
      className="grid gap-2 rounded-panel border border-edge bg-panel p-3"
    >
      <p className="text-[length:var(--text-sm)] text-ink-muted">
        Copy this password now. It is shown once and can’t be shown again.
      </p>
      <code
        data-testid={componentTestIds.otpPassword}
        className="rounded-control bg-sunken px-2 py-1 font-[family-name:var(--font-mono)] text-[length:var(--text-base)] text-ink select-all"
      >
        {password}
      </code>
      {copy === 'failed' ? (
        <p
          role="alert"
          className="text-[length:var(--text-sm)]"
          style={{ color: 'var(--color-danger)' }}
        >
          Couldn’t copy. Select the password and copy it by hand.
        </p>
      ) : null}
      <div className="flex gap-2">
        <button
          type="button"
          data-testid={componentTestIds.otpCopy}
          className="btn-ghost"
          onClick={() => void copyPassword()}
        >
          {copy === 'copied' ? 'Copied' : 'Copy'}
        </button>
        <button
          type="button"
          data-testid={componentTestIds.otpDone}
          className="btn-signal"
          onClick={() => {
            setVisible(false);
            onDone?.();
          }}
        >
          Done
        </button>
      </div>
    </div>
  );
}
