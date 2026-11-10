import type { WorkItemDto } from '@shiplean/core/api/workItems';
import type { FC } from 'react';
import { View } from 'react-native';
import { needsYouSectionId } from '../testIds';

export interface NeedsYouSectionProps {
  item: WorkItemDto;
  projectKey: string;
}

// TODO: the asks section from the design (decisions and questions). Approve, Discuss and Reply dispatch approve/discuss/reply through useWorkItemActions, never a core write. Labels and dates are the server's. Copy after posting: 'Posted on SHIP-n; the next agent to pick it up reads it'. It never promises a notification.
export const NeedsYouSection: FC<NeedsYouSectionProps> = () => <View testID={needsYouSectionId} />;
