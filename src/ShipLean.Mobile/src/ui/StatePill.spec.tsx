import { screen } from '@testing-library/react-native';
import { renderScreen } from '../test-support/renderScreen';
import { themes } from '../theme';
import { StatePill } from './StatePill';
import { primitiveIds } from './testIds';

describe.each(['light', 'dark'] as const)('StatePill (%s)', (scheme) => {
  it('names the state in its category colour', async () => {
    await renderScreen(<StatePill state={{ id: 's1', name: 'Active', category: 'inProgress' }} />, {
      scheme,
    });

    expect(screen.getByTestId(primitiveIds.statePill)).toHaveTextContent('Active');
    expect(screen.getByText('Active')).toHaveStyle({
      color: themes[scheme].stateCategory.inProgress,
    });
  });
});
