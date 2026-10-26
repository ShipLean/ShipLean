import type { SprintRef } from '../../../api/refs';
import type { BacklogRowDto, BacklogViewDto } from '../../../api/views';

/** A read of Planning with the revision and sprint a write answered put on its row. */
export function withWritten(
  view: BacklogViewDto,
  itemId: string,
  revision: number,
  sprint: SprintRef | null,
): BacklogViewDto {
  const update = (row: BacklogRowDto) => (row.id === itemId ? { ...row, revision, sprint } : row);
  return {
    ...view,
    sections: view.sections.map((section) => ({ ...section, rows: section.rows.map(update) })),
  };
}
