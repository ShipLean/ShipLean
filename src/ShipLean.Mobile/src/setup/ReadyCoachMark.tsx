import type { FunctionComponent } from 'react';
import { View } from 'react-native';
import { readyCoachMarkIds } from './ReadyCoachMark.testIds';

export interface ReadyCoachMarkProps {
  onTry: () => void;
  onSkip: () => void;
}

// TODO: replace this empty stub with the ready coach mark.
export const ReadyCoachMark: FunctionComponent<ReadyCoachMarkProps> = () => (
  <View testID={readyCoachMarkIds.root} />
);
