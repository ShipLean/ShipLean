import { useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import type { ProjectDto } from '@shiplean/core/api/projects';
import type { SprintDto } from '@shiplean/core/api/sprints';
import { pagesTestIds } from '../features/pages/testIds';
import { RetroNumbers } from '../features/retros/sprint/RetroNumbers';
import type { RailSheet } from '../features/retros/sprint/RetroRailSheets';
import { SprintRetroPanel } from '../features/retros/sprint/SprintRetroPanel';
import { parseSprintRetroAddress } from '../features/retros/sprint/sprintRetroAddress';
import { useSetDecide } from '../features/retros/sprint/useSetDecide';
import { useSprintRetro } from '../features/retros/useRetroQueries';
import { SprintHeader, type SprintHeaderProps } from '../features/sprintHeader/SprintHeader';
import { useTouchShell } from '../touch/useTouchShell';

export interface SprintRetroSlotProps {
  project: ProjectDto;
  sprint: SprintDto;
  header: SprintHeaderProps;
}

// The sprint header and the Retro tab under it. At touch the four numbers sit in the header, before the tabs (SHIP-1626
// D2), so this slot owns the header and the open rail sheet that the numbers and the panel's ⋯ both use.
export function SprintRetroSlot({ project, sprint, header }: SprintRetroSlotProps) {
  const { touch } = useTouchShell();
  const [params] = useSearchParams();
  const { decide } = parseSprintRetroAddress(params);
  const setDecide = useSetDecide();
  // The open rail sheet belongs to one sprint, so moving to another sprint closes it (as SprintHeader does with its edit state).
  const [open, setOpen] = useState<{ sprintId: string; sheet: RailSheet } | null>(null);
  const sheet = open?.sprintId === sprint.id ? open.sheet : null;
  const setSheet = (next: RailSheet | null) =>
    setOpen(next && { sprintId: sprint.id, sheet: next });
  const retro = useSprintRetro(project.id, sprint.id).data;
  const numbers =
    touch && retro ? (
      <RetroNumbers
        kpis={retro.kpis}
        decide={decide}
        onOpen={setSheet}
        onToggleDecide={() => setDecide(retro, !decide)}
      />
    ) : undefined;
  return (
    <>
      <SprintHeader {...header} beforeTabs={numbers} />
      <div data-testid={pagesTestIds.slotSprintRetro}>
        <SprintRetroPanel
          key={sprint.id}
          project={project}
          sprint={sprint}
          sheet={sheet}
          onSheet={setSheet}
        />
      </div>
    </>
  );
}
