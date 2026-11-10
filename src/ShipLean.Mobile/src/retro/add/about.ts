// Pure: what a typed retro point is about (P-R1). The first `<PROJECTKEY>-<n>` in the text names an item; with none,
// the point is about the shown sprint. Never both.
export interface PointAbout {
  key: string | null;
  sprintId: string | null;
}

const escape = (s: string) => s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');

// The first key of this project in the text, as written but upper-cased, or null.
export function keyNamedIn(text: string, projectKey: string | undefined): string | null {
  if (projectKey === undefined || projectKey === '') return null;
  const match = new RegExp(`(?<![A-Za-z0-9])${escape(projectKey)}-(\\d+)(?![A-Za-z0-9])`, 'i').exec(
    text,
  );
  return match === null ? null : `${projectKey.toUpperCase()}-${match[1]}`;
}

export function aboutOf(
  text: string,
  projectKey: string | undefined,
  sprintId: string | null,
): PointAbout {
  const key = keyNamedIn(text, projectKey);
  return key === null ? { key: null, sprintId } : { key, sprintId: null };
}
