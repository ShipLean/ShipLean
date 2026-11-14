import { useState } from 'react';
import { Link } from 'react-router-dom';
import { ApiRequestError } from '@shiplean/core/api/client';
import type { TaskCommitsSectionDto } from '@shiplean/core/api/taskChanges';
import { routePath } from '../../routes';
import { inputClass } from '../workItem/form/inputClasses';
import { taskChangesTestIds as ids } from './testIds';

export interface CommitsSectionProps {
  projectKey: string;
  itemKey: string;
  section: TaskCommitsSectionDto;
  /** "n of m files viewed" at the current head. */
  viewedCount: number;
  onAddCommit(repo: string | null, sha: string): Promise<void>;
}

const buttonClass =
  'inline-flex h-7 items-center rounded-control border px-3 text-[length:var(--text-sm)] font-medium focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-signal disabled:opacity-50';

const isUrl = (value: string) => /^https?:\/\//i.test(value);

// The Changes section of a Task or Bug page: its commits, totals, open comments and the way into the Changes page.
export function CommitsSection({
  projectKey,
  itemKey,
  section,
  viewedCount,
  onAddCommit,
}: CommitsSectionProps) {
  const { commits, totals } = section;
  const empty = commits.length === 0;
  return (
    <section
      data-testid={ids.section}
      aria-label="Changes"
      className="grid grid-cols-[minmax(0,1fr)] content-start gap-2 border-b border-edge px-4 py-3"
    >
      <Header
        section={section}
        viewedCount={viewedCount}
        empty={empty}
        projectKey={projectKey}
        itemKey={itemKey}
        totals={totals}
      />
      {empty ? (
        <AddCommit onAddCommit={onAddCommit} />
      ) : (
        <div className="overflow-hidden rounded-panel border border-edge">
          <table className="w-full border-collapse text-[length:var(--text-sm)]">
            <tbody>
              {commits.map((commit) => (
                <tr
                  key={commit.id}
                  data-testid={ids.sectionRow}
                  className="border-t first:border-t-0 border-edge"
                >
                  <td className="h-[34px] px-2 whitespace-nowrap">
                    <a
                      href={commit.commitUrl}
                      title={commit.sha}
                      className="font-[family-name:var(--font-mono)] text-signal"
                    >
                      {commit.shortSha}
                    </a>
                  </td>
                  <td className="w-full max-w-0 truncate px-2" title={commit.subject}>
                    {commit.subject}
                  </td>
                  <td className="px-2 whitespace-nowrap">
                    <span className="rounded-control bg-signal-soft px-1.5 text-[length:var(--text-xs)]">
                      {commit.repo}
                    </span>
                  </td>
                  <td className="px-2 whitespace-nowrap">{commit.addedBy}</td>
                  <td className="px-2 whitespace-nowrap text-[length:var(--text-xs)] text-ink-muted">
                    {commit.authoredAtLabel}
                  </td>
                  <td className="px-2 text-right whitespace-nowrap">
                    <span className="text-good">+{commit.additions}</span>{' '}
                    <span className="text-danger">−{commit.deletions}</span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}

function Header({
  section,
  viewedCount,
  empty,
  projectKey,
  itemKey,
  totals,
}: {
  section: TaskCommitsSectionDto;
  viewedCount: number;
  empty: boolean;
  projectKey: string;
  itemKey: string;
  totals: TaskCommitsSectionDto['totals'];
}) {
  return (
    <div className="flex flex-wrap items-center gap-2">
      <h2 className="m-0 text-[length:var(--text-md)] font-semibold">Changes</h2>
      <span data-testid={ids.sectionCount} className="sr-only">
        {totals.commits}
      </span>
      {empty ? (
        <span className="text-[length:var(--text-sm)] text-ink-muted">No commits yet</span>
      ) : (
        <span
          data-testid={ids.sectionTotals}
          className="text-[length:var(--text-sm)] text-ink-muted"
        >
          {totals.commits} commits · {totals.files} files ·{' '}
          <span className="text-good">+{totals.additions}</span>{' '}
          <span className="text-danger">−{totals.deletions}</span>
        </span>
      )}
      {empty ? null : (
        <>
          <span className="ml-auto" />
          {section.openComments > 0 ? (
            <span
              data-testid={ids.sectionOpenComments}
              className="rounded-control bg-warn/20 px-1.5 text-[length:var(--text-xs)]"
            >
              {section.openComments} open comments
            </span>
          ) : null}
          <span
            data-testid={ids.sectionViewed}
            className="text-[length:var(--text-xs)] text-ink-muted"
          >
            {viewedCount} / {totals.files} files viewed
          </span>
          <Link
            data-testid={ids.sectionReview}
            to={routePath('taskChanges', { key: projectKey, itemKey })}
            className={`${buttonClass} border-signal bg-signal text-signal-ink`}
          >
            Review changes
          </Link>
        </>
      )}
    </div>
  );
}

function AddCommit({
  onAddCommit,
}: {
  onAddCommit(repo: string | null, sha: string): Promise<void>;
}) {
  const [open, setOpen] = useState(false);
  const [repo, setRepo] = useState('');
  const [input, setInput] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const submit = async () => {
    const value = input.trim();
    const named = repo.trim();
    setBusy(true);
    setError(null);
    try {
      await onAddCommit(isUrl(value) || named === '' ? null : named, value);
      setOpen(false);
      setRepo('');
      setInput('');
    } catch (caught) {
      setError(caught instanceof ApiRequestError ? caught.message : 'The commit was not added.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <>
      <div className="flex">
        <button
          type="button"
          data-testid={ids.sectionAddOpen}
          className="ml-auto h-6 rounded-control px-2 text-[length:var(--text-xs)] text-signal hover:bg-signal-soft"
          onClick={() => setOpen(!open)}
        >
          + Add commit
        </button>
      </div>
      <div
        data-testid={ids.sectionEmpty}
        className="grid gap-1.5 rounded-panel border border-dashed border-edge p-3 text-[length:var(--text-sm)] text-ink-muted"
      >
        <span>
          When an agent finishes this task it sends the commits it made, and they show here with
          their diff. Tasks and Bugs take commits; Stories and Features don&apos;t.
        </span>
        <span>
          Agents send them with <code>report_task</code> (tasks of a plan) or{' '}
          <code>add_commits</code> (any task or bug). People add one with <b>+ Add commit</b>.
        </span>
      </div>
      {open ? (
        <form
          className="grid grid-cols-[220px_minmax(0,1fr)_auto_auto] items-end gap-2 rounded-panel border border-signal p-3"
          onSubmit={(event) => {
            event.preventDefault();
            void submit();
          }}
        >
          <label className="grid gap-1 text-[length:var(--text-xs)] text-ink-muted">
            Repository
            <input
              data-testid={ids.sectionAddRepo}
              aria-label="Repository"
              className={inputClass}
              value={repo}
              onChange={(event) => setRepo(event.target.value)}
            />
          </label>
          <label className="grid gap-1 text-[length:var(--text-xs)] text-ink-muted">
            Commit sha or URL
            <input
              data-testid={ids.sectionAddInput}
              aria-label="Commit sha or URL"
              className={inputClass}
              value={input}
              onChange={(event) => setInput(event.target.value)}
            />
          </label>
          <button
            type="submit"
            data-testid={ids.sectionAddSubmit}
            disabled={busy || input.trim() === ''}
            className={`${buttonClass} border-signal bg-signal text-signal-ink`}
          >
            Add commit
          </button>
          <button
            type="button"
            className={`${buttonClass} border-edge bg-panel text-ink hover:bg-signal-soft`}
            onClick={() => setOpen(false)}
          >
            Cancel
          </button>
          {error !== null ? (
            <span
              role="alert"
              data-testid={ids.sectionAddError}
              className="col-span-full text-[length:var(--text-xs)] text-alert"
            >
              {error}
            </span>
          ) : null}
        </form>
      ) : null}
    </>
  );
}
