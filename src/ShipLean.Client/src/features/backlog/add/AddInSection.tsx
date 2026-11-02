import { useState } from 'react';
import type { WorkItemTypeId } from '../../../api/refs';
import type { BacklogSectionDto } from '../../../api/views';
import type { ParentRefDto } from '../../../api/workItems';
import { backlogTestIds as ids } from '../testIds';
import { AddRowFields } from './AddRowFields';
import { EMPTY_DRAFT, useAddChain, useOpenSection, type Draft } from './useAddChain';

export interface AddInSectionProps {
  section: BacklogSectionDto;
  columnCount: number;
  projectId: string;
}

// The "+ Add" row at the end of a Planning section. It opens the add row, whose items go at the top of this
// section's sprint, each next one under the one before; the type and parent stay until it closes.
export function AddInSection({ section, columnCount, projectId }: AddInSectionProps) {
  const { open, closeSection } = useOpenSection();
  const [type, setType] = useState<WorkItemTypeId>('story');
  const [parent, setParent] = useState<ParentRefDto | null>(null);
  const [draft, setDraft] = useState<Draft>(EMPTY_DRAFT);
  const chain = useAddChain();
  const sprintId = section.sprint?.id ?? null;

  if (open !== section.id) return null;
  return (
    <tr data-testid={ids.addRow} data-section={section.id} className="h-11 bg-signal-soft">
      <td colSpan={columnCount} className="px-2">
        <AddRowFields
          projectId={projectId}
          type={type}
          parent={parent}
          draft={draft}
          onDraft={setDraft}
          onType={setType}
          onParent={setParent}
          hint={`Goes at the top of ${section.title}`}
          error={chain.error}
          onClose={() => {
            closeSection();
            setParent(null);
            setType('story');
            setDraft(EMPTY_DRAFT);
            chain.reset();
          }}
          onAdd={(fields) => chain.inSprint(sprintId, fields)}
        />
      </td>
    </tr>
  );
}

/** The "+ Add" button in a sprint section's header, as the design has it; it opens the section's add row. */
export function AddInButton({
  section,
  filtering,
}: {
  section: BacklogSectionDto;
  filtering: boolean;
}) {
  const { openSection } = useOpenSection();
  return (
    <button
      type="button"
      data-testid={ids.addHere}
      className="btn-ghost"
      disabled={filtering}
      title={
        filtering ? "Off while a filter is on: rank is relative to rows you can't see" : undefined
      }
      onClick={() => openSection(section.id)}
    >
      + Add
    </button>
  );
}
