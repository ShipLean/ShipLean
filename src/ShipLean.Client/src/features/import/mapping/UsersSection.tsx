import { useState } from 'react';
import type { ImportMappingDto } from '../../../api/import';
import type { UserRef } from '../../../api/refs';
import { importTestIds as ids } from '../testIds';
import { UserRow } from './UserRow';

// People matched by email are tucked away; the rest are listed with a choice.
export function UsersSection({
  mapping,
  people,
  onChange,
}: {
  mapping: ImportMappingDto;
  people: UserRef[];
  onChange: (mapping: ImportMappingDto) => void;
}) {
  const [showMatched, setShowMatched] = useState(false);
  const matched = mapping.users.filter((u) => u.resolution === 'matched').length;
  // A person new since the last run is always listed, so the highlighted row can be seen.
  const rows = mapping.users.filter(
    (u) => showMatched || u.resolution !== 'matched' || u.isNewSinceLastRun,
  );
  return (
    <section
      id="import-mapping-users"
      tabIndex={-1}
      data-testid={ids.mappingUsers}
      className="panel p-[var(--space-4)]"
    >
      <header className="flex items-baseline gap-[var(--space-3)]">
        <h2 className="font-semibold">Users</h2>
        <span className="flex-1 text-ink-muted">
          {mapping.users.length} people · {matched} matched by email ·{' '}
          {mapping.users.length - matched} not in ShipLean
        </span>
        <button
          type="button"
          data-testid={ids.mappingShowMatched}
          className="btn-ghost"
          aria-expanded={showMatched}
          onClick={() => setShowMatched(!showMatched)}
        >
          {showMatched ? 'Hide matched' : 'Show matched'}
        </button>
      </header>
      <table className="w-full text-left">
        <thead>
          <tr>
            <th>Jira user</th>
            <th>Items</th>
            <th>In ShipLean</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <UserRow
              key={row.jiraAccountId}
              row={row}
              people={people}
              mapping={mapping}
              onChange={onChange}
            />
          ))}
        </tbody>
      </table>
    </section>
  );
}
