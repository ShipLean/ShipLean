import type { ReactNode } from 'react';
import { Pressable, Text, View } from 'react-native';
import { useTheme } from '../theme';

export interface RowProps {
  title: string;
  subtitle?: string;
  // Trailing content: a value, a chevron, a badge.
  right?: ReactNode;
  onPress?: () => void;
  testID: string;
}

export function Row({ title, subtitle, right, onPress, testID }: RowProps) {
  const theme = useTheme();
  const { colors } = theme;
  return (
    <Pressable
      testID={testID}
      accessibilityRole={onPress ? 'button' : undefined}
      disabled={onPress === undefined}
      onPress={onPress}
      style={({ pressed }) => ({
        minHeight: theme.touch.row,
        flexDirection: 'row',
        alignItems: 'center',
        gap: theme.space[3],
        paddingVertical: theme.space[2],
        paddingHorizontal: theme.space[4],
        backgroundColor: pressed ? colors.accentSoft : colors.surface,
        borderBottomWidth: 1,
        borderBottomColor: colors.border,
      })}
    >
      <View style={{ flex: 1 }}>
        <Text
          numberOfLines={1}
          style={{
            color: colors.text,
            fontSize: theme.font.size.md,
            fontWeight: theme.font.weight.semibold,
          }}
        >
          {title}
        </Text>
        {subtitle !== undefined ? (
          <Text numberOfLines={1} style={{ color: colors.muted, fontSize: theme.font.size.sm }}>
            {subtitle}
          </Text>
        ) : null}
      </View>
      {right}
    </Pressable>
  );
}
