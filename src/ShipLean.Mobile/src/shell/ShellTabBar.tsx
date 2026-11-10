import { useRouter } from 'expo-router';
import type { BottomTabBarProps } from 'expo-router/js-tabs';
import { useInboxBadge } from '../inbox/useInboxBadge';
import { links } from '../links';
import { FirstResultSheet } from '../setup/FirstResultSheet';
import { ReadyCoachMark } from '../setup/ReadyCoachMark';
import { useFirstCommand } from '../setup/useFirstCommand';
import { TabBar, type TabBarTab, type TabId } from '../ui/TabBar';
import { MicSlot } from '../voice/MicSlot';
import { useProject } from './useProject';

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
  const router = useRouter();
  const badge = useInboxBadge(projectKey);
  const { project } = useProject(projectKey);
  const first = useFirstCommand(projectKey);
  const focused = state.routes[state.index]?.name;
  const active = TABS.find((t) => t.id === focused)?.id ?? 'inbox';
  const tabs: TabBarTab[] = TABS.map((t) =>
    t.id === 'inbox' && badge !== null ? { ...t, badge } : t,
  );

  const onPress = (id: TabId) => {
    const route = state.routes.find((r) => r.name === id);
    if (route === undefined) return;
    const event = emitter.emit({ type: 'tabPress', target: route.key, canPreventDefault: true });
    if (id === focused || event.defaultPrevented) return;
    // A tab not opened yet has no params: expo-router's Tabs.Screen takes no initialParams, and navigateToTab sends
    // the tab's own params, so its screen would get no project key. Open it by its link; an opened tab keeps its own.
    if (hasProjectKey(route.params)) navigateToTab(route.key);
    else router.navigate(links[id](projectKey));
  };

  // The coach mark comes after the bar: its card sits a tab bar's height above its root's bottom edge. It keeps the
  // guide's flags itself (guide.ts), and its first hold or Skip is all the guided command needs, so the callbacks
  // have nothing more to do. The first answer after it opens the first-result sheet (useFirstCommand).
  return (
    <>
      <TabBar
        tabs={tabs}
        active={active}
        onPress={onPress}
        center={<MicSlot placement="tabBar" />}
      />
      <ReadyCoachMark onTry={noop} onSkip={noop} />
      {project === undefined ? null : (
        <FirstResultSheet
          open={first.open}
          projectId={project.id}
          projectKey={projectKey}
          onOpenInbox={first.onOpenInbox}
          onClose={first.onClose}
        />
      )}
    </>
  );
}

const noop = () => {};

const hasProjectKey = (params: object | null | undefined) => params != null && 'key' in params;
