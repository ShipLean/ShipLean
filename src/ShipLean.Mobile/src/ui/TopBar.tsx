import type { ReactNode } from 'react';
import { Pressable, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useTheme } from '../theme';
import { Icon } from './Icon';
import { uiIds } from './testIds';

export const topBarSubId = `${uiIds.topBar}-sub`;
export const topBarBackId = `${uiIds.topBar}-back`;

export interface TopBarProps {
  title: string;
  // A muted line under the title ("Step 1 of 3").
  sub?: string;
  // Shows a back button; leave out where there is nowhere to go back to.
  onBack?: () => void;
  left?: ReactNode;
  right?: ReactNode;
}

export function TopBar({ title, sub, onBack, left, right }: TopBarProps) {
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
      {onBack === undefined ? null : (
        <Pressable
          testID={topBarBackId}
          accessibilityRole="button"
          accessibilityLabel="Back"
          onPress={onBack}
          hitSlop={8}
        >
          <Icon name="back" />
        </Pressable>
      )}
      {left}
      <View style={{ flex: 1 }}>
        <Text
          testID={uiIds.topBarTitle}
          numberOfLines={1}
          style={{
            color: colors.text,
            fontSize: theme.font.size.lg,
            fontWeight: theme.font.weight.bold,
          }}
        >
          {title}
        </Text>
        {sub === undefined ? null : (
          <Text
            testID={topBarSubId}
            numberOfLines={1}
            style={{ color: colors.muted, fontSize: theme.font.size.sm }}
          >
            {sub}
          </Text>
        )}
      </View>
      {right}
    </View>
  );
}
