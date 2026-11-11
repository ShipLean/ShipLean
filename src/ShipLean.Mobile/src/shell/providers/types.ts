import type { ReactNode } from 'react';

// What every project provider slot is given. A slot may ignore the key.
export interface ProjectSlotProps {
  projectKey: string;
  children: ReactNode;
}

// What every app-wide provider slot is given.
export interface AppSlotProps {
  children: ReactNode;
}
