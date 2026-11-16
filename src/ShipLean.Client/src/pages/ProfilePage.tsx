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
        <div className="mb-3 flex flex-wrap items-baseline gap-x-3 touch:mb-0 touch:px-3">
          <h1 className="text-[length:var(--text-lg)] font-semibold text-ink touch:hidden">
            Your profile
          </h1>
          <p
            data-testid={pagesTestIds.profileIntro}
            className="text-[length:var(--text-sm)] text-ink-muted"
          >
            Only you see these settings. Your name and avatar show wherever your work does.
          </p>
        </div>
        <ProfileIdentitySlot />
        <ProfileAccountSlot />
        <ProfileDevicesSlot />
        <ProfileConnectedAgentsSlot />
        <SaveBarSpacer />
      </div>
    </InstallScreen>
  );
}
