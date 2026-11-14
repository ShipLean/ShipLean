import { useEffect, useMemo, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import type { ProjectDto } from '@shiplean/core/api/projects';
import { useWorkItem } from '../workItem/workItemApi';
import { ChangesToolbar } from './ChangesToolbar';
import { CodeCommentComposer } from './CodeCommentComposer';
import { CommentsColumn } from './CommentsColumn';
import { DiffColumn } from './DiffColumn';
import { FilesColumn } from './FilesColumn';
import { deriveMarkers } from './markers';
import { NarrowNotice } from './NarrowNotice';
import {
  useAddCodeComment,
  useApproveChanges,
  useReplyToCodeComment,
  useRequestChanges,
  useResolveCodeComment,
  useTaskChanges,
} from './taskChangesApi';
import { taskChangesTestIds as ids } from './testIds';
import type { ComposerTarget, DiffLayout } from './types';
import { useWideScreen } from './useWideScreen';
import { readViewed, restampViewed, saveViewed, setViewed, type ViewedState } from './viewedStore';

export interface ChangesScreenProps {
  project: ProjectDto;
  itemKey: string;
}

// The full-width Changes page. Under 900px it shows the notice and fetches nothing. The address is the source of truth for
// the commit shown, the open file and the layout: ?commit=<commitId>&file=<path>&layout=split.
export function ChangesScreen({ project, itemKey }: ChangesScreenProps) {
  const wide = useWideScreen();
  if (!wide) return <NarrowNotice projectKey={project.key} itemKey={itemKey} />;
  return <WideChanges project={project} itemKey={itemKey} />;
}

function WideChanges({ project, itemKey }: ChangesScreenProps) {
  const item = useWorkItem(project.id, itemKey);
  const workItemId = item.data?.id;
  const [params, setParams] = useSearchParams();
  const commit = params.get('commit') ?? 'all';
  const openFile = params.get('file');
  const layout: DiffLayout = params.get('layout') === 'split' ? 'split' : 'unified';
  const { query, newCommit, reload } = useTaskChanges(project.id, workItemId, commit);
  const [composerTarget, setComposerTarget] = useState<ComposerTarget | null>(null);
  // The marks after the reviewer's last toggle; before one, the marks in storage.
  const [toggled, setToggled] = useState<ViewedState | null>(null);
  const addComment = useAddCodeComment(project.id, workItemId ?? '');
  const reply = useReplyToCodeComment(project.id, workItemId ?? '');
  const resolve = useResolveCodeComment(project.id, workItemId ?? '');
  const approve = useApproveChanges(project.id, workItemId ?? '');
  const request = useRequestChanges(project.id, workItemId ?? '');
  const data = query.data;
  const markers = useMemo(() => deriveMarkers(data?.comments ?? []), [data?.comments]);
  const files = useMemo(
    () =>
      (data?.parts ?? []).flatMap((part) =>
        part.files.map((file) => ({ repo: part.repo, path: file.path, patchId: file.patchId })),
      ),
    [data?.parts],
  );
  const headCommitId = data?.headCommitId ?? null;

  // A new head keeps Viewed on the files whose patch did not change. Only the page of all commits has every patch.
  const viewed = useMemo(() => {
    if (workItemId === undefined) return {};
    const stored = toggled ?? readViewed(workItemId);
    return data?.selected === 'all' ? restampViewed(stored, headCommitId, files) : stored;
  }, [workItemId, data?.selected, headCommitId, files, toggled]);
  useEffect(() => {
    if (workItemId !== undefined && data?.selected === 'all') saveViewed(workItemId, viewed);
  }, [workItemId, data?.selected, viewed]);

  if (item.data === undefined || data === undefined) {
    if (query.isError) {
      return (
        <div data-testid={ids.loadFailed} role="alert" className="p-4 text-alert">
          {query.error.message}
        </div>
      );
    }
    return (
      <div data-testid={ids.loading} className="p-4 text-ink-muted">
        Loading…
      </div>
    );
  }
  const id = item.data.id;
  const update = (name: string, value: string | null) =>
    setParams(
      (prev) => {
        const next = new URLSearchParams(prev);
        if (value === null) next.delete(name);
        else next.set(name, value);
        return next;
      },
      { replace: true },
    );
  const viewedCount = files.filter(
    (f) => viewed[`${f.repo}\u0000${f.path}`]?.patchId === f.patchId,
  ).length;
  const composer =
    composerTarget === null ? null : (
      <CodeCommentComposer
        target={composerTarget}
        onPost={async (body) => {
          await addComment.mutateAsync({ ...composerTarget, body });
          setComposerTarget(null);
        }}
        onCancel={() => setComposerTarget(null)}
      />
    );
  const viewedProps = {
    viewed,
    onToggleViewed: (repo: string, path: string, patchId: string, on: boolean) =>
      setToggled(setViewed(id, repo, path, patchId, headCommitId, on)),
  };
  return (
    <div data-testid={ids.screen} className="grid gap-3">
      <ChangesToolbar
        data={data}
        layout={layout}
        viewedCount={viewedCount}
        fileTotal={files.length}
        newCommit={newCommit}
        onReload={reload}
        onPickCommit={(next) => update('commit', next === 'all' ? null : next)}
        onLayout={(next) => update('layout', next === 'split' ? 'split' : null)}
        review={{
          approve: (note) => approve.mutateAsync({ note, headCommitId: headCommitId ?? '' }),
          requestChanges: (note, keepAssignee) => request.mutateAsync({ note, keepAssignee }),
        }}
      />
      <div className="grid grid-cols-[16rem_minmax(0,1fr)_20rem] gap-4">
        <FilesColumn
          data={data}
          openFile={openFile}
          onOpenFile={(path) => update('file', path)}
          {...viewedProps}
        />
        <DiffColumn
          data={data}
          layout={layout}
          openFile={openFile}
          markers={markers}
          onAddLine={setComposerTarget}
          composerTarget={composerTarget}
          composer={composer}
          {...viewedProps}
        />
        <CommentsColumn
          data={data}
          onReply={async (commentId, body) => {
            await reply.mutateAsync({ commentId, body });
          }}
          onResolve={async (commentId, resolved) => {
            await resolve.mutateAsync({ commentId, resolved });
          }}
        />
      </div>
    </div>
  );
}
