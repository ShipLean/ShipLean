import { useState } from 'react';
import type { PromptDto, PromptVersionDto, PromptVersionSource } from '@shiplean/core/api/prompts';
import { promptsTestIds as ids } from '../testIds';
import { usePromptHistory, useUseVersion } from '../usePrompts';

const sourceLabel: Record<PromptVersionSource, string> = {
  editor: 'Editor',
  builtInUpdate: 'Built-in update',
  migrated: 'Moved from Integrations',
};

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
      <p role="alert" className="m-0 text-alert">
        The history could not be loaded.
      </p>
    );
  }
  if (!history.data) return <p className="m-0 text-ink-muted">Loading history...</p>;

  const { versions, baseLabel } = history.data;
  const anyInUse = versions.some((v) => v.inUse);
  const confirm = (number: number) => {
    use.mutate(number, { onSettled: () => setConfirming(null) });
  };
  const actions = (v: PromptVersionDto) => {
    if (v.inUse) return <span className="chip">In use</span>;
    if (!canEdit) return null;
    if (confirming !== v.number) {
      return (
        <button
          type="button"
          data-testid={ids.historyUse}
          className="btn touch:min-h-[var(--touch-target)]"
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
      className="grid gap-2"
    >
      {use.isError ? (
        <p role="alert" className="m-0 text-alert">
          {use.error.message}
        </p>
      ) : null}
      {versions.length === 0 ? (
        <p data-testid={ids.historyEmpty} className="m-0 text-ink-muted">
          No custom versions yet.
        </p>
      ) : (
        <table
          data-testid={ids.historyTable}
          className="w-full text-left text-[length:var(--text-sm)]"
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
                className={v.inUse ? 'bg-signal-soft' : undefined}
              >
                <td>v{v.number}</td>
                <td>{v.note}</td>
                <td>{v.by}</td>
                <td>{v.atLabel}</td>
                <td>{sourceLabel[v.source]}</td>
                <td>{actions(v)}</td>
              </tr>
            ))}
            {baseLabel === null ? null : (
              <tr data-testid={ids.historyBuiltInRow}>
                <td>Built-in</td>
                <td>The versions started from the {baseLabel}</td>
                <td />
                <td />
                <td />
                <td>
                  {canEdit && anyInUse ? (
                    <button
                      type="button"
                      data-testid={ids.historyBuiltInUse}
                      className="btn touch:min-h-[var(--touch-target)]"
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
    </section>
  );
}
