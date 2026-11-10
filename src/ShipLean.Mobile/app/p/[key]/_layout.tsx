import { Stack, useLocalSearchParams } from 'expo-router';
import { ProjectProviders } from '../../../src/shell/ProjectProviders';

// One project: its tabs, with the stack screens (Work item, a Feature's board, the digest) on top of them.
export default function ProjectLayout() {
  const { key } = useLocalSearchParams<{ key: string }>();
  return (
    <ProjectProviders projectKey={key}>
      <Stack screenOptions={{ headerShown: false }} />
    </ProjectProviders>
  );
}
