import { Text, View } from 'react-native';
import { useTheme } from '../../theme';
import type { VoiceSource } from '../internals';
import {
  heardQuoteFixedFromId,
  heardQuoteLabelId,
  heardQuoteMarkId,
  heardQuoteMissId,
  heardQuoteTextId,
} from './HeardQuote.testIds';

export interface HeardQuoteProps {
  // What was heard or typed, word for word.
  text: string;
  source: VoiceSource;
  // Spans of text that didn't match, underlined.
  misses: { start: number; end: number }[];
  // The sentence before a Fix, shown with the new one.
  fixedFrom?: string;
  // heardQuoteId unless a sheet shows more than one.
  testID: string;
}

interface Part {
  text: string;
  miss: boolean;
}

// The text cut at the misses, in order; overlapping or out-of-range spans are clamped, so no word is dropped or
// repeated.
function parts(text: string, misses: { start: number; end: number }[]): Part[] {
  const sorted = [...misses].sort((a, b) => a.start - b.start);
  const out: Part[] = [];
  let at = 0;
  for (const m of sorted) {
    const start = Math.max(at, Math.min(m.start, text.length));
    const end = Math.max(start, Math.min(m.end, text.length));
    if (start > at) out.push({ text: text.slice(at, start), miss: false });
    if (end > start) out.push({ text: text.slice(start, end), miss: true });
    at = Math.max(at, end);
  }
  if (at < text.length) out.push({ text: text.slice(at), miss: false });
  return out;
}

// The heard sentence on every preview and failure sheet: the words are never dropped. Voice reads "Heard", in
// italics, with the words that didn't match underlined (voice no-match); after a Fix it shows the first sentence
// and the correction (voice D12). Typed reads "You typed" with a keyboard mark and no italics (typed D5).
export function HeardQuote({ text, source, misses, fixedFrom, testID }: HeardQuoteProps) {
  const theme = useTheme();
  const { colors } = theme;
  const voice = source === 'voice';
  const words = parts(text, misses).map((p, i) =>
    p.miss ? (
      <Text
        key={i}
        testID={heardQuoteMissId(testID)}
        style={{
          fontStyle: 'normal',
          fontWeight: theme.font.weight.semibold,
          textDecorationLine: 'underline',
          textDecorationColor: colors.warn,
        }}
      >
        {p.text}
      </Text>
    ) : (
      p.text
    ),
  );
  return (
    <View
      testID={testID}
      style={{
        flexDirection: 'row',
        alignItems: 'flex-start',
        gap: theme.space[2],
        paddingVertical: theme.space[2],
        paddingHorizontal: theme.space[3],
        borderRadius: theme.radius.md,
        backgroundColor: voice ? 'transparent' : colors.surface2,
        borderLeftWidth: voice ? 3 : 0,
        borderLeftColor: colors.border,
      }}
    >
      {voice ? null : (
        <Text
          testID={heardQuoteMarkId(testID)}
          accessibilityLabel="Typed"
          style={{ color: colors.muted, fontSize: theme.font.size.body15 }}
        >
          ⌨
        </Text>
      )}
      <View style={{ flex: 1, minWidth: 0, gap: theme.space[1] }}>
        <Text
          testID={heardQuoteLabelId(testID)}
          style={{
            color: colors.muted,
            fontSize: theme.font.size.xs,
            fontWeight: theme.font.weight.semibold,
          }}
        >
          {voice ? 'Heard' : 'You typed'}
        </Text>
        {fixedFrom !== undefined ? (
          <Text
            testID={heardQuoteFixedFromId(testID)}
            style={{
              color: colors.muted,
              fontSize: theme.font.size.body15,
              fontStyle: voice ? 'italic' : 'normal',
            }}
          >
            {voice ? `“${fixedFrom}”` : fixedFrom}
          </Text>
        ) : null}
        <Text
          testID={heardQuoteTextId(testID)}
          style={
            fixedFrom !== undefined
              ? {
                  color: colors.accent,
                  fontSize: theme.font.size.body15,
                  fontWeight: theme.font.weight.medium,
                  fontStyle: 'normal',
                }
              : {
                  color: colors.text,
                  fontSize: theme.font.size.body15,
                  fontStyle: voice ? 'italic' : 'normal',
                }
          }
        >
          {fixedFrom !== undefined ? 'Fix: “' : voice ? '“' : ''}
          {words}
          {fixedFrom !== undefined || voice ? '”' : ''}
        </Text>
      </View>
    </View>
  );
}
