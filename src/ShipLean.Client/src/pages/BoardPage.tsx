import { useSearchParams } from 'react-router-dom';
import { pagesTestIds } from '../features/pages/testIds';
import type { Level } from '../routes';
import { BoardSettingsSlot } from '../slots/BoardSettingsSlot';
import { BoardSlot } from '../slots/BoardSlot';
import { boardSlots } from './boardSlots';
import { boardRouteIds } from './pageRouteIds';
import { ProjectScreen } from './ProjectScreen';

// A level's board. Choosing a settings section sets ?settings=, closing the dialog removes it, and a link to
// ?settings=columns opens the dialog from anywhere.
export function BoardPage({ level }: { level: Level }) {
  const [params, setParams] = useSearchParams();
  return (
    <ProjectScreen routeId={boardRouteIds[level]} testId={pagesTestIds.boardPage}>
      {(project) => {
        const slots = boardSlots(project, level, params, setParams);
        return (
          <>
            <BoardSlot {...slots.board} />
            <BoardSettingsSlot {...slots.boardSettings} />
          </>
        );
      }}
    </ProjectScreen>
  );
}
