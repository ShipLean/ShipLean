import type { BacklogRowDto, BacklogSectionDto } from '../../../api/views';
import type { PullLine } from './usePullLine';

// The pull line, drawn under the row where the forecast's cut falls.
// TODO: the line with its Pull button and drag grip replaces this empty row.
export function PullLineRow({
  line,
  row,
  section,
  projectId,
  columnCount,
}: {
  line: PullLine;
  row: BacklogRowDto;
  section: BacklogSectionDto;
  projectId: string;
  columnCount: number;
}) {
  void [line, row, section, projectId, columnCount];
  return null;
}
