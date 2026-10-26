import type { ProjectDto } from '../api/projects';
import { ImportRunSlot } from '../slots/ImportRunSlot';
import { ImportStepperSlot } from '../slots/ImportStepperSlot';
import { importSlots } from './importSlots';

export function ImportSlots({ project }: { project: ProjectDto | null }) {
  const slots = importSlots(project);
  return (
    <>
      <ImportStepperSlot {...slots.importStepper} />
      <ImportRunSlot {...slots.importRun} />
    </>
  );
}
