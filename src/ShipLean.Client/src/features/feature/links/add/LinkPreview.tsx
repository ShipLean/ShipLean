import type { LinkPreviewDto } from '../../../../api/linkCards';
import { featureTestIds as ids } from '../../testIds';
import { LinkStateChip } from '../section/LinkStateChip';
import { kindBadge } from '../section/linkSource';

// What the link will look like once added, with the server's one-line message about it.
export function LinkPreview({ preview }: { preview: LinkPreviewDto }) {
  const withheld = preview.previewStatus === 'private';
  return (
    <>
      <p
        data-testid={ids.addLinkMessage}
        className={
          withheld
            ? 'm-0 rounded-control border-l-4 border-caution bg-surface-2 p-2 text-[length:var(--text-sm)] text-ink'
            : 'm-0 text-[length:var(--text-sm)] text-ink-muted'
        }
      >
        {preview.message}
      </p>
      <div
        data-testid={ids.addLinkPreview}
        data-kind={preview.kind}
        className="flex max-w-xl items-start gap-2 rounded-control border border-edge bg-surface-2 p-2.5 text-[length:var(--text-sm)]"
      >
        <span className="mt-0.5 inline-flex h-5 shrink-0 items-center rounded-control bg-panel px-1 text-[length:var(--text-xs)] font-semibold text-ink-muted">
          {kindBadge(preview.kind)}
        </span>
        <div className="grid min-w-0 flex-1 gap-0.5">
          <div className="flex gap-1.5">
            {preview.number === null ? null : (
              <span className="text-ink-muted">
                {preview.kind.startsWith('gitlabMerge') ? '!' : '#'}
                {preview.number}
              </span>
            )}
            <span data-testid={ids.addLinkPreviewTitle} className="min-w-0 truncate font-medium">
              {preview.title}
            </span>
          </div>
          <span className="truncate text-[length:var(--text-xs)] text-ink-muted">
            {preview.location ??
              (preview.previewStatus === 'ok' ? preview.host : `${preview.host} · no preview`)}
          </span>
        </div>
        {preview.state ? <LinkStateChip state={preview.state} /> : null}
      </div>
    </>
  );
}
