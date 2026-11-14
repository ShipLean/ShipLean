import { PatchDiff } from '@pierre/diffs/react';
import type { DiffLineAnnotation } from '@pierre/diffs';
import type { CodeSide, DiffFileDto, DiffPartDto } from '@shiplean/core/api/taskChanges';
import { useEffect, useMemo, useRef, useState } from 'react';
import type { ReactNode } from 'react';
import { currentThemeType, diffThemes, diffUnsafeCss } from './diffTheme';
import { lineTextOf, parseFile, withHeader } from './diffLines';
import type { LineMarker } from './markers';
import { taskChangesTestIds as ids } from './testIds';
import { isViewed } from './viewedStore';
import { fileKey } from './types';
import type { ChangesData, ComposerTarget, DiffLayout, ViewedProps } from './types';

export interface DiffColumnProps extends ViewedProps {
  data: ChangesData;
  layout: DiffLayout;
  /** A file key, `<repo>:<path>` (see `fileKey`). */
  openFile: string | null;
  /** Bump to scroll to `openFile` again, e.g. when its row is clicked while it is already open. */
  scrollNonce?: number;
  markers: LineMarker[];
  /** The + on a line. */
  onAddLine(target: ComposerTarget): void;
  /** Where the composer goes: the diff renders `composer` under `composerTarget`'s line. Null when none is open. */
  composerTarget: ComposerTarget | null;
  composer: ReactNode;
}

type Annotation = { kind: 'marker'; marker: LineMarker } | { kind: 'composer' };

const sideOf = (side: CodeSide) => (side === 'old' ? 'deletions' : 'additions');
const codeSide = (side: 'deletions' | 'additions'): CodeSide =>
  side === 'deletions' ? 'old' : 'new';
const shaOf = (part: DiffPartDto) => part.sha ?? part.headSha;

// The file at the part's head on its host, from the repository URL the commit rows carry. Only GitHub and GitLab are known;
// a deleted file has no blob at the head, and an unknown repo or provider gets no link.
function hostLink(data: ChangesData, part: DiffPartDto, file: DiffFileDto) {
  if (file.status === 'deleted') return null;
  const commit = data.commits.find((c) => c.repo === part.repo);
  if (!commit) return null;
  const base = commit.repoUrl.replace(/\/+$/, '');
  const blob =
    commit.provider === 'gitlab' ? '/-/blob/' : commit.provider === 'github' ? '/blob/' : null;
  if (blob === null) return null;
  return {
    href: `${base}${blob}${shaOf(part)}/${file.path}`,
    host: commit.provider === 'gitlab' ? 'GitLab' : 'GitHub',
  };
}

export function DiffColumn(props: DiffColumnProps) {
  const { data, openFile, scrollNonce } = props;
  const root = useRef<HTMLDivElement>(null);

  // Each diff renders after mount and grows the page, so scrolling once would land short: keep the file at the top while the
  // page grows, until the reader scrolls or presses something.
  useEffect(() => {
    const column = root.current;
    if (openFile === null || !column) return;
    const scroll = () =>
      [...column.querySelectorAll<HTMLElement>('[data-file-key]')]
        .find((e) => e.dataset.fileKey === openFile)
        ?.scrollIntoView({ block: 'start' });
    scroll();
    const observer = new ResizeObserver(scroll);
    observer.observe(column);
    const stop = () => observer.disconnect();
    const events = ['wheel', 'touchstart', 'pointerdown', 'keydown'] as const;
    for (const name of events) window.addEventListener(name, stop, { once: true });
    return () => {
      observer.disconnect();
      for (const name of events) window.removeEventListener(name, stop);
    };
  }, [openFile, scrollNonce, data.headCommitId, data.selected]);

  const noAccess = data.commits.filter((c) => c.access !== 'ok');
  return (
    <div
      ref={root}
      data-testid={ids.diff}
      className="grid content-start gap-3 bg-canvas p-4 pb-[120px]"
    >
      {noAccess.map((commit) => (
        <div
          key={commit.id}
          data-testid={ids.diffNoAccess}
          className="max-w-[760px] rounded-control border border-edge bg-panel p-3 text-[length:var(--text-sm)]"
        >
          <b>Can&apos;t show this diff.</b> ShipLean can&apos;t read {commit.repo}
          {commit.access === 'unreadable'
            ? ': the instance token was refused.'
            : ': the commit is no longer there.'}{' '}
          <a
            data-testid={ids.diffOpenOnHost}
            href={commit.commitUrl}
            target="_blank"
            rel="noreferrer"
            className="text-signal underline"
          >
            Open {commit.shortSha} on {commit.provider === 'gitlab' ? 'GitLab' : 'GitHub'}
          </a>
        </div>
      ))}
      {data.parts.flatMap((part) =>
        part.files.map((file) => (
          <DiffFile
            key={`${part.repo}:${shaOf(part)}:${file.path}`}
            part={part}
            file={file}
            {...props}
          />
        )),
      )}
    </div>
  );
}

