import type { ReactNode } from 'react';
import type { ProjectDto } from '@shiplean/core/api/projects';
import { promptsTestIds as ids } from '../testIds';

// The Ways of working page header (SHIP-2280 Contract 7): the title, what the page holds for this project, the purple
// "N custom" pill, and at the right the unsaved pill and the search box.
// TODO: structure only; the header styling (pills, phone layout) replaces these plain classes.
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
      className="flex min-w-0 flex-wrap items-center gap-x-3 gap-y-2"
    >
      <h1 className="m-0 text-[length:var(--text-lg)] font-semibold">Ways of working</h1>
      <span className="text-[length:var(--text-sm)] text-ink-muted">
        The prompts agents in {project.name} get, in the order they meet them
      </span>
      {customCount > 0 ? (
        <span
          data-testid={ids.customCountPill}
          className="text-[length:var(--text-xs)] font-medium text-label-purple"
        >
          {customCount} custom
        </span>
      ) : null}
      <span className="flex-1" />
      {unsaved ? (
        <span
          data-testid={ids.unsavedPill}
          className="text-[length:var(--text-xs)] font-medium text-warn"
        >
          ● Unsaved custom prompt
        </span>
      ) : null}
      {search}
    </header>
  );
}
