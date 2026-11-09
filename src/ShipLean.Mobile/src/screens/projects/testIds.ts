// Screen-local test ids for Projects. The root id (screen-projects) is in ../testIds.ts.
export const projectsIds = {
  loading: 'projects-loading',
  list: 'projects-list',
  avatar: 'projects-avatar',
} as const;

export const projectRowId = (key: string) => `project-row-${key}`;
