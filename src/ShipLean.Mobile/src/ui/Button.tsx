import { ActivityIndicator, Pressable, Text } from 'react-native';

export type ButtonVariant = 'primary' | 'ghost' | 'danger';

export interface ButtonProps {
  label: string;
  onPress: () => void;
  variant?: ButtonVariant;
  disabled?: boolean;
  // Shows progress and refuses presses, as disabled does.
  busy?: boolean;
  testID: string;
}

// TODO: unstyled; the themed button (variants, touch size) replaces this body without changing props or test ids.
// variant is read by the themed body.
export function Button({ label, onPress, disabled, busy, testID }: ButtonProps) {
  const off = disabled === true || busy === true;
  return (
    <Pressable
      testID={testID}
      accessibilityRole="button"
      accessibilityLabel={label}
      accessibilityState={{ disabled: off, busy: busy === true }}
      disabled={off}
      onPress={onPress}
    >
      {busy ? <ActivityIndicator /> : null}
      <Text>{label}</Text>
    </Pressable>
  );
}
