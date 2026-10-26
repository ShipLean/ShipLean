import { useQueryClient } from '@tanstack/react-query';
import { ToastHost } from '../../../components/ToastHost';
import { boardRootKey } from '../boardApi';
import { BoardView } from '../grid/BoardView';
import { BOARD_ID, PROJECT } from './fixtures';

// Toasts outlast every assertion, so a check that a toast is absent cannot pass by waiting it out.
const LONG_TOAST_MS = 600_000;

// The board with a button that refetches it the way a teammate's event does (CT only: the hub is not connected).
export function BoardHarness({ level = 'stories' }: { level?: 'stories' | 'features' | 'epics' }) {
  const queryClient = useQueryClient();
  return (
    <>
      <button
        type="button"
        data-testid="harness-refetch"
        onClick={() =>
          void queryClient.invalidateQueries({ queryKey: boardRootKey(PROJECT.id, BOARD_ID) })
        }
      >
        Refetch
      </button>
      <ToastHost durationMs={LONG_TOAST_MS} />
      <BoardView project={PROJECT} level={level} />
    </>
  );
}
