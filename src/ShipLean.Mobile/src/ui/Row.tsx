import type { ReactNode } from 'react';
import { Pressable, Text, View } from 'react-native';

export interface RowProps {
  title: string;
  subtitle?: string;
  // Trailing content: a value, a chevron, a badge.
  right?: ReactNode;
  onPress?: () => void;
  testID: string;
}

// TODO: unstyled; the themed row (touch height, separators) replaces this body without changing props or test ids.
export function Row({ title, subtitle, right, onPress, testID }: RowProps) {
  return (
    <Pressable
      testID={testID}
      accessibilityRole={onPress ? 'button' : undefined}
      disabled={onPress === undefined}
      onPress={onPress}
    >
      <View>
        <Text>{title}</Text>
        {subtitle !== undefined ? <Text>{subtitle}</Text> : null}
      </View>
      {right}
    </Pressable>
  );
}
