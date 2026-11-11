import type { BacklogRowDto } from '@shiplean/core/api/views';
import { screen } from '@testing-library/react-native';
import { aBacklogRow } from '../../test-support/backlog';
import { renderScreen } from '../../test-support/renderScreen';
import { themes } from '../../theme';
import { primitiveIds } from '../../ui/testIds';
import { backlogDoneId, backlogMoveButtonId, backlogNeedsBarId, backlogTypeId } from '../testIds';
import { BacklogRow } from './BacklogRow';

// Icons are hidden from screen readers, so the queries look at hidden elements too.
const HIDDEN = { includeHiddenElements: true };

const feature = (over: Partial<BacklogRowDto> = {}) =>
  aBacklogRow({
    key: 'SHIP-707',
    title: 'Mobile web',
    type: { id: 'feature', name: 'Feature' },
    rollups: [{ type: 'story', open: 5, closed: 9, inCurrentSprint: 3, inOtherSprint: 0 }],
    ...over,
  });

const noop = () => undefined;

describe.each(['light', 'dark'] as const)('BacklogRow (%s)', (scheme) => {
  const render = (row: BacklogRowDto, onMove?: () => void) =>
    renderScreen(
      <BacklogRow row={row} level="features" highlight={null} onOpen={noop} onMove={onMove} />,
      { scheme },
    );

  it('shows a type chip, the progress bar and "9 of 14" on a Feature with rollups', async () => {
    await render(feature());

    expect(screen.getByTestId(backlogTypeId('SHIP-707'))).toBeOnTheScreen();
    expect(screen.getByTestId(primitiveIds.typeChip).props.accessibilityLabel).toBe('Feature');
    expect(screen.getByTestId(primitiveIds.progressBar)).toBeOnTheScreen();
    expect(screen.getByTestId(backlogDoneId('SHIP-707'))).toHaveTextContent('9 of 14');
  });

  it('lines a needs-you row up with a plain row and draws the bar out of the flow', async () => {
    await render(feature({ needsYou: { questions: 1, decisions: 1, onChildren: false } }));
    const bar = screen.getByTestId(backlogNeedsBarId('SHIP-707'));
    expect(bar).toHaveStyle({
      position: 'absolute',
      left: 0,
      backgroundColor: themes[scheme].colors.warn,
    });
    expect(screen.getByTestId(primitiveIds.needsDecisions)).toBeOnTheScreen();
    expect(screen.getByTestId(primitiveIds.needsQuestions)).toBeOnTheScreen();
  });

  it('has a 36 px rank icon button as the move control', async () => {
    await render(feature(), noop);

    const move = screen.getByTestId(backlogMoveButtonId('SHIP-707'));
    expect(move).toHaveStyle({ width: 36, height: 36 });
    expect(screen.getByTestId('icon-rank', HIDDEN)).toBeOnTheScreen();
  });
});
