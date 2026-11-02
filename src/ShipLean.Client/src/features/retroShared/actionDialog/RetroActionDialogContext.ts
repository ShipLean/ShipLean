import { createContext } from 'react';
import type { RetroActionScope } from './RetroActionScope';

export interface OpenRetroActionDialog {
  pointIds?: string[];
  themeId?: string;
  existingWorkItemId?: string;
  scope: RetroActionScope;
}

export interface RetroActionDialogApi {
  projectId: string;
  open: (request: OpenRetroActionDialog) => void;
  close: () => void;
}

export const RetroActionDialogContext = createContext<RetroActionDialogApi | null>(null);
