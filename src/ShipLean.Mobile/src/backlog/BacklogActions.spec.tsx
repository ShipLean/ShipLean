import { backlogKeys } from '@shiplean/core/api/backlog';
import { act, waitFor } from '@testing-library/react-native';
import { aPending, fakeRunner } from '../test-support/actions';
import { renderWithRunner } from '../test-support/renderWithRunner';
import type { BacklogAction } from './actions';
import { BacklogActionsProvider } from './BacklogActions';

const PROJECT = '00000002-0000-4000-8000-000000000001';

const addItem: BacklogAction = {
  area: 'backlog',
  kind: 'addItem',
  itemId: null,
  key: null,
  projectId: PROJECT,
  type: 'feature',
  title: 'New',
  parentId: null,
  parentKey: null,
  placement: null,
};

const settled = (id: string, state: 'undo' | 'sent' | 'failed') =>
  aPending({ id, batchId: `batch-${id}`, state, action: addItem });

async function mount(initial = [aPending({ id: 'old', state: 'sent', action: addItem })]) {
  const runner = fakeRunner();
  runner.setPending(initial);
  const view = await renderWithRunner(<BacklogActionsProvider>{null}</BacklogActionsProvider>, {
    runner,
  });
  const spy = jest.spyOn(view.queryClient, 'invalidateQueries');
  return { runner, spy };
}

describe('BacklogActionsProvider refresh', () => {
  // Red when a sent change leaves the project's backlog unread, or a change settled before mount reads it again.
  it('reads the project backlog again when a change is sent, not for one settled at mount', async () => {
    const { runner, spy } = await mount();
    expect(spy).not.toHaveBeenCalled();

    await act(async () => runner.setPending([settled('a', 'undo')]));
    expect(spy).not.toHaveBeenCalled();
    await act(async () => runner.setPending([settled('a', 'sent')]));
    await waitFor(() => expect(spy).toHaveBeenCalledWith({ queryKey: backlogKeys.root(PROJECT) }));
    expect(spy).toHaveBeenCalledTimes(1);
  });

  // Red when a batch of moves reads the list once per move.
  it('reads the backlog once for a batch that settles several changes together', async () => {
    const { runner, spy } = await mount([]);
    const inBatch = (id: string, state: 'undo' | 'sent') =>
      aPending({ id, batchId: 'voice', state, action: addItem });
    await act(async () => runner.setPending([inBatch('a', 'undo'), inBatch('b', 'undo')]));
    await act(async () => runner.setPending([inBatch('a', 'sent'), inBatch('b', 'sent')]));
    await waitFor(() => expect(spy).toHaveBeenCalledTimes(1));
  });

  // Red when a refusal seen once hides the same change refused again after a retry.
  it('reads the backlog again when a change is refused, retried and refused again', async () => {
    const { runner, spy } = await mount([]);
    await act(async () => runner.setPending([settled('a', 'failed')]));
    await waitFor(() => expect(spy).toHaveBeenCalledTimes(1));
    await act(async () => runner.setPending([settled('a', 'undo')]));
    await act(async () => runner.setPending([settled('a', 'failed')]));
    await waitFor(() => expect(spy).toHaveBeenCalledTimes(2));
  });
});
