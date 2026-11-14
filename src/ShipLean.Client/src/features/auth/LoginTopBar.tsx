import { useQuery } from '@tanstack/react-query';
import { useTouchShell } from '../../touch/useTouchShell';
import { fetchInstance, instanceKey } from './authApi';
import { authTestIds } from './testIds';

// The sign-in page's top bar: the mark and the instance's name, so people know which install they are signing in to.
export function LoginTopBar() {
  const instance = useQuery({ queryKey: instanceKey, queryFn: fetchInstance, retry: false });
  const { touch } = useTouchShell();
  // On a phone the name is cut with an ellipsis, and left out when the instance is just called "ShipLean" (SHIP-736 D7).
  const name =
    instance.data && !(touch && instance.data.name === 'ShipLean') ? instance.data.name : null;
  return (
    <header
      data-testid={authTestIds.topBar}
      className="flex h-(--bar-height) flex-none items-center gap-2 border-b border-edge bg-panel px-4 touch:h-[calc(var(--mobile-bar-height)+env(safe-area-inset-top))] touch:pt-[env(safe-area-inset-top)]"
    >
      <span className="flex flex-none items-center gap-[7px] text-[length:var(--text-md)] font-bold tracking-tight">
        <i className="inline-block h-4 w-4 rounded bg-signal" />
        ShipLean
      </span>
      {name !== null ? (
        <span
          data-testid={authTestIds.instanceName}
          className="min-w-0 truncate text-[length:var(--text-sm)] text-ink-muted"
        >
          <span aria-hidden="true" className="mr-2">
            ·
          </span>
          {name}
        </span>
      ) : null}
    </header>
  );
}
