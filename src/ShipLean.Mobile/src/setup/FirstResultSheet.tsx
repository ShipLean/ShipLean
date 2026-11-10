import type { FunctionComponent } from 'react';
import { View } from 'react-native';
import { firstResultSheetIds } from './FirstResultSheet.testIds';

export interface FirstResultSheetProps {
  open: boolean;
  projectId: string;
  projectKey: string;
  onOpenInbox: () => void;
  onClose: () => void;
}

// TODO: replace this empty stub with the first-result sheet.
export const FirstResultSheet: FunctionComponent<FirstResultSheetProps> = () => (
  <View testID={firstResultSheetIds.root} />
);
