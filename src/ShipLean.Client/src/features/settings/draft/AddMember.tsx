import { useState } from 'react';
import { ApiRequestError } from '../../../api/client';
import { fetchUserByEmail } from '../settingsApi';
import { settingsTestIds } from '../testIds';
import type { TabProps } from './tabProps';

// Adds by the full email: the server finds a person only by the exact address, so a partial one says not found.
export function AddMember({ draft, setDraft }: Pick<TabProps, 'draft' | 'setDraft'>) {
  const [email, setEmail] = useState('');
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);

  const add = async () => {
    const wanted = email.trim();
    if (wanted === '') return;
    setError(undefined);
    const existing = draft.members.find((m) => m.email.toLowerCase() === wanted.toLowerCase());
    if (existing !== undefined && !existing.removed) {
      setError(`${existing.user.name} is already a member.`);
      return;
    }
    setBusy(true);
    try {
      const user = await fetchUserByEmail(wanted);
      setDraft((d) => ({
        ...d,
        members: d.members.some((m) => m.user.id === user.id)
          ? d.members.map((m) => (m.user.id === user.id ? { ...m, removed: false } : m))
          : [...d.members, { email: wanted, user, role: 'member', removed: false, fresh: true }],
      }));
      setEmail('');
    } catch (failure) {
      if (!(failure instanceof ApiRequestError)) throw failure;
      setError(failure.message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <div>
      <div className="flex gap-2">
        <input
          type="email"
          data-testid={settingsTestIds.memberAddEmail}
          aria-label="Email of the person to add"
          placeholder="Add a member by their full email"
          className="h-7 w-72 rounded-control border border-edge bg-panel px-2 text-[length:var(--text-sm)] text-ink"
          value={email}
          autoComplete="off"
          onChange={(e) => setEmail(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === 'Enter') void add();
          }}
        />
        <button
          type="button"
          data-testid={settingsTestIds.memberAdd}
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
          data-testid={settingsTestIds.memberAddError}
          className="mt-1 text-[length:var(--text-sm)] text-alert"
        >
          {error}
        </div>
      )}
    </div>
  );
}
