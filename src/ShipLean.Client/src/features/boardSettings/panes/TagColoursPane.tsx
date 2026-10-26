import type { BoardSettingsJson } from '../../../api/persisted';
import type { PaneProps } from '../../../components/SettingsDialog/types';
import type { BoardSettingsContext } from '../panes';
import { boardSettingsTestIds as ids } from '../testIds';
import { StylePreview } from './styles/StylePreview';
import { listedTags } from './tags/tagEdits';
import { TagList } from './tags/TagList';
import { useProjectTags } from './tags/useProjectTags';

/** A colour per tag for this board; tags without one show in the neutral style. */
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
        Tag colours apply on this board, and the Stories backlog borrows them. Tags without a colour
        show in the neutral style.
      </p>
      <div className="grid grid-cols-[minmax(0,1fr)_minmax(0,16rem)] items-start gap-3">
        {body}
        <StylePreview
          draft={draft}
          level={context.level}
          captions={false}
          testId={ids.tagsPreview}
        />
      </div>
    </div>
  );
}
