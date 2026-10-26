import type { ProjectDto, ProjectRole } from '../../../../api/projects';

export const PROJECT_ID = '00000002-0000-4000-8000-000000000001';

export const projectDto = (role: ProjectRole): ProjectDto => ({
  id: PROJECT_ID,
  key: 'SHIP',
  name: 'Storefront',
  description: '',
  role,
  memberCount: 2,
  workingDays: [1, 2, 3, 4, 5],
  velocityWindow: 3,
  velocityOverride: null,
  nextItemNumber: 184,
});
