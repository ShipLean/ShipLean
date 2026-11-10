import { fireEvent, screen } from '@testing-library/react-native';
import { renderScreen } from '../test-support/renderScreen';
import { themes } from '../theme';
import { Segmented } from './Segmented';
import { optionCountId, optionId } from './testIds';

type Level = 'epics' | 'features' | 'stories';
const options = [
  { value: 'epics' as const, label: 'Epics' },
  { value: 'features' as const, label: 'Features', count: 12 },
  { value: 'stories' as const, label: 'Stories', count: 0 },
];

describe.each(['light', 'dark'] as const)('Segmented (%s)', (scheme) => {
  it('calls onChange with the option pressed', async () => {
    const onChange = jest.fn<void, [Level]>();
    await renderScreen(
      <Segmented options={options} value="features" onChange={onChange} testID="level" />,
      { scheme },
    );

    await fireEvent.press(screen.getByTestId(optionId('level', 'stories')));

    expect(onChange).toHaveBeenCalledWith('stories');
  });

  it('shows counts where given, 0 included, and none where not', async () => {
    await renderScreen(
      <Segmented options={options} value="features" onChange={() => {}} testID="level" />,
      { scheme },
    );

    expect(screen.getByTestId(optionCountId('level', 'features'))).toHaveTextContent('12');
    expect(screen.getByTestId(optionCountId('level', 'stories'))).toHaveTextContent('0');
    expect(screen.queryByTestId(optionCountId('level', 'epics'))).toBeNull();
  });

  it('raises the selected option on the theme surface', async () => {
    await renderScreen(
      <Segmented options={options} value="features" onChange={() => {}} testID="level" />,
      { scheme },
    );

    expect(screen.getByTestId(optionId('level', 'features'))).toBeSelected();
    expect(screen.getByTestId(optionId('level', 'features'))).toHaveStyle({
      backgroundColor: themes[scheme].colors.surface,
    });
    expect(screen.getByTestId(optionId('level', 'epics'))).not.toBeSelected();
  });
});
