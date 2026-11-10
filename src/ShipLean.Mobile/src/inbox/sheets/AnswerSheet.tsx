import { View } from 'react-native';
import type { AnswerSheetRequest } from '../InboxSheets';
import { inboxIds } from '../testIds';

export interface AnswerSheetProps {
  request: AnswerSheetRequest;
  onClose: () => void;
}

// TODO: the reply sheet (the ask quoted, quick answers, where the answer goes, typing) replaces this stub.
export function AnswerSheet({ request }: AnswerSheetProps) {
  void request;
  return <View testID={inboxIds.answerSheet} />;
}
