import { Route, Routes, useLocation } from 'react-router-dom';
import { useSyncExternalStore } from 'react';
import { getToasts, subscribeToasts } from '../../../../../components/toastStore';
import { useActionSource } from '../../useActionSource';
import { actionsHarnessIds as ids } from './harnessIds';

// The real action source with a button per action, and the location and toasts written out for a test to read.
// It sits under /p/:key/*, so mount it on /p/SHIP/... for the shell project to resolve from the route.
function Panel() {
  const location = useLocation();
  const toasts = useSyncExternalStore(subscribeToasts, getToasts);
  const rows = useActionSource('').groups.flatMap((group) => group.rows);
  return (
    <div>
      <span data-testid={ids.location}>{location.pathname + location.search}</span>
      <span data-testid={ids.toasts}>{toasts.map((toast) => toast.message).join('|')}</span>
      {rows.map((row) =>
        row.kind === 'action' ? (
          <button key={row.id} data-testid={ids.pick(row.id)} onClick={row.run}>
            {row.label}
          </button>
        ) : null,
      )}
    </div>
  );
}

export function ActionsHarness() {
  return (
    <Routes>
      <Route path="/p/:key/*" element={<Panel />} />
    </Routes>
  );
}
