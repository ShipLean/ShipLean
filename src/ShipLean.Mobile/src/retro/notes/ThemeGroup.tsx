import type { RetroThemeGroupDto } from '@shiplean/core/api/retros';
import { Text, View } from 'react-native';
import { useTheme } from '../../theme';
import { tint } from '../../ui/layout';
import { retroRecurringId, retroThemeId } from '../testIds';

export interface ThemeGroupProps {
  group: RetroThemeGroupDto;
}

// retro.html .th: the theme's name (Unthemed when it has none), a Recurring chip, and how many points it holds.
export function ThemeGroupHeading({ group }: ThemeGroupProps) {
  const theme = useTheme();
  const { colors } = theme;
  const id = group.theme?.id ?? null;
  return (
    <View
      testID={retroThemeId(id)}
      accessibilityRole="header"
      style={{
        flexDirection: 'row',
        alignItems: 'center',
        gap: theme.space[2],
        paddingHorizontal: theme.space[4],
        paddingTop: theme.space[3],
        paddingBottom: theme.space[1],
      }}
    >
      {group.theme === null ? (
        <Text
          style={{
            fontSize: theme.font.size.sm,
            fontStyle: 'italic',
            color: colors.muted,
          }}
        >
          Unthemed
        </Text>
      ) : (
        <Text
          numberOfLines={1}
          style={{
            flexShrink: 1,
            fontSize: theme.font.size.sm,
            fontWeight: theme.font.weight.semibold,
            color: colors.muted,
          }}
        >
          {group.theme.name}
        </Text>
      )}
      {group.recurring ? (
        <View
          testID={retroRecurringId(id)}
          style={{
            paddingHorizontal: theme.space[2],
            borderRadius: theme.radius.xs,
            backgroundColor: tint(colors.warn, 0.18),
          }}
        >
          <Text style={{ fontSize: theme.font.size.xs, color: colors.text }}>Recurring</Text>
        </View>
      ) : null}
      <View style={{ flex: 1 }} />
      <Text style={{ fontSize: theme.font.size.sm, color: colors.muted }}>
        {group.points.length}
      </Text>
    </View>
  );
}
