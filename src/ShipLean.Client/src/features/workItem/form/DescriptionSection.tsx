import type { WorkItemDto } from '../../../api/workItems';
import { workItemTestIds as ids } from '../testIds';
import { workItemAttachmentsKey } from '../workItemApi';
import { MarkdownSection } from './MarkdownSection';

export function DescriptionSection({
  item,
  value,
  changed,
  onChange,
}: {
  item: WorkItemDto;
  value: string;
  changed: boolean;
  onChange: (description: string) => void;
}) {
  return (
    <MarkdownSection
      title="Description"
      field="description"
      emptyText="No description yet. Edit to add one."
      value={value}
      savedValue={item.description}
      savedHtml={item.descriptionHtml}
      changed={changed}
      workItemId={item.id}
      attachmentsKey={workItemAttachmentsKey(item.projectId, item.key)}
      ids={{
        section: ids.description,
        view: ids.descriptionView,
        edit: ids.descriptionEdit,
        input: ids.descriptionInput,
        preview: ids.descriptionPreview,
        pasting: ids.descriptionPasting,
        pasteError: ids.descriptionPasteError,
        externalNote: ids.descriptionExternalNote,
      }}
      onChange={onChange}
    />
  );
}
