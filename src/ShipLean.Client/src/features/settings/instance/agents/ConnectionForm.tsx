import { useState } from 'react';
import { settingsTestIds as ids } from '../../testIds';
import { FieldError } from '../integrations/FieldError';
import { errorField } from '../integrations/model';
import { errorText } from '../model';
import { isComplete, type AgentConnectionPut } from './model';

const input =
  'h-7 w-full max-w-md rounded-control border border-edge bg-panel px-2 text-[length:var(--text-sm)] text-ink disabled:opacity-60';

interface ConnectionFormProps {
  initial: AgentConnectionPut;
  submitLabel: string;
  pending: boolean;
  error: unknown;
  onSubmit: (put: AgentConnectionPut) => void;
  onCancel: () => void;
}

/** Name and receiver URL, for adding a connection and for editing one. A server error shows under the field it blames. */
export function ConnectionForm({
  initial,
  submitLabel,
  pending,
  error,
  onSubmit,
  onCancel,
}: ConnectionFormProps) {
  const [put, setPut] = useState(initial);
  const blamed = errorField(error);
  const message = error ? errorText(error) : undefined;
  const named = blamed === 'name' || blamed === 'receiverUrl';
  return (
    <div className="grid gap-3">
      <label className="grid gap-1 text-[length:var(--text-sm)] font-medium text-ink">
        Name
        <input
          data-testid={ids.agentsFormName}
          className={input}
          value={put.name}
          maxLength={80}
          autoComplete="off"
          disabled={pending}
          onChange={(e) => setPut((p) => ({ ...p, name: e.target.value }))}
        />
        <FieldError
          message={blamed === 'name' ? message : undefined}
          testId={ids.agentsFieldError}
        />
      </label>
      <label className="grid gap-1 text-[length:var(--text-sm)] font-medium text-ink">
        Receiver URL
        <input
          data-testid={ids.agentsFormUrl}
          className={`${input} font-mono`}
          value={put.receiverUrl}
          spellCheck={false}
          autoComplete="off"
          disabled={pending}
          onChange={(e) => setPut((p) => ({ ...p, receiverUrl: e.target.value }))}
        />
        <span className="font-normal text-ink-muted">
          Where ShipLean sends the webhook. https, or http on localhost.
        </span>
        <FieldError
          message={blamed === 'receiverUrl' ? message : undefined}
          testId={ids.agentsFieldError}
        />
      </label>
      {message !== undefined && !named ? (
        <p
          role="alert"
          data-testid={ids.agentsActionError}
          className="m-0 text-[length:var(--text-sm)] text-alert"
        >
          {message}
        </p>
      ) : null}
      <div className="flex items-center gap-2">
        <button
          type="button"
          data-testid={ids.agentsFormCancel}
          className="btn-ghost"
          disabled={pending}
          onClick={onCancel}
        >
          Cancel
        </button>
        <button
          type="button"
          data-testid={ids.agentsFormSave}
          className="btn-signal"
          disabled={pending || !isComplete(put)}
          onClick={() => onSubmit({ name: put.name.trim(), receiverUrl: put.receiverUrl.trim() })}
        >
          {submitLabel}
        </button>
      </div>
    </div>
  );
}
