import { screen } from '@testing-library/react-native';
import { renderScreen } from '../test-support/renderScreen';
import { themes } from '../theme';
import { TypeChip } from './TypeChip';
import { primitiveIds } from './testIds';

const IDS = ['epic', 'feature', 'story', 'bug', 'task', 'action'] as const;
const LETTER = { epic: 'E', feature: 'F', story: 'S', bug: 'B', task: 'T', action: 'A' } as const;

describe.each(['light', 'dark'] as const)('TypeChip (%s)', (scheme) => {
  it.each(IDS)('shows the letter and colour of %s', async (id) => {
    await renderScreen(<TypeChip type={{ id, name: id }} />, { scheme });

    expect(screen.getByTestId(primitiveIds.typeChip)).toHaveTextContent(LETTER[id]);
    expect(screen.getByTestId(primitiveIds.typeChip)).toHaveStyle({
      backgroundColor: themes[scheme].colors.type[id],
    });
  });

  it('takes the word Feature', async () => {
    await renderScreen(<TypeChip type="Feature" />, { scheme });

    expect(screen.getByTestId(primitiveIds.typeChip)).toHaveTextContent('F');
    expect(screen.getByTestId(primitiveIds.typeChip)).toHaveStyle({
      backgroundColor: themes[scheme].colors.type.feature,
    });
  });
});
