import { pagesTestIds } from '../features/pages/testIds';
import { ProfileAccount } from '../features/settings/profile/account/ProfileAccount';

export type ProfileAccountSlotProps = Record<string, never>;

export function ProfileAccountSlot(props: ProfileAccountSlotProps) {
  void props;
  return (
    <div data-testid={pagesTestIds.slotProfileAccount}>
      <ProfileAccount />
    </div>
  );
}
