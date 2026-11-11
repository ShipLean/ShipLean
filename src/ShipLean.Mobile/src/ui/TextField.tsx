import { Pressable, Text, TextInput, View } from 'react-native';
import { useTheme } from '../theme';
import { Icon } from './Icon';
import { fieldErrorId, fieldHintId } from './testIds';

export const fieldClearId = (testID: string) => `${testID}-clear`;

export interface TextFieldProps {
  label: string;
  value: string;
  onChangeText: (text: string) => void;
  onBlur?: () => void;
  // Shows a clear (x) button while there is text; it calls this.
  onClear?: () => void;
  // The line under the field when there is no error.
  hint?: string;
  // Replaces the hint and marks the field invalid.
  error?: string;
  // On the input itself; the hint and error lines derive theirs from it (ui/testIds.ts).
  testID: string;
}

export function TextField({
  label,
  value,
  onChangeText,
  onBlur,
  onClear,
  hint,
  error,
  testID,
}: TextFieldProps) {
  const theme = useTheme();
  const { colors } = theme;
  return (
    <View style={{ gap: theme.space[1] }}>
      <Text
        style={{
          color: colors.text,
          fontSize: theme.font.size.sm,
          fontWeight: theme.font.weight.semibold,
        }}
      >
        {label}
      </Text>
      <View
        style={{
          minHeight: theme.touch.control,
          flexDirection: 'row',
          alignItems: 'center',
          gap: theme.space[2],
          paddingHorizontal: theme.space[3],
          borderRadius: theme.radius.md,
          borderWidth: 1,
          borderColor: error !== undefined ? colors.danger : colors.border,
          backgroundColor: colors.surface,
        }}
      >
        <TextInput
          testID={testID}
          accessibilityLabel={label}
          accessibilityHint={error ?? hint}
          value={value}
          onChangeText={onChangeText}
          onBlur={onBlur}
          autoCapitalize="none"
          autoCorrect={false}
          placeholderTextColor={colors.muted}
          style={{
            flex: 1,
            minHeight: theme.touch.control - 2,
            color: colors.text,
            fontSize: theme.font.size.input,
          }}
        />

        {onClear !== undefined && value !== '' ? (
          <Pressable
            testID={fieldClearId(testID)}
            accessibilityRole="button"
            accessibilityLabel={`Clear ${label}`}
            onPress={onClear}
            hitSlop={8}
          >
            <Icon name="x" size={16} color={colors.muted} />
          </Pressable>
        ) : null}
      </View>
      {error !== undefined ? (
        <Text
          testID={fieldErrorId(testID)}
          accessibilityRole="alert"
          style={{ color: colors.danger, fontSize: theme.font.size.sm }}
        >
          {error}
        </Text>
      ) : hint !== undefined ? (
        <Text
          testID={fieldHintId(testID)}
          style={{ color: colors.muted, fontSize: theme.font.size.sm }}
        >
          {hint}
        </Text>
      ) : null}
    </View>
  );
}
