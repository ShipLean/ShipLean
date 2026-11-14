import type { ReactNode } from 'react';
import { TouchSelectModeProvider } from '../touch/selectMode';

// CT-only: the touch Select mode a backlog page mounts around its slots (BacklogSlots), for a mode mounted on its own.
export function SelectModeHarness({ children }: { children: ReactNode }) {
  return <TouchSelectModeProvider>{children}</TouchSelectModeProvider>;
}
