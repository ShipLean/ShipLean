import { useMemo, useState, type ReactNode } from 'react';
import {
  RetroActionDialogContext,
  type OpenRetroActionDialog,
  type RetroActionDialogApi,
} from './RetroActionDialogContext';
import { RetroActionDialog } from './RetroActionDialog';

/** Mounted once per retro page and on the work item page: holds the project and the one open dialog, if any. */
export function RetroActionDialogProvider({
  projectId,
  children,
}: {
  projectId: string;
  children: ReactNode;
}) {
  const [request, setRequest] = useState<OpenRetroActionDialog | null>(null);
  const api = useMemo<RetroActionDialogApi>(
    () => ({ projectId, open: setRequest, close: () => setRequest(null) }),
    [projectId],
  );
  return (
    <RetroActionDialogContext.Provider value={api}>
      {children}
      {request ? <RetroActionDialog request={request} onClose={api.close} /> : null}
    </RetroActionDialogContext.Provider>
  );
}
