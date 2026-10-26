import type { UserRef } from '../../../api/refs';
import { Avatar } from '../../../components/Avatar';
import { taskboardTestIds as ids } from '../testIds';
import { UNASSIGNED_FACE } from './faceFilter';

/** The people across the top (and Unassigned): pressing faces filters the cards by assignee. */
export function FaceFilter({
  people,
  chosen,
  onToggle,
}: {
  people: UserRef[];
  chosen: ReadonlySet<string>;
  onToggle: (faceId: string) => void;
}) {
  return (
    <div data-testid={ids.faces} role="group" aria-label="Show tasks for" className="flex gap-1">
      {[...people.map((p) => ({ id: p.id, person: p })), { id: UNASSIGNED_FACE, person: null }].map(
        ({ id, person }) => (
          <button
            key={id}
            type="button"
            data-testid={ids.face}
            data-face={id}
            aria-pressed={chosen.has(id)}
            title={person?.name ?? 'Unassigned'}
            className={`rounded-full p-0.5 ${chosen.has(id) ? 'ring-2 ring-signal' : 'opacity-80'}`}
            onClick={() => onToggle(id)}
          >
            <Avatar {...(person ?? {})} />
          </button>
        ),
      )}
    </div>
  );
}
