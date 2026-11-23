import { onCLS, onFCP, onINP, onLCP, onTTFB } from 'web-vitals';
import { createReportBuffer } from './reportBuffer';
import { postWebVitals } from './webVitalsApi';

let started = false;

/**
 * Measures Core Web Vitals for this tab and sends them, per screen and navigation, when the page is hidden or left.
 * Runs at most once per tab: React strict mode runs the effect that calls it twice.
 */
export function startReporter(): void {
  if (started) return;
  started = true;
  const buffer = createReportBuffer({
    startPathname: location.pathname,
    screenSize: `${String(screen.width)}x${String(screen.height)}`,
    send: (reports) => void postWebVitals(reports),
  });
  const add = buffer.add;
  onLCP(add, { reportSoftNavs: true });
  onINP(add, { reportSoftNavs: true });
  onCLS(add, { reportSoftNavs: true });
  onFCP(add);
  onTTFB(add);
  // On the document, not the window: the library reports its last LCP, INP and CLS from capture listeners on the
  // window, which run first, so this flush carries them.
  document.addEventListener('visibilitychange', () => {
    if (document.visibilityState === 'hidden') buffer.flush();
  });
}
