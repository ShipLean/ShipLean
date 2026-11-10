import type { WorkItemDto } from '@shiplean/core/api/workItems';
import type { FC } from 'react';
import { View } from 'react-native';
import { itemActionsRowId } from '../testIds';

export interface ItemActionsRowProps {
  item: WorkItemDto;
  projectKey: string;
}

// TODO: the action row from the design (state, assign, note).
export const ItemActionsRow: FC<ItemActionsRowProps> = () => <View testID={itemActionsRowId} />;
