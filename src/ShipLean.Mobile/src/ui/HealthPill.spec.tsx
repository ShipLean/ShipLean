import { screen } from '@testing-library/react-native';
import { renderScreen } from '../test-support/renderScreen';
import { themes } from '../theme';
import { HealthPill } from './HealthPill';
import { primitiveIds } from './testIds';

describe.each(['light', 'dark'] as const)('HealthPill (%s)', (scheme) => {
  it('renders nothing for null', async () => {
    await renderScreen(<HealthPill health={null} />, { scheme });

    expect(screen.queryByTestId(primitiveIds.healthPill)).toBeNull();
  });

  it('renders nothing for onTrack', async () => {
    await renderScreen(<HealthPill health="onTrack" />, { scheme });

    expect(screen.queryByTestId(primitiveIds.healthPill)).toBeNull();
  });

  it('shows At risk in the warn colour', async () => {
    await renderScreen(<HealthPill health="atRisk" />, { scheme });

    expect(screen.getByTestId(primitiveIds.healthPill)).toHaveTextContent('At risk');
    expect(screen.getByText('At risk')).toHaveStyle({ color: themes[scheme].colors.warn });
  });

  it('shows Blocked in the danger colour', async () => {
    await renderScreen(<HealthPill health="blocked" />, { scheme });

    expect(screen.getByTestId(primitiveIds.healthPill)).toHaveTextContent('Blocked');
    expect(screen.getByText('Blocked')).toHaveStyle({ color: themes[scheme].colors.danger });
  });
});
