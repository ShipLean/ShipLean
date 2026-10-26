import type { ProjectSummaryDto } from '../../../api/projects';

export interface PickerGroup {
  // Absent while searching: a flat list of matches.
  title: string | undefined;
  projects: ProjectSummaryDto[];
}

const matches = (project: ProjectSummaryDto, query: string) =>
  project.name.toLowerCase().includes(query) || project.key.toLowerCase().includes(query);

// With no search: Recent (the saved ids, most recent first, skipping projects you are no longer in), then All your
// projects alphabetically (those not already under Recent). With a search: the projects whose key or name contains it.
export function pickerGroups(
  projects: ProjectSummaryDto[],
  recentIds: string[],
  search: string,
): PickerGroup[] {
  const query = search.trim().toLowerCase();
  if (query !== '') {
    return [{ title: undefined, projects: projects.filter((p) => matches(p, query)) }];
  }
  const recent = recentIds
    .map((id) => projects.find((p) => p.id === id))
    .filter((p): p is ProjectSummaryDto => p !== undefined);
  const rest = projects
    .filter((p) => !recent.includes(p))
    .sort((a, b) => a.name.localeCompare(b.name));
  return [
    ...(recent.length > 0 ? [{ title: 'Recent', projects: recent }] : []),
    { title: 'All your projects', projects: rest },
  ];
}
