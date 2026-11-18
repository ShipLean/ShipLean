import { useTouchShell } from '../../../touch/useTouchShell';
import { AddMember } from './AddMember';
import { MemberRow } from './MemberRow';
import { Panel } from './Panel';
import type { DraftMember } from './model';
import type { TabProps } from './tabProps';
import { settingsTestIds } from '../testIds';

// "5 people · 1 admin": the draft's members, without the ones marked for removal.
const peopleSummary = (members: DraftMember[]) => {
  const kept = members.filter((m) => !m.removed);
  const admins = kept.filter((m) => m.role === 'admin').length;
  return `${kept.length} ${kept.length === 1 ? 'person' : 'people'} · ${admins} ${admins === 1 ? 'admin' : 'admins'}`;
};

// At touch the people summary and Add member sit at the top, above the rows (SHIP-1809 P6); desktop keeps Add below.
export function MembersTab({
  draft,
  setDraft,
  readOnly,
  changedMembers,
}: TabProps & { changedMembers: ReadonlySet<string> }) {
  const { touch } = useTouchShell();
  const change = (email: string, patch: Partial<DraftMember>) =>
    setDraft((d) => ({
      ...d,
      members: d.members.map((m) => (m.email === email ? { ...m, ...patch } : m)),
    }));
  const drop = (email: string) =>
    setDraft((d) => ({ ...d, members: d.members.filter((m) => m.email !== email) }));
  return (
    <Panel title="Members" testId={settingsTestIds.membersPanel}>
      {touch ? (
        <>
          <p
            data-testid={settingsTestIds.membersSummary}
            className="-mt-1 text-[length:var(--text-sm)] text-ink-muted"
          >
            {peopleSummary(draft.members)}
          </p>
          {readOnly ? null : <AddMember draft={draft} setDraft={setDraft} />}
        </>
      ) : null}
      <table className="w-full text-left text-[length:var(--text-sm)] touch:block">
        <thead className="touch:hidden">
          <tr className="text-ink-muted">
            <th className="px-3 py-1 font-medium">Member</th>
            <th className="px-3 py-1 font-medium">Email</th>
            <th className="px-3 py-1 font-medium">Role</th>
            <th />
          </tr>
        </thead>
        <tbody className="touch:block">
          {draft.members.map((member) => (
            <MemberRow
              key={member.user.id}
              member={member}
              readOnly={readOnly}
              changed={changedMembers.has(member.user.id)}
              onRole={(role) => change(member.email, { role })}
              onRemove={() =>
                member.fresh ? drop(member.email) : change(member.email, { removed: true })
              }
              onUndo={() => change(member.email, { removed: false })}
            />
          ))}
        </tbody>
      </table>
      {readOnly || touch ? null : <AddMember draft={draft} setDraft={setDraft} />}
      <p className="text-[length:var(--text-sm)] text-ink-muted">
        Admins change project settings and manage members; everyone can plan and edit work. A
        project always keeps at least one admin.
      </p>
    </Panel>
  );
}
