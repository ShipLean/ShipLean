import type { WorkItemDto } from '@shiplean/core/api/workItems';
import type { FC } from 'react';
import { View } from 'react-native';
import { goalDetailsId } from '../testIds';

export interface GoalDetailsProps {
  item: WorkItemDto;
}

// TODO: the goal and status line from the design; server labels only (no client dates); goal and status line render word for word.
export const GoalDetails: FC<GoalDetailsProps> = () => <View testID={goalDetailsId} />;
