import { BottomTabBarHeightContext } from 'expo-router/js-tabs';
import { useContext, type ReactNode } from 'react';
import { View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useTheme } from '../theme';

// Fills the window with the theme background and keeps its content out of the left, right and bottom insets. The top
// inset is the TopBar's: it paints the status-bar strip. Inside a tab scene the tab bar already pays the bottom inset
// (it sits under the scene, above the home indicator), so the scene adds none: a second one was a blank band.
export interface ScreenProps {
  children: ReactNode;
  // The screen's root id from src/screens/testIds.ts.
  testID: string;
}

export function Screen({ children, testID }: ScreenProps) {
  const theme = useTheme();
  const insets = useSafeAreaInsets();
  const inTabScene = useContext(BottomTabBarHeightContext) !== undefined;
  return (
    <View
      testID={testID}
      style={{
        flex: 1,
        backgroundColor: theme.colors.bg,
        paddingBottom: inTabScene ? 0 : insets.bottom,
        paddingLeft: insets.left,
        paddingRight: insets.right,
      }}
    >
      {children}
    </View>
  );
}
