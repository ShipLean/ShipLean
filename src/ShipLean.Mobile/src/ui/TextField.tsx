import { Text, TextInput, View } from 'react-native';
import { useTheme } from '../theme';
import { fieldErrorId, fieldHintId } from './testIds';

export interface TextFieldProps {
  label: string;
  value: string;
  onChangeText: (text: string) => void;
  onBlur?: () => void;
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
      <TextInput
        testID={testID}
        accessibilityLabel={label}
        value={value}
        onChangeText={onChangeText}
        onBlur={onBlur}
        autoCapitalize="none"
        autoCorrect={false}
        placeholderTextColor={colors.muted}
        style={{
          minHeight: theme.touch.control,
          paddingHorizontal: theme.space[3],
          borderRadius: theme.radius.md,
          borderWidth: 1,
          borderColor: error !== undefined ? colors.danger : colors.border,
          backgroundColor: colors.surface,
          color: colors.text,
          fontSize: theme.font.size.input,
        }}
      />
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
