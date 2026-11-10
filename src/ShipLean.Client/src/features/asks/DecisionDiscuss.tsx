import { useState } from 'react';
import { showToast } from '../../components/toastStore';
import { textareaClass } from '../workItem/form/inputClasses';
import { discussQuote } from './discussQuote';
import type { DecisionEditorProps } from './regions';
import { asksTestIds as ids } from './testIds';
import { useAskWrites } from './useAskWrites';

// The inline comment box under a decision. Posting quotes the decision in Discussion and leaves it as it is; a refused
// post shows the message and keeps the text.
export function DecisionDiscuss({ item, decision, onClose }: DecisionEditorProps) {
  const { discuss } = useAskWrites(item.projectId, item.key, item.id);
  const [text, setText] = useState('');
  const body = text.trim();
  const post = () => {
    if (body === '' || discuss.isPending) return;
    discuss.mutate(
      { number: decision.number, body: discussQuote(decision, body) },
      {
        onSuccess: () => {
          showToast(`Comment posted · ${decision.label} stays ${decision.state}`);
          onClose();
        },
      },
    );
  };
  return (
    <div
      data-testid={ids.decisionDiscuss}
      className="mt-1.5 grid gap-1.5 rounded-panel border border-signal bg-canvas px-3 py-2"
    >
      <span className="text-[length:var(--text-xs)] font-medium text-ink-muted">
        Discuss {decision.label}
      </span>
      <blockquote className="m-0 border-l-2 border-edge pl-2 text-[length:var(--text-sm)] text-ink-muted">
        <b>{decision.label}</b> · {decision.question}: {decision.currentAnswer}
      </blockquote>
      <textarea
        data-testid={ids.discussInput}
        aria-label={`Discuss ${decision.label}`}
        rows={3}
        placeholder="Your comment. Markdown and @mentions"
        className={`${textareaClass} min-h-[52px] font-[family-name:var(--font-sans)]`}
        value={text}
        onChange={(event) => setText(event.target.value)}
        onKeyDown={(event) => {
          if (event.key === 'Enter' && (event.ctrlKey || event.metaKey)) {
            event.preventDefault();
            post();
          }
        }}
      />
      <div className="flex flex-wrap items-center gap-2">
        <button
          type="button"
          data-testid={ids.discussPost}
          disabled={body === '' || discuss.isPending}
          className="h-7 rounded-control bg-signal px-3 text-[length:var(--text-sm)] font-semibold text-signal-ink disabled:opacity-50"
          onClick={post}
        >
          Comment
        </button>
        <button
          type="button"
          data-testid={ids.discussCancel}
          className="h-7 rounded-control px-3 text-[length:var(--text-sm)] text-ink hover:bg-signal-soft"
          onClick={() => {
            discuss.reset();
            onClose();
          }}
        >
          Cancel
        </button>
        <span className="ml-auto text-[length:var(--text-xs)] text-ink-muted">
          Posts to Discussion with {decision.label} quoted. {decision.label} stays {decision.state}.
        </span>
      </div>
      {discuss.error ? (
        <span
          data-testid={ids.discussError}
          role="alert"
          className="text-[length:var(--text-xs)] text-alert"
        >
          {discuss.error.message}
        </span>
      ) : null}
    </div>
  );
}
