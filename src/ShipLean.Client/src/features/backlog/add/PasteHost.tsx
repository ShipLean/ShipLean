import type { ReactNode } from 'react';

// Wraps a screen (its toolbar and its new-item row) and holds the paste review those open.
// TODO: the paste dialog and its review grid replace this pass-through.
export function PasteHost({
  projectId,
  projectKey,
  children,
}: {
  projectId: string;
  projectKey: string;
  children: ReactNode;
}) {
  void [projectId, projectKey];
  return <>{children}</>;
}
