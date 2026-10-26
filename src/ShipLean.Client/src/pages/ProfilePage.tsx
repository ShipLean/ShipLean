import { pagesTestIds } from '../features/pages/testIds';
import { ProfileAccountSlot } from '../slots/ProfileAccountSlot';
import { ProfileConnectedAgentsSlot } from '../slots/ProfileConnectedAgentsSlot';
import { ProfileIdentitySlot } from '../slots/ProfileIdentitySlot';
import { pageRouteIds } from './pageRouteIds';
import { InstallScreen } from './InstallScreen';

export function ProfilePage() {
  return (
    <InstallScreen routeId={pageRouteIds.profile} testId={pagesTestIds.profilePage}>
      <ProfileIdentitySlot />
      <ProfileAccountSlot />
      <ProfileConnectedAgentsSlot />
    </InstallScreen>
  );
}
