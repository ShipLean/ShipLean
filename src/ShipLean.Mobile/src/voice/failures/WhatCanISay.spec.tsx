import { fireEvent, screen } from '@testing-library/react-native';
import { renderScreen } from '../../test-support/renderScreen';
import type { ScreenVoiceSpec } from '../contracts';
import {
  whatCanISayActionId,
  whatCanISayDoneId,
  whatCanISayEmptyId,
  whatCanISayWebOnlyId,
} from './WhatCanISay.testIds';
import { WhatCanISay } from './WhatCanISay';

const spec: ScreenVoiceSpec = {
  screen: 'backlog',
  actions: [
    { name: 'move', description: 'Move an item up or down', args: {} },
    { name: 'setState', description: 'Set an item to a state', args: {} },
    { name: 'moveTask', description: 'Move a task', args: {}, webOnly: 'Tasks move on the web' },
  ],
  context: () => ({}),
  hotwords: () => [],
  toActions: () => ({
    ok: false,
    failure: { kind: 'noValidAction', detail: { action: null, reason: 'none' } },
  }),
};

describe('WhatCanISay', () => {
  it("lists the screen's actions and the web-only ones with where", async () => {
    const onClose = jest.fn();
    await renderScreen(<WhatCanISay spec={spec} onClose={onClose} />);
    expect(screen.getByTestId(whatCanISayActionId('move'))).toHaveTextContent(
      'Move an item up or down',
    );
    expect(screen.getByTestId(whatCanISayActionId('setState'))).toBeOnTheScreen();
    expect(screen.queryByTestId(whatCanISayActionId('moveTask'))).toBeNull();
    const web = screen.getByTestId(whatCanISayWebOnlyId('moveTask'));
    expect(web).toHaveTextContent('Move a taskTasks move on the web');
    await fireEvent.press(screen.getByTestId(whatCanISayDoneId));
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('says so when the screen has no voice commands', async () => {
    await renderScreen(<WhatCanISay spec={null} onClose={jest.fn()} />);
    expect(screen.getByTestId(whatCanISayEmptyId)).toBeOnTheScreen();
  });
});
