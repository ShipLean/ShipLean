import { useState } from 'react';
import type { PromptDto, PromptVersionDto, PromptVersionSource } from '@shiplean/core/api/prompts';
import { Chip } from '../../../components/Chip';
import { promptsTestIds as ids } from '../testIds';
import { usePromptHistory, useUseVersion } from '../usePrompts';

const sourceLabel: Record<PromptVersionSource, string> = {
  editor: 'Editor',
  builtInUpdate: 'Built-in update',
  migrated: 'Moved from Integrations',
  mcp: 'MCP',
};

const inUseTint = 'bg-[color-mix(in_srgb,var(--color-accent-soft)_60%,transparent)]';

// Every version, newest first, with Use vN for admins, ending in the built-in the versions started from.
export function HistoryTab(props: {
  projectId: string;
  prompt: PromptDto;
  canEdit: boolean;
  onUseBuiltIn: () => void;
}) {
  const { projectId, prompt, canEdit } = props;
  const history = usePromptHistory(projectId, prompt.key, true);
  const use = useUseVersion(projectId, prompt.key);
  const [confirming, setConfirming] = useState<number | null>(null);

  if (history.isError) {
    return (
      <p role="alert" className="m-0 p-3 text-alert">
        The history could not be loaded.
      </p>
    );
  }
  if (!history.data) return <p className="m-0 p-3 text-ink-muted">Loading history...</p>;

  const { versions, baseLabel } = history.data;
  const anyInUse = versions.some((v) => v.inUse);
  const confirm = (number: number) => {
    use.mutate(number, { onSettled: () => setConfirming(null) });
  };
  const actions = (v: PromptVersionDto) => {
    if (v.inUse) {
      return (
        <span data-testid={ids.historyInUsePill}>
          <Chip tone="accent">In use</Chip>
        </span>
      );
    }
    if (!canEdit) return null;
    if (confirming !== v.number) {
      return (
        <button
          type="button"
          data-testid={ids.historyUse}
          className="btn-ghost touch:min-h-[var(--touch-target)]"
          onClick={() => setConfirming(v.number)}
        >
          Use v{v.number}
        </button>
      );
    }
    return (
      <span className="flex flex-wrap items-center gap-2">
        <span data-testid={ids.historyNote}>Agents get v{v.number} from their next call.</span>
        <button
          type="button"
          data-testid={ids.historyConfirm}
          className="btn-signal touch:min-h-[var(--touch-target)]"
          disabled={use.isPending}
          onClick={() => confirm(v.number)}
        >
          Confirm
        </button>
        <button
          type="button"
          className="btn touch:min-h-[var(--touch-target)]"
          onClick={() => setConfirming(null)}
        >
          Cancel
        </button>
      </span>
    );
  };

  return (
    <section
      data-testid={ids.panelHistory}
      aria-label={`History of ${prompt.name}`}
      className="grid gap-2 pb-3"
    >
      {use.isError ? (
        <p role="alert" className="m-0 px-3 text-alert">
          {use.error.message}
        </p>
      ) : null}
      {versions.length === 0 ? (
        <p data-testid={ids.historyEmpty} className="m-0 p-3 text-ink-muted">
          No custom versions yet.
        </p>
      ) : (
        <table
          data-testid={ids.historyTable}
          className="w-full border-collapse text-left text-[length:var(--text-sm)] [&_td]:border-b [&_td]:border-edge [&_td]:px-3 [&_td]:py-2 [&_th]:border-b [&_th]:border-edge [&_th]:px-3 [&_th]:py-2 [&_th]:text-[length:var(--text-xs)] [&_th]:font-medium [&_th]:text-ink-muted"
        >
          <thead>
            <tr>
              <th>Version</th>
              <th>What changed</th>
              <th>Who</th>
              <th>When</th>
              <th>Source</th>
              <th aria-label="Actions" />
            </tr>
          </thead>
          <tbody>
            {versions.map((v) => (
              <tr
                key={v.number}
                data-testid={v.inUse ? ids.historyRowInUse : ids.historyRow}
                className={
                  v.inUse
                    ? 'bg-[color-mix(in_srgb,var(--color-accent-soft)_60%,transparent)]'
                    : undefined
                }
              >
                <td>v{v.number}</td>
                <td data-testid={ids.historyVersionNote}>{v.note}</td>
                <td>
                  {v.by}
                  {v.agent ? (
                    <span
                      data-testid={ids.historyAgent}
                      className="flex flex-wrap items-center gap-1"
                    >
                      via {v.agent} <Chip tone="purple">Agent</Chip>
                    </span>
                  ) : null}
                </td>
                <td>{v.atLabel}</td>
                <td>{sourceLabel[v.source]}</td>
                <td>{actions(v)}</td>
              </tr>
            ))}
            {baseLabel === null ? null : (
              <tr
                data-testid={ids.historyBuiltInRow}
                className={`text-ink-muted ${anyInUse ? '' : inUseTint}`}
              >
                <td>—</td>
                <td>
                  Built-in from ShipLean{' '}
                  <code
                    data-testid={ids.historyBuiltInHash}
                    className="font-[family-name:var(--font-mono)]"
                  >
                    {prompt.builtInHash.slice(0, 12)}
                  </code>
                </td>
                <td>ShipLean</td>
                <td>{baseLabel}</td>
                <td />
                <td>
                  {anyInUse ? null : (
                    <span data-testid={ids.historyInUsePill}>
                      <Chip tone="accent">In use</Chip>
                    </span>
                  )}
                  {canEdit && anyInUse ? (
                    <button
                      type="button"
                      data-testid={ids.historyBuiltInUse}
                      className="btn-ghost touch:min-h-[var(--touch-target)]"
                      onClick={props.onUseBuiltIn}
                    >
                      Use built-in
                    </button>
                  ) : null}
                </td>
              </tr>
            )}
          </tbody>
        </table>
      )}
      {versions.length === 0 ? null : (
        <p
          data-testid={ids.historyFootNote}
          className="m-0 px-3 text-[length:var(--text-sm)] text-ink-muted"
        >
          Nothing is deleted. “Use built-in” stops using your version; it stays here to restore.
        </p>
      )}
    </section>
  );
}
