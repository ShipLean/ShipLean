import { screen } from '@testing-library/react-native';
import { renderScreen } from '../../test-support/renderScreen';
import { themes } from '../../theme';
import { HeardQuote } from './HeardQuote';
import {
  heardQuoteFixedFromId,
  heardQuoteId,
  heardQuoteLabelId,
  heardQuoteMarkId,
  heardQuoteMissId,
  heardQuoteTextId,
} from './HeardQuote.testIds';

const HEARD = 'Set the onboarding story to Active';
// "the onboarding story" (voice.html no-match).
const MISS = { start: 4, end: 24 };

describe('HeardQuote', () => {
  it('voice shows Heard in italics with the unmatched words underlined', async () => {
    await renderScreen(
      <HeardQuote text={HEARD} source="voice" misses={[MISS]} testID={heardQuoteId} />,
    );
    expect(screen.getByTestId(heardQuoteLabelId(heardQuoteId))).toHaveTextContent('Heard', {
      exact: true,
    });
    const quote = screen.getByTestId(heardQuoteTextId(heardQuoteId));
    expect(quote).toHaveTextContent(`“${HEARD}”`);
    expect(quote).toHaveStyle({ fontStyle: 'italic' });
    expect(quote).not.toHaveStyle({ textDecorationLine: 'underline' });

    const misses = screen.getAllByTestId(heardQuoteMissId(heardQuoteId));
    expect(misses.map((m) => m.props.children)).toEqual(['the onboarding story']);
    expect(misses[0]).toHaveStyle({
      textDecorationLine: 'underline',
      textDecorationColor: themes.light.colors.warn,
    });
    expect(screen.queryByTestId(heardQuoteMarkId(heardQuoteId))).not.toBeOnTheScreen();
  });

  it('typed shows You typed with the keyboard mark and no italics', async () => {
    await renderScreen(
      <HeardQuote text="set this to active" source="typed" misses={[]} testID="quote-a" />,
    );
    expect(screen.getByTestId(heardQuoteLabelId('quote-a'))).toHaveTextContent('You typed', {
      exact: true,
    });
    expect(screen.getByTestId(heardQuoteMarkId('quote-a'))).toHaveTextContent('⌨');
    const quote = screen.getByTestId(heardQuoteTextId('quote-a'));
    expect(quote).toHaveTextContent('set this to active', { exact: true });
    expect(quote).toHaveStyle({ fontStyle: 'normal' });
    expect(screen.queryByTestId(heardQuoteMissId('quote-a'))).not.toBeOnTheScreen();
  });

  for (const scheme of ['light', 'dark'] as const) {
    it(`a fixed quote shows the first sentence and the correction (${scheme})`, async () => {
      await renderScreen(
        <HeardQuote
          text="No, D11, not D10"
          source="voice"
          misses={[]}
          fixedFrom="Approve D10 and set this to active"
          testID={heardQuoteId}
        />,
        { scheme },
      );
      const { colors } = themes[scheme];
      const first = screen.getByTestId(heardQuoteFixedFromId(heardQuoteId));
      expect(first).toHaveTextContent('“Approve D10 and set this to active”', { exact: true });
      expect(first).toHaveStyle({ color: colors.text, fontStyle: 'italic' });
      const fix = screen.getByTestId(heardQuoteTextId(heardQuoteId));
      expect(fix).toHaveTextContent('Fix: “No, D11, not D10”', { exact: true });
      expect(fix).toHaveStyle({ color: colors.accent, fontStyle: 'normal' });
    });
  }
});
