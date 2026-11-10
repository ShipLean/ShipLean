import { backlogKeys, type BacklogFilter } from '@shiplean/core/api/backlog';
import { act, fireEvent, screen } from '@testing-library/react-native';
import { UNDO } from '../../actions/undo';
import { aPending, fakeRunner } from '../../test-support/actions';
import { aBacklogView } from '../../test-support/backlog';
import { renderWithRunner } from '../../test-support/renderWithRunner';
import type { Scheme } from '../../theme/themes';
import { optionId } from '../../ui/testIds';
import { BacklogActionsProvider } from '../BacklogActions';
import type { BacklogLevel } from '../actions';
import { addRowErrorId, addRowId, addRowInputId, addRowLabelId, addRowTypeId } from '../testIds';
import { AddRow } from './AddRow';

const PROJECT = '00000002-0000-4000-8000-000000000001';
const FEATURE = { id: 'feature-906', key: 'SHIP-906' };
const TOP = { slot: 'first', beforeItemId: null };

async function open(
  level: BacklogLevel,
  scheme: Scheme,
  parent: { id: string; key: string } | null = null,
) {
  const runner = fakeRunner();
  const view = await renderWithRunner(
    <BacklogActionsProvider>
      <AddRow projectId={PROJECT} level={level} parent={parent} />
    </BacklogActionsProvider>,
    { runner, scheme },
  );
  return { runner, queryClient: view.queryClient };
}

const input = () => screen.getByTestId(addRowInputId);
async function type(text: string) {
  await fireEvent.changeText(input(), text);
  await fireEvent(input(), 'submitEditing');
}

describe.each<Scheme>(['light', 'dark'])('Backlog add row (%s)', (scheme) => {
  it('adds a Feature at the Features level on Enter and keeps the row open', async () => {
    const { runner } = await open('features', scheme);
    expect(screen.getByTestId(addRowLabelId)).toHaveTextContent('+ New Feature');
    expect(input().props.placeholder).toBe('Title, then Enter to add at the top');
    await type('  Offline queue  ');
    expect(runner.dispatched).toHaveLength(1);
    expect(runner.dispatched[0].batch).toEqual([
      {
        area: 'backlog',
        kind: 'addItem',
        itemId: null,
        key: null,
        projectId: PROJECT,
        type: 'feature',
        title: 'Offline queue',
        parentId: null,
        parentKey: null,
        placement: TOP,
      },
    ]);
    expect(runner.dispatched[0].opts).toMatchObject({ origin: 'typed', undo: UNDO.typed });
    // The row clears and stays open for the next title.
    expect(input().props.value).toBe('');
    expect(screen.getByTestId(addRowId)).toBeTruthy();
    await type('Second one');
    expect(runner.dispatched).toHaveLength(2);
  });

  it('offers Story and Bug at the Stories level', async () => {
    const { runner } = await open('stories', scheme);
    expect(screen.getByTestId(optionId(addRowTypeId, 'story'))).toHaveProp(
      'accessibilityState',
      expect.objectContaining({ selected: true }),
    );
    await type('A story');
    await fireEvent.press(screen.getByTestId(optionId(addRowTypeId, 'bug')));
    await type('A bug');
    expect(runner.dispatched.map((d) => (d.batch[0] as { type: string }).type)).toEqual([
      'story',
      'bug',
    ]);
  });

  it('uses the filtered Feature as the parent', async () => {
    const { runner } = await open('stories', scheme, FEATURE);
    await type('Under the Feature');
    expect(runner.dispatched[0].batch[0]).toMatchObject({
      parentId: FEATURE.id,
      parentKey: FEATURE.key,
    });
  });

  it('adds nothing for a blank title', async () => {
    const { runner } = await open('features', scheme);
    await type('');
    await type('   ');
    expect(runner.dispatched).toHaveLength(0);
  });

  it('shows the server validation message under the row', async () => {
    const { runner } = await open('stories', scheme);
    await type('x'.repeat(300));
    expect(screen.queryByTestId(addRowErrorId)).toBeNull();
    await act(async () => {
      runner.setPending([
        aPending({
          batchId: runner.dispatched[0].batchId,
          state: 'failed',
          failure: 'validation',
          error: 'The title is too long.',
        }),
      ]);
    });
    expect(await screen.findByTestId(addRowErrorId)).toHaveTextContent('The title is too long.');
    // Typing again clears it.
    await fireEvent.changeText(input(), 'short');
    expect(screen.queryByTestId(addRowErrorId)).toBeNull();
  });

  it("reads the backlog again once its add is sent, and not for another row's batch", async () => {
    const { runner, queryClient } = await open('features', scheme);
    const filter: BacklogFilter = { mode: 'list', filter: '', includeCompleted: false };
    const key = backlogKeys.level(PROJECT, 'features', filter);
    queryClient.setQueryData(key, aBacklogView('features', []));
    await type('Offline queue');
    await act(async () => {
      runner.setPending([aPending({ id: 'other', batchId: 'someone-else', state: 'sent' })]);
    });
    expect(queryClient.getQueryState(key)?.isInvalidated).toBe(false);
    await act(async () => {
      runner.setPending([
        aPending({ id: 'mine', batchId: runner.dispatched[0].batchId, state: 'sent' }),
      ]);
    });
    await screen.findByTestId(addRowId);
    expect(queryClient.getQueryState(key)?.isInvalidated).toBe(true);
  });
});
