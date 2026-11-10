import type { ProjectSlotProps } from './types';

// TODO: the Inbox provider (its actions and persisted query) replaces this pass-through.
export function InboxProvider({ children }: ProjectSlotProps) {
  return children;
}
