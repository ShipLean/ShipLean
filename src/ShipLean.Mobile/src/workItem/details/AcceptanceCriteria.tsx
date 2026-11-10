import type { WorkItemDto } from '@shiplean/core/api/workItems';
import type { FC } from 'react';
import { View } from 'react-native';
import { acceptanceCriteriaId } from '../testIds';

export interface AcceptanceCriteriaProps {
  item: WorkItemDto;
}

// TODO: the acceptance criteria from the design.
export const AcceptanceCriteria: FC<AcceptanceCriteriaProps> = () => (
  <View testID={acceptanceCriteriaId} />
);
