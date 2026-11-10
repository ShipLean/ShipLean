import type { BacklogLevelParam } from '../links';
import { BacklogView } from '../backlog/list/BacklogView';
import { Screen } from '../ui/Screen';
import { screenIds } from './testIds';

export interface BacklogScreenProps {
  projectKey: string;
  level?: BacklogLevelParam;
  // Keys, such as SHIP-12.
  epic?: string;
  feature?: string;
}

// The Backlog tab. A link's level, epic and feature say where it opens; a new link opens afresh.
export function BacklogScreen({ projectKey, level, epic, feature }: BacklogScreenProps) {
  return (
    <Screen testID={screenIds.backlog}>
      <BacklogView
        key={`${projectKey}|${level ?? ''}|${epic ?? ''}|${feature ?? ''}`}
        projectKey={projectKey}
        level={level}
        epic={epic}
        feature={feature}
      />
    </Screen>
  );
}
