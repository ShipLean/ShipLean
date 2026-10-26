import { StateField } from '../../workItem/form/StateField';
import { TagsField } from '../../workItem/form/TagsField';
import { closedCount } from '../children/storiesAndBugs';
import type { FeatureRegionProps } from '../FeatureRegionProps';
import { featureTestIds as ids } from '../testIds';
import { EpicField } from './EpicField';
import { ReadOnlyCell } from './ReadOnlyCell';
import { SlugField } from './SlugField';

// State, Epic, Slug, Started, the stories-and-bugs count and Tags across the full width.
export function FeatureFieldsStrip({ item, draft, changed, setField }: FeatureRegionProps) {
  const { closed, total } = closedCount(item.children);
  return (
    <div
      data-testid={ids.strip}
      aria-label="Fields"
      className="flex flex-wrap items-start gap-x-4 gap-y-2 border-b border-edge bg-panel px-4 py-3"
    >
      <StateField
        projectId={item.projectId}
        type={item.type.id}
        value={draft.state}
        changed={changed.includes('state')}
        onChange={(state) => setField({ state })}
      />
      <EpicField
        projectId={item.projectId}
        itemId={item.id}
        value={draft.parent}
        changed={changed.includes('parent')}
        onChange={(parent) => setField({ parent })}
      />
      <SlugField slug={item.slug} />
      <ReadOnlyCell label="Started" field="started" testId={ids.stripStarted}>
        {item.startedLabel ?? 'Not started'}
      </ReadOnlyCell>
      <ReadOnlyCell label="Stories and bugs" field="children" testId={ids.stripChildrenCount}>
        {closed} of {total} closed
      </ReadOnlyCell>
      <TagsField
        value={draft.tags}
        changed={changed.includes('tags')}
        onChange={(tags) => setField({ tags })}
      />
    </div>
  );
}
