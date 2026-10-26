import type { WorkItemDto } from '../../api/workItems';
import type { Draft, DraftField } from '../workItem/form/draft';

/**
 * What the Feature form hands every region. `item` is the live query result, not the form's saved base: a child closing
 * doesn't bump the Feature's revision, and still reaches the regions. `draft`, `dirty`, `changed` and `setField` are the
 * form's: a region that edits a field writes it into the draft and Save sends it. `saveError` is the last failed save
 * (`field` names the request field at fault when the server said so), cleared by the next save or Discard.
 */
export interface FeatureRegionProps {
  item: WorkItemDto;
  draft: Draft;
  dirty: boolean;
  changed: readonly DraftField[];
  setField: (patch: Partial<Draft>) => void;
  saveError: { message: string; field: string | null } | null;
  projectKey: string;
}
