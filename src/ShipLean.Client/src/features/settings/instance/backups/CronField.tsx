import { useEffect, useId, useState } from 'react';
import { backupsTestIds } from '../../../backups/testIds';
import { CRON_PREVIEW_DEBOUNCE_MS, cronPresets } from './cronPresets';
import { useCronPreview } from './backupsApi';

export interface CronFieldProps {
  value: string;
  onChange: (cron: string) => void;
  /** The server's message for `cron` after a failed save. */
  error: string | undefined;
  /** Report this section's own validity on mount and whenever it changes. */
  onValidityChange: (valid: boolean) => void;
}

/** The text, once it has stopped changing for the debounce interval. */
function useDebounced(text: string): string {
  const [settled, setSettled] = useState(text);
  useEffect(() => {
    const timer = setTimeout(() => setSettled(text), CRON_PREVIEW_DEBOUNCE_MS);
    return () => clearTimeout(timer);
  }, [text]);
  return settled;
}

/** A cron expression with presets, what it means in words, and the next runs, as the server reads it. */
export function CronField({ value, onChange, error, onValidityChange }: CronFieldProps) {
  const previewId = useId();
  const text = value.trim();
  const settled = useDebounced(text);
  const preview = useCronPreview(settled);
  // The answer is current only for the text it was asked about; while typing or loading there is none to trust.
  const current = settled === text && !preview.isPlaceholderData ? preview.data : undefined;
  const valid = current?.valid === true;
  useEffect(() => onValidityChange(valid), [valid, onValidityChange]);

  const shown = preview.isPlaceholderData || settled !== text ? preview.data : current;
  const message = current?.error ?? error;
  const stale = shown !== undefined && current === undefined;

  return (
    <div className="grid gap-2">
      <div role="group" aria-label="Presets" className="flex flex-wrap gap-1">
        {cronPresets.map((preset) => (
          <button
            key={preset.cron}
            type="button"
            data-testid={backupsTestIds.backupsCronPreset}
            aria-pressed={text === preset.cron}
            className="btn"
            onClick={() => onChange(preset.cron)}
          >
            {preset.label}
          </button>
        ))}
      </div>
      <input
        aria-label="Cron expression"
        aria-describedby={previewId}
        aria-invalid={message !== undefined}
        spellCheck={false}
        data-testid={backupsTestIds.backupsCronInput}
        className="h-7 w-52 rounded-control border border-edge bg-panel px-2 font-mono text-[length:var(--text-sm)] text-ink"
        value={value}
        onChange={(e) => onChange(e.target.value)}
      />
      <span className="text-[length:var(--text-xs)] text-ink-muted">
        Five fields in UTC: minute, hour, day of month, month, day of week (UTC). Runs must be at
        least 1 hour apart.
      </span>
      <div
        id={previewId}
        aria-live="polite"
        className={`grid gap-1 text-[length:var(--text-sm)] ${stale ? 'opacity-60' : ''}`}
      >
        {shown?.description != null ? (
          <span data-testid={backupsTestIds.backupsCronDescription}>{shown.description} (UTC)</span>
        ) : null}
        {message !== undefined && message !== null ? (
          <span role="alert" data-testid={backupsTestIds.backupsCronError} className="text-alert">
            {message}
          </span>
        ) : null}
        {shown !== undefined && shown.nextRuns.length > 0 ? (
          <ol className="m-0 grid list-none gap-0.5 p-0 font-mono text-[length:var(--text-xs)]">
            {shown.nextRuns.map((run) => (
              <li key={run.at} data-testid={backupsTestIds.backupsCronNextRun}>
                {run.label}
              </li>
            ))}
          </ol>
        ) : null}
      </div>
    </div>
  );
}
