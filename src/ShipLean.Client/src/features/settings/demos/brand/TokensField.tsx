import { useState } from 'react';
import type { ProjectDemoBrandDto } from '@shiplean/core/api/demos';
import { showToast } from '../../../../components/toastStore';
import { useSaveDemoTokens } from '../../../demos/demosApi';
import { demosTestIds as ids } from '../../../demos/testIds';

const textarea =
  'w-full max-w-xl rounded-control border border-edge bg-panel px-2 py-1 font-mono text-[length:var(--text-sm)] text-ink disabled:opacity-60';

/** The design tokens text, listing the seven names slides use as hints, saved on its own button. */
export function TokensField({
  projectId,
  brand,
  readOnly,
}: {
  projectId: string;
  brand: ProjectDemoBrandDto;
  readOnly: boolean;
}) {
  const save = useSaveDemoTokens(projectId);
  const [text, setText] = useState(brand.tokens);
  const [error, setError] = useState<string | null>(null);

  return (
    <div className="grid gap-2 text-[length:var(--text-sm)]">
      <label className="grid gap-1 font-medium text-ink">
        Design tokens
        <textarea
          data-testid={ids.demoTokens}
          className={textarea}
          rows={5}
          value={text}
          readOnly={readOnly}
          disabled={save.isPending}
          aria-invalid={error === null ? undefined : true}
          placeholder="--color-accent: #2563c9;"
          onChange={(event) => setText(event.target.value)}
        />
      </label>
      {error === null ? null : (
        <div
          data-testid={ids.demoTokensError}
          role="alert"
          className="max-w-xl border-l-[3px] border-alert px-2.5 py-1.5 text-alert"
        >
          {error}
        </div>
      )}
      {readOnly ? null : (
        <div>
          <button
            type="button"
            data-testid={ids.demoTokensSave}
            className="btn-signal"
            disabled={save.isPending}
            onClick={() =>
              save.mutate(
                { tokens: text.trim() === '' ? null : text },
                {
                  onSuccess: () => setError(null),
                  onError: (failure) => {
                    setError(failure.message);
                    showToast(failure.message, { tone: 'danger' });
                  },
                },
              )
            }
          >
            Save tokens
          </button>
        </div>
      )}
      <div
        data-testid={ids.demoTokensHints}
        className="grid max-w-xl gap-1 rounded-control border border-dashed border-edge px-3 py-2"
      >
        <span className="text-[length:var(--text-xs)] font-medium text-ink-muted uppercase">
          The seven names slides use, with ShipLean&apos;s values
        </span>
        {brand.defaultTokens.map((token) => (
          <div key={token.name} className="flex flex-wrap gap-x-3">
            <code className="text-ink">{token.name}</code>
            <span className="font-mono text-ink-muted">{token.value}</span>
          </div>
        ))}
        <span className="text-[length:var(--text-xs)] text-ink-muted">
          One per line as <code>--name: value;</code>. Leave a name out and it keeps ShipLean&apos;s
          value. Other names are saved and not used.
        </span>
      </div>
    </div>
  );
}
