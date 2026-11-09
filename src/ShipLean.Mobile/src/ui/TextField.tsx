import { Text, TextInput, View } from 'react-native';
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

// TODO: unstyled; the themed field (label, touch size, 16pt input text) replaces this body without changing props or
// test ids.
export function TextField({
  label,
  value,
  onChangeText,
  onBlur,
  hint,
  error,
  testID,
}: TextFieldProps) {
  return (
    <View>
      <Text>{label}</Text>
      <TextInput
        testID={testID}
        accessibilityLabel={label}
        value={value}
        onChangeText={onChangeText}
        onBlur={onBlur}
        autoCapitalize="none"
        autoCorrect={false}
      />
      {error !== undefined ? (
        <Text testID={fieldErrorId(testID)} accessibilityRole="alert">
          {error}
        </Text>
      ) : hint !== undefined ? (
        <Text testID={fieldHintId(testID)}>{hint}</Text>
      ) : null}
    </View>
  );
}
