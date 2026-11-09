import type { ReactNode } from 'react';
import { Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useTheme } from '../theme';
import { uiIds } from './testIds';

export interface TopBarProps {
  title: string;
  left?: ReactNode;
  right?: ReactNode;
}

export function TopBar({ title, left, right }: TopBarProps) {
  const theme = useTheme();
  const insets = useSafeAreaInsets();
  const { colors } = theme;
  return (
    <View
      testID={uiIds.topBar}
      accessibilityRole="header"
      style={{
        // The bar paints the status-bar strip too (the mock-ups' surface colour), so it grows by the top inset.
        height: theme.touch.bar + insets.top,
        paddingTop: insets.top,
        flexDirection: 'row',
        alignItems: 'center',
        gap: theme.space[2],
        paddingLeft: Math.max(theme.space[4], insets.left),
        paddingRight: Math.max(theme.space[4], insets.right),
        backgroundColor: colors.surface,
        borderBottomWidth: 1,
        borderBottomColor: colors.border,
      }}
    >
      {left}
      <Text
        testID={uiIds.topBarTitle}
        numberOfLines={1}
        style={{
          flex: 1,
          color: colors.text,
          fontSize: theme.font.size.lg,
          fontWeight: theme.font.weight.bold,
        }}
      >
        {title}
      </Text>
      {right}
    </View>
  );
}
