import { Pressable, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useTheme } from '../theme';
import { primitiveIds } from './testIds';

export interface TabRootBarProps {
  projectKey: string;
  title: string;
  subtitle?: string;
  // The key tile returns to Projects.
  onTilePress: () => void;
}

// The top bar of every tab root (Inbox, Backlog, Features, Retro): the project's key tile, then the title.
export function TabRootBar({ projectKey, title, subtitle, onTilePress }: TabRootBarProps) {
  const theme = useTheme();
  const insets = useSafeAreaInsets();
  const { colors } = theme;
  return (
    <View
      testID={primitiveIds.tabRootBar}
      accessibilityRole="header"
      style={{
        height: theme.touch.bar + insets.top,
        paddingTop: insets.top,
        flexDirection: 'row',
        alignItems: 'center',
        gap: theme.space[1],
        paddingLeft: Math.max(theme.space[2], insets.left),
        paddingRight: Math.max(theme.space[2], insets.right),
        backgroundColor: colors.surface,
        borderBottomWidth: 1,
        borderBottomColor: colors.border,
      }}
    >
      <Pressable
        testID={primitiveIds.tabRootTile}
        accessibilityRole="button"
        accessibilityLabel={`${projectKey}, back to Projects`}
        onPress={onTilePress}
        style={({ pressed }) => ({
          minWidth: theme.touch.control,
          minHeight: theme.touch.control,
          paddingHorizontal: theme.space[1],
          alignItems: 'center',
          justifyContent: 'center',
          opacity: pressed ? 0.8 : 1,
        })}
      >
        <View
          style={{
            minWidth: theme.touch.control - theme.space[3] + 2,
            height: theme.space[5] + theme.space[1],
            paddingHorizontal: theme.space[1] + 2,
            borderRadius: theme.radius.md,
            alignItems: 'center',
            justifyContent: 'center',
            backgroundColor: colors.accent,
          }}
        >
          <Text
            style={{
              color: colors.accentFg,
              fontSize: theme.font.size.xxs,
              fontWeight: theme.font.weight.bold,
              fontFamily: theme.font.mono,
            }}
          >
            {projectKey}
          </Text>
        </View>
      </Pressable>
      <View style={{ flex: 1, minWidth: 0, paddingHorizontal: theme.space[1] }}>
        <Text
          numberOfLines={1}
          style={{
            color: colors.text,
            fontSize: theme.font.size.lg,
            fontWeight: theme.font.weight.semibold,
          }}
        >
          {title}
        </Text>
        {subtitle ? (
          <Text numberOfLines={1} style={{ color: colors.muted, fontSize: theme.font.size.sm }}>
            {subtitle}
          </Text>
        ) : null}
      </View>
    </View>
  );
}
