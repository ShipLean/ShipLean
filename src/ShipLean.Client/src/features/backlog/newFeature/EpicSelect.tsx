import type { WorkItemDto } from '../../../api/workItems';
import { useWorkItemsOfType } from '../../workItem/workItemApi';
import { parentTypesOf } from '../../workItem/form/ParentField';
import { backlogTestIds as ids } from '../testIds';

// A Feature nested under another reads `Parent › Feature`, as in the Feature form's parent menu.
const pathOf = (item: WorkItemDto): string =>
  item.type.id === 'feature' && item.parent?.type.id === 'epic'
    ? `${item.parent.title} › ${item.title}`
    : item.title;

// The Epic field: the same candidates the Feature form's Epic field offers (parentTypesOf('feature')).
export function EpicSelect({
  projectId,
  value,
  onChange,
}: {
  projectId: string;
  value: string;
  onChange: (parentId: string) => void;
}) {
  const [first, second] = parentTypesOf('feature');
  const firstItems = useWorkItemsOfType(projectId, first, true);
  const secondItems = useWorkItemsOfType(projectId, second, true);
  const options = [...(firstItems.data ?? []), ...(secondItems.data ?? [])];
  return (
    <select
      id="new-feature-epic"
      data-testid={ids.newFeatureEpic}
      className="field w-full"
      value={value}
      onChange={(event) => onChange(event.target.value)}
    >
      <option value="">No Epic</option>
      {options.map((item) => (
        <option key={item.id} value={item.id}>
          {pathOf(item)}
        </option>
      ))}
    </select>
  );
}
