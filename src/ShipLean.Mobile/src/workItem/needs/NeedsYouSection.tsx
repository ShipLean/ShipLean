import type { WorkItemDto } from '@shiplean/core/api/workItems';
import type { FC } from 'react';
import { View } from 'react-native';
import { needsYouSectionId } from '../testIds';

export interface NeedsYouSectionProps {
  item: WorkItemDto;
  projectKey: string;
}

// TODO: the asks section from the design (decisions and questions).
export const NeedsYouSection: FC<NeedsYouSectionProps> = () => <View testID={needsYouSectionId} />;
