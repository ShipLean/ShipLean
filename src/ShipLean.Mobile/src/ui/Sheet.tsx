import type { ReactNode } from 'react';
import { Text, View } from 'react-native';
import { sheetTitleId } from './testIds';

export interface SheetProps {
  title: string;
  children: ReactNode;
  // The sheet's root id from src/screens/testIds.ts.
  testID: string;
}

// The body of a route presented as a sheet (the route sets the presentation).
// TODO: unstyled; the themed sheet (grabber, title, padding) replaces this body without changing props or test ids.
export function Sheet({ title, children, testID }: SheetProps) {
  return (
    <View testID={testID} style={{ flex: 1 }}>
      <Text testID={sheetTitleId(testID)} accessibilityRole="header">
        {title}
      </Text>
      {children}
    </View>
  );
}
