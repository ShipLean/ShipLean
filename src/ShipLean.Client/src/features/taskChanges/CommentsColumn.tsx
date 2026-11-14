import { useState } from 'react';
import type { CodeCommentDto } from '@shiplean/core/api/taskChanges';
import { Markdown } from '../../components/Markdown';
import { textareaClass } from '../workItem/form/inputClasses';
import { taskChangesTestIds as ids } from './testIds';
import type { ChangesData } from './types';

export interface CommentsColumnProps {
  data: ChangesData;
  onReply(commentId: string, body: string): Promise<void>;
  onResolve(commentId: string, resolved: boolean): Promise<void>;
}

const pill = 'rounded-full px-1.5 text-[length:var(--text-2xs)]';

function Message({ comment }: { comment: CodeCommentDto }) {
  return (
    <div className="grid gap-0.5" data-testid={ids.commentBody}>
      <div className="flex items-baseline gap-1.5 text-[length:var(--text-xs)]">
        <b className="text-ink">{comment.by}</b>
        {comment.byAgent ? <span className="text-ink-muted">(agent)</span> : null}
        <span className="text-ink-muted">{comment.createdAtLabel}</span>
      </div>
      <Markdown html={comment.bodyHtml} />
    </div>
  );
}

function Thread({
  thread,
  onReply,
  onResolve,
}: { thread: CodeCommentDto } & Omit<CommentsColumnProps, 'data'>) {
  const [replying, setReplying] = useState(false);
  const [text, setText] = useState('');
  const [busy, setBusy] = useState(false);
  const file = (thread.path ?? '').split('/').pop();
  const body = text.trim();

  const send = () => {
    if (body === '' || busy) return;
    setBusy(true);
    onReply(thread.id, body).then(
      () => {
        setBusy(false);
        setText('');
        setReplying(false);
      },
      () => setBusy(false),
    );
  };

  return (
    <div
      data-testid={ids.thread}
      className="grid gap-1.5 rounded-control border border-edge bg-surface px-3 py-2 text-[length:var(--text-sm)]"
    >
      <div className="flex min-w-0 items-center gap-1.5 font-[family-name:var(--font-mono)] text-[length:var(--text-2xs)] text-ink-muted">
        <span className="truncate">
          {thread.repo ? `${thread.repo} · ` : ''}
          {file}:{thread.line} · {thread.side}
        </span>
        <span className="ml-auto" />
        {thread.sentBack ? (
          <span data-testid={ids.commentSentBack} className={`${pill} bg-sunken`}>
            Sent back
          </span>
        ) : null}
        {thread.outdated ? (
          <span className={`${pill} bg-sunken text-caution`}>Outdated</span>
        ) : null}
        {thread.resolved ? <span className={`${pill} bg-sunken text-good`}>Resolved</span> : null}
      </div>
      {thread.lineText ? (
        <pre
          data-testid={thread.outdated ? ids.commentOldLine : undefined}
          className={`m-0 overflow-hidden text-ellipsis rounded-[var(--radius-xs)] bg-sunken px-1.5 font-[family-name:var(--font-mono)] text-[length:var(--text-2xs)] leading-relaxed ${thread.outdated ? 'line-through' : ''}`}
        >
          {thread.lineText.trim()}
        </pre>
      ) : null}
      <Message comment={thread} />
      {thread.replies.map((reply) => (
        <Message key={reply.id} comment={reply} />
      ))}
      {thread.outdated ? (
        <span className="text-[length:var(--text-xs)] text-ink-muted">
          The line changed. Resolve it, or reply to keep it open.
        </span>
      ) : null}
      {replying ? (
        <div className="grid gap-1.5">
          <textarea
            data-testid={ids.replyBody}
            aria-label="Reply"
            className={`${textareaClass} min-h-12 font-[family-name:var(--font-sans)]`}
            value={text}
            onChange={(event) => setText(event.target.value)}
            onKeyDown={(event) => {
              if (event.key === 'Enter' && (event.ctrlKey || event.metaKey)) {
                event.preventDefault();
                send();
              } else if (event.key === 'Escape') {
                event.preventDefault();
                setReplying(false);
              }
            }}
          />
          <button
            type="button"
            data-testid={ids.replySend}
            disabled={body === '' || busy}
            className="h-7 justify-self-end rounded-control bg-signal px-3 text-[length:var(--text-sm)] font-semibold text-signal-ink disabled:opacity-50"
            onClick={send}
          >
            Reply
          </button>
        </div>
      ) : null}
      <div className="flex items-center gap-2">
        {replying ? null : (
          <button
            type="button"
            data-testid={ids.replyOpen}
            className="h-6 rounded-control border border-edge px-2 text-[length:var(--text-xs)]"
            onClick={() => setReplying(true)}
          >
            Reply
          </button>
        )}
        <button
          type="button"
          data-testid={ids.resolve}
          className="h-6 rounded-control border border-edge px-2 text-[length:var(--text-xs)]"
          onClick={() => void onResolve(thread.id, !thread.resolved).catch(() => undefined)}
        >
          {thread.resolved ? 'Reopen' : 'Resolve'}
        </button>
      </div>
    </div>
  );
}

// The right-hand rail: the task's code comment threads, Open (unresolved) or All, with reply and resolve.
export function CommentsColumn({ data, onReply, onResolve }: CommentsColumnProps) {
  const [tab, setTab] = useState<'open' | 'all'>('open');
  const open = data.comments.filter((c) => !c.resolved);
  const list = tab === 'open' ? open : data.comments;
  const tabClass = (on: boolean) =>
    `h-7 rounded-control px-2 text-[length:var(--text-sm)] ${on ? 'bg-signal-soft text-ink' : 'text-ink-muted'}`;
  return (
    <aside
      data-testid={ids.comments}
      aria-label="Comments"
      className="flex flex-col gap-2 overflow-auto border-l border-edge bg-canvas p-3"
    >
      <span className="text-[length:var(--text-xs)] font-semibold uppercase tracking-wide text-ink-muted">
        Comments
      </span>
      <div role="tablist" className="flex gap-1">
        <button
          type="button"
          role="tab"
          aria-selected={tab === 'open'}
          data-testid={ids.commentsTabOpen}
          className={tabClass(tab === 'open')}
          onClick={() => setTab('open')}
        >
          Open ({open.length})
        </button>
        <button
          type="button"
          role="tab"
          aria-selected={tab === 'all'}
          data-testid={ids.commentsTabAll}
          className={tabClass(tab === 'all')}
          onClick={() => setTab('all')}
        >
          All ({data.comments.length})
        </button>
      </div>
      {list.map((thread) => (
        <Thread key={thread.id} thread={thread} onReply={onReply} onResolve={onResolve} />
      ))}
      <p className="text-[length:var(--text-xs)] text-ink-muted">
        Comments stay in ShipLean, on {data.itemKey}. <b>Request changes</b> sends the open ones
        back with the task, so the next agent to pick it up gets them in its brief.
      </p>
    </aside>
  );
}
