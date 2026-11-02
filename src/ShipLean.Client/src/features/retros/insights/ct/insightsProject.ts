import type { ProjectDto } from '../../../../api/projects';

export const insightsProject: ProjectDto = {
  id: '00000002-0000-4000-8001-000000000001',
  key: 'SHIP',
  name: 'ShipLean',
  description: '',
  role: 'admin',
  memberCount: 5,
  workingDays: [1, 2, 3, 4, 5],
  velocityWindow: 3,
  velocityOverride: null,
  nextItemNumber: 200,
};
