import { aHealth, aMe, aProject } from './builders';
import type { ApiTable } from './fakeApi';

// What a signed-in app reads: me, the projects and the server's health. Spread it and override routes as needed:
// fakeApi({ ...signedInApi(), 'GET /api/projects': [] }).
export function signedInApi(): ApiTable {
  return {
    'GET /api/me': aMe(),
    'GET /api/projects': [
      aProject(),
      aProject({ id: '0199f000-0000-7000-8000-0000000000a2', key: 'OPS', name: 'Operations' }),
    ],
    'GET /api/health': aHealth(),
  };
}
