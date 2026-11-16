import type { InspectionSeverityCountsDto } from '@shiplean/core/api/views';

/** A bar's parts, bottom up: sev 1 (critical) to sev 4 (low), then bugs with no severity. */
export const SEVERITY_PARTS = ['sev1', 'sev2', 'sev3', 'sev4', 'unset'] as const;
export type SeverityPart = (typeof SEVERITY_PARTS)[number];

/** The bug colour, fading towards the surface as severity drops (design bugChart); no severity is muted. */
export const SEVERITY_FILLS: Record<SeverityPart, string> = {
  sev1: 'var(--type-bug)',
  sev2: 'color-mix(in srgb, var(--type-bug) 72%, var(--color-surface))',
  sev3: 'color-mix(in srgb, var(--type-bug) 45%, var(--color-surface))',
  sev4: 'color-mix(in srgb, var(--type-bug) 22%, var(--color-surface))',
  unset: 'color-mix(in srgb, var(--color-muted) 35%, var(--color-surface))',
};

export const SEVERITY_LABELS: Record<SeverityPart, string> = {
  sev1: 'Sev 1',
  sev2: 'Sev 2',
  sev3: 'Sev 3',
  sev4: 'Sev 4',
  unset: 'No severity',
};

/** The server's count for one part. */
export const severityCount = (counts: InspectionSeverityCountsDto, part: SeverityPart): number =>
  counts[part];

/** A bug's severity (1 to 4, or null) as its part. */
export const severityPart = (severity: number | null): SeverityPart =>
  SEVERITY_PARTS.find((part) => part === `sev${severity}`) ?? 'unset';

/** A per-100 figure, or "—" when no linked PR merged. */
export const per100 = (value: number | null): string => (value === null ? '—' : String(value));
