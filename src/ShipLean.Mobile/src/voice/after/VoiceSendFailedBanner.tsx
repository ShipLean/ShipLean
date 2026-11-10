import { View } from 'react-native';
import { voiceSendFailedId } from './VoiceSendFailedBanner.testIds';

export interface VoiceSendFailedBannerProps {
  // The item whose refused voice changes to show; without one, every refused voice change.
  itemId?: string;
  itemKey?: string;
}

// The banner for queued voice changes the server refused when they were sent.
// TODO: the refused voice changes from the runner, with what was refused and why, replace this empty banner.
export function VoiceSendFailedBanner({ itemId, itemKey }: VoiceSendFailedBannerProps) {
  void itemId;
  void itemKey;
  return <View testID={voiceSendFailedId} />;
}
