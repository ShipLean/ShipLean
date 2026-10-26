import type { TokenEdit } from './model';
import { noTokenEdit } from './model';
import { FieldError } from './FieldError';

const input =
  'h-7 w-full max-w-md rounded-control border border-edge bg-panel px-2 font-mono text-[length:var(--text-sm)] text-ink disabled:opacity-60';

export interface TokenFieldIds {
  input: string;
  lastFour: string;
  replace: string;
  clear: string;
  env: string;
}

interface TokenFieldProps {
  label: string;
  hint: string;
  /** null: no token is set. */
  lastFour: string | null;
  fromEnv: boolean;
  edit: TokenEdit;
  ids: TokenFieldIds;
  disabled: boolean;
  error?: string;
  onChange: (edit: TokenEdit) => void;
}

/** A write-only secret: the saved one shows its last four characters with Replace and Clear, never the value. */
export function TokenField({
  label,
  hint,
  lastFour,
  fromEnv,
  edit,
  ids,
  disabled,
  error,
  onChange,
}: TokenFieldProps) {
  const showInput = lastFour === null || edit.replacing;
  return (
    <div className="grid gap-1 text-[length:var(--text-sm)] font-medium text-ink">
      <label htmlFor={ids.input}>{label}</label>
      {showInput && !edit.clear ? (
        <input
          id={ids.input}
          data-testid={ids.input}
          type="password"
          className={input}
          value={edit.value}
          autoComplete="off"
          spellCheck={false}
          disabled={disabled}
          onChange={(e) => onChange({ ...edit, value: e.target.value })}
        />
      ) : (
        <div className="flex items-center gap-2 font-normal">
          {edit.clear ? (
            <span className="text-ink-muted">Removed when you save</span>
          ) : (
            <span data-testid={ids.lastFour} className="font-mono text-ink">
              {`•••• ${lastFour ?? ''}`}
            </span>
          )}
          {fromEnv ? (
            <span data-testid={ids.env} className="text-ink-muted">
              From the environment
            </span>
          ) : null}
          <button
            type="button"
            data-testid={ids.replace}
            className="btn-ghost"
            disabled={disabled}
            onClick={() => onChange({ ...noTokenEdit, replacing: true })}
          >
            Replace
          </button>
          {fromEnv || edit.clear ? null : (
            <button
              type="button"
              data-testid={ids.clear}
              className="btn-ghost"
              disabled={disabled}
              onClick={() => onChange({ ...noTokenEdit, clear: true })}
            >
              Clear
            </button>
          )}
        </div>
      )}
      <span className="font-normal text-ink-muted">{hint}</span>
      <FieldError message={error} />
    </div>
  );
}
