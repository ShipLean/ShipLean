import type { ReactNode } from 'react';
import { Text, View } from 'react-native';
import { useTheme } from '../theme';
import { uiIds } from './testIds';

export interface TopBarProps {
  title: string;
  left?: ReactNode;
  right?: ReactNode;
}

export function TopBar({ title, left, right }: TopBarProps) {
  const theme = useTheme();
  const { colors } = theme;
  return (
    <View
      testID={uiIds.topBar}
      accessibilityRole="header"
      style={{
        height: theme.touch.bar,
        flexDirection: 'row',
        alignItems: 'center',
        gap: theme.space[2],
        paddingHorizontal: theme.space[4],
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
