import { backupsTestIds } from '../../../backups/testIds';
import type { BackupSettings } from './backupsApi';

export interface BackupStatusPanelProps {
  settings: BackupSettings;
}

// TODO: replace with the real panel: the health line, Back up now (it calls useRunBackupNow itself) and the history
// of runs. Back up now stays allowed while scheduled backups are off.
export function BackupStatusPanel({ settings }: BackupStatusPanelProps) {
  return (
    <div data-testid={backupsTestIds.backupsHealth} data-health={settings.health}>
      {settings.health}
    </div>
  );
}
