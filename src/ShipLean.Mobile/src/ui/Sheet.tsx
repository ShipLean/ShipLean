import type { ReactNode } from 'react';
import { Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useTheme } from '../theme';
import { sheetTitleId } from './testIds';

export interface SheetProps {
  title: string;
  children: ReactNode;
  // The sheet's root id from src/screens/testIds.ts.
  testID: string;
}

// The body of a route presented as a sheet (the route sets the presentation).
export function Sheet({ title, children, testID }: SheetProps) {
  const theme = useTheme();
  const insets = useSafeAreaInsets();
  return (
    <View
      testID={testID}
      style={{
        flex: 1,
        backgroundColor: theme.colors.surface,
        paddingHorizontal: theme.space[4],
        paddingBottom: insets.bottom + theme.space[3],
      }}
    >
      <View
        style={{
          alignSelf: 'center',
          width: 36,
          height: 4,
          borderRadius: 2,
          marginTop: theme.space[2],
          backgroundColor: theme.colors.border,
        }}
      />
      <Text
        testID={sheetTitleId(testID)}
        accessibilityRole="header"
        style={{
          color: theme.colors.text,
          fontSize: theme.font.size.lg,
          fontWeight: theme.font.weight.bold,
          paddingVertical: theme.space[3],
        }}
      >
        {title}
      </Text>
      {children}
    </View>
  );
}
