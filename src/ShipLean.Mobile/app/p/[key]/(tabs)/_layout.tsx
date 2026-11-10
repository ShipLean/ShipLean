import { useLocalSearchParams } from 'expo-router';
import { Tabs } from 'expo-router/js-tabs';
import { ShellTabBar } from '../../../../src/shell/ShellTabBar';

// The project's tabs, in order Inbox · Backlog · [mic] · Features · Retro (the mic slot is in the tab bar).
export default function ProjectTabsLayout() {
  const { key } = useLocalSearchParams<{ key: string }>();
  return (
    <Tabs
      screenOptions={{ headerShown: false }}
      tabBar={(props) => <ShellTabBar {...props} projectKey={key} />}
    >
      <Tabs.Screen name="inbox" />
      <Tabs.Screen name="backlog" />
      <Tabs.Screen name="features" />
      <Tabs.Screen name="retro" />
    </Tabs>
  );
}
