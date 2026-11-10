import { StackPlaceholder } from '../shell/StackPlaceholder';
import { screenIds } from './testIds';

export interface FeatureBoardScreenProps {
  projectKey: string;
  featureKey: string;
}

// TODO: a Feature's board replaces this placeholder.
export function FeatureBoardScreen({ featureKey }: FeatureBoardScreenProps) {
  return <StackPlaceholder title={featureKey} testID={screenIds.featureBoard} />;
}
