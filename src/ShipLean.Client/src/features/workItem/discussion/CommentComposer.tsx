import { useRef, useState } from 'react';
import type { UserRef } from '../../../api/refs';
import { textareaClass } from '../form/inputClasses';
import { workItemTestIds as ids } from '../testIds';
import { MentionMenu } from './MentionMenu';
import { applyMention, mentionAt, suggestMentions } from './mentions';

// The new-comment box. Typing @ suggests project members by the typed prefix; Ctrl/Cmd+Enter or Comment posts at once.
export function CommentComposer({
  people,
  posting,
  error,
  onPost,
}: {
  people: UserRef[];
  posting: boolean;
  error: string | null;
  onPost: (body: string) => Promise<unknown>;
}) {
  const [text, setText] = useState('');
  const [caret, setCaret] = useState(0);
  const [highlighted, setHighlighted] = useState(0);
  const [dismissedAt, setDismissedAt] = useState<number | null>(null);
  const input = useRef<HTMLTextAreaElement>(null);
  const mention = mentionAt(text, caret);
  const hits =
    mention && mention.start !== dismissedAt ? suggestMentions(people, mention.query) : [];
  const open = mention !== null && hits.length > 0;
  const body = text.trim();

  const post = () => {
    if (body === '' || posting) return;
    void onPost(body).then(
      () => setText(''),
      () => undefined,
    );
  };
  const pick = (person: UserRef) => {
    if (!mention) return;
    const next = applyMention(text, caret, mention, person);
    setText(next.text);
    setCaret(next.caret);
    setHighlighted(0);
    requestAnimationFrame(() => input.current?.setSelectionRange(next.caret, next.caret));
  };
  const track = (target: HTMLTextAreaElement) => {
    setCaret(target.selectionStart);
    setHighlighted(0);
  };

  return (
    <div className="grid gap-1.5">
      <div className="relative">
        <textarea
          ref={input}
          data-testid={ids.commentInput}
          aria-label="New comment"
          placeholder="Add a comment. Type @ to mention someone"
          className={`${textareaClass} min-h-14 font-[family-name:var(--font-sans)]`}
          value={text}
          onChange={(event) => {
            setText(event.target.value);
            track(event.target);
          }}
          onSelect={(event) => setCaret(event.currentTarget.selectionStart)}
          onKeyDown={(event) => {
            if (event.key === 'Enter' && (event.ctrlKey || event.metaKey)) {
              event.preventDefault();
              post();
            } else if (open && event.key === 'ArrowDown') {
              event.preventDefault();
              setHighlighted((highlighted + 1) % hits.length);
            } else if (open && event.key === 'ArrowUp') {
              event.preventDefault();
              setHighlighted((highlighted + hits.length - 1) % hits.length);
            } else if (open && (event.key === 'Enter' || event.key === 'Tab')) {
              event.preventDefault();
              const chosen = hits[highlighted];
              if (chosen) pick(chosen);
            } else if (open && event.key === 'Escape' && mention) {
              event.preventDefault();
              setDismissedAt(mention.start);
            }
          }}
        />
        {open ? <MentionMenu people={hits} highlighted={highlighted} onPick={pick} /> : null}
      </div>
      <div className="flex items-center gap-2">
        <span className="text-[length:var(--text-xs)] text-ink-muted">
          Markdown and @mentions · Ctrl+Enter posts · comments post straight away, not with Save
        </span>
        <button
          type="button"
          data-testid={ids.commentPost}
          disabled={body === '' || posting}
          className="ml-auto h-7 rounded-control bg-signal px-3 text-[length:var(--text-sm)] font-semibold text-signal-ink disabled:opacity-50"
          onClick={post}
        >
          Comment
        </button>
      </div>
      {error ? (
        <span
          data-testid={ids.commentError}
          role="alert"
          className="text-[length:var(--text-xs)] text-alert"
        >
          {error}
        </span>
      ) : null}
    </div>
  );
}
