import { useCallback, useState, type ReactNode } from 'react';
import type { WorkItemTypeId } from '@shiplean/core/api/refs';
import { PasteReview } from './PasteReview';
import { PasteContext } from './usePasteIntake';

// Wraps a screen (its toolbar and its new-item row) and holds the paste review those open.
export function PasteHost({
  projectId,
  projectKey,
  children,
}: {
  projectId: string;
  projectKey: string;
  children: ReactNode;
}) {
  const [opened, setOpened] = useState<{ text: string; defaultType: WorkItemTypeId } | null>(null);
  const open = useCallback(
    (text: string, defaultType: WorkItemTypeId) => setOpened({ text, defaultType }),
    [],
  );
  return (
    <PasteContext.Provider value={open}>
      {children}
      {opened ? (
        <PasteReview
          projectId={projectId}
          projectKey={projectKey}
          initialText={opened.text}
          defaultType={opened.defaultType}
          onClose={() => setOpened(null)}
        />
      ) : null}
    </PasteContext.Provider>
  );
}
