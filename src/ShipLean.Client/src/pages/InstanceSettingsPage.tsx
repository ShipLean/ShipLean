import { useParams } from 'react-router-dom';
import { pagesTestIds } from '../features/pages/testIds';
import { instanceSettingsTabs, type InstanceSettingsTab } from '../routes';
import { InstanceAgentsSlot } from '../slots/InstanceAgentsSlot';
import { InstanceGeneralSlot } from '../slots/InstanceGeneralSlot';
import { InstanceIntegrationsSlot } from '../slots/InstanceIntegrationsSlot';
import { InstanceProjectsSlot } from '../slots/InstanceProjectsSlot';
import { InstanceUsersSlot } from '../slots/InstanceUsersSlot';
import { instanceSettingsSlots } from './instanceSettingsSlots';
import { InstallScreen } from './InstallScreen';
import { NotFoundState } from './NotFoundState';
import { pageRouteIds } from './pageRouteIds';

// One instance settings tab, each in its own slot; a tab that does not exist is not found.
export function InstanceSettingsPage() {
  const { tab: raw } = useParams();
  const tab: InstanceSettingsTab | undefined = instanceSettingsTabs.find((t) => t === raw);
  if (tab === undefined) return <NotFoundState />;
  const slots = instanceSettingsSlots(tab);
  return (
    <InstallScreen
      routeId={pageRouteIds.instanceSettings}
      testId={pagesTestIds.instanceSettingsPage}
    >
      {slots.instanceGeneral && <InstanceGeneralSlot {...slots.instanceGeneral} />}
      {slots.instanceUsers && <InstanceUsersSlot {...slots.instanceUsers} />}
      {slots.instanceProjects && <InstanceProjectsSlot {...slots.instanceProjects} />}
      {slots.instanceIntegrations && <InstanceIntegrationsSlot {...slots.instanceIntegrations} />}
      {slots.instanceAgents && <InstanceAgentsSlot {...slots.instanceAgents} />}
    </InstallScreen>
  );
}
