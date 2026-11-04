import { showToast } from '../../components/toastStore';
import { useCreateSprintDemo } from './demosApi';
import type { DemoProps } from './DemoProps';
import { demosTestIds as ids } from './testIds';

// TODO: the styled band with the status chip, the item link and the delivery labels.
export function DemoStatusBand({ demo }: DemoProps) {
  const create = useCreateSprintDemo(demo.sprintId);
  return (
    <div data-testid={ids.demoStatusBand}>
      {demo.statusMessage !== null && (
        <p data-testid={ids.demoStatusMessage}>{demo.statusMessage}</p>
      )}
      {demo.actions.canCreate ? (
        <button
          type="button"
          data-testid={ids.demoCreate}
          disabled={create.isPending}
          onClick={() =>
            create.mutate(undefined, {
              onError: (failure) => showToast(failure.message, { tone: 'danger' }),
            })
          }
        >
          Create demo
        </button>
      ) : (
        demo.actions.createBlockedMessage !== null && (
          <p data-testid={ids.demoCreateBlocked}>{demo.actions.createBlockedMessage}</p>
        )
      )}
    </div>
  );
}
