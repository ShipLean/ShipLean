import { useEffect, useRef } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { setTimeZone } from '@shiplean/core/api/me';
import { useMe } from '../useShellQueries';
import { browserZone } from './browserZone';

// Once ['me'] answers with no zone, stores the browser's zone (source browser, which the server applies only while
// none is set) once per page load, then re-reads every query: screens loaded before it hold UTC labels. A zone the
// server refuses is left alone; the Profile page still shows "Not set".
export function useBrowserZoneOnLoad() {
  const me = useMe().data;
  const queryClient = useQueryClient();
  const sent = useRef(false);
  useEffect(() => {
    if (me?.timeZone !== null || sent.current) return;
    const zone = browserZone();
    if (zone === null) return;
    sent.current = true;
    void setTimeZone(zone, 'browser').then(
      () => queryClient.invalidateQueries(),
      () => undefined,
    );
  }, [me, queryClient]);
}
