import type { WorkItemDto } from '@shiplean/core/api/workItems';
import type { FC } from 'react';
import { View } from 'react-native';
import { timelineId } from '../testIds';

export interface TimelineProps {
  item: WorkItemDto;
}

// TODO: the notes and history timeline from the design.
export const Timeline: FC<TimelineProps> = () => <View testID={timelineId} />;
