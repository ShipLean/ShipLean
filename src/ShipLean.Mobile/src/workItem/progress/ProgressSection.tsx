import type { WorkItemDto } from '@shiplean/core/api/workItems';
import type { FC } from 'react';
import { View } from 'react-native';
import { progressSectionId } from '../testIds';

export interface ProgressSectionProps {
  item: WorkItemDto;
  projectId: string;
}

// TODO: the Feature progress section from the design.
export const ProgressSection: FC<ProgressSectionProps> = () => <View testID={progressSectionId} />;
