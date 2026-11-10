import type { FunctionComponent } from 'react';
import { View } from 'react-native';
import { modelsStepIds } from './ModelsStep.testIds';

export interface ModelsStepProps {
  onContinue: () => void;
  onSkip: () => void;
}

// TODO: replace this empty stub with the models step of setup.
export const ModelsStep: FunctionComponent<ModelsStepProps> = () => (
  <View testID={modelsStepIds.root} />
);
