import { fireEvent, screen } from '@testing-library/react-native';
import { useState } from 'react';
import { Pressable, Text } from 'react-native';
import { UNDO } from '../actions/undo';
import { InboxActionsContext } from '../inbox/InboxActions';
import { InboxSheetsContext } from '../inbox/InboxSheets';
import { fakeRunner } from '../test-support/actions';
import { fakeInboxActions, fakeInboxSheets } from '../test-support/inbox';
import { FeaturesProvider } from '../shell/providers/features';
import { aFeatureGroup, aFeatureRow, anOverview, featuresApi } from '../test-support/features';
import { renderWithRunner } from '../test-support/renderWithRunner';
import type { FeaturesAction } from './actions';
import { FeatureDetails } from './details/FeatureDetails';
import { FeaturesActionsProvider, useFeaturesActions } from './FeaturesActions';
import { featureDetailsId } from './testIds';

const note: FeaturesAction = {
  area: 'features',
  kind: 'addNote',
  itemId: 'i1',
  key: 'SHIP-1',
  body: 'hi',
};

function Tapper() {
  const { dispatch } = useFeaturesActions();
  return (
    <Pressable testID="tap" onPress={() => dispatch(note)}>
      <Text>tap</Text>
    </Pressable>
  );
}

// Renders the provider again with new children, the way a parent's update does.
function Host() {
  const [n, setN] = useState(0);
  return (
    <FeaturesProvider projectKey="SHIP">
      <Pressable testID="bump" onPress={() => setN(n + 1)}>
        <Text>{n}</Text>
      </Pressable>
    </FeaturesProvider>
  );
}

describe('Features seams', () => {
  it('the expanded-row stub renders its root id from its props', async () => {
    const row = aFeatureRow({ key: 'SHIP-906' });
    featuresApi(
      {},
      {
        projectId: 'p',
        overview: anOverview([aFeatureGroup('inProgress', [row])], { projectId: 'p' }),
      },
    );
    await renderWithRunner(
      <InboxActionsContext.Provider value={fakeInboxActions()}>
        <InboxSheetsContext.Provider value={fakeInboxSheets()}>
          <FeaturesActionsProvider>
            <FeatureDetails row={row} projectId="p" projectKey="SHIP" />
          </FeaturesActionsProvider>
        </InboxSheetsContext.Provider>
      </InboxActionsContext.Provider>,
    );
    expect(screen.getByTestId(featureDetailsId('SHIP-906'))).toBeTruthy();
  });

  it('dispatches one tap action through the runner with the tap window', async () => {
    const runner = fakeRunner();
    await renderWithRunner(
      <FeaturesActionsProvider>
        <Tapper />
      </FeaturesActionsProvider>,
      { runner },
    );
    await fireEvent.press(screen.getByTestId('tap'));
    expect(runner.dispatched).toHaveLength(1);
    expect(runner.dispatched[0].batch).toEqual([note]);
    expect(runner.dispatched[0].opts).toEqual({
      undo: UNDO.tap,
      origin: 'tap',
      allOrNothing: true,
    });
  });

  it('registers the executor once', async () => {
    const runner = fakeRunner();
    await renderWithRunner(<Host />, { runner });
    await fireEvent.press(screen.getByTestId('bump'));
    await fireEvent.press(screen.getByTestId('bump'));
    expect(runner.registered).toHaveLength(1);
    expect(runner.registered[0].area).toBe('features');
  });
});
