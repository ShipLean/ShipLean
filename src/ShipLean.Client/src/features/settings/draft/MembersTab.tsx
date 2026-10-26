import { AddMember } from './AddMember';
import { MemberRow } from './MemberRow';
import { Panel } from './Panel';
import type { DraftMember } from './model';
import type { TabProps } from './tabProps';
import { settingsTestIds } from '../testIds';

export function MembersTab({
  draft,
  setDraft,
  readOnly,
  changedMembers,
}: TabProps & { changedMembers: ReadonlySet<string> }) {
  const change = (email: string, patch: Partial<DraftMember>) =>
    setDraft((d) => ({
      ...d,
      members: d.members.map((m) => (m.email === email ? { ...m, ...patch } : m)),
    }));
  const drop = (email: string) =>
    setDraft((d) => ({ ...d, members: d.members.filter((m) => m.email !== email) }));
  return (
    <Panel title="Members" testId={settingsTestIds.membersPanel}>
      <table className="w-full text-left text-[length:var(--text-sm)]">
        <thead>
          <tr className="text-ink-muted">
            <th className="px-3 py-1 font-medium">Member</th>
            <th className="px-3 py-1 font-medium">Email</th>
            <th className="px-3 py-1 font-medium">Role</th>
            <th />
          </tr>
        </thead>
        <tbody>
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
      {readOnly ? null : <AddMember draft={draft} setDraft={setDraft} />}
      <p className="text-[length:var(--text-sm)] text-ink-muted">
        Admins change project settings and manage members; everyone can plan and edit work. A
        project always keeps at least one admin.
      </p>
    </Panel>
  );
}
