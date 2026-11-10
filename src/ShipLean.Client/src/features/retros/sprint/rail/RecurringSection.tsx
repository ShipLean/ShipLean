import type { RetroMentionStripDto } from '@shiplean/core/api/retros';
import type { SprintRef } from '@shiplean/core/api/refs';
import { MentionStrip } from '../../../retroShared/MentionStrip';
import { retrosTestIds as ids } from '../../testIds';
import { RailSection } from './RailSection';

/** Recurring themes, each with its mention strip and the dashed line where a fix shipped. */
export function RecurringSection({
  strips,
  sprints,
}: {
  strips: RetroMentionStripDto[];
  sprints: SprintRef[];
}) {
  const names = sprints.map((s) => s.name);
  return (
    <RailSection
      testId={ids.retroRailRecurring}
      title="Recurring"
      hint="Mentioned in 3 of the last 4 sprints with no change that helped. Dashed line: a fix shipped."
      empty={strips.length === 0}
    >
      {strips.map((strip) => (
        <div key={strip.theme.id} className="flex items-center justify-between gap-2">
          <span>{strip.theme.name}</span>
          <MentionStrip
            counts={strip.counts}
            fixAt={strip.fixAt}
            label={strip.theme.name}
            sprintNames={names}
          />
        </div>
      ))}
    </RailSection>
  );
}
