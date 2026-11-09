import { Text, View } from 'react-native';
import { useTheme } from '../theme';
import { Button } from './Button';
import type { StateAction } from './StateAction';
import { uiIds } from './testIds';

export interface ErrorStateProps {
  title: string;
  body?: string;
  // Usually Retry.
  action?: StateAction;
}

export function ErrorState({ title, body, action }: ErrorStateProps) {
  const theme = useTheme();
  const { colors } = theme;
  return (
    <View
      testID={uiIds.errorState}
      accessibilityRole="alert"
      style={{
        alignItems: 'center',
        gap: theme.space[2],
        padding: theme.space[5],
      }}
    >
      <Text
        style={{
          color: colors.danger,
          fontSize: theme.font.size.lg,
          fontWeight: theme.font.weight.bold,
          textAlign: 'center',
        }}
      >
        {title}
      </Text>
      {body !== undefined ? (
        <Text style={{ color: colors.muted, fontSize: theme.font.size.md, textAlign: 'center' }}>
          {body}
        </Text>
      ) : null}
      {action ? (
        <View style={{ marginTop: theme.space[2], alignSelf: 'stretch' }}>
          <Button label={action.label} onPress={action.onPress} testID={uiIds.errorStateAction} />
        </View>
      ) : null}
    </View>
  );
}
