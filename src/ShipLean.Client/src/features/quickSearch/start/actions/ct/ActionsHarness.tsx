import { useState } from 'react';
import { Route, Routes, useLocation } from 'react-router-dom';
import { useSyncExternalStore } from 'react';
import { getToasts, subscribeToasts } from '../../../../../components/toastStore';
import { useActionSource } from '../../useActionSource';
import { actionsHarnessIds as ids } from './harnessIds';

// The real action source with a button per action, built like the dialog host: the outer component owns the
// toasts, the location and a done flag, and the child that calls the hook unmounts as soon as a row has run,
// as the dialog's onDone does. It sits under /p/:key/*, so mount it on /p/SHIP/... for the project to resolve.
function Rows({ onDone }: { onDone: () => void }) {
  const rows = useActionSource('').groups.flatMap((group) => group.rows);
  return (
    <>
      {rows.map((row) =>
        row.kind === 'action' ? (
          <div key={row.id}>
            <button
              data-testid={ids.pick(row.id)}
              onClick={() => {
                row.run();
                onDone();
              }}
            >
              {row.label}
            </button>
            <span data-testid={ids.detail(row.id)}>{row.detail}</span>
          </div>
        ) : null,
      )}
    </>
  );
}

function Panel() {
  const location = useLocation();
  const toasts = useSyncExternalStore(subscribeToasts, getToasts);
  const [done, setDone] = useState(false);
  return (
    <div>
      <span data-testid={ids.location}>{location.pathname + location.search}</span>
      <span data-testid={ids.toasts}>{toasts.map((toast) => toast.message).join('|')}</span>
      {done ? null : <Rows onDone={() => setDone(true)} />}
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
