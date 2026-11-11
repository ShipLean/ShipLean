import { screen } from '@testing-library/react-native';
import { aUser } from '../test-support/inbox';
import { renderScreen } from '../test-support/renderScreen';
import { themes } from '../theme';
import { Avatar } from './Avatar';
import { primitiveIds } from './testIds';

describe.each(['light', 'dark'] as const)('Avatar (%s)', (scheme) => {
  it('shows a person’s initials on their colour', async () => {
    await renderScreen(<Avatar user={aUser('Priya Shah', 'PS', '#2f7fd8')} />, { scheme });

    expect(screen.getByTestId(primitiveIds.avatar)).toHaveTextContent('PS');
    expect(screen.getByTestId(primitiveIds.avatar)).toHaveStyle({ backgroundColor: '#2f7fd8' });
  });

  it('shows AI on the info colour for an agent', async () => {
    await renderScreen(<Avatar user={aUser()} agent />, { scheme });

    expect(screen.getByTestId(primitiveIds.avatar)).toHaveTextContent('AI');
    expect(screen.getByTestId(primitiveIds.avatar)).toHaveStyle({
      backgroundColor: themes[scheme].colors.info,
    });
  });

  it('is 28 round, or 22 when small', async () => {
    await renderScreen(<Avatar user={aUser()} size="sm" />, { scheme });

    expect(screen.getByTestId(primitiveIds.avatar)).toHaveStyle({ width: 22, height: 22 });
  });
});
