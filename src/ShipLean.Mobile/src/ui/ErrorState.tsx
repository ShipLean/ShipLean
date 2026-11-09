import { Text, View } from 'react-native';
import { Button } from './Button';
import type { StateAction } from './StateAction';
import { uiIds } from './testIds';

export interface ErrorStateProps {
  title: string;
  body?: string;
  // Usually Retry.
  action?: StateAction;
}

// TODO: unstyled; the themed error state replaces this body without changing props or test ids.
export function ErrorState({ title, body, action }: ErrorStateProps) {
  return (
    <View testID={uiIds.errorState} accessibilityRole="alert">
      <Text>{title}</Text>
      {body !== undefined ? <Text>{body}</Text> : null}
      {action ? (
        <Button label={action.label} onPress={action.onPress} testID={uiIds.errorStateAction} />
      ) : null}
    </View>
  );
}
