import { useQuery } from '@tanstack/react-query';
import { fetchInstance, instanceKey } from './authApi';
import { authTestIds } from './testIds';

// The sign-in page's top bar: the mark and the instance's name, so people know which install they are signing in to.
export function LoginTopBar() {
  const instance = useQuery({ queryKey: instanceKey, queryFn: fetchInstance, retry: false });
  return (
    <header className="flex h-(--bar-height) flex-none items-center gap-2 border-b border-edge bg-panel px-4">
      <span className="flex items-center gap-[7px] text-[length:var(--text-md)] font-bold tracking-tight">
        <i className="inline-block h-4 w-4 rounded bg-signal" />
        ShipLean
      </span>
      {instance.data ? (
        <span
          data-testid={authTestIds.instanceName}
          className="text-[length:var(--text-sm)] text-ink-muted"
        >
          <span aria-hidden="true" className="mr-2">
            ·
          </span>
          {instance.data.name}
        </span>
      ) : null}
    </header>
  );
}
