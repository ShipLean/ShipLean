import type { UserRef } from '../../../api/refs';
import { Avatar } from '../../../components/Avatar';
import { workItemTestIds as ids } from '../testIds';
import { useProjectMembers } from '../workItemApi';
import { FieldCell } from './FieldCell';
import { FieldMenu } from './FieldMenu';

const optionClass =
  'flex h-[30px] items-center gap-2 rounded-control px-2 text-left text-[length:var(--text-sm)] text-ink hover:bg-signal-soft focus-visible:bg-signal-soft focus-visible:outline-none';

export function AssigneeField({
  projectId,
  value,
  changed,
  onChange,
}: {
  projectId: string;
  value: UserRef | null;
  changed: boolean;
  onChange: (assignee: UserRef | null) => void;
}) {
  const members = useProjectMembers(projectId);
  return (
    <FieldCell label="Assignee" field="assignee" changed={changed} className="min-w-36">
      <FieldMenu
        buttonTestId={ids.assigneeButton}
        buttonLabel="Assignee"
        button={
          <>
            <Avatar {...(value ?? {})} />
            <span className={value ? '' : 'text-ink-muted'}>{value?.name ?? 'Unassigned'}</span>
          </>
        }
      >
        {(close) => (
          <>
            <button
              type="button"
              role="menuitemradio"
              aria-checked={value === null}
              data-testid={ids.assigneeOption}
              className={optionClass}
              onClick={() => {
                onChange(null);
                close();
              }}
            >
              <Avatar />
              <span className="text-ink-muted">Unassigned</span>
            </button>
            {(members.data ?? []).map((member) => (
              <button
                key={member.user.id}
                type="button"
                role="menuitemradio"
                aria-checked={value?.id === member.user.id}
                data-testid={ids.assigneeOption}
                className={optionClass}
                onClick={() => {
                  onChange(member.user);
                  close();
                }}
              >
                <Avatar {...member.user} />
                {member.user.name}
              </button>
            ))}
          </>
        )}
      </FieldMenu>
    </FieldCell>
  );
}
