import type { RetroInsightsMode, RetroVerdict } from '../../../../api/retros';
import { retrosTestIds as ids } from '../../testIds';
import { DefinitionTip } from './DefinitionTip';
import { Tip } from './Tip';
import { verdictHeadIntro, verdictWording } from './tipWording';

const ROWS: { verdict: RetroVerdict; term: string }[] = [
  { verdict: 'helped', term: 'Helped' },
  { verdict: 'someEffect', term: 'Some effect' },
  { verdict: 'noEffect', term: 'No effect' },
  { verdict: 'tooEarly', term: 'Too early' },
];

export function LastChangeHeading({
  className,
  mode,
}: {
  className: string;
  mode: RetroInsightsMode;
}) {
  return (
    <th data-testid={ids.retroTipVerdictHead} data-mode={mode} className={className}>
      <Tip
        tip={
          <DefinitionTip
            intro={verdictHeadIntro}
            rows={ROWS.map((r) => ({ term: r.term, meaning: verdictWording(r.verdict, mode) }))}
          />
        }
      >
        Last change
      </Tip>
    </th>
  );
}
