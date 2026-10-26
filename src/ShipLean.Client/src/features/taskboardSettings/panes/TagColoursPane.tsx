import type { BoardSettingsJson } from '../../../api/persisted';
import type { PaneProps } from '../../../components/SettingsDialog/types';
import type { BoardSettingsContext } from '../../boardSettings/panes';
import { listedTags } from '../../boardSettings/panes/tags/tagEdits';
import { useProjectTags } from '../../boardSettings/panes/tags/useProjectTags';
import { taskboardSettingsTestIds as ids } from '../testIds';
import { TagList } from './tags/TagList';

/** A colour per tag on the Taskboard; tags without one show in the neutral style. */
export function TagColoursPane({
  draft,
  onChange,
  readOnly,
  context,
}: PaneProps<BoardSettingsJson, BoardSettingsContext>) {
  const tags = useProjectTags(context.projectId);
  const body =
    tags.data === undefined ? (
      tags.isError ? (
        <p data-testid={ids.tagsLoadFailed} role="alert" className="m-0 text-alert">
          The tags could not be loaded.
        </p>
      ) : (
        <p data-testid={ids.tagsLoading} className="m-0 text-ink-muted">
          Loading tags…
        </p>
      )
    ) : (
      <TagList
        tags={listedTags(tags.data, draft.tagColours)}
        draft={draft}
        readOnly={readOnly}
        onChange={onChange}
      />
    );
  return (
    <div data-testid={ids.tagsPane} className="grid content-start gap-3">
      <p className="m-0 text-[length:var(--text-xs)] text-ink-muted">
        Tag colours apply to the tags on task cards on this Taskboard. Tags without a colour show in
        the neutral style.
      </p>
      {body}
    </div>
  );
}
