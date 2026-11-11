// What an Inbox row says, as pure functions (K7b): which asks it opens with, its kind label, the line that opens the
// other asks, the buttons an ask gets, and what an answer in flight shows in place of those buttons.
import type { InboxAskDto, InboxAskKind, InboxItemDto } from '@shiplean/core/api/inbox';
import type { UserRef } from '@shiplean/core/api/refs';
import { agentDisplayName } from '../ui/agentName';
import type { IconName } from '../ui/Icon';
import type { PendingAnswer } from './actions';

// The asks a row opens with: one ask, or every decision when it opens with a decision (a batch, never one tap).
// rest is every other ask, opened in place.
export function splitAsks(item: InboxItemDto): { first: InboxAskDto[]; rest: InboxAskDto[] } {
  const head = item.asks[0];
  if (head === undefined) return { first: [], rest: [] };
  if (head.kind !== 'decision') return { first: [head], rest: item.asks.slice(1) };
  return {
    first: item.asks.filter((a) => a.kind === 'decision'),
    rest: item.asks.filter((a) => a.kind !== 'decision'),
  };
}

const dNumber = (label: string): number | null => {
  const match = /^D(\d+)$/.exec(label);
  return match === null ? null : Number(match[1]);
};

// "D1–D8" for a run of decisions, "D1, D3–D5" with gaps; other labels ("Q2", "@1") stay as they are, after the Ds.
export function compressLabels(labels: string[]): string {
  const numbers = labels
    .map(dNumber)
    .filter((n): n is number => n !== null)
    .sort((a, b) => a - b);
  const runs: string[] = [];
  for (let i = 0; i < numbers.length;) {
    let end = i;
    while (numbers[end + 1] === numbers[end]! + 1) end += 1;
    runs.push(end === i ? `D${numbers[i]}` : `D${numbers[i]}–D${numbers[end]}`);
    i = end + 1;
  }
  return [...runs, ...labels.filter((l) => dNumber(l) === null)].join(', ');
}

const KIND_TEXT: Record<Exclude<InboxAskKind, 'decision'>, string> = {
  question: 'Needs input',
  mention: 'Mentioned you',
  status: 'Status line to check',
};

// Runs of one kind, in order.
function groups(asks: InboxAskDto[]): InboxAskDto[][] {
  const out: InboxAskDto[][] = [];
  for (const ask of asks) {
    const last = out[out.length - 1];
    if (last !== undefined && last[0]!.kind === ask.kind) last.push(ask);
    else out.push([ask]);
  }
  return out;
}

const kindOf = (group: InboxAskDto[]): { label: string; detail: string | null } => {
  const kind = group[0]!.kind;
  if (kind === 'status') return { label: KIND_TEXT.status, detail: 'you own this' };
  if (kind !== 'decision') return { label: KIND_TEXT[kind], detail: null };
  const range = compressLabels(group.map((a) => a.label));
  return { label: group.length === 1 ? 'Decision' : `${group.length} decisions`, detail: range };
};

// "Decision", "8 decisions", "Needs input", "Mentioned you", "Status line to check". A mix of kinds joins one label
// per kind: "Needs input + 2 decisions". The decision numbers are in kindDetail, not here.
export function kindLabel(asks: InboxAskDto[]): string {
  return groups(asks)
    .map((g) => kindOf(g).label)
    .join(' + ');
}

// The muted text after the label: "D10", "D1–D8", "you own this" on a status line. Null when there is none.
export function kindDetail(asks: InboxAskDto[]): string | null {
  const details = groups(asks)
    .map((g) => kindOf(g).detail)
    .filter((d): d is string => d !== null);
  return details.length === 0 ? null : details.join(', ');
}

const KIND_ICON: Record<InboxAskKind, IconName> = {
  decision: 'check',
  question: 'message',
  mention: 'user',
  status: 'clock',
};

// The icon before the label: the first kind's.
export function kindIcon(asks: InboxAskDto[]): IconName {
  return KIND_ICON[asks[0]!.kind];
}

const NOUN: Record<InboxAskKind, [string, string]> = {
  decision: ['decision', 'decisions'],
  question: ['question', 'questions'],
  mention: ['mention', 'mentions'],
  status: ['status line', 'status line'],
};

// "2 decisions too · D10, D11". Null when there are no other asks.
export function moreLine(rest: InboxAskDto[]): string | null {
  if (rest.length === 0) return null;
  const nouns = groups(rest).map((g) => {
    const [one, many] = NOUN[g[0]!.kind];
    const text = g.length === 1 ? one : many;
    return g[0]!.kind === 'status' ? text : `${g.length} ${text}`;
  });
  // Listed one by one, as the design does ("D10, D11"): these are the asks still to open.
  const labels = rest
    .map((a) => a.label)
    .filter((l) => l !== 'Status')
    .join(', ');
  const head = nouns.join(', ');
  const line = `${head.charAt(0).toUpperCase()}${head.slice(1)} too`;
  return labels === '' ? line : `${line} · ${labels}`;
}

export type ButtonIntent =
  | { kind: 'approve'; number: number }
  | { kind: 'review'; scrollToConfirm: boolean }
  | { kind: 'option'; number: number; optionIndex: number; option: string }
  | { kind: 'seen'; mentionId: string }
  | { kind: 'stillTrue' }
  | { kind: 'sheet'; mode: 'question' | 'discuss' | 'mention' | 'status' };

