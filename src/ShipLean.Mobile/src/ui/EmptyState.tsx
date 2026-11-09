import { Text, View } from 'react-native';
import { Button } from './Button';
import type { StateAction } from './StateAction';
import { uiIds } from './testIds';

export interface EmptyStateProps {
  title: string;
  body?: string;
  action?: StateAction;
}

// TODO: unstyled; the themed empty state replaces this body without changing props or test ids.
export function EmptyState({ title, body, action }: EmptyStateProps) {
  return (
    <View testID={uiIds.emptyState}>
      <Text>{title}</Text>
      {body !== undefined ? <Text>{body}</Text> : null}
      {action ? (
        <Button
          label={action.label}
          onPress={action.onPress}
          variant="ghost"
          testID={uiIds.emptyStateAction}
        />
      ) : null}
    </View>
  );
}
