import type { ImportMappingDto, UserMappingDto } from '../../../api/import';
import type { UserRef } from '../../../api/refs';
import { importTestIds as ids } from '../testIds';
import { NewChip } from './NewChip';
import { CREATE_USER, JIRA_IMPORT_USER, setUser } from './mappingEdits';

const choiceOf = (u: UserMappingDto) =>
  u.resolution === 'create'
    ? CREATE_USER
    : u.resolution === 'jiraImportUser'
      ? JIRA_IMPORT_USER
      : (u.user?.id ?? JIRA_IMPORT_USER);

// One person in Jira: create them (no password), attribute their work to the Jira import user, or say who they are.
export function UserRow({
  row,
  people,
  mapping,
  onChange,
}: {
  row: UserMappingDto;
  people: UserRef[];
  mapping: ImportMappingDto;
  onChange: (mapping: ImportMappingDto) => void;
}) {
  const options =
    row.user !== null && !people.some((p) => p.id === row.user?.id)
      ? [...people, row.user]
      : people;
  return (
    <tr
      data-testid={ids.mappingUserRow}
      data-resolution={row.resolution}
      data-new={row.isNewSinceLastRun}
    >
      <td>
        {row.name}{' '}
        <span className="text-ink-muted">
          {row.isBot ? 'automation account' : (row.email ?? 'email hidden in Jira')}
        </span>{' '}
        {row.isNewSinceLastRun ? <NewChip /> : null}
      </td>
      <td>{row.references.toLocaleString('en-GB')}</td>
      <td>
        <select
          data-testid={ids.mappingUserSelect}
          className="field"
          aria-label={`What to do with ${row.name}`}
          value={choiceOf(row)}
          onChange={(event) =>
            onChange(setUser(mapping, row.jiraAccountId, event.target.value, options))
          }
        >
          {row.isBot ? null : <option value={CREATE_USER}>Create user (no password)</option>}
          <option value={JIRA_IMPORT_USER}>Attribute to “Jira import”</option>
          <optgroup label="Same person as">
            {options.map((p) => (
              <option key={p.id} value={p.id}>
                {p.name}
              </option>
            ))}
          </optgroup>
        </select>
      </td>
    </tr>
  );
}
