import type { InboxAskDto, InboxItemDto } from '@shiplean/core/api/inbox';
import { createContext, useContext, useMemo, useState, type ReactNode } from 'react';
import { AnswerSheet } from './sheets/AnswerSheet';
import { ReviewSheet } from './sheets/ReviewSheet';

// One ask answered in the reply sheet: a question, a decision to discuss, a mention, or a new status line.
export interface AnswerSheetRequest {
  mode: 'question' | 'discuss' | 'mention' | 'status';
  item: InboxItemDto;
  ask: InboxAskDto;
  projectId: string;
}

export interface InboxSheets {
  openAnswer(req: AnswerSheetRequest): void;
  openReview(item: InboxItemDto, opts: { scrollToConfirm: boolean; projectId: string }): void;
  close(): void;
}

export const InboxSheetsContext = createContext<InboxSheets | null>(null);

type OpenSheet =
  | { kind: 'answer'; request: AnswerSheetRequest }
  | { kind: 'review'; item: InboxItemDto; scrollToConfirm: boolean; projectId: string }
  | null;

// Hosts the Inbox's two sheets, so every screen (the Inbox, the work item, the Features tab) opens the same ones.
export function InboxSheetsProvider({ children }: { children: ReactNode }) {
  const [open, setOpen] = useState<OpenSheet>(null);
  const sheets = useMemo<InboxSheets>(
    () => ({
      openAnswer: (request) => setOpen({ kind: 'answer', request }),
      openReview: (item, { scrollToConfirm, projectId }) =>
        setOpen({ kind: 'review', item, scrollToConfirm, projectId }),
      close: () => setOpen(null),
    }),
    [],
  );
  return (
    <InboxSheetsContext.Provider value={sheets}>
      {children}
      {open?.kind === 'answer' ? (
        <AnswerSheet request={open.request} onClose={sheets.close} />
      ) : null}
      {open?.kind === 'review' ? (
        <ReviewSheet
          item={open.item}
          scrollToConfirm={open.scrollToConfirm}
          projectId={open.projectId}
          onClose={sheets.close}
        />
      ) : null}
    </InboxSheetsContext.Provider>
  );
}

export function useInboxSheets(): InboxSheets {
  const sheets = useContext(InboxSheetsContext);
  if (sheets === null) throw new Error('useInboxSheets needs an InboxSheetsProvider above it.');
  return sheets;
}
