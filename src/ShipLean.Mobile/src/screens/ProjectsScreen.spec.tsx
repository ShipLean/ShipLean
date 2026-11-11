import { act, fireEvent, screen } from '@testing-library/react-native';
import { aProject } from '../test-support/builders';
import { apiError, fakeApi, type ApiAnswer } from '../test-support/fakeApi';
import { renderApp } from '../test-support/renderApp';
import { signedInApi } from '../test-support/signedInApi';
import { uiIds } from '../ui/testIds';
import { projectRowId, projectsIds } from './projects/testIds';
import { screenIds } from './testIds';

const PROJECTS = 'GET /api/projects';

test('loading', async () => {
  let release: () => void = () => undefined;
  const gate = new Promise<void>((resolve) => {
    release = resolve;
  });
  const api = fakeApi({
    ...signedInApi(),
    [PROJECTS]: async () => {
      await gate;
      return [];
    },
  });
  await renderApp({ api });

  expect(await screen.findByTestId(screenIds.projects)).toBeOnTheScreen();
  expect(screen.getByTestId(projectsIds.loading)).toBeOnTheScreen();
  await act(async () => release());
  expect(await screen.findByTestId(uiIds.emptyState)).toBeOnTheScreen();
});

test('lists key and name', async () => {
  await renderApp();

  const ship = await screen.findByTestId(projectRowId('SHIP'));
  expect(ship).toHaveTextContent(/ShipLean/);
  expect(ship).toHaveTextContent(/SHIP/);
  expect(screen.getByTestId(projectRowId('OPS'))).toHaveTextContent(/Operations/);
  expect(screen.getByTestId(projectsIds.avatar)).toHaveTextContent('JD');
});

test('empty', async () => {
  await renderApp({ api: fakeApi({ ...signedInApi(), [PROJECTS]: [] }) });

  expect(await screen.findByTestId(uiIds.emptyState)).toHaveTextContent(
    "You're not in any projects yet.",
  );
});

test('error then Retry loads', async () => {
  let failing = true;
  const api = fakeApi({
    ...signedInApi(),
    [PROJECTS]: (): ApiAnswer => (failing ? apiError(500, 'internal_error') : [aProject()]),
  });
  await renderApp({ api });

  expect(
    await screen.findByTestId(
      uiIds.errorState,
      {},
      { timeout: 5000 /* react-query retries once after a 1 s delay */ },
    ),
  ).toBeOnTheScreen();
  failing = false;
  await fireEvent.press(screen.getByTestId(uiIds.errorStateAction));

  expect(await screen.findByTestId(projectRowId('SHIP'))).toBeOnTheScreen();
});

test('pull to refresh refetches', async () => {
  const { api } = await renderApp();
  await screen.findByTestId(projectRowId('SHIP'));
  expect(api.callsTo(PROJECTS)).toHaveLength(1);

  const list = screen.getByTestId(projectsIds.list);
  await act(async () => {
    list.props.refreshControl.props.onRefresh();
  });

  expect(api.callsTo(PROJECTS)).toHaveLength(2);
});
