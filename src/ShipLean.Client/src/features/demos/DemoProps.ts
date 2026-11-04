import type { SprintDemoDto } from '../../api/demos';
import type { DemoPlayerHandle } from './DemoPlayerHandle';

/** The props of every part of the Demo tab below the panel. Frozen: tasks fill the parts in without editing each other's files. */
export interface DemoProps {
  demo: SprintDemoDto;
  player: DemoPlayerHandle;
}
