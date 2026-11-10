import type { WorkItemDto } from '@shiplean/core/api/workItems';
import type { FC } from 'react';
import { View } from 'react-native';
import { assignSheetId } from '../testIds';

export interface AssignSheetProps {
  item: WorkItemDto;
  open: boolean;
  onClose: () => void;
}

// TODO: the assign sheet from the design.
export const AssignSheet: FC<AssignSheetProps> = () => <View testID={assignSheetId} />;
