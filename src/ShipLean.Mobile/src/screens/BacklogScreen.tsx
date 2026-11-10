import type { BacklogLevelParam } from '../links';
import { TabPlaceholder } from '../shell/TabPlaceholder';
import { screenIds } from './testIds';

export interface BacklogScreenProps {
  projectKey: string;
  level?: BacklogLevelParam;
  // Keys, such as SHIP-12.
  epic?: string;
  feature?: string;
}

// TODO: the Backlog (its levels, filtered by an Epic or a Feature) replaces this placeholder.
export function BacklogScreen({ projectKey }: BacklogScreenProps) {
  return <TabPlaceholder projectKey={projectKey} title="Backlog" testID={screenIds.backlog} />;
}
