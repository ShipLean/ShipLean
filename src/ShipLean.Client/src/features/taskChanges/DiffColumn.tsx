import type { ReactNode } from 'react';
import type { LineMarker } from './markers';
import { taskChangesTestIds as ids } from './testIds';
import type { ChangesData, ComposerTarget, DiffLayout, ViewedProps } from './types';

export interface DiffColumnProps extends ViewedProps {
  data: ChangesData;
  layout: DiffLayout;
  /** A file key, `<repo>:<path>` (see `fileKey`). */
  openFile: string | null;
  markers: LineMarker[];
  /** The + on a line. */
  onAddLine(target: ComposerTarget): void;
  /** Where the composer goes: the diff renders `composer` under `composerTarget`'s line. Null when none is open. */
  composerTarget: ComposerTarget | null;
  composer: ReactNode;
}

// Stub: the @pierre/diffs renderer with markers and the composer under its line replaces this.
export function DiffColumn({ data }: DiffColumnProps) {
  return <div data-testid={ids.diff}>{data.parts.length} diffs</div>;
}
