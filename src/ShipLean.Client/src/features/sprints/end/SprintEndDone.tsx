import { Dialog } from '../../../components/Dialog';
import { sprintsTestIds as ids } from '../testIds';

/** What a person sees when a teammate closed the sprint while their dialog was open: it is done, nothing more to do. */
export function SprintEndDone({
  sprintName,
  onClose,
}: {
  sprintName: string;
  onClose: () => void;
}) {
  return (
    <Dialog
      title={`${sprintName} is closed`}
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
        {`Someone on the team just closed ${sprintName}. There is nothing left to decide.`}
      </p>
    </Dialog>
  );
}
