import { DigestView } from '../features/digest/DigestView';
import { Screen } from '../ui/Screen';
import { screenIds } from './testIds';

export interface DigestScreenProps {
  projectKey: string;
}

// What changed in the project's Features since you last pressed Mark as seen.
export function DigestScreen({ projectKey }: DigestScreenProps) {
  return (
    <Screen testID={screenIds.digest}>
      <DigestView key={projectKey} projectKey={projectKey} />
    </Screen>
  );
}
