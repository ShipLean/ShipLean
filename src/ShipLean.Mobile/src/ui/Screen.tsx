import type { ReactNode } from 'react';
import { View } from 'react-native';

export interface ScreenProps {
  children: ReactNode;
  // The screen's root id from src/screens/testIds.ts.
  testID: string;
}

// TODO: unstyled; the themed screen (background, safe-area insets) replaces this body without changing props or test
// ids.
export function Screen({ children, testID }: ScreenProps) {
  return (
    <View testID={testID} style={{ flex: 1 }}>
      {children}
    </View>
  );
}
