import { TabPlaceholder } from '../shell/TabPlaceholder';
import { screenIds } from './testIds';

export interface FeaturesScreenProps {
  projectKey: string;
}

// TODO: the Features board replaces this placeholder.
export function FeaturesScreen({ projectKey }: FeaturesScreenProps) {
  return <TabPlaceholder projectKey={projectKey} title="Features" testID={screenIds.features} />;
}
