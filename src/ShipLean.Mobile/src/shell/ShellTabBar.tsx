import type { BottomTabBarProps } from 'expo-router/js-tabs';
import { useInboxBadge } from '../inbox/useInboxBadge';
import { TabBar, type TabBarTab, type TabId } from '../ui/TabBar';
import { MicSlot } from '../voice/MicSlot';

// The tab routes in (tabs)/_layout.tsx, by route name; TabBar puts the mic between Backlog and Features.
const TABS: { id: TabId; label: string }[] = [
  { id: 'inbox', label: 'Inbox' },
  { id: 'backlog', label: 'Backlog' },
  { id: 'features', label: 'Features' },
  { id: 'retro', label: 'Retro' },
];

export interface ShellTabBarProps extends BottomTabBarProps {
  projectKey: string;
}

// The project's tab bar: the four tabs, the mic in the middle and the Inbox badge.
export function ShellTabBar({ state, emitter, navigateToTab, projectKey }: ShellTabBarProps) {
  const badge = useInboxBadge(projectKey);
  const focused = state.routes[state.index]?.name;
  const active = TABS.find((t) => t.id === focused)?.id ?? 'inbox';
  const tabs: TabBarTab[] = TABS.map((t) =>
    t.id === 'inbox' && badge !== null ? { ...t, badge } : t,
  );

  const onPress = (id: TabId) => {
    const route = state.routes.find((r) => r.name === id);
    if (route === undefined) return;
    const event = emitter.emit({ type: 'tabPress', target: route.key, canPreventDefault: true });
    if (id !== focused && !event.defaultPrevented) navigateToTab(route.key);
  };

  return (
    <TabBar tabs={tabs} active={active} onPress={onPress} center={<MicSlot placement="tabBar" />} />
  );
}
