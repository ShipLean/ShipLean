import { projectsKey } from '@shiplean/core/api/projects';
import { memoryStore } from '@shiplean/core/platform';
import { act, fireEvent, screen, waitFor } from '@testing-library/react-native';
import type { ReactElement } from 'react';
import { createActionRunner } from '../../actions/runner';
import { UNDO } from '../../actions/undo';
import { aPending, fakeRunner } from '../../test-support/actions';
import { aProject } from '../../test-support/builders';
import { apiError, restoreFetch } from '../../test-support/fakeApi';
import { retroApi } from '../../test-support/retro';
import { renderWithRunner } from '../../test-support/renderWithRunner';
import type { Scheme } from '../../theme/themes';
import { fieldErrorId } from '../../ui/testIds';
import type { RetroAction } from '../actions';
import { RetroActionsProvider } from '../RetroActions';
import { retroAddCategoryId, retroIds } from '../testIds';
import { AddPointSheet } from './AddPointSheet';

afterEach(restoreFetch);

const project = aProject();
const noop = () => undefined;
const sheet = (category: 'improve' | null = null): ReactElement => (
  <RetroActionsProvider>
    <AddPointSheet
      projectId={project.id}
      sprintId="sprint-25"
      open
      onClose={noop}
      category={category}
    />
  </RetroActionsProvider>
);
const type = (text: string) => fireEvent.changeText(screen.getByTestId(retroIds.addText), text);
// Renders the sheet and waits for the projects list its key lookup reads, so nothing updates after the test.
async function renderSheet(category: 'improve' | null, options: { scheme: Scheme }) {
  retroApi({ 'GET /api/projects': [project] }, { projectId: project.id });
  const rendered = await renderWithRunner(sheet(category), options);
  await waitFor(() =>
    expect(rendered.queryClient.getQueryState(projectsKey)?.status).toBe('success'),
  );
  return rendered;
}
const add = () => fireEvent.press(screen.getByTestId(retroIds.addSubmit));

describe.each<Scheme>(['light', 'dark'])('Add point sheet (%s)', (scheme) => {
  it('dispatches addPoint with the category and the text as typed', async () => {
    const { runner } = await renderSheet(null, { scheme });
    await fireEvent.press(screen.getByTestId(retroAddCategoryId('wentBadly')));
    await type('  the S22 build took 40 minutes  ');
    await add();
    expect(runner.dispatched).toHaveLength(1);
    expect(runner.dispatched[0].batch).toEqual<RetroAction[]>([
      {
        area: 'retro',
        kind: 'addPoint',
        itemId: null,
        key: null,
        projectId: project.id,
        sprintId: 'sprint-25',
        category: 'wentBadly',
        categoryGuessed: false,
        text: 'the S22 build took 40 minutes',
      },
    ]);
  });

  it('is about the item the text names, and not the sprint', async () => {
    const { runner } = await renderSheet(null, { scheme });
    await fireEvent.press(screen.getByTestId(retroAddCategoryId('improve')));
    await type('Profile the small model before SHIP-906 gets more voice actions.');
    await waitFor(() => expect(screen.getByText('About SHIP-906.')).toBeTruthy());
    await add();
    expect(runner.dispatched[0].batch[0]).toMatchObject({ key: 'SHIP-906', sprintId: null });
  });

  it('keeps Add off until a category and text are set', async () => {
    const { runner } = await renderSheet(null, { scheme });
    expect(screen.getByTestId(retroIds.addSubmit)).toBeDisabled();
    await type('Only text.');
    expect(screen.getByTestId(retroIds.addSubmit)).toBeDisabled();
    await fireEvent.press(screen.getByTestId(retroAddCategoryId('notInPlan')));
    expect(screen.getByTestId(retroIds.addSubmit)).toBeEnabled();
    await type('   ');
    expect(screen.getByTestId(retroIds.addSubmit)).toBeDisabled();
    await add();
    expect(runner.dispatched).toEqual([]);
  });

  it('preselects the category of the open view and none otherwise', async () => {
    await renderSheet('improve', { scheme });
    expect(screen.getByTestId(retroAddCategoryId('improve'))).toBeSelected();
    expect(screen.getByTestId(retroAddCategoryId('wentWell'))).not.toBeSelected();
  });

  it('dispatches with UNDO.typed and origin typed', async () => {
    const { runner } = await renderSheet('improve', { scheme });
    await type('Say which baselines come from CI.');
    await add();
    expect(runner.dispatched[0].opts).toEqual({
      undo: UNDO.typed,
      origin: 'typed',
      allOrNothing: true,
    });
  });

  it('closes once the send has landed and stays open while it has not', async () => {
    const onClose = jest.fn();
    retroApi({ 'GET /api/projects': [project] }, { projectId: project.id });
    const runner = fakeRunner();
    await renderWithRunner(
      <RetroActionsProvider>
        <AddPointSheet
          projectId={project.id}
          sprintId="sprint-25"
          open
          onClose={onClose}
          category="improve"
        />
      </RetroActionsProvider>,
      { scheme, runner },
    );
    await type('Profile the small model first.');
    await add();
    const batchId = runner.dispatched[0].batchId;
    await act(async () => {
      runner.setPending([aPending({ batchId, state: 'sending' })]);
    });
    expect(onClose).not.toHaveBeenCalled();
    await act(async () => {
      runner.setPending([aPending({ batchId, state: 'sent' })]);
    });
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('shows the server validation message', async () => {
    const fake = retroApi({ 'GET /api/projects': [project] }, { projectId: project.id });
    fake.failNext(
      `POST /api/projects/${project.id}/retros/points`,
      apiError(400, 'validation', 'A point needs some text.'),
    );
    const runner = createActionRunner({
      store: memoryStore(),
      scope: 'https://shiplean.test\ntom@example.com',
      isOffline: () => false,
      onOnline: () => noop,
      now: () => new Date(),
    });
    runner.start();
    const rendered = await renderWithRunner(sheet('improve'), { scheme, runner });
    await waitFor(() =>
      expect(rendered.queryClient.getQueryState(projectsKey)?.status).toBe('success'),
    );
    await type('Profile the small model first.');
    await add();
    const error = await screen.findByTestId(fieldErrorId(retroIds.addText));
    expect(error).toHaveTextContent('A point needs some text.');
    expect(screen.getByTestId(retroIds.addSheet)).toBeTruthy();
    runner.stop();
  });
});
