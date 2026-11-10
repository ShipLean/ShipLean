import { RetroView } from '../retro/notes/RetroView';
import { Screen } from '../ui/Screen';
import { screenIds } from './testIds';

export interface RetroScreenProps {
  projectKey: string;
}

// The Retro tab: the sprint's points by category and theme.
export function RetroScreen({ projectKey }: RetroScreenProps) {
  return (
    <Screen testID={screenIds.retro}>
      <RetroView projectKey={projectKey} />
    </Screen>
  );
}
