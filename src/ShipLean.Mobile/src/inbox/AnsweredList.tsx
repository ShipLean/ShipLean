import { View } from 'react-native';
import { inboxIds } from './testIds';

export interface AnsweredListProps {
  projectId: string;
}

// TODO: the Answered list (what you answered in the last 7 days, and what happened next) replaces this stub.
export function AnsweredList({ projectId }: AnsweredListProps) {
  void projectId;
  return <View testID={inboxIds.answeredList} />;
}
