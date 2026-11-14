import { useEffect, useRef, useState } from 'react';
import { textareaClass } from '../workItem/form/inputClasses';
import { taskChangesTestIds as ids } from './testIds';
import type { ComposerTarget } from './types';

export interface CodeCommentComposerProps {
  target: ComposerTarget;
  onPost(body: string): Promise<void>;
  onCancel(): void;
}

// The line comment box. Ctrl/Cmd+Enter posts, Esc cancels. It only reports the text: ChangesScreen adds the anchor and posts.
export function CodeCommentComposer({ target, onPost, onCancel }: CodeCommentComposerProps) {
  const [text, setText] = useState('');
  const [posting, setPosting] = useState(false);
  const input = useRef<HTMLTextAreaElement>(null);
  useEffect(() => input.current?.focus(), []);
  const body = text.trim();
  const file = target.path.split('/').pop() ?? target.path;

  const post = () => {
    if (body === '' || posting) return;
    setPosting(true);
    onPost(body).then(
      () => setPosting(false),
      () => setPosting(false),
    );
  };

  return (
    <div
      data-testid={ids.composer}
      className="grid w-[460px] max-w-full gap-1.5 rounded-control border border-signal bg-panel p-2"
    >
      <span className="text-[length:var(--text-xs)] text-ink-muted">
        Comment on {file} line {target.line} ({target.side}) · {target.sha.slice(0, 7)}
      </span>
      <textarea
        ref={input}
        data-testid={ids.composerBody}
        aria-label="Comment"
        className={`${textareaClass} min-h-16 font-[family-name:var(--font-sans)]`}
        value={text}
        onChange={(event) => setText(event.target.value)}
        onKeyDown={(event) => {
          if (event.key === 'Enter' && (event.ctrlKey || event.metaKey)) {
            event.preventDefault();
            post();
          } else if (event.key === 'Escape') {
            event.preventDefault();
            onCancel();
          }
        }}
      />
      <div className="flex items-center justify-end gap-2">
        <button
          type="button"
          data-testid={ids.composerCancel}
          className="h-7 rounded-control px-3 text-[length:var(--text-sm)] text-ink-muted"
          onClick={onCancel}
        >
          Cancel
        </button>
        <button
          type="button"
          data-testid={ids.composerPost}
          disabled={body === '' || posting}
          className="h-7 rounded-control bg-signal px-3 text-[length:var(--text-sm)] font-semibold text-signal-ink disabled:opacity-50"
          onClick={post}
        >
          Comment
        </button>
      </div>
    </div>
  );
}
