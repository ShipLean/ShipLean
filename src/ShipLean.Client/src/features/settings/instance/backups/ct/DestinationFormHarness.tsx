import { useState } from 'react';
import { BackupDestinationForm } from '../BackupDestinationForm';
import { draftOf, type BackupDraft, type BackupErrors } from '../backupForm';
import type { BackupSettings } from '../backupsApi';

// Mounts the destination form alone with server errors fixed by the test, and shows the validity it reports.
export function DestinationFormHarness({
  settings,
  errors = {},
}: {
  settings: BackupSettings;
  errors?: BackupErrors;
}) {
  const [draft, setDraft] = useState<BackupDraft>(() => draftOf(settings));
  const [valid, setValid] = useState<boolean | null>(null);
  return (
    <div>
      <BackupDestinationForm
        settings={settings}
        draft={draft}
        errors={errors}
        onChange={(patch) => setDraft((d) => ({ ...d, ...patch }))}
        onValidityChange={setValid}
      />
      <output data-testid="destination-harness-valid">{String(valid)}</output>
      <output data-testid="destination-harness-draft">{JSON.stringify(draft)}</output>
    </div>
  );
}
