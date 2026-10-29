import type { ActionJson } from '../../../../api/persisted';
import { Markdown } from '../../../../components/Markdown';
import { useRenderedMarkdown } from '../../../workItem/workItemApi';
import { boardSettingsTestIds as ids } from '../../testIds';
import type { AgentConnections } from './useAgentConnections';

/** The server's limit on a prompt (BoardSettingsValidation.MaxPromptLength), in UTF-16 units like the server's Length. */
export const MAX_PROMPT_LENGTH = 20_000;

/** One Send to agent action: the prompt as markdown with the server's render beside it, the connection, remove. */
export function ActionRow({
  action,
  connections,
  readOnly,
  canRemove,
  onChange,
  onRemove,
}: {
  action: ActionJson;
  connections: AgentConnections;
  readOnly: boolean;
  canRemove: boolean;
  onChange(next: ActionJson): void;
  onRemove(): void;
}) {
  const blank = action.prompt.trim() === '';
  const tooLong = action.prompt.length > MAX_PROMPT_LENGTH;
  const rendered = useRenderedMarkdown(action.prompt, !blank && !tooLong);
  const known =
    action.connectionId === null || connections.options.some((o) => o.id === action.connectionId);
  return (
    <div
      data-testid={ids.actionRow}
      className="grid gap-2 rounded-control border border-edge bg-canvas p-2"
    >
      <div className="grid grid-cols-[minmax(0,1fr)_240px] gap-3">
        <label className="grid gap-1 text-[length:var(--text-xs)] text-ink-muted">
          Send to agent · prompt, markdown
          <textarea
            data-testid={ids.actionPrompt}
            className="field w-full font-mono text-[length:var(--text-sm)]"
            rows={4}
            aria-invalid={tooLong}
            placeholder="What the agent should do when an item makes this move"
            value={action.prompt}
            readOnly={readOnly}
            onChange={(event) => onChange({ ...action, prompt: event.target.value })}
          />
          {tooLong ? (
            <span data-testid={ids.actionPromptError} role="alert" className="text-alert">
              A prompt is at most {MAX_PROMPT_LENGTH.toLocaleString('en-US')} characters. This one
              is {action.prompt.length.toLocaleString('en-US')}.
            </span>
          ) : null}
        </label>
        <div
          data-testid={ids.actionPreview}
          className="rounded-control border border-edge bg-panel px-3 py-2 text-[length:var(--text-sm)]"
        >
          {blank || tooLong ? (
            <span className="text-ink-muted">Nothing yet</span>
          ) : (
            <Markdown html={rendered.data?.html ?? ''} />
          )}
        </div>
      </div>
      <div className="flex items-center gap-2 text-[length:var(--text-sm)]">
        <label className="flex items-center gap-2 text-ink-muted">
          Connection
          <select
            data-testid={ids.actionConnection}
            className="field"
            disabled={readOnly}
            value={action.connectionId ?? ''}
            onChange={(event) =>
              onChange({
                ...action,
                connectionId: event.target.value === '' ? null : event.target.value,
              })
            }
          >
            <option value="">
              {connections.defaultName === null
                ? 'Project default'
                : `Project default (${connections.defaultName})`}
            </option>
            {connections.options.map((option) => (
              <option key={option.id} value={option.id}>
                {option.name}
              </option>
            ))}
            {known ? null : (
              <option value={action.connectionId ?? ''}>Unavailable connection</option>
            )}
          </select>
        </label>
        <span className="flex-1" />
        <button
          type="button"
          data-testid={ids.actionRemove}
          disabled={readOnly || !canRemove}
          title={canRemove ? 'Remove this action' : 'A transition keeps at least one action'}
          className="h-6 rounded-control px-2 text-ink-muted hover:bg-sunken disabled:cursor-default disabled:opacity-35"
          onClick={onRemove}
        >
          Remove action
        </button>
      </div>
    </div>
  );
}
