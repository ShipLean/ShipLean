import { Link } from 'react-router-dom';
import { settingsTestIds as ids } from '../testIds';

export function NewProjectBlocked() {
  return (
    <div
      data-testid={ids.newProjectBlocked}
      className="mx-auto mt-8 grid max-w-xl gap-2 rounded-panel border border-edge bg-panel p-6 text-center"
    >
      <h1 className="text-[length:var(--text-lg)] font-semibold text-ink">
        Only instance admins can create projects here
      </h1>
      <p className="text-[length:var(--text-sm)] text-ink-muted">
        This instance is set so that new projects come from an admin. Ask one, and tell them the
        name and key you would like.
      </p>
      <div>
        <Link to="/" className="text-[length:var(--text-sm)] text-signal">
          Back
        </Link>
      </div>
    </div>
  );
}
