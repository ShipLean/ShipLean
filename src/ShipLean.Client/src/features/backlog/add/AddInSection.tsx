import type { BacklogSectionDto } from '../../../api/views';

export interface AddInSectionProps {
  section: BacklogSectionDto;
  columnCount: number;
  projectId: string;
  projectKey: string;
  /** A filter, search or check queue is on: rank is relative to rows you can't see. */
  filtering: boolean;
}

// The "add inside this section" row at the end of a Planning section.
// TODO: the add-in-section row replaces this empty one.
export function AddInSection(props: AddInSectionProps) {
  void props;
  return null;
}
