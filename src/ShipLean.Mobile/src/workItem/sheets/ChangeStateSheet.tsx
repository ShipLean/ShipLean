import type { WorkItemDto } from '@shiplean/core/api/workItems';
import type { FC } from 'react';
import { View } from 'react-native';
import { changeStateSheetId } from '../testIds';

export interface ChangeStateSheetProps {
  item: WorkItemDto;
  open: boolean;
  onClose: () => void;
}

// TODO: the change-state sheet from the design.
export const ChangeStateSheet: FC<ChangeStateSheetProps> = () => (
  <View testID={changeStateSheetId} />
);
