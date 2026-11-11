import { BacklogActionsProvider } from '../../backlog/BacklogActions';
import type { ProjectSlotProps } from './types';

export function BacklogProvider({ children }: ProjectSlotProps) {
  return <BacklogActionsProvider>{children}</BacklogActionsProvider>;
}
