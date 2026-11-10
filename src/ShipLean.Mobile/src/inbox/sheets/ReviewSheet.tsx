import type { InboxItemDto } from '@shiplean/core/api/inbox';
import { View } from 'react-native';
import { inboxIds } from '../testIds';

export interface ReviewSheetProps {
  item: InboxItemDto;
  // Approve all opens the same sheet scrolled to its Confirm button.
  scrollToConfirm: boolean;
  projectId: string;
  onClose: () => void;
}

// TODO: the review sheet (every decision with its recommended answer chosen, Change, Add why, Confirm N) replaces this stub.
export function ReviewSheet({ item }: ReviewSheetProps) {
  void item;
  return <View testID={inboxIds.reviewSheet} />;
}
