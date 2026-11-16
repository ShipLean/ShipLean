import { useEffect } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { setTimeZone } from '@shiplean/core/api/me';
import { useMe } from '../useShellQueries';
import { browserZone } from './browserZone';

// The one capture of this page load, set before the call: a shell that mounts again, or a refetched ['me'] that still
// answers no zone, does not send again, and an empty or refused result is not retried.
let attempted = false;

// Once ['me'] answers with no zone, stores the browser's zone (source browser, which the server applies only while
// none is set) once per page load, then re-reads every query: screens loaded before it hold UTC labels. A zone the
// server refuses is left alone; the Profile page still shows "Not set".
export function useBrowserZoneOnLoad() {
  const me = useMe().data;
  const queryClient = useQueryClient();
  useEffect(() => {
    if (me?.timeZone !== null || attempted) return;
    attempted = true;
    const zone = browserZone();
    if (zone === null) return;
    void setTimeZone(zone, 'browser').then(
      () => queryClient.invalidateQueries(),
      () => undefined,
    );
  }, [me, queryClient]);
}
