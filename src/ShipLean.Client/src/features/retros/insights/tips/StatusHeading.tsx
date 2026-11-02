import type { RetroInsightsMode } from '../../../../api/retros';
import { retrosTestIds as ids } from '../../testIds';

// TODO: the Status tooltip, worded for the mode, replaces this plain heading.
export function StatusHeading({ className, mode }: { className: string; mode: RetroInsightsMode }) {
  return (
    <th data-testid={ids.retroTipStatusHead} data-mode={mode} className={className}>
      Status
    </th>
  );
}
