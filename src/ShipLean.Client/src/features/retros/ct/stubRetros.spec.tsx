import { expect, test } from '@playwright/experimental-ct-react';
import {
  actionDialog,
  insightsWeeks,
  noteStates,
  retroActionDetail,
  retroFeatures,
  retroItem,
  stubRetros,
} from './retroFixtures';

// A page that only fetches, to see what the stub answers.
async function ask(
  page: Parameters<typeof stubRetros>[0],
  method: string,
  path: string,
  body?: unknown,
) {
  return page.evaluate(
    async ({ method, path, body }) => {
      const response = await fetch(path, {
        method,
        headers: { 'content-type': 'application/json' },
        body: body === undefined ? undefined : JSON.stringify(body),
      });
      const text = await response.text();
      return { status: response.status, json: text === '' ? null : (JSON.parse(text) as unknown) };
    },
    { method, path, body },
  );
}

test('stubRetros_WhenTheNewRoutesAreAsked_ShouldAnswerEachWithItsPinnedShape', async ({
  mount,
  page,
}) => {
  const stub = await stubRetros(page);
  await mount(<div />);
  const id = '00000000-0000-4000-8000-000000000001';

  expect(await ask(page, 'GET', '/api/projects/p1/retros/features')).toEqual({
    status: 200,
    json: retroFeatures,
  });
  expect(await ask(page, 'GET', '/api/projects/p1/retros/items/SHIP-180')).toEqual({
    status: 200,
    json: retroItem,
  });
  expect(await ask(page, 'GET', '/api/projects/p1/retros/action-dialog?point=a&point=b')).toEqual({
    status: 200,
    json: actionDialog,
  });
  expect(await ask(page, 'GET', `/api/retros/actions/${id}`)).toEqual({
    status: 200,
    json: retroActionDetail,
  });
  expect(await ask(page, 'POST', '/api/projects/p1/retros/actions', {})).toEqual({
    status: 201,
    json: retroActionDetail,
  });
  expect(await ask(page, 'POST', `/api/retros/actions/${id}/points`, {})).toEqual({
    status: 200,
    json: retroActionDetail,
  });
  expect(await ask(page, 'PUT', `/api/retros/actions/${id}`, {})).toEqual({
    status: 200,
    json: retroActionDetail,
  });
  expect(await ask(page, 'POST', `/api/retros/points/${id}/not-needed`, { reason: 'x' })).toEqual({
    status: 200,
    json: noteStates.notNeeded,
  });
  expect(await ask(page, 'POST', `/api/retros/points/${id}/undo-not-needed`)).toEqual({
    status: 200,
    json: noteStates.undecided,
  });
  for (const verb of ['merge', 'keep-apart']) {
    expect(await ask(page, 'POST', `/api/retros/theme-merge-proposals/${id}/${verb}`)).toEqual({
      status: 204,
      json: null,
    });
  }
  const weeks = await ask(page, 'GET', '/api/projects/p1/retros/insights?range=w6');
  expect(weeks.json).toEqual(insightsWeeks);
  expect(stub.writes()).toHaveLength(6 + 1);
});
