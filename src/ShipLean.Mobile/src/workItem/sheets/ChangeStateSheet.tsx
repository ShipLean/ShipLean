import type { WorkItemDto } from '@shiplean/core/api/workItems';
import type { FC } from 'react';
import { View } from 'react-native';
import { changeStateSheetId } from '../testIds';

export interface ChangeStateSheetProps {
  item: WorkItemDto;
  open: boolean;
  onClose: () => void;
}

// TODO: the change-state sheet from the design. Dispatch setState/assign/addNote via useWorkItemActions with item.revision and the from* values; no mutation hook.
export const ChangeStateSheet: FC<ChangeStateSheetProps> = () => (
  <View testID={changeStateSheetId} />
);
