import type { FunctionComponent } from 'react';
import { View } from 'react-native';
import { fromSetupTipIds } from './FromSetupTip.testIds';

export interface FromSetupTipProps {
  projectKey: string;
}

// TODO: replace this empty stub with the Inbox tip shown after setup.
export const FromSetupTip: FunctionComponent<FromSetupTipProps> = () => (
  <View testID={fromSetupTipIds.root} />
);
