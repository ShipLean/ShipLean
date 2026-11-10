import { Stack, useLocalSearchParams } from 'expo-router';
import { ProjectProviders } from '../../../src/shell/ProjectProviders';

// A cold link to an item, a board or the digest opens with the tabs underneath it, so back lands on a tab.
export const unstable_settings = { anchor: '(tabs)' };

// One project: its tabs, with the stack screens (Work item, a Feature's board, the digest) on top of them.
export default function ProjectLayout() {
  const { key } = useLocalSearchParams<{ key: string }>();
  return (
    <ProjectProviders projectKey={key}>
      <Stack screenOptions={{ headerShown: false }} />
    </ProjectProviders>
  );
}
