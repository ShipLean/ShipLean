import type { ImportMappingDto } from '../../../api/import';
import type { StateDto } from '../../../api/projects';
import { TypeIcon } from '../../../components/TypeIcon';
import { importTestIds as ids } from '../testIds';
import { StatusRow } from './StatusRow';
import { typeLabels } from './mappingEdits';

// Per Jira type, grouped under the ShipLean type it now maps to; a skipped type has no statuses to map.
export function StatusesSection({
  mapping,
  states,
  onChange,
}: {
  mapping: ImportMappingDto;
  states: StateDto[];
  onChange: (mapping: ImportMappingDto) => void;
}) {
  return (
    <section
      id="import-mapping-statuses"
      tabIndex={-1}
      data-testid={ids.mappingStatuses}
      className="panel p-[var(--space-4)]"
    >
      <h2 className="font-semibold">Statuses → States</h2>
      <p className="text-ink-muted">
        A status with no matching state is created and given a board column.
      </p>
      <table className="w-full text-left">
        <thead>
          <tr>
            <th>Jira status</th>
            <th>Issues</th>
            <th>ShipLean state</th>
            <th>Category</th>
          </tr>
        </thead>
        {mapping.issueTypes
          .filter((t) => t.target !== 'skip')
          .map((t) => (
            <tbody
              key={t.jiraType}
              data-testid={ids.mappingStatusGroup}
              data-jira-type={t.jiraType}
            >
              <tr>
                <th colSpan={4}>
                  <span className="flex items-center gap-1">
                    <TypeIcon type={t.target as Exclude<typeof t.target, 'skip'>} />
                    {typeLabels[t.target]}
                    <span className="font-normal text-ink-muted">from Jira {t.jiraType}</span>
                  </span>
                </th>
              </tr>
              {mapping.statuses
                .filter((s) => s.jiraType === t.jiraType)
                .map((row) => (
                  <StatusRow
                    key={row.jiraStatus}
                    row={row}
                    ofType={states.filter((s) => s.type === t.target)}
                    mapping={mapping}
                    onChange={onChange}
                  />
                ))}
            </tbody>
          ))}
      </table>
    </section>
  );
}
