import { SignedInDevicesPanel } from '../features/devices/SignedInDevicesPanel';

export type ProfileDevicesSlotProps = Record<string, never>;

export function ProfileDevicesSlot(props: ProfileDevicesSlotProps) {
  void props;
  return <SignedInDevicesPanel />;
}
