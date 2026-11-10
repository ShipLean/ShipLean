// A work item and its asks as an Inbox row (E17), so the work item and Features screens open the Inbox sheets without
// building Inbox shapes themselves. Pure.
import type { DecisionDto, ItemAsksDto, QuestionDto } from '@shiplean/core/api/asks';
import type { InboxAskDto, InboxItemDto } from '@shiplean/core/api/inbox';
import type { WorkItemDto } from '@shiplean/core/api/workItems';

const byNumber = <T extends { number: number }>(a: T, b: T) => a.number - b.number;

const questionAsk = (q: QuestionDto): InboxAskDto => ({
  kind: 'question',
  label: q.label,
  at: q.askedAt,
  atLabel: q.askedAtLabel,
  from: q.askedBy,
  agentName: q.agentName,
  decision: null,
  question: { number: q.number, text: q.text, options: q.options },
  mention: null,
  status: null,
});

const decisionAsk = (d: DecisionDto): InboxAskDto => ({
  kind: 'decision',
  label: d.label,
  at: d.raisedAt,
  atLabel: d.raisedAtLabel,
  from: d.raisedBy,
  agentName: d.raisedAgentName,
  decision: {
    number: d.number,
    question: d.question,
    recommended: d.recommended,
    alternatives: d.alternatives,
  },
  question: null,
  mention: null,
  status: null,
});

// A Feature's status line as an ask, or null when it has none (statusLine is "" on every other type). As on the server
// (K2): at is when it was last verified, or the Feature's creation when never; from is who verified it.
export function statusAsk(item: WorkItemDto): InboxAskDto | null {
  if (item.statusLine === '') return null;
  const verified = item.statusVerified;
  return {
    kind: 'status',
    label: 'Status',
    at: verified?.at ?? item.createdAt,
    atLabel: verified?.atLabel ?? item.createdAtLabel,
    from: verified?.by ?? null,
    agentName: verified?.agent ?? null,
    decision: null,
    question: null,
    mention: null,
    status: { statusLine: item.statusLine, verifiedAt: verified?.at ?? null },
  };
}

// The item's open questions (Qn) and decisions (Dn) by number, then its status ask. The screens that use this show no
// age or what waits on the item, so those are left empty.
export function toInboxItem(item: WorkItemDto, asks: ItemAsksDto | null): InboxItemDto {
  const questions = (asks?.questions ?? []).filter((q) => q.state === 'open').sort(byNumber);
  const decisions = (asks?.decisions ?? []).filter((d) => d.state === 'open').sort(byNumber);
  const status = statusAsk(item);
  const all = [
    ...questions.map(questionAsk),
    ...decisions.map(decisionAsk),
    ...(status === null ? [] : [status]),
  ];
  const oldest = all.reduce<string | null>(
    (min, a) => (min === null || Date.parse(a.at) < Date.parse(min) ? a.at : min),
    null,
  );
  return {
    id: item.id,
    key: item.key,
    type: item.type.id,
    title: item.title,
    revision: item.revision,
    underKey: null,
    oldestAt: oldest ?? item.createdAt,
    ageLabel: '',
    longWait: false,
    waitedOnBy: [],
    waitedOnByMore: 0,
    asks: all,
  };
}

// An empty status line as an ask, so a Feature that has none can get its first one from the answer sheet (web parity:
// the status band always offers Edit status). Not part of toInboxItem: the Inbox must not list a status ask on every
// line-less Feature. at is the Feature's creation and from is empty, as for a line that was never verified.
export function newStatusAsk(item: WorkItemDto): InboxAskDto {
  return {
    kind: 'status',
    label: 'Status',
    at: item.createdAt,
    atLabel: item.createdAtLabel,
    from: null,
    agentName: null,
    decision: null,
    question: null,
    mention: null,
    status: { statusLine: '', verifiedAt: null },
  };
}
