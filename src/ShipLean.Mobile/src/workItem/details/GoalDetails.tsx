import type { WorkItemDto } from '@shiplean/core/api/workItems';
import type { FC } from 'react';
import { View } from 'react-native';
import { goalDetailsId } from '../testIds';

export interface GoalDetailsProps {
  item: WorkItemDto;
}

// TODO: the goal and status line from the design.
export const GoalDetails: FC<GoalDetailsProps> = () => <View testID={goalDetailsId} />;
