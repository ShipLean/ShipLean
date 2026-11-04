import type { DemoProps } from './DemoProps';
import { demosTestIds as ids } from './testIds';

// TODO: the chapter list that seeks the player.
export function DemoChapters({ demo }: DemoProps) {
  return <div data-testid={ids.demoChapters}>{demo.chapters.length} chapters</div>;
}
