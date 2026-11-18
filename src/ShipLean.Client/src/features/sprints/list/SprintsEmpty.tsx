import { useState } from 'react';
import { NewSprintRail } from '../newSprint/NewSprintRail';
import { sprintsTestIds as ids } from '../testIds';

/** A project with no sprints at 900px and below (SHIP-1807 P7): one panel saying what a sprint is, with + New sprint. */
export function SprintsEmpty({ projectId }: { projectId: string }) {
  const [creating, setCreating] = useState(false);
  return (
    <section
      data-testid={ids.listEmpty}
      className="grid justify-items-start gap-2 rounded-md border border-dashed border-edge bg-panel px-3 py-4"
    >
      <h2 className="m-0 text-[length:var(--text-md)] font-semibold">No sprints yet</h2>
      <p className="m-0 text-ink-muted">
        A sprint is a timebox the team commits points to. Create the first one; velocity shows here
        once three have ended.
      </p>
      <button
        type="button"
        data-testid={ids.listEmptyNew}
        className="btn-signal h-[var(--touch-target)]"
        onClick={() => setCreating(true)}
      >
        + New sprint
      </button>
      {creating ? <NewSprintRail projectId={projectId} onClose={() => setCreating(false)} /> : null}
    </section>
  );
}
