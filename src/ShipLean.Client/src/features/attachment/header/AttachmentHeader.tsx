import type { AttachmentMarkdownDto } from '../../../api/attachments';
import { BackToFeature } from '../BackToFeature';
import { attachmentTestIds as ids } from '../testIds';
import { attachmentMeta } from './attachmentMeta';
import { ReplaceButton } from './ReplaceButton';
import { ViewToggle, type AttachmentView } from './ViewToggle';

// Back to the Feature, then the file's name and facts, with the toolbar: Rendered / Source, Download, Replace….
export function AttachmentHeader({
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
  const { attachment, workItem } = data;
  return (
    <header className="grid gap-1 border-b border-edge bg-surface px-4 pt-2 pb-3">
      <BackToFeature projectKey={projectKey} itemKey={itemKey} title={workItem.title} />
      <div className="flex flex-wrap items-center gap-2">
        <h1
          data-testid={ids.name}
          className="m-0 font-[family-name:var(--font-mono)] text-[length:var(--text-lg)] font-semibold"
        >
          {attachment.name}
        </h1>
        <span data-testid={ids.meta} className="text-[length:var(--text-xs)] text-ink-muted">
          {attachmentMeta(attachment)}
        </span>
        <span className="flex-1" />
        <ViewToggle view={view} onChange={onViewChange} />
        <a
          data-testid={ids.download}
          href={attachment.downloadUrl}
          download={attachment.name}
          className="inline-flex h-7 items-center rounded-control border border-edge bg-panel px-3 text-[length:var(--text-sm)] text-ink no-underline"
        >
          Download
        </a>
        <ReplaceButton pending={replacing} onPick={onReplace} />
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
    </header>
  );
}
