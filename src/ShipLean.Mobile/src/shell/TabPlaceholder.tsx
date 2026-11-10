import { Screen } from '../ui/Screen';
import { TopBar } from '../ui/TopBar';
import { KeyTile } from './KeyTile';

export interface TabPlaceholderProps {
  projectKey: string;
  title: string;
  // The screen's root id from src/screens/testIds.ts.
  testID: string;
}

// A tab root that isn't built yet: the top bar with the key tile, and nothing else.
export function TabPlaceholder({ projectKey, title, testID }: TabPlaceholderProps) {
  return (
    <Screen testID={testID}>
      <TopBar title={title} left={<KeyTile projectKey={projectKey} />} />
    </Screen>
  );
}
