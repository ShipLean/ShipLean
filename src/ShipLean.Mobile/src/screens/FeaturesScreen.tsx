import { FeaturesView } from '../features/list/FeaturesView';
import { Screen } from '../ui/Screen';
import { screenIds } from './testIds';

export interface FeaturesScreenProps {
  projectKey: string;
}

// The Features tab: every Feature grouped by state.
export function FeaturesScreen({ projectKey }: FeaturesScreenProps) {
  return (
    <Screen testID={screenIds.features}>
      <FeaturesView key={projectKey} projectKey={projectKey} />
    </Screen>
  );
}