export interface AskButton {
  // The last part of the test id: approve, discuss, review, approve-all, option-0, reply, seen, still-true, new-status.
  id: string;
  label: string;
  primary: boolean;
  icon?: IconName;
  intent: ButtonIntent;
}

// The buttons for the asks of one block: one ask, or a batch of decisions (several, in the order given).
export function buttonsFor(asks: InboxAskDto[]): AskButton[] {
  const ask = asks[0];
  if (ask === undefined) return [];
  if (ask.kind === 'decision') {
    if (asks.length > 1) {
      return [
        {
          id: 'review',
          label: `Review all ${asks.length}`,
          primary: false,
          intent: { kind: 'review', scrollToConfirm: false },
        },
        {
          id: 'approve-all',
          label: `Approve all ${asks.length}`,
          primary: true,
          icon: 'check',
          intent: { kind: 'review', scrollToConfirm: true },
        },
      ];
    }
    return [
      {
        id: 'approve',
        label: 'Approve',
        primary: true,
        icon: 'check',
        intent: { kind: 'approve', number: ask.decision!.number },
      },
      {
        id: 'discuss',
        label: 'Discuss',
        primary: false,
        intent: { kind: 'sheet', mode: 'discuss' },
      },
    ];
  }
  if (ask.kind === 'question') {
    const question = ask.question!;
    return [
      ...question.options.map((option, optionIndex): AskButton => ({
        id: `option-${optionIndex}`,
        label: option,
        primary: false,
        intent: { kind: 'option', number: question.number, optionIndex, option },
      })),
      {
        id: 'reply',
        label: 'Reply',
        primary: false,
        icon: 'mic',
        intent: { kind: 'sheet', mode: 'question' },
      },
    ];
  }
  if (ask.kind === 'mention') {
    return [
      {
        id: 'reply',
        label: 'Reply',
        primary: false,
        icon: 'mic',
        intent: { kind: 'sheet', mode: 'mention' },
      },
      {
        id: 'seen',
        label: 'Seen',
        primary: false,
        icon: 'check',
        intent: { kind: 'seen', mentionId: ask.mention!.mentionId },
      },
    ];
  }
  return [
    {
      id: 'still-true',
      label: 'Still true',
      primary: false,
      icon: 'check',
      intent: { kind: 'stillTrue' },
    },
    {
      id: 'new-status',
      label: 'New status',
      primary: false,
      icon: 'mic',
      intent: { kind: 'sheet', mode: 'status' },
    },
  ];
}

// The runner's latest answer to any of these labels that is still in play (an undone one is gone).
export function pendingFor(labels: string[], pending: PendingAnswer[]): PendingAnswer | null {
  const mine = pending.filter(
    (p) => p.undoOutcome !== 'undone' && p.labels.some((l) => labels.includes(l)),
  );
  return mine[mine.length - 1] ?? null;
}

const VERB: Record<PendingAnswer['action']['kind'], string> = {
  approve: 'approved',
  chooseDecision: 'decided',
  review: 'decided',
  chooseOption: 'answered',
  answer: 'answered',
  discuss: 'discussed',
  mentionReply: 'replied',
  seen: 'seen',
  stillTrue: 'still true',
  newStatus: 'updated',
};

// What shows in place of an ask's buttons while its answer is in the runner. A failed answer gets its buttons back,
// and this line says where to look.
export function pendingLine(p: PendingAnswer): string {
  switch (p.state) {
    case 'undo':
    case 'sent':
      return `${compressLabels(p.labels)} ${VERB[p.action.kind]} · just now`;
    case 'queued':
      return "Sends when you're online";
    case 'sending':
    case 'uncertain':
      return 'Sending…';
    case 'failed':
      return "Didn't send · see Answered";
  }
}

// "SHIP-906 waits on this", "SHIP-906 and SHIP-907 wait on this", "SHIP-1, SHIP-2 and 2 more wait on this". Null when
// nothing waits on the item.
export function waitsLine(item: InboxItemDto): string | null {
  const names = item.waitedOnBy.map((r) => r.key);
  if (item.waitedOnByMore > 0) names.push(`${item.waitedOnByMore} more`);
  if (names.length === 0) return null;
  const last = names[names.length - 1]!;
  const list = names.length === 1 ? last : `${names.slice(0, -1).join(', ')} and ${last}`;
  return `${list} ${names.length === 1 ? 'waits' : 'wait'} on this`;
}

// Where an ask came from: the agent's name (without the host) or the person's. A status line shows who last checked it,
// and when.
export function fromLine(ask: InboxAskDto): string {
  if (ask.kind === 'status') {
    return ask.from === null ? 'Never checked' : `Checked by ${ask.from.name} · ${ask.atLabel}`;
  }
  return ask.agentName === null ? (ask.from?.name ?? '') : agentDisplayName(ask.agentName);
}

// The avatar before the from line: AI for an agent, the person's initials otherwise. Null when nobody is named.
export function fromAvatar(ask: InboxAskDto): { user: UserRef; agent: boolean } | null {
  if (ask.from === null) return null;
  return { user: ask.from, agent: ask.kind !== 'status' && ask.agentName !== null };
}
