import { StackPlaceholder } from '../shell/StackPlaceholder';
import { screenIds } from './testIds';

export interface WorkItemScreenProps {
  projectKey: string;
  itemKey: string;
}

// TODO: the Work item screen replaces this placeholder.
export function WorkItemScreen({ itemKey }: WorkItemScreenProps) {
  return <StackPlaceholder title={itemKey} testID={screenIds.workItem} />;
}
