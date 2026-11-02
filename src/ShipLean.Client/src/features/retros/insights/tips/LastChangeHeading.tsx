import type { RetroInsightsMode } from '../../../../api/retros';
import { retrosTestIds as ids } from '../../testIds';

// TODO: the Last change tooltip, worded for the mode, replaces this plain heading.
export function LastChangeHeading({
  className,
  mode,
}: {
  className: string;
  mode: RetroInsightsMode;
}) {
  return (
    <th data-testid={ids.retroTipVerdictHead} data-mode={mode} className={className}>
      Last change
    </th>
  );
}
