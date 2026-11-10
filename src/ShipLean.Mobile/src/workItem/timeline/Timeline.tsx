import type { WorkItemDto } from '@shiplean/core/api/workItems';
import type { FC } from 'react';
import { View } from 'react-native';
import { timelineId } from '../testIds';

export interface TimelineProps {
  item: WorkItemDto;
}

// TODO: notes and history from the design, newest first as the web orders them. Dates are the server's *Label fields, never a client-formatted Date. Note bodies render word for word.
export const Timeline: FC<TimelineProps> = () => <View testID={timelineId} />;
