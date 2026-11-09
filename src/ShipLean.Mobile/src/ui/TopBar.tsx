import type { ReactNode } from 'react';
import { Text, View } from 'react-native';
import { uiIds } from './testIds';

export interface TopBarProps {
  title: string;
  left?: ReactNode;
  right?: ReactNode;
}

// TODO: unstyled; the themed bar (bar height, safe-area top) replaces this body without changing props or test ids.
export function TopBar({ title, left, right }: TopBarProps) {
  return (
    <View testID={uiIds.topBar} accessibilityRole="header">
      {left}
      <Text testID={uiIds.topBarTitle}>{title}</Text>
      {right}
    </View>
  );
}
