import { TabPlaceholder } from '../shell/TabPlaceholder';
import { screenIds } from './testIds';

export interface RetroScreenProps {
  projectKey: string;
}

// TODO: the sprint Retro replaces this placeholder.
export function RetroScreen({ projectKey }: RetroScreenProps) {
  return <TabPlaceholder projectKey={projectKey} title="Retro" testID={screenIds.retro} />;
}
