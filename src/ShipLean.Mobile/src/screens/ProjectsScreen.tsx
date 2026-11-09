import { Screen } from '../ui/Screen';
import { TopBar } from '../ui/TopBar';
import { screenIds } from './testIds';

// TODO: placeholder; the Projects list (pull to refresh, loading, empty and error states, avatar to Account) replaces
// it.
export function ProjectsScreen() {
  return (
    <Screen testID={screenIds.projects}>
      <TopBar title="Projects" />
    </Screen>
  );
}
