import { render, screen, within } from '@testing-library/react-native';
import type { ReactNode } from 'react';
import { Text } from 'react-native';
import { AppProviders } from './AppProviders';
import { ProjectProviders } from './ProjectProviders';

// Each slot becomes a View with its own id, so the nesting shows in the tree.
function mockSlot(exportName: string, slot: string) {
  const { createElement } = jest.requireActual<typeof import('react')>('react');
  const { View } = jest.requireActual<typeof import('react-native')>('react-native');
  return {
    [exportName]: ({ children }: { children: ReactNode }) =>
      createElement(View, { testID: `slot-${slot}` }, children),
  };
}

jest.mock('./providers/query', () => mockSlot('QueryProvider', 'query'));
jest.mock('./providers/mic', () => mockSlot('MicProvider', 'mic'));
jest.mock('./providers/runner', () => mockSlot('RunnerProvider', 'runner'));
jest.mock('./providers/voice', () => mockSlot('VoiceProvider', 'voice'));
jest.mock('./providers/inbox', () => mockSlot('InboxProvider', 'inbox'));
jest.mock('./providers/workItem', () => mockSlot('WorkItemProvider', 'workItem'));
jest.mock('./providers/backlog', () => mockSlot('BacklogProvider', 'backlog'));
jest.mock('./providers/features', () => mockSlot('FeaturesProvider', 'features'));
jest.mock('./providers/retro', () => mockSlot('RetroProvider', 'retro'));

// Outermost first: AppProviders' slots, then ProjectProviders'.
const ORDER = [
  'query',
  'mic',
  'runner',
  'voice',
  'inbox',
  'workItem',
  'backlog',
  'features',
  'retro',
];

describe('provider slots', () => {
  it('nests every provider slot once, in order', async () => {
    await render(
      <AppProviders>
        <ProjectProviders projectKey="SHIP">
          <Text testID="slot-child">child</Text>
        </ProjectProviders>
      </AppProviders>,
    );

    for (const slot of ORDER) expect(screen.getAllByTestId(`slot-${slot}`)).toHaveLength(1);
    const ids = [...ORDER, 'child'].map((slot) => `slot-${slot}`);
    for (let i = 1; i < ids.length; i++) {
      const outer = screen.getByTestId(ids[i - 1]!);
      expect(within(outer).getByTestId(ids[i]!)).toBeOnTheScreen();
      // The inner slot is a direct child of the outer one: nothing between them, nothing skipped.
      const children = outer.children.filter((child) => typeof child !== 'string');
      expect(children.map((child) => child.props.testID)).toEqual([ids[i]]);
    }
  });
});
