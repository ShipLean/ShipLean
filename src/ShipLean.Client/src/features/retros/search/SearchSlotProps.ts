import type { ProjectDto } from '../../../api/projects';
import type { RetroSearchQuery } from '../../../api/retrosApi';
import type { RetroSearchDto } from '../../../api/retros';

// What the parts of the search screen are given: the project, the filters as the address holds them, the result, and the
// ways to change them. `change` merges into the filters (an undefined value clears one) and drops the open retro unless
// the patch names it; `select` opens a retro or, with null, closes it.
export interface RetrosSearchSlotProps {
  project: ProjectDto;
  query: RetroSearchQuery;
  search: RetroSearchDto;
  change: (patch: Partial<RetroSearchQuery>) => void;
  select: (retroId: string | null) => void;
}
