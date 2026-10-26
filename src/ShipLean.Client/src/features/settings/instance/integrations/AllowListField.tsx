import { settingsTestIds } from '../../testIds';
import { FieldError } from './FieldError';

interface AllowListFieldProps {
  text: string;
  disabled: boolean;
  error?: string;
  onChange: (text: string) => void;
}

export function AllowListField({ text, disabled, error, onChange }: AllowListFieldProps) {
  return (
    <label className="grid gap-1 text-[length:var(--text-sm)] font-medium text-ink">
      Preview allow-list
      <textarea
        data-testid={settingsTestIds.integrationsAllowHosts}
        className="h-24 w-full max-w-md rounded-control border border-edge bg-panel px-2 py-1 font-mono text-[length:var(--text-sm)] text-ink disabled:opacity-60"
        value={text}
        spellCheck={false}
        disabled={disabled}
        onChange={(e) => onChange(e.target.value)}
      />
      <span className="font-normal text-ink-muted">
        Hosts a link card may fetch a preview from, one a line, such as docs.example.com: no scheme,
        path or port.
      </span>
      <FieldError message={error} />
    </label>
  );
}
