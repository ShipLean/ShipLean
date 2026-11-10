import { StackPlaceholder } from '../shell/StackPlaceholder';
import { screenIds } from './testIds';

export interface DigestScreenProps {
  projectKey: string;
}

// TODO: the Features digest replaces this placeholder.
export function DigestScreen({ projectKey }: DigestScreenProps) {
  return <StackPlaceholder title={`${projectKey} digest`} testID={screenIds.digest} />;
}
