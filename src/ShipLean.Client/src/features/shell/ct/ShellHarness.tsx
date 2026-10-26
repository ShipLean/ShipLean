import { useState } from 'react';
import { Route, Routes, useLocation } from 'react-router-dom';
import { Dialog } from '../../../components/Dialog';
import { Shell } from '../../../Shell';
import { shellHarnessIds as ids } from './harnessIds';

// A routed page that says where it is, so a test can see where the shell sent it, and can open a modal dialog.
function Probe() {
  const { pathname, search } = useLocation();
  const [dialog, setDialog] = useState(false);
  return (
    <div data-testid={ids.page}>
      <span data-testid={ids.location}>{pathname + search}</span>
      <button type="button" data-testid={ids.openDialog} onClick={() => setDialog(true)}>
        Open dialog
      </button>
      {dialog ? (
        <Dialog title="A modal" onClose={() => setDialog(false)}>
          <p data-testid={ids.dialogBody}>Modal</p>
        </Dialog>
      ) : null}
    </div>
  );
}

// The shell as AppRoutes mounts it (a pathless layout route), with a probe for every page.
export function ShellHarness() {
  return (
    <Routes>
      <Route element={<Shell />}>
        <Route path="/p/:key/*" element={<Probe />} />
        <Route path="*" element={<Probe />} />
      </Route>
    </Routes>
  );
}
