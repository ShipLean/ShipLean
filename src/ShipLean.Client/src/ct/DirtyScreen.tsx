import { useBlocker, useNavigate } from 'react-router-dom';
import { blockerIds as ids } from './blockerIds';

// A stand-in for a screen with unsaved changes, for the component test that proves the router blocks navigation
// (useBlocker needs a data router).
export function DirtyScreen() {
  const navigate = useNavigate();
  const blocker = useBlocker(true);
  return (
    <div>
      <button data-testid={ids.leave} onClick={() => void navigate('/elsewhere')}>
        leave
      </button>
      <span data-testid={ids.state}>{blocker.state}</span>
      {blocker.state === 'blocked' && (
        <button data-testid={ids.stay} onClick={() => blocker.reset()}>
          stay
        </button>
      )}
    </div>
  );
}
