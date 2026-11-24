import type { ReactNode } from 'react';
import type { ProjectDto } from '@shiplean/core/api/projects';
import { promptsTestIds as ids } from '../testIds';

// The Ways of working page header (SHIP-2280 Contract 7, design prompts.html .head): a band on the panel surface, ruled off from the page under it,
// with the title, what the page holds for this project, the purple "N custom" pill, and at the right the amber unsaved pill
// and the search box. Under 900px the title and subtitle each take a row, the custom pill and the unsaved pill share one (custom left, unsaved right) and the search runs full width (mock-up phone).
export function WaysOfWorkingHeader({
  project,
  customCount,
  unsaved,
  search,
}: {
  project: ProjectDto;
  customCount: number;
  unsaved: boolean;
  search: ReactNode;
}) {
  return (
    <header
      data-testid={ids.header}
      aria-label="Ways of working"
      className="flex min-w-0 flex-wrap items-center gap-3 border-b border-edge bg-panel px-4 py-3"
    >
      <h1 className="m-0 touch:basis-full text-[length:var(--text-lg)] font-semibold">
        Ways of working
      </h1>
      <span className="text-[length:var(--text-sm)] text-ink-muted touch:basis-full">
        The prompts agents in {project.name} get, in the order they meet them
      </span>
      {customCount > 0 ? (
        <span
          data-testid={ids.customCountPill}
          className="inline-flex h-6 items-center gap-1.5 self-center rounded-full bg-label-purple/[18%] px-2.5 text-[length:var(--text-xs)] font-medium whitespace-nowrap"
        >
          <span aria-hidden="true" className="size-1.5 rounded-full bg-label-purple" />
          {customCount} custom
        </span>
      ) : null}
      <span className="flex-1" />
      {unsaved ? (
        <span
          data-testid={ids.unsavedPill}
          className="inline-flex h-6 items-center gap-1.5 self-center rounded-full bg-caution/20 px-2.25 text-[length:var(--text-xs)] font-semibold whitespace-nowrap"
        >
          <span aria-hidden="true" className="text-caution">
            ●
          </span>{' '}
          Unsaved custom prompt
        </span>
      ) : null}
      {search}
    </header>
  );
}
