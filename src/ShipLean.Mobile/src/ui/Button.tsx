import { ActivityIndicator, Pressable, Text } from 'react-native';
import { useTheme } from '../theme';

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

export function Button({
  label,
  onPress,
  variant = 'primary',
  disabled,
  busy,
  testID,
}: ButtonProps) {
  const theme = useTheme();
  const { colors } = theme;
  const off = disabled === true || busy === true;
  const look = {
    primary: { bg: colors.accent, fg: colors.accentFg, border: colors.accent },
    ghost: { bg: 'transparent', fg: colors.accent, border: colors.border },
    danger: { bg: 'transparent', fg: colors.danger, border: colors.danger },
  }[variant];
  return (
    <Pressable
      testID={testID}
      accessibilityRole="button"
      accessibilityLabel={label}
      accessibilityState={{ disabled: off, busy: busy === true }}
      disabled={off}
      onPress={onPress}
      style={({ pressed }) => ({
        minHeight: theme.touch.control,
        flexDirection: 'row',
        alignItems: 'center',
        justifyContent: 'center',
        gap: theme.space[2],
        paddingHorizontal: theme.space[4],
        borderRadius: theme.radius.md,
        borderWidth: 1,
        backgroundColor: look.bg,
        borderColor: look.border,
        opacity: off ? 0.5 : pressed ? 0.8 : 1,
      })}
    >
      {busy ? <ActivityIndicator color={look.fg} /> : null}
      <Text
        style={{
          color: look.fg,
          fontSize: theme.font.size.md,
          fontWeight: theme.font.weight.semibold,
        }}
      >
        {label}
      </Text>
    </Pressable>
  );
}
