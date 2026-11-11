import { ActivityIndicator, Pressable, Text } from 'react-native';
import { useTheme } from '../theme';
import { Icon, type IconName } from './Icon';

// app.css .btn.sm: 36 high, 14 side padding.
const SMALL = { height: 36, padding: 14 } as const;

export type ButtonVariant = 'primary' | 'ghost' | 'danger' | 'soft' | 'link';

export interface ButtonProps {
  label: string;
  onPress: () => void;
  variant?: ButtonVariant;
  // Leading icon, drawn in the label's colour.
  icon?: IconName;
  size?: 'md' | 'sm';
  disabled?: boolean;
  // Shows progress and refuses presses, as disabled does.
  busy?: boolean;
  testID: string;
}

export function Button({
  label,
  onPress,
  variant = 'primary',
  icon,
  size = 'md',
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
    // app.css .btn: surface-2 fill, text colour; .btn.ghost: accent text with no border.
    soft: { bg: colors.surface2, fg: colors.text, border: 'transparent' },
    link: { bg: 'transparent', fg: colors.accent, border: 'transparent' },
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
        minHeight: size === 'sm' ? SMALL.height : theme.touch.control,
        flexDirection: 'row',
        alignItems: 'center',
        justifyContent: 'center',
        gap: theme.space[2],
        paddingHorizontal: size === 'sm' ? SMALL.padding : theme.space[4],
        borderRadius: size === 'sm' ? theme.radius.sm : theme.radius.md,
        borderWidth: 1,
        backgroundColor: look.bg,
        borderColor: look.border,
        opacity: off ? 0.5 : pressed ? 0.8 : 1,
      })}
    >
      {busy ? <ActivityIndicator color={look.fg} /> : null}
      {icon === undefined ? null : (
        <Icon name={icon} size={size === 'sm' ? 16 : 20} color={look.fg} />
      )}
      <Text
        style={{
          color: look.fg,
          fontSize: size === 'sm' ? theme.font.size.base : theme.font.size.md,
          fontWeight: theme.font.weight.semibold,
        }}
      >
        {label}
      </Text>
    </Pressable>
  );
}
