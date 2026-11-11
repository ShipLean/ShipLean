import { screen } from '@testing-library/react-native';
import { View } from 'react-native';
import { renderScreen } from '../test-support/renderScreen';
import { themes } from '../theme';
import { ICON_NAMES, Icon } from './Icon';

// Icons are hidden from screen readers, so the queries look at hidden elements too.
const HIDDEN = { includeHiddenElements: true };

describe('Icon', () => {
  it('renders every name in the IconName union', async () => {
    await renderScreen(
      <View>
        {ICON_NAMES.map((name) => (
          <Icon key={name} name={name} />
        ))}
      </View>,
    );
    for (const name of ICON_NAMES) {
      expect(screen.getByTestId(`icon-${name}`, HIDDEN)).toBeOnTheScreen();
    }
    expect(ICON_NAMES).toEqual(
      expect.arrayContaining(
        'inbox list board retro mic search filter more check message clock note chevron down back rank spark thumb plus x link play'.split(
          ' ',
        ),
      ),
    );
  });

  it('is 20 points and the text colour by default', async () => {
    await renderScreen(<Icon name="mic" />, { scheme: 'dark' });
    const props = screen.getByTestId('icon-mic', HIDDEN).props;
    expect(props.width).toBe(20);
    expect(props.stroke).toBe(themes.dark.colors.text);
    expect(props.strokeWidth).toBe(1.8);
  });

  it('takes a size and a colour', async () => {
    await renderScreen(<Icon name="check" size={24} color="#123456" />);
    const props = screen.getByTestId('icon-check', HIDDEN).props;
    expect(props.width).toBe(24);
    expect(props.stroke).toBe('#123456');
  });
});
