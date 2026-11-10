import { useState } from 'react';
import { Avatar } from '../../components/Avatar';
import { Chip } from '../../components/Chip';
import { showToast } from '../../components/toastStore';
import type { DecisionDto } from '@shiplean/core/api/asks';
import { DecisionChange } from './DecisionChange';
import { DecisionDiscuss } from './DecisionDiscuss';
import { DecisionHistory } from './DecisionHistory';
import { DecisionMenu } from './DecisionMenu';
import type { AsksRegionProps } from './regions';
import { asksTestIds as ids } from './testIds';
import { useAskWrites } from './useAskWrites';

const tinyButton =
  'inline-flex h-[22px] items-center rounded-control border px-[7px] text-[length:var(--text-xs)] font-medium text-ink hover:bg-signal-soft disabled:opacity-45';

type Editor = 'change' | 'discuss' | null;

function AnswerTag({ decision }: { decision: DecisionDto }) {
  const tag =
    decision.answerKind === 'recommended'
      ? 'Recommended'
      : decision.answerKind === 'own'
        ? 'Own answer'
        : 'Alternative';
  return (
    <span
      data-testid={ids.decisionTag}
      title={
        decision.answerKind === 'own'
          ? 'Written by a person, not one of the listed options (D12)'
          : undefined
      }
    >
      <Chip tone={decision.answerKind === 'recommended' ? 'accent' : 'muted'}>{tag}</Chip>
    </span>
  );
}

// An agent's write shows the agent's name (design: AI avatar), not the person whose token it used.
function SettledBy({ decision }: { decision: DecisionDto }) {
  const who = decision.answeredBy;
  const agent = decision.answeredAgentName;
  if (!who && !agent) return null;
  return (
    <>
      {agent ? <Avatar name={`Agent: ${agent}`} initials="AI" /> : who ? <Avatar {...who} /> : null}
      <span>
        {agent ?? who?.name} · {decision.answeredAtLabel}
      </span>
    </>
  );
}

function StateLine({ decision }: { decision: DecisionDto }) {
  const by = <SettledBy decision={decision} />;
  const withdrawnNote = [...decision.history].reverse().find((e) => e.kind === 'withdrawn')?.note;
  switch (decision.state) {
    case 'open':
      return (
        <>
          <span title="Agents carry on with the recommended answer. It stays open until someone confirms or changes it.">
            <Chip tone="warn">Going with recommended</Chip>
          </span>
          <span>
            raised by {decision.raisedAgentName ?? decision.raisedBy.name}
            {decision.raisedAgentName ? <Chip tone="purple">Agent</Chip> : null} ·{' '}
            {decision.raisedAtLabel}
          </span>
        </>
      );
    case 'confirmed':
      return (
        <>
          <Chip tone="ok">✓ Confirmed</Chip>
          {by}
          {decision.fromDescription ? (
            <span title="Recorded from a Markdown table by an agent, with the answer already given in the text">
              · from the description
            </span>
          ) : null}
        </>
      );
    case 'changed':
      return (
        <>
          <Chip tone="accent">Changed</Chip>
          {by}
        </>
      );
    case 'withdrawn':
      return (
        <>
          <Chip>Withdrawn</Chip>
          {decision.answeredAgentName || decision.answeredBy ? (
            <span>
              {decision.answeredAgentName
                ? `${decision.answeredAgentName} (agent)`
                : decision.answeredBy?.name}{' '}
              · {decision.answeredAtLabel}
              {withdrawnNote ? ` · “${withdrawnNote}”` : ''}
            </span>
          ) : null}
        </>
      );
  }
}

