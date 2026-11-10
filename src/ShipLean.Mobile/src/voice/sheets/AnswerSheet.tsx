import { View } from 'react-native';
import type { AnswerSheetContent } from '../contracts';
import { answerSheetId } from './AnswerSheet.testIds';

export interface AnswerSheetProps {
  content: AnswerSheetContent;
  onClose: () => void;
}

// A question's answer, shown on screen (never read aloud).
// TODO: the lead line and the rows (key, title, counts) replace this empty sheet.
export function AnswerSheet({ content, onClose }: AnswerSheetProps) {
  void content;
  void onClose;
  return <View testID={answerSheetId} />;
}
