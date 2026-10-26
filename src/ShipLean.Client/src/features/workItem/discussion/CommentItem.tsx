import { useState } from 'react';
import type { CommentDto } from '../../../api/comments';
import { Avatar } from '../../../components/Avatar';
import { Markdown } from '../../../components/Markdown';
import { textareaClass } from '../form/inputClasses';
import { workItemTestIds as ids } from '../testIds';

const iconButton =
  'h-6 rounded-control px-2 text-[length:var(--text-xs)] text-ink-muted hover:bg-signal-soft focus-visible:outline-2 focus-visible:outline-signal';

// One comment: who, when (labels come from the server), the server-rendered markdown, and for the author edit, for the
// author or a project admin archive. Any member can mark it superseded from the ⋯ menu: it stays, struck through. In the
// Feature's Log an agent's entry reads "Agent · via MCP" instead of naming the agent.
export function CommentItem({
  comment,
  canEdit,
  canArchive,
  saving,
  error,
  viaMcp,
  onSave,
  onArchive,
  onSupersede,
}: {
  comment: CommentDto;
  canEdit: boolean;
  canArchive: boolean;
  saving: boolean;
  error: string | null;
  onSave: (body: string) => Promise<unknown>;
  viaMcp: boolean;
  onArchive: () => void;
  onSupersede: () => void;
}) {
  const [editing, setEditing] = useState(false);
  const [menuOpen, setMenuOpen] = useState(false);
  const [draft, setDraft] = useState(comment.body);
  const [saveError, setSaveError] = useState<string | null>(null);
  const body = draft.trim();
  const author = comment.author;
  return (
    <li
      data-testid={ids.comment}
      data-comment-id={comment.id}
      className="grid grid-cols-[20px_minmax(0,1fr)] gap-2 border-t border-edge/60 py-2 first:border-t-0 first:pt-0"
    >
      <Avatar {...(author ?? {})} />
      <div className="grid gap-1">
        <div className="flex items-baseline gap-2 text-[length:var(--text-sm)]">
          <b data-testid={ids.commentAuthor} className="font-semibold">
            {author?.name ?? 'Unknown author'}
          </b>
          <span
            data-testid={ids.commentWhen}
            className="text-[length:var(--text-xs)] text-ink-muted"
          >
            {comment.createdLabel}
          </span>
          {comment.editedLabel ? (
            <span
              data-testid={ids.commentEdited}
              className="text-[length:var(--text-xs)] text-ink-muted"
            >
              edited {comment.editedLabel}
            </span>
          ) : null}
          {comment.agentName && viaMcp ? (
            <span
              data-testid={ids.commentVia}
              className="text-[length:var(--text-xs)] text-ink-muted"
            >
              Agent · via MCP
            </span>
          ) : null}
          {comment.agentName && !viaMcp ? (
            <span
              data-testid={ids.commentAgent}
              className="text-[length:var(--text-xs)] text-ink-muted"
            >
              via {comment.agentName}
            </span>
          ) : null}
          {comment.superseded ? (
            <span
              data-testid={ids.commentSuperseded}
              title={`Kept for the record. Marked by ${comment.superseded.by.name}, ${comment.superseded.atLabel}`}
              className="rounded-full bg-surface-2 px-[7px] text-[length:var(--text-xs)] font-medium"
            >
              Superseded
            </span>
          ) : null}
          <span className="relative ml-auto flex">
            {canEdit && !editing ? (
              <button
                type="button"
                data-testid={ids.commentEdit}
                className={iconButton}
                onClick={() => {
                  setDraft(comment.body);
                  setSaveError(null);
                  setEditing(true);
                }}
              >
                Edit
              </button>
            ) : null}
            {comment.superseded ? null : (
              <button
                type="button"
                data-testid={ids.commentMenu}
                aria-label="Entry options"
                aria-expanded={menuOpen}
                className={iconButton}
                onClick={() => setMenuOpen((open) => !open)}
              >
                ⋯
              </button>
            )}
            {menuOpen ? (
              <div className="absolute top-6 right-0 z-10 rounded-control border border-edge bg-canvas p-1 shadow-panel">
                <button
                  type="button"
                  data-testid={ids.commentSupersede}
                  className={`${iconButton} whitespace-nowrap`}
                  onClick={() => {
                    setMenuOpen(false);
                    onSupersede();
                  }}
                >
                  Mark superseded
                </button>
              </div>
            ) : null}
            {canArchive ? (
              <button
                type="button"
                data-testid={ids.commentArchive}
                className={iconButton}
                onClick={onArchive}
              >
                Archive
              </button>
            ) : null}
          </span>
        </div>
        {editing ? (
          <div className="grid gap-1.5">
            <textarea
              data-testid={ids.commentEditInput}
              aria-label="Edit comment"
              className={`${textareaClass} min-h-14 font-[family-name:var(--font-sans)]`}
              value={draft}
              onChange={(event) => setDraft(event.target.value)}
            />
            <div className="flex gap-2">
              <button
                type="button"
                data-testid={ids.commentEditSave}
                disabled={body === '' || body === comment.body || saving}
                className="h-7 rounded-control bg-signal px-3 text-[length:var(--text-sm)] font-semibold text-signal-ink disabled:opacity-50"
                onClick={() =>
                  void onSave(body).then(
                    () => setEditing(false),
                    (failure: unknown) =>
                      setSaveError(
                        failure instanceof Error
                          ? failure.message
                          : 'The comment could not be saved.',
                      ),
                  )
                }
              >
                Save
              </button>
              <button
                type="button"
                data-testid={ids.commentEditCancel}
                className={iconButton}
                onClick={() => {
                  setSaveError(null);
                  setEditing(false);
                }}
              >
                Cancel
              </button>
            </div>
          </div>
        ) : (
          <div
            data-testid={ids.commentBody}
            className={comment.superseded ? 'text-ink-muted line-through' : undefined}
          >
            <Markdown html={comment.bodyHtml} />
          </div>
        )}
        {(error ?? saveError) ? (
          <span
            data-testid={ids.commentError}
            role="alert"
            className="text-[length:var(--text-xs)] text-alert"
          >
            {error ?? saveError}
          </span>
        ) : null}
      </div>
    </li>
  );
}
