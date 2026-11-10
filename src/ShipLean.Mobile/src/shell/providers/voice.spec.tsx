import { act, screen } from '@testing-library/react-native';
import { useEffect, useState } from 'react';
import { Text } from 'react-native';
import { renderWithRunner } from '../../test-support/renderWithRunner';
import { requestComposer, useComposerRequest } from '../../voice/typed/composerRequest';
import { VoiceProvider } from './voice';

let mockSession = { origin: 'https://ship.test', email: 'tom@ship.test', signedIn: true };
jest.mock('../../session/useSession', () => ({ useSession: () => mockSession }));

function Pending() {
  const { request } = useComposerRequest();
  return <Text testID="pending">{request?.text ?? 'none'}</Text>;
}

// Re-renders the slot in place, like a session or a project change does.
let rerender: (key: string) => void = () => {};
let bump: () => void = () => {};
function Host() {
  const [key, setKey] = useState('SHIP');
  const [, setN] = useState(0);
  useEffect(() => {
    rerender = setKey;
    bump = () => setN((n) => n + 1);
  }, []);
  return (
    <VoiceProvider projectKey={key}>
      <Pending />
    </VoiceProvider>
  );
}

const ask = () =>
  act(async () =>
    requestComposer({ mode: 'command', text: 'move it', reason: 'micOff', questionNumber: null }),
  );

beforeEach(() => {
  mockSession = { origin: 'https://ship.test', email: 'tom@ship.test', signedIn: true };
});

describe('the voice slot and the pending composer request', () => {
  it('keeps the request while nothing changes', async () => {
    await renderWithRunner(<Host />);
    await ask();
    await act(async () => bump());
    expect(screen.getByTestId('pending')).toHaveTextContent('move it');
  });

  it('clears the request on a project switch', async () => {
    await renderWithRunner(<Host />);
    await ask();
    await act(async () => rerender('OPS'));
    expect(screen.getByTestId('pending')).toHaveTextContent('none');
  });

  it('clears the request on sign-out', async () => {
    await renderWithRunner(<Host />);
    await ask();
    mockSession = { ...mockSession, signedIn: false };
    await act(async () => bump());
    expect(screen.getByTestId('pending')).toHaveTextContent('none');
  });
});
