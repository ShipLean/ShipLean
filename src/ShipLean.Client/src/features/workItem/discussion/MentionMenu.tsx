import type { UserRef } from '../../../api/refs';
import { Avatar } from '../../../components/Avatar';
import { workItemTestIds as ids } from '../testIds';

// The people the typed @name could mean. mousedown picks (the textarea keeps focus); the keyboard is the composer's.
export function MentionMenu({
  people,
  highlighted,
  onPick,
}: {
  people: UserRef[];
  highlighted: number;
  onPick: (person: UserRef) => void;
}) {
  return (
    <div
      data-testid={ids.mentionMenu}
      role="listbox"
      aria-label="Mention"
      className="absolute top-full left-0 z-10 mt-1 grid min-w-48 gap-0.5 rounded-control border border-edge bg-panel p-1 shadow-md"
    >
      {people.map((person, index) => (
        <button
          key={person.id}
          type="button"
          role="option"
          aria-selected={index === highlighted}
          data-testid={ids.mentionOption}
          className={`flex items-center gap-2 rounded-control px-2 py-1 text-left text-[length:var(--text-sm)] ${index === highlighted ? 'bg-signal-soft' : 'hover:bg-signal-soft'}`}
          onMouseDown={(event) => {
            event.preventDefault();
            onPick(person);
          }}
        >
          <Avatar {...person} />
          {person.name}
        </button>
      ))}
    </div>
  );
}
