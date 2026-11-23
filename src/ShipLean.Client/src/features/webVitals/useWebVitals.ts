import { useQuery } from '@tanstack/react-query';
import { useEffect } from 'react';
import { getWebVitalsConfig, webVitalsConfigKey } from './webVitalsApi';

// Asks once per page load whether the instance wants web vitals and, when it does, loads the reporter chunk and
// starts it. A failed config request or a blocked chunk (an ad blocker may block it) is ignored: measuring is never
// worth breaking the app or showing the person anything.
export function useWebVitals() {
  const enabled = useQuery({
    queryKey: webVitalsConfigKey,
    queryFn: getWebVitalsConfig,
    retry: false,
    staleTime: Infinity,
  }).data?.enabled;
  useEffect(() => {
    if (enabled !== true) return;
    import('./startReporter').then(({ startReporter }) => startReporter()).catch(() => undefined);
  }, [enabled]);
}
