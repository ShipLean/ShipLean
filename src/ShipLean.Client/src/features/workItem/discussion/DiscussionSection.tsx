import { useQuery } from '@tanstack/react-query';
import type { ProjectDto } from '../../../api/projects';
import { fetchMe, meKey } from '../../../queries/shared';
import { workItemTestIds as ids } from '../testIds';
import { fetchComments, useProjectMembers, useWorkItem, workItemCommentsKey } from '../workItemApi';
import { CommentComposer } from './CommentComposer';
import { CommentItem } from './CommentItem';
import { useCommentEvents } from './useCommentEvents';
import { useCommentWrites } from './useCommentWrites';

// What makes the section the Feature's Log: its title, its own test ids and empty text. The Log lists newest first and
// marks an agent's entry "Agent · via MCP".
export interface LogMode {
  title: string;
  countTestId: string;
  emptyTestId: string;
  emptyText: string;
}

// The item's comments, oldest first (newest first as a Log), and the box for a new one. Comments are their own records and save at once; nothing
// here reads or writes the form's draft. The `@` suggestions come from the project's members.
export function DiscussionSection({
  project,
  itemKey,
  log,
}: {
  project: ProjectDto;
  itemKey: string;
  log?: LogMode;
}) {
  const item = useWorkItem(project.id, itemKey).data;
  const me = useQuery({ queryKey: meKey, queryFn: fetchMe }).data;
  const members = useProjectMembers(project.id).data;
  const workItemId = item?.id ?? '';
  const comments = useQuery({
    queryKey: workItemCommentsKey(project.id, itemKey),
    queryFn: () => fetchComments(workItemId),
    enabled: item !== undefined,
  });
  useCommentEvents(project.id, itemKey, item?.id);
  const writes = useCommentWrites(project.id, itemKey, workItemId);
  if (!item) return null;
  const loaded = comments.data ?? [];
  const list = log ? [...loaded].reverse() : loaded;
  return (
    <section
      data-testid={ids.discussion}
      aria-label={log?.title ?? 'Discussion'}
      className="grid max-w-[780px] gap-3"
    >
      <header className="flex items-center gap-2">
        <h2 className="text-[length:var(--text-md)] font-semibold">{log?.title ?? 'Discussion'}</h2>
        <span
          data-testid={log?.countTestId ?? ids.commentsCount}
          className="text-[length:var(--text-xs)] text-ink-muted"
        >
          {list.length}
        </span>
      </header>
      {comments.isError ? (
        <p
          data-testid={ids.commentsLoadFailed}
          role="alert"
          className="m-0 text-[length:var(--text-sm)] text-alert"
        >
          The comments could not be loaded.
        </p>
      ) : list.length === 0 && comments.isSuccess ? (
        <p
          data-testid={log?.emptyTestId ?? ids.commentsEmpty}
          className="m-0 text-[length:var(--text-sm)] text-ink-muted"
        >
          {log?.emptyText ?? 'No comments yet.'}
        </p>
      ) : (
        <ol className="m-0 grid list-none p-0">
          {list.map((comment) => {
            const mine = comment.author !== null && comment.author.id === me?.id;
            return (
              <CommentItem
                key={comment.id}
                comment={comment}
                canEdit={mine}
                canArchive={mine || project.role === 'admin'}
                saving={writes.edit.isPending}
                error={
                  writes.archive.variables === comment.id
                    ? (writes.archive.error?.message ?? null)
                    : null
                }
                viaMcp={log !== undefined}
                onSave={(body) => writes.edit.mutateAsync({ id: comment.id, body })}
                onArchive={() => writes.archive.mutate(comment.id)}
                onSupersede={() => writes.supersede.mutate(comment.id)}
              />
            );
          })}
        </ol>
      )}
      <CommentComposer
        people={(members ?? []).map((member) => member.user)}
        posting={writes.add.isPending}
        error={writes.add.error?.message ?? null}
        onPost={(body) => writes.add.mutateAsync(body)}
      />
    </section>
  );
}
