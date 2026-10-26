import { useState } from 'react';
import { ApiRequestError } from '../../../api/client';
import { Panel } from '../draft/Panel';
import { fetchUserByEmail } from '../settingsApi';
import { settingsTestIds as ids } from '../testIds';
import type { FormProps } from './formProps';

interface MembersPanelProps extends FormProps {
  myEmail: string;
}

// Exact email only: the server finds a person by the whole address. Everyone added here joins as a Member; you are the Admin.
export function MembersPanel({ form, patch, myEmail }: MembersPanelProps) {
  const [email, setEmail] = useState('');
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);

  const add = async () => {
    const wanted = email.trim();
    if (wanted === '') return;
    setError(undefined);
    const lower = wanted.toLowerCase();
    if (
      lower === myEmail.toLowerCase() ||
      form.members.some((m) => m.email.toLowerCase() === lower)
    ) {
      setError(`${wanted} is already on the project.`);
      return;
    }
    setBusy(true);
    try {
      const user = await fetchUserByEmail(wanted);
      patch({ members: [...form.members, { email: wanted, user }] });
      setEmail('');
    } catch (failure) {
      if (!(failure instanceof ApiRequestError)) throw failure;
      setError(failure.message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <Panel title="Members">
      <div className="text-[length:var(--text-sm)] text-ink-muted">
        You are the Admin. More people can be added later in Project settings.
      </div>
      {form.members.map((m) => (
        <div
          key={m.user.id}
          data-testid={ids.newProjectMemberRow}
          className="flex items-center gap-3 text-[length:var(--text-sm)] text-ink"
        >
          <span className="font-medium">{m.user.name}</span>
          <span className="text-ink-muted">{m.email}</span>
          <button
            type="button"
            data-testid={ids.newProjectMemberRemove}
            aria-label={`Remove ${m.user.name}`}
            className="ml-auto text-ink-muted"
            onClick={() => patch({ members: form.members.filter((x) => x.user.id !== m.user.id) })}
          >
            ✕
          </button>
        </div>
      ))}
      <div className="flex gap-2">
        <input
          type="email"
          data-testid={ids.newProjectMemberEmail}
          aria-label="Email of the person to add"
          placeholder="Add a member by their full email"
          autoComplete="off"
          className="h-7 rounded-control border border-edge bg-panel px-2 text-[length:var(--text-sm)] text-ink w-72"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === 'Enter') void add();
          }}
        />
        <button
          type="button"
          data-testid={ids.newProjectMemberAdd}
          disabled={busy || email.trim() === ''}
          className="h-7 rounded-control border border-edge bg-panel px-3 text-[length:var(--text-sm)] font-medium text-ink disabled:opacity-50"
          onClick={() => void add()}
        >
          Add
        </button>
      </div>
      {error === undefined ? null : (
        <div
          role="alert"
          data-testid={ids.newProjectMemberError}
          className="text-[length:var(--text-sm)] text-alert"
        >
          {error}
        </div>
      )}
    </Panel>
  );
}
