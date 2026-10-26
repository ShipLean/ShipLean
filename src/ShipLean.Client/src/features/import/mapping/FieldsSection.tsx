import type { ImportMappingDto } from '../../../api/import';
import { importTestIds as ids } from '../testIds';
import { setInclude, setStoryPointsField } from './mappingEdits';
import type { IncludeOption } from './mappingEdits';

const options: [IncludeOption, string, string][] = [
  ['includeComponents', 'Components', ids.mappingIncludeComponents],
  ['includeComments', 'Comments', ids.mappingIncludeComments],
  ['includeAttachments', 'Attachments', ids.mappingIncludeAttachments],
  ['includeChangelog', 'Changelog (revisions)', ids.mappingIncludeChangelog],
];

// The story points field and which optional parts come across. The rest of the field list is fixed.
export function FieldsSection({
  mapping,
  onChange,
}: {
  mapping: ImportMappingDto;
  onChange: (mapping: ImportMappingDto) => void;
}) {
  const { fields } = mapping;
  const chosen = fields.candidates.find((c) => c.fieldId === fields.storyPointsFieldId);
  return (
    <section
      id="import-mapping-fields"
      tabIndex={-1}
      data-testid={ids.mappingFields}
      className="panel flex flex-col gap-[var(--space-3)] p-[var(--space-4)]"
    >
      <h2 className="font-semibold">Fields</h2>
      <label className="flex items-center gap-[var(--space-2)]">
        Story points field
        <select
          data-testid={ids.mappingStoryPoints}
          className="field"
          value={fields.storyPointsFieldId}
          onChange={(event) => onChange(setStoryPointsField(mapping, event.target.value))}
        >
          {fields.candidates.map((c) => (
            <option key={c.fieldId} value={c.fieldId}>
              {c.name} · {c.fieldId}
            </option>
          ))}
        </select>
      </label>
      {chosen ? (
        <p data-testid={ids.mappingStoryPointsNote} className="text-ink-muted">
          {chosen.populated.toLocaleString('en-GB')} set · {chosen.missing.toLocaleString('en-GB')}{' '}
          without story points import unestimated.
        </p>
      ) : null}
      <ul className="m-0 flex list-none flex-wrap gap-[var(--space-4)] p-0">
        {options.map(([option, label, testId]) => (
          <li key={option}>
            <label className="flex items-center gap-1">
              <input
                type="checkbox"
                data-testid={testId}
                checked={fields[option]}
                onChange={(event) => onChange(setInclude(mapping, option, event.target.checked))}
              />
              {label}
            </label>
          </li>
        ))}
      </ul>
    </section>
  );
}
