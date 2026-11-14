import { useState } from 'react';
import type { MemberDto } from '@shiplean/core/api/projects';
import { Avatar } from '../../../../components/Avatar';
import { useMe } from '../../../shell/useShellQueries';
import { useProjectMembers } from '../../../workItem/workItemApi';
import { sprintsTestIds as ids } from '../../testIds';
import { InProgressSheet } from './InProgressSheet';
import type { RailSummary } from './railSummary';
import { ReadySheet } from './ReadySheet';

const ROW =
  'grid min-h-12 w-full grid-cols-[minmax(0,1fr)_auto_14px] items-center gap-2.5 border-0 border-b border-edge bg-transparent px-3 py-1.5 text-left last:border-b-0';

/** The rail at 900px and below (SHIP-724 D1): two rows above the list, Ready to pick up and In progress now, each opening a sheet. */
export function GlanceRows({ projectId, summary }: { projectId: string; summary: RailSummary }) {
  const members = useProjectMembers(projectId).data ?? [];
  const meId = useMe().data?.id;
  const [open, setOpen] = useState<'ready' | 'active' | null>(null);
  const close = () => setOpen(null);
  const { ready, activeByUser } = summary;
  const working = members.filter(({ user }) => (activeByUser.get(user.id) ?? []).length > 0);
  const itemCount = [...activeByUser.values()].reduce((sum, items) => sum + items.length, 0);
  return (
    <div data-testid={ids.glanceRows} className="-mx-3 grid border-y border-edge bg-panel">
      <button
        type="button"
        data-testid={ids.glanceReady}
        className={ROW}
        onClick={() => setOpen('ready')}
      >
        <span className="grid min-w-0">
          <b className="text-[length:var(--text-base)] font-semibold">
            Ready to pick up · {ready.length}
          </b>
          <span className="truncate text-[length:var(--text-sm)] text-ink-muted">
            {ready.length === 0
              ? 'Nothing waiting'
              : ready.map(({ item }) => item.title).join(' · ')}
          </span>
        </span>
        <span />
        <Chevron />
      </button>
      <button
        type="button"
        data-testid={ids.glanceActive}
        className={ROW}
        onClick={() => setOpen('active')}
      >
        <span className="grid min-w-0">
          <b className="text-[length:var(--text-base)] font-semibold">In progress now</b>
          <span className="truncate text-[length:var(--text-sm)] text-ink-muted">
            {working.length} {working.length === 1 ? 'person' : 'people'} · {itemCount}{' '}
            {itemCount === 1 ? 'item' : 'items'}
          </span>
        </span>
        <Faces members={working} />
        <Chevron />
      </button>
      <ReadySheet open={open === 'ready'} ready={ready} onClose={close} />
      <InProgressSheet
        open={open === 'active'}
        members={members}
        meId={meId}
        activeByUser={activeByUser}
        onClose={close}
      />
    </div>
  );
}

function Faces({ members }: { members: MemberDto[] }) {
  return (
    <span className="flex">
      {members.map(({ user }, index) => (
        <span
          key={user.id}
          className={`rounded-full shadow-[0_0_0_2px_var(--color-surface)] ${index > 0 ? '-ml-1.5' : ''}`}
        >
          <Avatar {...user} />
        </span>
      ))}
    </span>
  );
}

function Chevron() {
  return (
    <span aria-hidden="true" className="text-[length:var(--text-md)] text-ink-muted">
      ›
    </span>
  );
}
