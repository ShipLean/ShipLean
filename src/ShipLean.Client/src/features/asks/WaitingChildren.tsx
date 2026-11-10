import { TypeIcon } from '../../components/TypeIcon';
import { Avatar } from '../../components/Avatar';
import { Chip } from '../../components/Chip';
import { useMe } from '../shell/useShellQueries';
import { ItemLink } from '../itemPeek/ItemLink';
import type { AsksRegionProps } from './regions';
import { asksTestIds as ids } from './testIds';

const plural = (n: number, one: string) => `${n} ${n === 1 ? one : `${one}s`}`;

// A Feature's roll-up: one row for each Story or Bug below it with open asks. They are answered on each item.
export function WaitingChildren({ item, asks }: AsksRegionProps) {
  const me = useMe().data;
  const children = asks.waitingChildren;
  if (children.length === 0) return null;
  return (
    <div className="grid gap-1">
      <div className="flex items-baseline gap-2 text-[length:var(--text-xs)] text-ink-muted">
        <span className="font-semibold tracking-wide uppercase">Stories and bugs waiting</span>
        <span>{children.length}</span>
        <span>· their asks are answered on each item</span>
      </div>
      <ul className="m-0 grid list-none p-0">
        {children.map((child) => (
          <li
            key={child.id}
            data-testid={ids.waitingChildRow}
            className="flex min-w-0 flex-wrap items-center gap-2 border-t border-edge/60 py-1.5 first:border-t-0"
          >
            <TypeIcon type={child.type} />
            <ItemLink
              projectKey={item.projectKey}
              itemKey={child.key}
              className="font-mono text-[length:var(--text-xs)] text-ink-muted"
            >
              {child.key}
            </ItemLink>
            <span className="min-w-0 flex-1 truncate text-[length:var(--text-sm)]">
              {child.title}
            </span>
            <span
              title={
                child.ownerIsCreator
                  ? 'Nobody is assigned, so it waits on the creator'
                  : 'The assignee'
              }
              className="inline-flex items-center gap-1 text-[length:var(--text-xs)] text-ink-muted"
            >
              {child.owner ? <Avatar {...child.owner} /> : null}
              Waiting on{' '}
              {child.owner && child.owner.id === me?.id ? 'you' : (child.owner?.name ?? 'nobody')}
              {child.ownerIsCreator ? ' (creator)' : ''}
            </span>
            <span className="inline-flex gap-1">
              {child.openQuestions > 0 ? (
                <Chip tone="warn">{plural(child.openQuestions, 'question')}</Chip>
              ) : null}
              {child.openDecisions > 0 ? (
                <Chip>{plural(child.openDecisions, 'decision')} open</Chip>
              ) : null}
            </span>
          </li>
        ))}
      </ul>
    </div>
  );
}
