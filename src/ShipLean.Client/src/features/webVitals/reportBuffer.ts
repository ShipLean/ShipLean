import { matchRoute } from '../../routes';
import type { WebVitalsReport } from './webVitalsApi';

/** The part of a web-vitals metric the buffer reads; the library's `Metric` fits it. */
export interface VitalsMetric {
  name: 'CLS' | 'FCP' | 'INP' | 'LCP' | 'TTFB';
  value: number;
  navigationType: string;
  navigationId?: number;
  navigationURL?: string;
}

export interface ReportBufferOptions {
  /** `location.pathname` when the reporter started: the screen of a metric without a navigation URL. */
  startPathname: string;
  /** `${screen.width}x${screen.height}`. */
  screenSize: string;
  /** Sends one POST's worth of reports (at most `maxReports`). */
  send: (reports: WebVitalsReport[]) => void;
}

export interface ReportBuffer {
  add(metric: VitalsMetric): void;
  flush(): void;
}

/** The most reports one POST carries, and the count at which the buffer sends without waiting for the page to hide. */
export const maxReports = 20;

const fieldOf = { CLS: 'cls', FCP: 'fcp', INP: 'inp', LCP: 'lcp', TTFB: 'ttfb' } as const;

function pathnameOf(url: string): string {
  return new URL(url, 'http://localhost').pathname;
}

/**
 * Collects metrics into one report per navigation and hands them to `send`. Each metric counts at most once per
 * navigation: a later value for a metric already taken, sent or not, is ignored. The report names only the screen's
 * route id, never the real path, so no work item key or query leaves the browser.
 */
export function createReportBuffer({
  startPathname,
  screenSize,
  send,
}: ReportBufferOptions): ReportBuffer {
  const pending = new Map<string, WebVitalsReport>();
  const taken = new Set<string>();

  function flush() {
    const reports = [...pending.values()];
    pending.clear();
    for (let i = 0; i < reports.length; i += maxReports) send(reports.slice(i, i + maxReports));
  }

  function add(metric: VitalsMetric) {
    const key =
      metric.navigationId !== undefined
        ? `id:${String(metric.navigationId)}`
        : metric.navigationURL !== undefined
          ? `url:${metric.navigationURL}`
          : 'load';
    const navigation = metric.navigationType === 'soft-navigation' ? 'soft' : 'hard';
    const field = fieldOf[metric.name];
    // The library reports TTFB as 0 for a soft navigation: there was no request.
    if (navigation === 'soft' && field === 'ttfb') return;
    const takenKey = `${key}|${field}`;
    if (taken.has(takenKey)) return;
    const pathname =
      metric.navigationURL !== undefined ? pathnameOf(metric.navigationURL) : startPathname;
    const screen = matchRoute(pathname)?.id;
    if (screen === undefined) return;
    taken.add(takenKey);
    let report = pending.get(key);
    if (report === undefined) {
      report = {
        screen,
        navigation,
        lcp: null,
        inp: null,
        cls: null,
        fcp: null,
        ttfb: null,
        screenSize,
      };
      pending.set(key, report);
    }
    report[field] = metric.value;
    if (pending.size >= maxReports) flush();
  }

  return { add, flush };
}
