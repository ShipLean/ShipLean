import { Dialog } from '../../../components/Dialog';
import { sprintsTestIds as ids } from '../testIds';

/** What a teammate sees when the rollover was applied while their dialog was open: it is done, nothing more to do. */
export function SprintEndDone({
  sprintName,
  onClose,
}: {
  sprintName: string;
  onClose: () => void;
}) {
  return (
    <Dialog
      title={`${sprintName} has ended`}
      onClose={onClose}
      footer={
        <>
          <span className="flex-1" />
          <button
            type="button"
            data-testid={ids.endDoneClose}
            className="btn-signal"
            onClick={onClose}
          >
            Close
          </button>
        </>
      }
    >
      <p data-testid={ids.endDone} role="status" className="m-0">
        {`The unfinished items in ${sprintName} were just moved by someone on the team. There is nothing left to decide.`}
      </p>
    </Dialog>
  );
}
