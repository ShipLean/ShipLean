import { screen } from '@testing-library/react-native';
import { Text } from 'react-native';
import { renderScreen } from '../test-support/renderScreen';
import { Chip } from './Chip';
import { NeedsPill } from './NeedsPill';
import { StatePill } from './StatePill';
import { TabBar } from './TabBar';
import { Toast } from './Toast';
import { primitiveIds } from './testIds';

// The design's app.css sizes, pinned as numbers so a change to layout.ts shows up here.
describe.each(['light', 'dark'] as const)('app-scale sizes (%s)', (scheme) => {
  it('draws pills 22 high, chips 32 high, the toast 48 high and the mic slot 84 wide', async () => {
    await renderScreen(
      <>
        <StatePill state={{ id: 's', name: 'Active', category: 'inProgress' }} />
        <NeedsPill needs={{ questions: 1, decisions: 0 }} />
        <Chip label="All" selected={false} onPress={() => {}} testID="chip" />
        <Toast text="Saved" onDismiss={() => {}} />
        <TabBar
          tabs={[
            { id: 'inbox', label: 'Inbox' },
            { id: 'backlog', label: 'Backlog' },
          ]}
          active="inbox"
          onPress={() => {}}
          center={<Text>mic</Text>}
        />
      </>,
      { scheme },
    );

    expect(screen.getByTestId(primitiveIds.statePill)).toHaveStyle({ height: 22 });
    expect(screen.getByTestId(primitiveIds.needsPill)).toHaveStyle({ height: 22 });
    expect(screen.getByTestId('chip')).toHaveStyle({ height: 32 });
    expect(screen.getByTestId(primitiveIds.toast)).toHaveStyle({ minHeight: 48 });
    expect(screen.getByTestId(primitiveIds.tabMic)).toHaveStyle({ width: 84 });
  });
});
