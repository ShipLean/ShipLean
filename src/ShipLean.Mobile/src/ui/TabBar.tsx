import type { ReactNode } from 'react';
import { Pressable, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useTheme } from '../theme';
import { Icon, type IconName } from './Icon';
import { layout } from './layout';
import { primitiveIds, tabBadgeId, tabId } from './testIds';

export type TabId = 'inbox' | 'backlog' | 'features' | 'retro';

const TAB_ICON: Record<TabId, IconName> = {
  inbox: 'inbox',
  backlog: 'list',
  features: 'board',
  retro: 'retro',
};

export interface TabBarTab {
  id: TabId;
  label: string;
  // Hidden when missing or 0.
  badge?: number;
}

export interface TabBarProps {
  tabs: TabBarTab[];
  active: TabId;
  onPress: (id: TabId) => void;
  // The middle column (the mic), between the first half of the tabs and the rest.
  center: ReactNode;
}

// The project's tab bar (design/mobileapp README, "Tab bar"): Inbox · Backlog · [mic] · Features · Retro.
export function TabBar({ tabs, active, onPress, center }: TabBarProps) {
  const theme = useTheme();
  const insets = useSafeAreaInsets();
  const half = Math.ceil(tabs.length / 2);
  const tab = (t: TabBarTab) => (
    <TabButton key={t.id} tab={t} active={t.id === active} onPress={() => onPress(t.id)} />
  );
  // The mic rises above the bar, and a touch outside a parent's bounds never reaches its child on Android. So the
  // root reaches up by the rise (a negative margin lays the bar over the scene's last 26 points) and holds the mic
  // slot at full height; the bar's own surface is a layer behind, and the root passes touches through elsewhere.
  return (
    <View
      testID={primitiveIds.tabBar}
      accessibilityRole="tablist"
      pointerEvents="box-none"
      style={{
        height: layout.micRaise + layout.tabBar + insets.bottom,
        marginTop: -layout.micRaise,
        paddingTop: layout.micRaise,
        paddingBottom: insets.bottom,
        flexDirection: 'row',
        alignItems: 'stretch',
      }}
    >
      <View
        pointerEvents="none"
        style={{
          position: 'absolute',
          top: layout.micRaise,
          left: 0,
          right: 0,
          bottom: 0,
          backgroundColor: theme.colors.surface,
          borderTopWidth: 1,
          borderTopColor: theme.colors.border,
        }}
      />
      {tabs.slice(0, half).map(tab)}
      <View
        testID={primitiveIds.tabMic}
        pointerEvents="box-none"
        style={{ width: layout.micSlot, marginTop: -layout.micRaise, alignItems: 'center' }}
      >
        {center}
      </View>
      {tabs.slice(half).map(tab)}
    </View>
  );
}

function TabButton({
  tab,
  active,
  onPress,
}: {
  tab: TabBarTab;
  active: boolean;
  onPress: () => void;
}) {
  const theme = useTheme();
  const { colors } = theme;
  const badge = tab.badge ?? 0;
  return (
    <Pressable
      testID={tabId(tab.id)}
      accessibilityRole="tab"
      accessibilityLabel={badge > 0 ? `${tab.label}, ${badge} waiting` : tab.label}
      accessibilityState={{ selected: active }}
      onPress={onPress}
      style={{
        flex: 1,
        minHeight: theme.touch.control,
        alignItems: 'center',
        justifyContent: 'center',
        gap: layout.tabGap,
      }}
    >
      <Icon name={TAB_ICON[tab.id]} size={24} color={active ? colors.accent : colors.muted} />
      <Text
        style={{
          color: active ? colors.accent : colors.muted,
          fontSize: theme.font.size.xxs,
          fontWeight: theme.font.weight.medium,
        }}
      >
        {tab.label}
      </Text>
      {badge > 0 ? (
        <View
          testID={tabBadgeId(tab.id)}
          style={{
            position: 'absolute',
            top: layout.badgeTop,
            left: '50%',
            marginLeft: layout.badgeOffset,
            minWidth: layout.badge,
            height: layout.badge,
            paddingHorizontal: layout.badgePadding,
            borderRadius: theme.radius.lg,
            alignItems: 'center',
            justifyContent: 'center',
            backgroundColor: colors.warn,
          }}
        >
          <Text
            style={{
              color: colors.surface,
              fontSize: theme.font.size.xxs,
              fontWeight: theme.font.weight.semibold,
            }}
          >
            {badge}
          </Text>
        </View>
      ) : null}
    </Pressable>
  );
}
