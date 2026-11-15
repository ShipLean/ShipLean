import type { RunPageDto } from '@shiplean/core/api/inspection';
import { inspectionTestIds as ids } from './testIds';

export interface RunBannersProps {
  page: RunPageDto;
}

// What the agent does not send (log-only runs), a truncated trace, or an expired run.
// TODO: the banners replace this stub.
export function RunBanners({ page }: RunBannersProps) {
  return <div data-testid={ids.runBanner} data-log-only={page.logOnly} />;
}
