import { WorkItemActionsProvider } from '../../workItem/WorkItemActions';
import type { ProjectSlotProps } from './types';

export function WorkItemProvider({ children }: ProjectSlotProps) {
  return <WorkItemActionsProvider>{children}</WorkItemActionsProvider>;
}
