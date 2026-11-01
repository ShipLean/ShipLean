import { componentTestIds } from '../../components/testIds';

/** The Feature marker on a point or card: the accent chip, 16px high as the retro screens draw it. */
export function FeatureChip() {
  return (
    <span
      data-testid={componentTestIds.chip}
      data-tone="accent"
      className="inline-flex h-4 items-center rounded-full bg-signal-soft px-[7px] text-[length:var(--text-xs)] font-medium whitespace-nowrap text-ink"
    >
      Feature
    </span>
  );
}
