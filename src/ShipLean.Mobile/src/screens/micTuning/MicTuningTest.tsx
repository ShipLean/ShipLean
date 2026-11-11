import { View } from 'react-native';
import type { MicTuningFrom } from '../../links';
import { micTuningTestId } from './MicTuningTest.testIds';

export interface MicTuningTestProps {
  from: MicTuningFrom;
  // The item the person came from; the test's phrase uses a key from it.
  itemKey?: string;
}

// TODO: the two-step test (room, then voice), its result and the line it sets.
export function MicTuningTest(props: MicTuningTestProps) {
  void props;
  return <View testID={micTuningTestId} />;
}
