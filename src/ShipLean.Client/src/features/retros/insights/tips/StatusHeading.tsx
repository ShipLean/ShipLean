import type { RetroInsightsMode, RetroThemeStatus } from '../../../../api/retros';
import { retrosTestIds as ids } from '../../testIds';
import { DefinitionTip } from './DefinitionTip';
import { Tip } from './Tip';
import { statusHeadIntro, statusWording } from './tipWording';

const ROWS: { status: RetroThemeStatus; term: string }[] = [
  { status: 'recurring', term: 'Recurring' },
  { status: 'beingAddressed', term: 'Being addressed' },
  { status: 'resolved', term: 'Resolved' },
  { status: 'quiet', term: 'Quiet' },
];

export function StatusHeading({ className, mode }: { className: string; mode: RetroInsightsMode }) {
  return (
    <th data-testid={ids.retroTipStatusHead} data-mode={mode} className={className}>
      <Tip
        tip={
          <DefinitionTip
            intro={statusHeadIntro(mode)}
            rows={ROWS.map((r) => ({ term: r.term, meaning: statusWording(r.status, mode) }))}
          />
        }
      >
        Status
      </Tip>
    </th>
  );
}
