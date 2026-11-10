import { useState } from 'react';
import type { QuestionDto } from '@shiplean/core/api/asks';
import { Avatar } from '../../components/Avatar';
import { Chip } from '../../components/Chip';
import { Markdown } from '../../components/Markdown';
import { FieldMenu } from '../workItem/form/FieldMenu';
import { QuestionReply } from './QuestionReply';
import type { AsksItem } from './regions';
import { asksTestIds as ids } from './testIds';
import { useAskWrites } from './useAskWrites';

const stateChip = (question: QuestionDto) =>
  question.state === 'open' ? (
    <Chip tone="warn">Needs an answer</Chip>
  ) : question.state === 'answered' ? (
    <Chip tone="ok">✓ Answered</Chip>
  ) : (
    <Chip tone="muted">Withdrawn</Chip>
  );

// One question: its Q-label, text, state and who asked, the answer under it, and Reply and Withdraw while it is open.
export function QuestionRow({ item, question }: { item: AsksItem; question: QuestionDto }) {
  const [replying, setReplying] = useState(false);
  const writes = useAskWrites(item.projectId, item.key, item.id);
  const { answer } = question;
  const open = question.state === 'open';
  return (
    <li
      id={question.label}
      data-testid={ids.questionRow}
      className="grid scroll-mt-2 grid-cols-[30px_minmax(0,1fr)_auto] items-start gap-x-2 gap-y-0.5 border-t border-edge/60 p-2 first:border-t-0 max-[900px]:grid-cols-[30px_minmax(0,1fr)]"
    >
      <span
        data-testid={ids.questionLabel}
        className={`row-span-3 mt-px grid h-5 w-7 place-items-center rounded-sm border font-[family-name:var(--font-mono)] text-[length:var(--text-2xs)] leading-none font-semibold ${
          open
            ? 'border-[color-mix(in_srgb,var(--color-warn)_70%,var(--color-border))] bg-[color-mix(in_srgb,var(--color-warn)_14%,var(--color-surface))] text-ink'
            : 'border-edge bg-panel text-ink-muted'
        }`}
      >
        {question.label}
      </span>
      <span
        className={`min-w-0 text-[length:var(--text-sm)] leading-[1.4] break-words max-[900px]:text-[length:var(--text-md)] ${
          open ? 'font-semibold' : 'text-ink-muted'
        }`}
      >
        {question.text}
      </span>
      {open ? (
        <span className="col-start-3 row-span-2 row-start-1 flex items-center gap-1 max-[900px]:col-start-2 max-[900px]:row-auto max-[900px]:mt-1 max-[900px]:flex-wrap">
          <button
            type="button"
            data-testid={ids.questionReply}
            className={`h-[22px] rounded-control px-[7px] text-[length:var(--text-xs)] font-semibold max-[900px]:h-8 max-[900px]:px-3 ${
              replying ? 'border border-edge bg-panel text-ink' : 'bg-signal text-signal-ink'
            }`}
            onClick={() => setReplying(true)}
          >
            Reply
          </button>
          <FieldMenu
            buttonTestId={ids.questionMore}
            buttonLabel={`More for ${question.label}`}
            button="⋯"
          >
            {(close) => (
              <>
                <button
                  type="button"
                  role="menuitem"
                  data-testid={ids.questionWithdraw}
                  className="flex h-7 items-center rounded-sm px-2 text-left text-[length:var(--text-sm)] whitespace-nowrap hover:bg-signal-soft"
                  onClick={() => {
                    writes.withdrawQuestion.mutate(question.number);
                    close();
                  }}
                >
                  Withdraw {question.label}
                </button>
                <div className="mt-0.5 border-t border-edge px-2 pt-1.5 pb-0.5 text-[length:var(--text-xs)] text-ink-muted">
                  For a question that no longer matters. It stays listed.
                </div>
              </>
            )}
          </FieldMenu>
        </span>
      ) : null}
      <span className="col-start-2 flex flex-wrap items-center gap-x-1.5 gap-y-1 text-[length:var(--text-xs)] text-ink-muted">
        <span data-testid={ids.questionState}>{stateChip(question)}</span>
        <span>
          asked by{' '}
          {question.agentName ? (
            <>
              {question.agentName} <Chip tone="purple">Agent</Chip>
            </>
          ) : (
            question.askedBy.name
          )}{' '}
          · {question.askedAtLabel}
        </span>
      </span>
      {answer ? (
        <div
          data-testid={ids.questionAnswer}
          className="col-span-2 col-start-2 mt-0.5 grid grid-cols-[20px_minmax(0,1fr)] gap-1.5 rounded-r-sm border-l-2 border-l-state-completed bg-canvas px-2 py-1.5 text-[length:var(--text-sm)] max-[900px]:col-span-1"
        >
          {answer.by ? <Avatar {...answer.by} /> : <span />}
          <span className="min-w-0">
            <Markdown html={answer.bodyHtml} />
            <span className="block text-[length:var(--text-xs)] text-ink-muted">
              {answer.by?.name ?? answer.agentName} · {answer.atLabel} ·{' '}
              <a
                data-testid={ids.questionAnswerLink}
                href={`#comment-${answer.commentId}`}
                className="text-ink-muted hover:text-signal"
              >
                in Discussion
              </a>
            </span>
          </span>
        </div>
      ) : null}
      {writes.withdrawQuestion.error ? (
        <span
          data-testid={ids.questionWithdrawError}
          role="alert"
          className="col-span-2 col-start-2 text-[length:var(--text-xs)] text-alert max-[900px]:col-span-1"
        >
          {writes.withdrawQuestion.error.message}
        </span>
      ) : null}
      {replying ? (
        <QuestionReply question={question} writes={writes} onClose={() => setReplying(false)} />
      ) : null}
    </li>
  );
}
