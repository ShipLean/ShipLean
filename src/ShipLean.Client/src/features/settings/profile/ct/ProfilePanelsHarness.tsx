import { ToastHost } from '../../../../components/ToastHost';
import { ProfileAccount } from '../account/ProfileAccount';
import { ProfileIdentity } from '../identity/ProfileIdentity';

/** The profile and account panels in a column with the toast host, for the phone specs. */
export function ProfilePanelsHarness() {
  return (
    <div className="grid gap-3 p-3">
      <ProfileIdentity />
      <ProfileAccount />
      <ToastHost />
    </div>
  );
}
