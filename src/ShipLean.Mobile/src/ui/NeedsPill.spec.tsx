import { screen } from '@testing-library/react-native';
import { renderScreen } from '../test-support/renderScreen';
import { themes } from '../theme';
import { NeedsPill } from './NeedsPill';
import { primitiveIds } from './testIds';

describe.each(['light', 'dark'] as const)('NeedsPill (%s)', (scheme) => {
  it('is hidden at 0', async () => {
    await renderScreen(<NeedsPill needs={{ questions: 0, decisions: 0 }} />, { scheme });

    expect(screen.queryByTestId(primitiveIds.needsPill)).toBeNull();
  });

  it('is hidden for null', async () => {
    await renderScreen(<NeedsPill needs={null} />, { scheme });

    expect(screen.queryByTestId(primitiveIds.needsPill)).toBeNull();
  });

  it('counts questions and decisions together', async () => {
    await renderScreen(<NeedsPill needs={{ questions: 2, decisions: 1 }} />, { scheme });

    expect(screen.getByTestId(primitiveIds.needsPill)).toHaveTextContent('3');
    expect(screen.getByText('3')).toHaveStyle({ color: themes[scheme].colors.warn });
  });
});

describe('NeedsPill variants', () => {
  const needs = { questions: 2, decisions: 1 };
  const hidden = { includeHiddenElements: true };

  it('kinds shows a check with the decisions and a message with the questions', async () => {
    await renderScreen(<NeedsPill needs={needs} variant="kinds" />);

    expect(screen.getByTestId(primitiveIds.needsDecisions)).toHaveTextContent('1');
    expect(screen.getByTestId(primitiveIds.needsQuestions)).toHaveTextContent('2');
    expect(screen.getByTestId('icon-check', hidden)).toBeTruthy();
    expect(screen.getByTestId('icon-message', hidden)).toBeTruthy();
  });

  it('kinds leaves out a kind with none', async () => {
    await renderScreen(<NeedsPill needs={{ questions: 0, decisions: 4 }} variant="kinds" />);

    expect(screen.queryByTestId(primitiveIds.needsQuestions)).toBeNull();
  });

  it('label says Needs you', async () => {
    await renderScreen(<NeedsPill needs={needs} variant="label" />);

    expect(screen.getByTestId(primitiveIds.needsPill)).toHaveTextContent('Needs you');
  });

  it('label is hidden at 0', async () => {
    await renderScreen(<NeedsPill needs={{ questions: 0, decisions: 0 }} variant="label" />);

    expect(screen.queryByTestId(primitiveIds.needsPill)).toBeNull();
  });
});
