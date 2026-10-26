import type { WorkItemDto } from '../../../api/workItems';
import { featureTestIds as ids } from '../testIds';

// When the status was last written or confirmed, and who by. The label and staleness are the server's.
export function VerifiedChip({ verified }: { verified: WorkItemDto['statusVerified'] }) {
  if (verified === null) {
    return (
      <span
        data-testid={ids.statusVerified}
        className="text-[length:var(--text-xs)] text-ink-muted"
      >
        Not verified yet
      </span>
    );
  }
  return (
    <>
      <span
        data-testid={ids.statusVerified}
        data-stale={verified.stale}
        className={`rounded-control border px-1.5 text-[length:var(--text-xs)] font-semibold ${
          verified.stale ? 'border-caution text-caution' : 'border-edge bg-signal-soft text-good'
        }`}
      >
        {verified.stale ? '⚠' : '✓'} Verified {verified.atLabel}
      </span>
      <span
        data-testid={ids.statusVerifiedBy}
        className="text-[length:var(--text-xs)] text-ink-muted"
      >
        {verified.agent === null ? (verified.by?.name ?? '') : `${verified.agent} (agent)`}
        {verified.via === null ? null : (
          <>
            {' through '}
            <code className="font-[family-name:var(--font-mono)]">{verified.via}</code>
          </>
        )}
      </span>
    </>
  );
}
