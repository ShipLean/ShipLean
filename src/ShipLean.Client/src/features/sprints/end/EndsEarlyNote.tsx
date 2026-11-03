import { sprintsTestIds as ids } from '../testIds';

/** What closing a sprint that is still running does to its dates. */
export function EndsEarlyNote({ sprintName }: { sprintName: string }) {
  return (
    <div
      data-testid={ids.endEarly}
      className="rounded-panel border border-edge border-l-[3px] border-l-caution bg-sunken px-3 py-2 text-[length:var(--text-sm)]"
    >
      <b className="block">{`${sprintName} is still running`}</b>
      Closing it now moves its end date to today, which shortens its burndown.
    </div>
  );
}
