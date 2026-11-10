import { act, screen } from '@testing-library/react-native';
import { useEffect, useState } from 'react';
import { Text } from 'react-native';
import { useActionRunner } from '../../actions/ActionRunnerProvider';
import type { ActionRunner, AppAction, Executor } from '../../actions/types';
import { renderScreen } from '../../test-support/renderScreen';
import { primitiveIds } from '../../ui/testIds';
import { useToast } from '../../ui/Toast';
import { RunnerProvider } from './runner';

let mockSession: { origin: string | null; email: string } = {
  origin: 'https://ship.test',
  email: 'tom@ship.test',
};
jest.mock('../../session/useSession', () => ({ useSession: () => mockSession }));

const seen: ActionRunner[] = [];
function Probe() {
  seen.push(useActionRunner());
  return <Text testID="probe">probe</Text>;
}

// Re-renders the slot from inside the tree, so it updates in place the way a session change does.
let bump: () => void = () => {};
function Host({ children }: { children: () => React.ReactNode }) {
  const [, setN] = useState(0);
  useEffect(() => {
    bump = () => setN((n) => n + 1);
  }, []);
  return <RunnerProvider projectKey="SHIP">{children()}</RunnerProvider>;
}

function slot(child: () => React.ReactNode) {
  return <Host>{child}</Host>;
}

beforeEach(() => {
  seen.length = 0;
  mockSession = { origin: 'https://ship.test', email: 'tom@ship.test' };
  jest.useFakeTimers({ now: new Date('2030-01-01T09:00:00.000Z') });
});
afterEach(() => jest.useRealTimers());

describe('RunnerProvider', () => {
  it('gives children the same runner across re-renders of one session', async () => {
    await renderScreen(slot(() => <Probe />));
    await act(async () => bump());
    expect(seen.length).toBeGreaterThan(1);
    expect(new Set(seen).size).toBe(1);
  });

  it('builds a new runner when the signed-in email changes', async () => {
    await renderScreen(slot(() => <Probe />));
    mockSession = { origin: 'https://ship.test', email: 'ann@ship.test' };
    await act(async () => bump());
    expect(new Set(seen).size).toBe(2);
  });

  it('lets a child register an executor through useActionRunner', async () => {
    const executor: Executor<AppAction> = {
      area: 'workItem',
      send: async () => {},
      describe: () => ({ id: 'line', icon: 'dot', text: 'described', removable: true }),
      reversible: () => false,
    };
    function Registers() {
      const runner = useActionRunner();
      runner.register(executor);
      return null;
    }
    await renderScreen(
      slot(() => (
        <>
          <Registers />
          <Probe />
        </>
      )),
    );
    const action = { area: 'workItem', kind: 'setState', itemId: 'i', key: 'SHIP-1' } as const;
    expect(seen[0]!.describe(action).text).toBe('described');
  });

  it('renders the toast host inside the slot', async () => {
    function Shows() {
      const toast = useToast();
      useEffect(() => toast.show({ text: 'Moved to Done', ms: 4000 }), [toast]);
      return null;
    }
    await renderScreen(slot(() => <Shows />));
    await act(async () => {});
    expect(screen.getByTestId(primitiveIds.toast)).toBeTruthy();
    expect(screen.getByText('Moved to Done')).toBeTruthy();
  });
});
