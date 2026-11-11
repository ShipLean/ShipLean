import { useRef, useState } from 'react';
import type { QuestionDto } from '@shiplean/core/api/asks';
import { textareaClass } from '../workItem/form/inputClasses';
import { QuickAnswers } from './QuickAnswers';
import { asksTestIds as ids } from './testIds';
import type { AskWrites } from './useAskWrites';

// The inline answer box under a question. Posting closes it; a refused post shows the message and keeps the text.
export function QuestionReply({
  question,
  writes,
  onClose,
}: {
  question: QuestionDto;
  writes: AskWrites;
  onClose(): void;
}) {
  const [text, setText] = useState('');
  const input = useRef<HTMLTextAreaElement>(null);
  const { answer } = writes;
  const body = text.trim();
  const post = () => {
    if (body === '' || answer.isPending) return;
    answer.mutate({ number: question.number, body }, { onSuccess: onClose });
  };
  return (
    <div className="col-span-2 col-start-2 mt-1.5 grid gap-1.5 rounded-panel border border-signal bg-canvas px-3 py-2 max-[900px]:col-span-1">
      <span className="text-[length:var(--text-xs)] font-medium text-ink-muted">
        Answer {question.label}
      </span>
      <QuickAnswers
        label={question.label}
        options={question.options}
        onChoose={(option) => {
          setText(option);
          input.current?.focus();
        }}
      />
      <textarea
        ref={input}
        data-testid={ids.questionReplyInput}
        aria-label={`Answer ${question.label}`}
        rows={3}
        placeholder="Your answer. Markdown and @mentions"
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
          data-testid={ids.questionReplyPost}
          disabled={body === '' || answer.isPending}
          className="h-7 rounded-control bg-signal px-3 text-[length:var(--text-sm)] font-semibold text-signal-ink disabled:opacity-50"
          onClick={post}
        >
          Post answer
        </button>
        <button
          type="button"
          data-testid={ids.questionReplyCancel}
          className="h-7 rounded-control px-3 text-[length:var(--text-sm)] text-ink hover:bg-signal-soft"
          onClick={() => {
            answer.reset();
            onClose();
          }}
        >
          Cancel
        </button>
        <span className="ml-auto text-[length:var(--text-xs)] text-ink-muted">
          Posts to Discussion as the answer to {question.label} and closes it. The next agent reads
          it in its brief.
        </span>
      </div>
      {answer.error ? (
        <span
          data-testid={ids.questionReplyError}
          role="alert"
          className="text-[length:var(--text-xs)] text-alert"
        >
          {answer.error.message}
        </span>
      ) : null}
    </div>
  );
}
