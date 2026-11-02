import type { InstanceSettingsTab } from '../routes';
import { pagesTestIds } from '../features/pages/testIds';
import { InstanceAnalytics } from '../features/settings/instance/analytics/InstanceAnalytics';

export interface InstanceAnalyticsSlotProps {
  tab: InstanceSettingsTab;
}

export function InstanceAnalyticsSlot(props: InstanceAnalyticsSlotProps) {
  void props;
  return (
    <div data-testid={pagesTestIds.slotInstanceAnalytics}>
      <InstanceAnalytics />
    </div>
  );
}
