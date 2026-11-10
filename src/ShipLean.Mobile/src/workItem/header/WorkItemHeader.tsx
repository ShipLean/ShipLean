import type { WorkItemDto } from '@shiplean/core/api/workItems';
import type { FC } from 'react';
import { View } from 'react-native';
import { workItemHeaderId } from '../testIds';

export interface WorkItemHeaderProps {
  item: WorkItemDto;
  projectKey: string;
}

// TODO: the header from the design (key, title, state, assignee, health); server labels only (no client dates); goal and status line render word for word.
export const WorkItemHeader: FC<WorkItemHeaderProps> = () => <View testID={workItemHeaderId} />;
