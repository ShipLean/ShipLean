import { taskChangesTestIds as ids } from './testIds';
import { fileKey, type ChangesData, type ViewedProps } from './types';
import { isViewed } from './viewedStore';

export interface FilesColumnProps extends ViewedProps {
  data: ChangesData;
  /** A file key, `<repo>:<path>` (see `fileKey`). */
  openFile: string | null;
  onOpenFile(fileKey: string): void;
}

const STATUS_LETTER = { added: 'A', modified: 'M', deleted: 'D', renamed: 'R' } as const;
const STATUS_COLOUR = {
  added: 'text-good',
  modified: 'text-warn',
  deleted: 'text-danger',
  renamed: 'text-signal',
} as const;

// The changed files, grouped by repository when there is more than one. A row opens the file; the tick shows Viewed.
export function FilesColumn({ data, openFile, onOpenFile, viewed }: FilesColumnProps) {
  const total = data.parts.reduce((n, part) => n + part.files.length, 0);
  const viewedTotal = data.parts.reduce(
    (n, part) =>
      n + part.files.filter((f) => isViewed(viewed, part.repo, f.path, f.patchId)).length,
    0,
  );
  const grouped = new Set(data.parts.map((p) => p.repo)).size > 1;
  return (
    <nav
      data-testid={ids.files}
      aria-label="Changed files"
      className="content-start rounded-panel bg-panel-2 p-2 text-[length:var(--text-sm)]"
    >
      <div className="flex items-center gap-1.5 px-2 pb-1.5">
        <span className="text-[length:var(--text-xs)] font-semibold uppercase text-ink-muted">
          Files
        </span>
        <span className="text-ink-muted">{total}</span>
        <span className="ml-auto text-[length:var(--text-xs)] text-ink-muted">
          {viewedTotal} viewed
        </span>
      </div>
      {data.parts.map((part) => (
        <div key={part.repo}>
          {grouped && (
            <div className="h-6 truncate px-2 text-ink-muted" title={part.repo}>
              ▾ {part.repo}
            </div>
          )}
          {part.files.map((file) => {
            const key = fileKey(part.repo, file.path);
            const done = isViewed(viewed, part.repo, file.path, file.patchId);
            const slash = file.path.lastIndexOf('/');
            return (
              <button
                key={key}
                type="button"
                data-testid={ids.fileRow}
                data-file={key}
                aria-current={openFile === key}
                title={file.path}
                className="grid h-[26px] w-full grid-cols-[14px_minmax(0,1fr)_auto_auto] items-center gap-1.5 rounded-control px-1.5 text-left hover:bg-panel aria-current:bg-signal-soft"
                onClick={() => onOpenFile(key)}
              >
                <span
                  className={`text-center font-[family-name:var(--font-mono)] text-[9px] font-bold ${STATUS_COLOUR[file.status]}`}
                >
                  {STATUS_LETTER[file.status]}
                </span>
                <span className={`truncate ${done ? 'text-ink-muted' : ''}`}>
                  {file.path.slice(slash + 1)}
                </span>
                {file.openComments > 0 ? (
                  <span
                    title={`${file.openComments} open`}
                    className="inline-grid h-4 min-w-4 place-items-center rounded-full bg-warn px-1 text-[10px] font-bold text-black"
                  >
                    {file.openComments}
                  </span>
                ) : (
                  <span />
                )}
                <span
                  data-testid={ids.fileViewed}
                  className="w-3 text-center text-[length:var(--text-xs)] text-good"
                >
                  {done ? '✓' : ''}
                </span>
              </button>
            );
          })}
        </div>
      ))}
    </nav>
  );
}
