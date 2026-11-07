import { useEffect } from 'react';
import { backupsTestIds } from '../../../backups/testIds';

export interface CronFieldProps {
  value: string;
  onChange: (cron: string) => void;
  /** The server's message for `cron` after a failed save. */
  error: string | undefined;
  /** Report this section's own validity on mount and whenever it changes. */
  onValidityChange: (valid: boolean) => void;
}

// TODO: replace with the real field: presets, the description and next runs from useCronPreview (called inside),
// and validity from the server's reading of the expression.
export function CronField({ value, onChange, onValidityChange }: CronFieldProps) {
  const valid = value.trim() !== '';
  useEffect(() => onValidityChange(valid), [valid, onValidityChange]);
  return (
    <input
      aria-label="Cron expression"
      data-testid={backupsTestIds.backupsCronInput}
      value={value}
      onChange={(e) => onChange(e.target.value)}
    />
  );
}
