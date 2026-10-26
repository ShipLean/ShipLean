import { settingsTestIds } from '../testIds';

export function ReadOnlyBanner({ projectName, admins }: { projectName: string; admins: string[] }) {
  return (
    <div
      data-testid={settingsTestIds.readOnlyBanner}
      className="mb-4 rounded-panel border border-edge bg-signal-soft px-4 py-2 text-[length:var(--text-sm)] text-ink"
    >
      You&apos;re a member of {projectName}, not an admin, so these settings are read-only.
      {admins.length > 0 ? ` Ask ${admins.join(' or ')} to change them.` : ''}
    </div>
  );
}
