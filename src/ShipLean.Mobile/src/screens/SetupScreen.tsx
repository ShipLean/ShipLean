import type { SetupStep } from '../links';
import { StackPlaceholder } from '../shell/StackPlaceholder';
import { screenSetup } from './testIds';

export interface SetupScreenProps {
  step: SetupStep;
}

// TODO: the voice setup (choosing the models, the microphone, the download) replaces this placeholder.
export function SetupScreen({ step }: SetupScreenProps) {
  return <StackPlaceholder title="Set up voice" testID={screenSetup(step)} />;
}
