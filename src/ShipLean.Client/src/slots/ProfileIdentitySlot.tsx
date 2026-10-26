import { pagesTestIds } from '../features/pages/testIds';
import { ProfileIdentity } from '../features/settings/profile/identity/ProfileIdentity';

export type ProfileIdentitySlotProps = Record<string, never>;

export function ProfileIdentitySlot(props: ProfileIdentitySlotProps) {
  void props;
  return (
    <div data-testid={pagesTestIds.slotProfileIdentity}>
      <ProfileIdentity />
    </div>
  );
}
