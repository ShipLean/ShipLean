import { SaveBarSpacer } from '../touch/SaveBar';
import { pagesTestIds } from '../features/pages/testIds';
import { ProfileAccountSlot } from '../slots/ProfileAccountSlot';
import { ProfileConnectedAgentsSlot } from '../slots/ProfileConnectedAgentsSlot';
import { ProfileDevicesSlot } from '../slots/ProfileDevicesSlot';
import { ProfileIdentitySlot } from '../slots/ProfileIdentitySlot';
import { pageRouteIds } from './pageRouteIds';
import { InstallScreen } from './InstallScreen';

export function ProfilePage() {
  return (
    <InstallScreen routeId={pageRouteIds.profile} testId={pagesTestIds.profilePage}>
      {/* InstallScreen pads 16px; the phone layout wants 12px gutters, and that file is shared. */}
      <div className="touch:-mx-1 touch:-mt-1 touch:grid touch:gap-3">
        <p
          data-testid="profile-intro"
          className="hidden px-3 touch:block text-[length:var(--text-sm)] text-ink-muted"
        >
          Only you see these settings. Your name and avatar show wherever your work does.
        </p>
        <ProfileIdentitySlot />
        <ProfileAccountSlot />
        <ProfileDevicesSlot />
        <ProfileConnectedAgentsSlot />
        <SaveBarSpacer />
      </div>
    </InstallScreen>
  );
}
