// GET /api/search. C#: ShipLean.Contracts.Views.Search. Urls are relative SPA paths.
import type { StateCategory, SprintStatus, WorkItemTypeId } from './refs';
import { api } from './client';

export type SearchMatchedIn = 'title' | 'tags' | 'description' | 'comment' | 'attachment';

export interface SearchProjectDto {
  key: string;
  name: string;
  url: string;
}

export interface SearchSprintDto {
  name: string;
  goal: string;
  /** yyyy-MM-dd. */
  startDate: string;
  /** yyyy-MM-dd. */
  endDate: string;
  status: SprintStatus;
  datesLabel: string;
  projectKey: string;
  url: string;
}

export interface SearchItemDto {
  key: string;
  title: string;
  type: WorkItemTypeId;
  state: string;
  stateCategory: StateCategory;
  projectKey: string;
  /** Keys of the ancestors from the top down; empty for a top-level item. */
  parentPath: string[];
  matchedIn: SearchMatchedIn;
  snippet: string;
  url: string;
  /** The file name when an attachment matched. */
  attachment: string | null;
}

export interface SearchViewDto {
  query: string;
  semantic: boolean;
  projects: SearchProjectDto[];
  sprints: SearchSprintDto[];
  items: SearchItemDto[];
  more: boolean;
}

export interface SearchParams {
  q: string;
  prefix: boolean;
  closed: boolean;
  limit: number;
}

export function fetchSearch(params: SearchParams, signal?: AbortSignal): Promise<SearchViewDto> {
  const search = new URLSearchParams({
    q: params.q,
    prefix: String(params.prefix),
    closed: String(params.closed),
    limit: String(params.limit),
  });
  return api.get<SearchViewDto>(`/api/search?${search}`, { signal });
}
