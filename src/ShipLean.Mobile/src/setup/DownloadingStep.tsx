import type { FunctionComponent } from 'react';
import { View } from 'react-native';
import { downloadingStepIds } from './DownloadingStep.testIds';

export interface DownloadingStepProps {
  onUseApp: () => void;
}

// TODO: replace this empty stub with the downloading step of setup.
export const DownloadingStep: FunctionComponent<DownloadingStepProps> = () => (
  <View testID={downloadingStepIds.root} />
);
