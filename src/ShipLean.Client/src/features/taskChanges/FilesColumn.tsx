import { taskChangesTestIds as ids } from './testIds';
import type { ChangesData, ViewedProps } from './types';

export interface FilesColumnProps extends ViewedProps {
  data: ChangesData;
  /** A file key, `<repo>:<path>` (see `fileKey`). */
  openFile: string | null;
  onOpenFile(fileKey: string): void;
}

// Stub: the file list with its Viewed boxes replaces this.
export function FilesColumn({ data }: FilesColumnProps) {
  const files = data.parts.reduce((n, part) => n + part.files.length, 0);
  return <div data-testid={ids.files}>{files} files</div>;
}