// One decision (D<n>): its question, the answer that stands, who settled it, and its actions.
export function DecisionRow({ item, asks, decision }: AsksRegionProps & { decision: DecisionDto }) {
  const writes = useAskWrites(item.projectId, item.key, item.id);
  const [editor, setEditor] = useState<Editor>(null);
  const [historyOpen, setHistoryOpen] = useState(false);
  const withdrawn = decision.state === 'withdrawn';
  const open = decision.state === 'open';
  const toggleEditor = (next: Exclude<Editor, null>) =>
    setEditor((was) => (was === next ? null : next));
  const failed = (error: Error) => showToast(error.message, { tone: 'danger' });
  const hasHistory = decision.history.length > 1 || decision.state === 'changed';
  return (
    <li
      id={decision.label}
      data-testid={ids.decisionRow}
      data-state={decision.state}
      className="grid grid-cols-[2.25rem_minmax(0,1fr)_auto] items-start gap-x-2 gap-y-1 border-b border-edge/60 py-2 last:border-b-0"
    >
      <span data-testid={ids.decisionLabel} className="font-semibold text-ink-muted">
        {decision.label}
      </span>
      <span className={withdrawn ? 'text-ink-muted line-through' : 'font-medium'}>
        {decision.question}
      </span>
      <span className="flex items-center gap-1.5">
        {withdrawn ? null : (
          <>
            {open ? (
              <button
                type="button"
                data-testid={ids.decisionConfirm}
                title={`Accept “${decision.recommended}”`}
                disabled={writes.confirm.isPending}
                className={`${tinyButton} border-edge bg-panel`}
                onClick={() =>
                  writes.confirm.mutate([decision.number], {
                    onSuccess: () =>
                      showToast(`${decision.label} confirmed: “${decision.currentAnswer}”`, {
                        tone: 'ok',
                      }),
                    onError: failed,
                  })
                }
              >
                ✓ Confirm
              </button>
            ) : null}
            <button
              type="button"
              data-testid={ids.decisionChangeButton}
              aria-expanded={editor === 'change'}
              className={`${tinyButton} ${open ? 'border-edge bg-panel' : 'border-transparent'}`}
              onClick={() => toggleEditor('change')}
            >
              Change
            </button>
            <button
              type="button"
              data-testid={ids.decisionDiscussButton}
              aria-expanded={editor === 'discuss'}
              className={`${tinyButton} border-transparent`}
              onClick={() => toggleEditor('discuss')}
            >
              Discuss
            </button>
            <DecisionMenu
              item={item}
              decision={decision}
              onWithdraw={() =>
                writes.withdrawDecision.mutate(decision.number, {
                  onSuccess: () => showToast(`${decision.label} withdrawn. It stays listed.`),
                  onError: failed,
                })
              }
            />
          </>
        )}
      </span>
      <span
        data-testid={ids.decisionAnswer}
        className={`col-start-2 col-span-2 flex flex-wrap items-center gap-1.5 ${withdrawn ? 'text-ink-muted line-through' : ''}`}
      >
        <span className="text-[length:var(--text-xs)] text-ink-muted no-underline">
          {withdrawn ? 'Was' : 'Answer'}
        </span>
        <span>{decision.currentAnswer}</span>
        <AnswerTag decision={decision} />
      </span>
      <span
        data-testid={ids.decisionState}
        className="col-start-2 col-span-2 flex flex-wrap items-center gap-1.5 text-[length:var(--text-xs)] text-ink-muted"
      >
        <StateLine decision={decision} />
        {hasHistory ? (
          <>
            <span>·</span>
            <button
              type="button"
              data-testid={ids.decisionHistoryToggle}
              aria-expanded={historyOpen}
              className="text-ink-muted hover:text-signal hover:underline"
              onClick={() => setHistoryOpen((was) => !was)}
            >
              {historyOpen ? 'Hide history' : `History (${decision.history.length})`}
            </button>
          </>
        ) : null}
      </span>
      {historyOpen ? <DecisionHistory decision={decision} /> : null}
      {editor === 'change' ? (
        <div className="col-span-full">
          <DecisionChange
            item={item}
            asks={asks}
            decision={decision}
            onClose={() => setEditor(null)}
          />
        </div>
      ) : null}
      {editor === 'discuss' ? (
        <div className="col-span-full">
          <DecisionDiscuss
            item={item}
            asks={asks}
            decision={decision}
            onClose={() => setEditor(null)}
          />
        </div>
      ) : null}
    </li>
  );
}