function DiffFile({
  part,
  file,
  data,
  viewed,
  layout,
  markers,
  onAddLine,
  onToggleViewed,
  composerTarget,
  composer,
}: DiffColumnProps & { part: DiffPartDto; file: DiffFileDto }) {
  const sha = shaOf(part);
  const done = isViewed(viewed, part.repo, file.path, file.patchId);
  // Collapse is local to the page and separate from Viewed: a viewed file stays collapsed.
  const [folded, setFolded] = useState(false);
  const collapsed = done || folded;
  const link = hostLink(data, part, file);
  const meta = useMemo(
    () => parseFile(file.patch, file.path, file.oldPath),
    [file.patch, file.path, file.oldPath],
  );
  const annotations = useMemo(() => {
    const list: DiffLineAnnotation<Annotation>[] = [];
    for (const m of markers) {
      if (m.repo !== part.repo || m.path !== file.path) continue;
      if (part.sha !== null && m.sha !== part.sha) continue;
      list.push({
        side: sideOf(m.side),
        lineNumber: m.line,
        metadata: { kind: 'marker', marker: m },
      });
    }
    if (
      composerTarget?.repo === part.repo &&
      composerTarget.path === file.path &&
      composerTarget.sha === sha
    ) {
      list.push({
        side: sideOf(composerTarget.side),
        lineNumber: composerTarget.line,
        metadata: { kind: 'composer' },
      });
    }
    return list;
  }, [markers, composerTarget, part.repo, part.sha, sha, file.path]);

  const key = fileKey(part.repo, file.path);
  return (
    <section
      data-testid={ids.diffFile}
      data-file-key={key}
      className="scroll-mt-3 overflow-hidden rounded-control border border-edge bg-panel"
    >
      <header
        className={`sticky top-0 z-[2] flex min-h-9 items-center gap-2 bg-panel px-3 text-[length:var(--text-sm)] ${collapsed ? '' : 'border-b border-edge'}`}
      >
        <button
          type="button"
          data-testid={ids.diffFileCollapse}
          aria-label={collapsed ? 'Expand' : 'Collapse'}
          aria-expanded={!collapsed}
          disabled={done}
          className="w-4 border-0 bg-transparent p-0 text-ink-muted"
          onClick={() => setFolded(!folded)}
        >
          {collapsed ? '▸' : '▾'}
        </button>
        <span className="min-w-0 truncate font-mono text-[length:var(--text-xs)]">
          {file.oldPath !== null && <span className="text-ink-muted">{file.oldPath} → </span>}
          {file.path}
        </span>
        <span className="text-[length:var(--text-xs)] text-good">+{file.additions}</span>
        <span className="text-[length:var(--text-xs)] text-alert">-{file.deletions}</span>
        {link && (
          <a
            data-testid={ids.diffFileHostLink}
            href={link.href}
            target="_blank"
            rel="noreferrer"
            title={`Open this file at ${shortOf(part)} on ${link.host}`}
            className="ml-auto text-[length:var(--text-xs)] text-ink-muted no-underline hover:text-signal"
          >
            {link.host} ↗
          </a>
        )}
        <label
          className={`${link ? '' : 'ml-auto '}flex items-center gap-1 text-[length:var(--text-xs)]`}
        >
          <input
            type="checkbox"
            data-testid={ids.fileViewed}
            checked={done}
            onChange={(e) => onToggleViewed(part.repo, file.path, file.patchId, e.target.checked)}
          />
          Viewed
        </label>
      </header>
      {collapsed ? (
        <p className="m-0 px-3 py-2 text-[length:var(--text-sm)] text-ink-muted">
          {done ? 'Viewed. The diff is collapsed.' : ''}
        </p>
      ) : (
        <PatchDiff<Annotation>
          patch={withHeader(file.patch, file.path, file.oldPath)}
          options={{
            diffStyle: layout,
            themeType: currentThemeType(),
            theme: diffThemes,
            disableFileHeader: true,
            enableGutterUtility: true,
            // The library owns the click on its + (the slotted node sits under its own event layer).
            onGutterUtilityClick: (range) => {
              const side = codeSide(range.endSide ?? range.side ?? 'additions');
              onAddLine({
                repo: part.repo,
                sha,
                path: file.path,
                side,
                line: range.end,
                startLine: range.start === range.end ? null : range.start,
                lineText: lineTextOf(meta, side, range.end),
              });
            },
            unsafeCSS: diffUnsafeCss,
          }}
          lineAnnotations={annotations}
          renderAnnotation={(a) =>
            a.metadata.kind === 'composer' ? composer : <MarkerChip marker={a.metadata.marker} />
          }
        />
      )}
    </section>
  );
}

const shortOf = (part: DiffPartDto) => shaOf(part).slice(0, 7);

function MarkerChip({ marker }: { marker: LineMarker }) {
  return (
    <div className="px-3 py-1 font-sans text-[length:var(--text-xs)]">
      <span
        data-testid={marker.outdated ? ids.markerOutdated : ids.marker}
        className="rounded-control border border-edge bg-canvas px-2 py-0.5"
      >
        {marker.count} {marker.count === 1 ? 'comment' : 'comments'}
        {marker.outdated ? ' · outdated' : marker.open === 0 ? ' · resolved' : ''}
      </span>
    </div>
  );
}
