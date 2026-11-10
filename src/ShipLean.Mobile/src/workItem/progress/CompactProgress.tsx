import type { WorkItemDto } from '@shiplean/core/api/workItems';
import type { FC } from 'react';
import { View } from 'react-native';
import { compactProgressId } from '../testIds';

export interface CompactProgressProps {
  item: WorkItemDto;
  projectId: string;
}

// TODO: the compact progress line from the design.
export const CompactProgress: FC<CompactProgressProps> = () => <View testID={compactProgressId} />;
