import type { AskHistoryLineDto } from '@shiplean/core/api/asks';
import { Avatar } from '../../components/Avatar';
import type { AskHistoryLineProps } from './regions';
import { asksTestIds as ids } from './testIds';

// The design cuts a long answer to 58 characters plus an ellipsis once it passes 60.
const cut = (text: string) => (text.length > 60 ? `${text.slice(0, 58)}…` : text);

function wording(line: AskHistoryLineDto): {
  verb: string;
  before?: string;
  detail: string | null;
} {
  const to = line.to ? cut(line.to) : null;
  switch (line.kind) {
    case 'raised':
      return { verb: `raised ${line.badge}`, detail: null };
    case 'asked':
      return { verb: `asked ${line.badge}`, detail: null };
    case 'answered':
      return { verb: `answered ${line.badge}`, detail: to };
    case 'confirmed':
      return { verb: `confirmed ${line.badge}`, detail: to };
    case 'changed':
      return {
        verb: `changed ${line.badge}`,
        before: line.from && to ? `${cut(line.from)} → ` : undefined,
        detail: to,
      };
    case 'edited':
      return { verb: `edited ${line.badge}`, detail: null };
    case 'withdrawn':
      return { verb: `withdrew ${line.badge}`, detail: null };
  }
}

const badgeTone: Partial<Record<AskHistoryLineDto['kind'], string>> = {
  confirmed: 'border-ok/60',
  answered: 'border-ok/60',
  withdrawn: 'border-dashed border-accent/45 text-ink-muted',
};

// One ask event in the History rail: its D or Q badge, who (an agent first, when one acted), when, and the wording.
export function AskHistoryLine({ line }: AskHistoryLineProps) {
  const { verb, before, detail } = wording(line);
  return (
    <li
      data-testid={ids.historyLine}
      data-kind={line.kind}
      data-badge={line.badge}
      className="grid grid-cols-[22px_minmax(0,1fr)] gap-2 pb-3 last:pb-0"
    >
      <span
        title={line.badge}
        className={`-ml-0.5 grid h-5.5 w-6.5 place-items-center rounded-control border bg-surface font-mono text-[length:var(--text-2xs)] font-semibold ${badgeTone[line.kind] ?? 'border-accent/45'}`}
      >
        {line.badge}
      </span>
      <div className="grid grid-cols-[minmax(0,1fr)] gap-1">
        <div className="flex flex-wrap items-center gap-1.5 text-[length:var(--text-sm)]">
          {line.agentName ? (
            <Avatar name={`Agent: ${line.agentName}`} initials="AI" />
          ) : (
            <Avatar {...(line.by ?? {})} />
          )}
          <b className="font-medium [overflow-wrap:anywhere]">
            {line.agentName ?? line.by?.name ?? 'Unknown'}
          </b>
          <span className="text-[length:var(--text-xs)] text-ink-muted">{line.atLabel}</span>
        </div>
        <p className="m-0 text-[length:var(--text-sm)] [overflow-wrap:anywhere]">
          {verb}
          {detail ? (
            <>
              : {before}
              <b className="font-semibold">{detail}</b>
            </>
          ) : null}
        </p>
      </div>
    </li>
  );
}
