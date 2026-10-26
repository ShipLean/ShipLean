import type { BoardSettingsJson } from '../../../../api/persisted';
import { taskboardSettingsTestIds as ids } from '../../testIds';
import { withTagColour } from '../../../boardSettings/panes/tags/tagEdits';
import { TagRow } from './TagRow';

/** The tags of the project, each with its colour in the draft. */
export function TagList({
  tags,
  draft,
  readOnly,
  onChange,
}: {
  tags: string[];
  draft: BoardSettingsJson;
  readOnly: boolean;
  onChange(next: BoardSettingsJson): void;
}) {
  if (tags.length === 0) {
    return (
      <p data-testid={ids.tagsEmpty} className="m-0 text-ink-muted">
        No work item has a tag yet. A tag appears here as soon as someone uses it.
      </p>
    );
  }
  return (
    <div className="rounded-panel border border-edge bg-panel px-3">
      {tags.map((tag) => (
        <TagRow
          key={tag}
          tag={tag}
          colour={draft.tagColours.find((t) => t.tag === tag)?.colour ?? null}
          readOnly={readOnly}
          onChange={(colour) =>
            onChange({ ...draft, tagColours: withTagColour(draft.tagColours, tag, colour) })
          }
        />
      ))}
    </div>
  );
}
