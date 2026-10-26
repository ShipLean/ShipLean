import type { ImportMappingDto } from '../../../api/import';
import { useProjectMembers, useProjectStates } from '../../workItem/workItemApi';
import { importTestIds as ids } from '../testIds';
import { FieldsSection } from './FieldsSection';
import { IssueTypesSection } from './IssueTypesSection';
import { MappingWarnings } from './MappingWarnings';
import { StatusesSection } from './StatusesSection';
import { UsersSection } from './UsersSection';

// Step 4: the suggested mapping, every part editable. The edited mapping is what the stepper hands to the run.
export function MappingStep({
  projectId,
  mapping,
  onChange,
}: {
  projectId: string;
  mapping: ImportMappingDto;
  onChange: (mapping: ImportMappingDto) => void;
}) {
  const states = useProjectStates(projectId);
  const members = useProjectMembers(projectId);
  if (states.isPending || members.isPending) {
    return <p className="text-ink-muted">Reading the project…</p>;
  }
  if (states.isError || members.isError) {
    return (
      <p data-testid={ids.mappingError} role="alert" className="text-alert">
        The project's states and members could not be read.
      </p>
    );
  }
  return (
    <div data-testid={ids.mappingStep} className="flex flex-col gap-[var(--space-4)]">
      <MappingWarnings mapping={mapping} />
      <IssueTypesSection mapping={mapping} states={states.data} onChange={onChange} />
      <StatusesSection mapping={mapping} states={states.data} onChange={onChange} />
      <UsersSection
        mapping={mapping}
        people={members.data.map((m) => m.user)}
        onChange={onChange}
      />
      <FieldsSection mapping={mapping} onChange={onChange} />
    </div>
  );
}
