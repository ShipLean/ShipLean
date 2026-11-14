import { useState } from 'react';
import type { AttachmentMarkdownDto } from '@shiplean/core/api/attachments';
import { attachmentTestIds as ids } from '../testIds';
import { attachmentMeta } from '../header/attachmentMeta';
import { ViewToggle, type AttachmentView } from '../header/ViewToggle';
import { AboutThisFile } from '../side/AboutThisFile';
import { SiblingsPanel } from '../side/SiblingsPanel';
import { Sheet } from '../../../touch/Sheet';
import { AttachmentMoreSheet } from './AttachmentMoreSheet';

// The phone header (D1, D7, D8), in the page so it scrolls away: the file name (the item's Markdown files), a one-line
// facts row (About this file), then Rendered / Source / Edit and ⋯. ← back is in the top bar.
export function AttachmentTouchHeader({
  projectKey,
  itemKey,
  data,
  view,
  onViewChange,
  replacing,
  replaceError,
  onReplace,
}: {
  projectKey: string;
  itemKey: string;
  data: AttachmentMarkdownDto;
  view: AttachmentView;
  onViewChange: (view: AttachmentView) => void;
  replacing: boolean;
  replaceError: string | null;
  onReplace: (file: File) => void;
}) {
  const [sheet, setSheet] = useState<'files' | 'about' | null>(null);
  const { attachment } = data;
  const close = () => setSheet(null);
  return (
    <header
      data-testid={ids.header}
      className="grid gap-0.5 border-b border-edge bg-surface px-3 pt-2 pb-2"
    >
      <h1 className="m-0 flex min-w-0">
        <button
          type="button"
          data-testid={ids.nameButton}
          aria-haspopup="dialog"
          className="flex min-h-[var(--touch-target)] min-w-0 items-center gap-2 rounded-control text-left font-[family-name:var(--font-mono)] text-[length:var(--text-lg)] font-semibold"
          onClick={() => setSheet('files')}
        >
          <span data-testid={ids.name} className="min-w-0 truncate">
            {attachment.name}
          </span>
          <span aria-hidden="true" className="text-ink-muted">
            ▾
          </span>
        </button>
      </h1>
      <button
        type="button"
        data-testid={ids.facts}
        aria-haspopup="dialog"
        className="flex min-h-[var(--touch-target)] min-w-0 items-center gap-2 rounded-control text-left text-[length:var(--text-sm)] text-ink-muted"
        onClick={() => setSheet('about')}
      >
        <span data-testid={ids.meta} className="min-w-0 truncate">
          {attachmentMeta(attachment)}
        </span>
        <span aria-hidden="true">›</span>
      </button>
      <div className="flex items-center gap-2 pt-1">
        <ViewToggle view={view} onChange={onViewChange} />
        <span className="flex-1" />
        <AttachmentMoreSheet
          attachment={attachment}
          replacing={replacing}
          onReplace={onReplace}
          onAbout={() => setSheet('about')}
        />
      </div>
      {replaceError ? (
        <p
          data-testid={ids.replaceError}
          role="alert"
          className="m-0 text-[length:var(--text-sm)] text-alert"
        >
          {replaceError}
        </p>
      ) : null}
      <Sheet open={sheet === 'files'} title={`Markdown on ${itemKey}`} onClose={close}>
        <SiblingsPanel
          projectKey={projectKey}
          itemKey={itemKey}
          current={attachment}
          siblings={data.siblings}
          bare
          onNavigate={close}
        />
      </Sheet>
      <Sheet open={sheet === 'about'} title="About this file" onClose={close}>
        <AboutThisFile data={data} itemKey={itemKey} />
      </Sheet>
    </header>
  );
}
