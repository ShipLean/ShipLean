import type { FunctionComponent } from 'react';
import { View } from 'react-native';
import { microphoneStepIds } from './MicrophoneStep.testIds';

export interface MicrophoneStepProps {
  onContinue: () => void;
  onNotNow: () => void;
}

// TODO: replace this empty stub with the microphone step of setup.
export const MicrophoneStep: FunctionComponent<MicrophoneStepProps> = () => (
  <View testID={microphoneStepIds.root} />
);
