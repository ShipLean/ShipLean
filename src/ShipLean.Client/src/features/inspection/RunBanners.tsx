import type { RunPageDto } from '@shiplean/core/api/inspection';
import { inspectionTestIds as ids } from './testIds';
import './timeline/timeline.css';

export interface RunBannersProps {
  page: RunPageDto;
}

// What a log-only agent doesn't send (D13, D19) and a truncated trace with its Grafana link. An expired run and an
// unreachable store replace the whole page, so the run page shows those and never mounts these.
export function RunBanners({ page }: RunBannersProps) {
  const notice = page.logOnly ? page.banner : null;
  if (notice === null && !page.truncated) return null;
  return (
    <div data-testid={ids.runBanner} data-log-only={page.logOnly} data-truncated={page.truncated}>
      {notice === null ? null : (
        <div className="rb" role="note">
          <span>{notice}</span>
        </div>
      )}
      {page.truncated ? (
        <div className="rb warn" role="note" data-testid={ids.runTruncated}>
          <b>Only part of this trace came back.</b>
          <span>
            The store&apos;s per-trace size limit cut it short, so the end of the run is missing.
          </span>
          <span className="rb-spacer" />
          {page.grafanaUrl === null ? null : (
            <a
              href={page.grafanaUrl}
              target="_blank"
              rel="noreferrer"
              data-testid={ids.runGrafanaLink}
            >
              Open in Grafana ↗
            </a>
          )}
        </div>
      ) : null}
    </div>
  );
}
