import type { BottomTabBarProps } from 'expo-router/js-tabs';
import { Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { tabIds } from '../screens/testIds';
import { useTheme } from '../theme/useTheme';

// The tab routes in (tabs)/_layout.tsx, by route name, with the mic slot between Backlog and Features.
const LEFT = [
  { name: 'inbox', label: 'Inbox', testID: tabIds.inbox },
  { name: 'backlog', label: 'Backlog', testID: tabIds.backlog },
] as const;
const RIGHT = [
  { name: 'features', label: 'Features', testID: tabIds.features },
  { name: 'retro', label: 'Retro', testID: tabIds.retro },
] as const;

type Tab = (typeof LEFT)[number] | (typeof RIGHT)[number];

// TODO: T0p2's TabBar, with the mic slot in the centre and the Inbox badge, replaces this plain text tab bar.
export function ShellTabBar({ state, emitter, navigateToTab }: BottomTabBarProps) {
  const theme = useTheme();
  const insets = useSafeAreaInsets();
  const focused = state.routes[state.index]?.name;

  const tab = ({ name, label, testID }: Tab) => {
    const route = state.routes.find((r) => r.name === name);
    const selected = focused === name;
    const onPress = () => {
      if (route === undefined) return;
      const event = emitter.emit({ type: 'tabPress', target: route.key, canPreventDefault: true });
      if (!selected && !event.defaultPrevented) navigateToTab(route.key);
    };
    return (
      <Text
        key={name}
        testID={testID}
        accessibilityRole="tab"
        accessibilityState={{ selected }}
        onPress={onPress}
        style={{
          flex: 1,
          minHeight: theme.touch.control,
          textAlign: 'center',
          textAlignVertical: 'center',
          lineHeight: theme.touch.control,
          fontSize: theme.font.size.sm,
          fontWeight: selected ? theme.font.weight.bold : theme.font.weight.regular,
          color: selected ? theme.colors.accent : theme.colors.muted,
        }}
      >
        {label}
      </Text>
    );
  };

  return (
    <View
      accessibilityRole="tablist"
      style={{
        flexDirection: 'row',
        alignItems: 'center',
        paddingBottom: insets.bottom,
        backgroundColor: theme.colors.surface,
        borderTopWidth: 1,
        borderTopColor: theme.colors.border,
      }}
    >
      {LEFT.map(tab)}
      <View
        testID={tabIds.mic}
        accessibilityLabel="Set up voice"
        accessibilityState={{ disabled: true }}
        style={{ flex: 1, minHeight: theme.touch.control, justifyContent: 'center', opacity: 0.5 }}
      >
        <Text
          style={{ textAlign: 'center', fontSize: theme.font.size.xs, color: theme.colors.muted }}
        >
          Set up voice
        </Text>
      </View>
      {RIGHT.map(tab)}
    </View>
  );
}
