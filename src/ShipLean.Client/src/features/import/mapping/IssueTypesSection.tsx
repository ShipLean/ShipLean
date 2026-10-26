import type { ImportMappingDto, ImportTypeTarget } from '../../../api/import';
import { IMPORT_TYPE_TARGETS } from '../../../api/import';
import type { StateDto } from '../../../api/projects';
import { importTestIds as ids } from '../testIds';
import { NewChip } from './NewChip';
import { setTypeTarget, typeLabels } from './mappingEdits';

export function IssueTypesSection({
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
      id="import-mapping-types"
      tabIndex={-1}
      data-testid={ids.mappingTypes}
      className="panel p-[var(--space-4)]"
    >
      <h2 className="font-semibold">Issue types</h2>
      <table className="w-full text-left">
        <thead>
          <tr>
            <th>Jira type</th>
            <th>Issues</th>
            <th>ShipLean type</th>
            <th>Note</th>
          </tr>
        </thead>
        <tbody>
          {mapping.issueTypes.map((t) => (
            <tr
              key={t.jiraType}
              data-testid={ids.mappingTypeRow}
              data-jira-type={t.jiraType}
              data-new={t.isNewSinceLastRun}
            >
              <td>
                {t.jiraType} {t.isNewSinceLastRun ? <NewChip /> : null}
              </td>
              <td>{t.count.toLocaleString('en-GB')}</td>
              <td>
                <select
                  data-testid={ids.mappingTypeSelect}
                  className="field"
                  aria-label={`ShipLean type for Jira ${t.jiraType}`}
                  value={t.target}
                  onChange={(event) =>
                    onChange(
                      setTypeTarget(
                        mapping,
                        t.jiraType,
                        event.target.value as ImportTypeTarget,
                        states,
                      ),
                    )
                  }
                >
                  {IMPORT_TYPE_TARGETS.map((target) => (
                    <option key={target} value={target}>
                      {typeLabels[target]}
                    </option>
                  ))}
                </select>
              </td>
              <td className="text-ink-muted">{t.note ?? ''}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </section>
  );
}
