import type { WorkItemDto } from '@shiplean/core/api/workItems';
import type { FC } from 'react';
import { View } from 'react-native';
import { storyTasksId } from '../testIds';

export interface StoryTasksProps {
  item: WorkItemDto;
  projectId: string;
}

// TODO: the Story's task list from the design.
export const StoryTasks: FC<StoryTasksProps> = () => <View testID={storyTasksId} />;
