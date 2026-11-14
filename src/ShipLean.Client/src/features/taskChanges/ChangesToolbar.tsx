import { useState } from 'react';
import { taskChangesTestIds as ids } from './testIds';
import type { ChangesData, DiffLayout } from './types';

export interface ChangesToolbarProps {
  data: ChangesData;
  layout: DiffLayout;
  /** "n of m files viewed" at the current head. */
  viewedCount: number;
  fileTotal: number;
  /** A teammate added a commit since this page loaded; the banner offers `onReload`. */
  newCommit: boolean;
  onReload(): void;
  /** "all" or a work_item_commit id. */
  onPickCommit(commit: string): void;
  onLayout(layout: DiffLayout): void;
}

const optionClass =
  'grid h-[30px] grid-cols-[14px_64px_minmax(0,1fr)_auto] items-center gap-2 rounded-control px-2 text-left text-[length:var(--text-sm)] hover:bg-signal-soft';
const buttonClass =
  'inline-flex h-7 items-center rounded-control border border-edge bg-panel px-3 text-[length:var(--text-sm)] font-medium text-ink focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-signal';

// The bar above the files, diff and comments: the commit picker, the repositories, totals, the layout toggle and the viewed
// progress, with the new-commit banner under it. Approve and Request changes are ReviewBar's, at the end of the same row.
export function ChangesToolbar({
  data,
  layout,
  viewedCount,
  fileTotal,
  newCommit,
  onReload,
  onPickCommit,
  onLayout,
}: ChangesToolbarProps) {
  const [open, setOpen] = useState(false);
  const { commits, selected } = data;
  const current = commits.find((c) => c.id === selected);
  const additions = data.parts.reduce(
    (n, part) => n + part.files.reduce((m, f) => m + f.additions, 0),
    0,
  );
  const deletions = data.parts.reduce(
    (n, part) => n + part.files.reduce((m, f) => m + f.deletions, 0),
    0,
  );
  const repos = [...new Set(commits.map((c) => c.repo))];
  const pick = (commit: string) => {
    setOpen(false);
    onPickCommit(commit);
  };
  return (
    <div data-testid={ids.toolbar} className="grid gap-2">
      <div className="flex flex-wrap items-center gap-2">
        <span className="relative">
          <button
            type="button"
            data-testid={ids.commitPicker}
            aria-expanded={open}
            aria-haspopup="listbox"
            className={buttonClass}
            onClick={() => setOpen(!open)}
          >
            {current === undefined ? `All ${commits.length} commits` : current.shortSha} ▾
          </button>
          {open && (
            <div
              role="listbox"
              aria-label="Commits"
              className="absolute left-0 top-[calc(100%+4px)] z-30 grid w-[460px] gap-px rounded-panel border border-edge bg-panel p-1 shadow-lg"
            >
              <div className="px-2 pt-1.5 text-[length:var(--text-xs)] font-semibold uppercase text-ink-muted">
                Show
              </div>
              <button
                type="button"
                role="option"
                aria-selected={current === undefined}
                data-testid={ids.commitOption}
                className={optionClass}
                onClick={() => pick('all')}
              >
                <span className="text-signal">{current === undefined ? '✓' : ''}</span>
                <b className="col-span-2">All {commits.length}</b>
                <span />
              </button>
              <div className="px-2 pt-1.5 text-[length:var(--text-xs)] font-semibold uppercase text-ink-muted">
                One commit
              </div>
              {commits.map((commit) => (
                <button
                  key={commit.id}
                  type="button"
                  role="option"
                  aria-selected={commit.id === selected}
                  data-testid={ids.commitOption}
                  title={commit.repo}
                  className={optionClass}
                  onClick={() => pick(commit.id)}
                >
                  <span className="text-signal">{commit.id === selected ? '✓' : ''}</span>
                  <span className="font-[family-name:var(--font-mono)] text-signal">
                    {commit.shortSha}
                  </span>
                  <span className="truncate">
                    {commit.subject}
                    {repos.length > 1 ? ` · ${commit.repo}` : ''}
                  </span>
                  <span>
                    <span className="text-good">+{commit.additions}</span>{' '}
                    <span className="text-danger">−{commit.deletions}</span>
                  </span>
                </button>
              ))}
            </div>
          )}
        </span>
        {repos.map((repo) => (
          <span
            key={repo}
            className="rounded-control bg-signal-soft px-1.5 text-[length:var(--text-xs)]"
          >
            {repo}
          </span>
        ))}
        <span className="text-[length:var(--text-sm)] text-ink-muted">
          {fileTotal} files · <span className="text-good">+{additions}</span>{' '}
          <span className="text-danger">−{deletions}</span>
        </span>
        <span className="h-[18px] w-px bg-edge" />
        <span
          role="group"
          aria-label="Layout"
          data-testid={ids.layoutToggle}
          className="inline-flex"
        >
          {(['unified', 'split'] as const).map((mode) => (
            <button
              key={mode}
              type="button"
              aria-pressed={layout === mode}
              className={`${buttonClass} first:rounded-r-none last:rounded-l-none aria-pressed:bg-signal-soft`}
              onClick={() => onLayout(mode)}
            >
              {mode === 'unified' ? 'Unified' : 'Split'}
            </button>
          ))}
        </span>
        <span
          data-testid={ids.filesViewed}
          title="Viewed is kept in this browser"
          className="ml-auto inline-flex items-center gap-1.5 text-[length:var(--text-sm)] text-ink-muted"
        >
          <span className="h-1.5 w-[90px] overflow-hidden rounded-full bg-panel-2">
            <i
              className="block h-full bg-signal"
              style={{ width: `${fileTotal === 0 ? 0 : (viewedCount / fileTotal) * 100}%` }}
            />
          </span>
          {viewedCount} / {fileTotal} viewed
        </span>
      </div>
      {newCommit && (
        <div
          data-testid={ids.newCommitBanner}
          role="status"
          className="flex items-center gap-2 border-l-[3px] border-signal bg-signal-soft px-4 py-1.5 text-[length:var(--text-sm)]"
        >
          <b>A new commit since you looked</b>
          <button
            type="button"
            data-testid={ids.newCommitReload}
            className="ml-auto h-6 rounded-control px-2 text-[length:var(--text-xs)] text-signal hover:bg-signal-soft"
            onClick={onReload}
          >
            Reload
          </button>
        </div>
      )}
    </div>
  );
}
