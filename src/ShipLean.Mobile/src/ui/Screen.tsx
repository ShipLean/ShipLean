import type { ReactNode } from 'react';
import { View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useTheme } from '../theme';

// Fills the window with the theme background and keeps its content inside the safe area (a TopBar sits below the
// status bar because of this).
export interface ScreenProps {
  children: ReactNode;
  // The screen's root id from src/screens/testIds.ts.
  testID: string;
}

export function Screen({ children, testID }: ScreenProps) {
  const theme = useTheme();
  const insets = useSafeAreaInsets();
  return (
    <View
      testID={testID}
      style={{
        flex: 1,
        backgroundColor: theme.colors.bg,
        paddingTop: insets.top,
        paddingBottom: insets.bottom,
        paddingLeft: insets.left,
        paddingRight: insets.right,
      }}
    >
      {children}
    </View>
  );
}
