import { fetchSearch } from '@shiplean/core/api/search';
import { platform } from '@shiplean/core/platform';
import { useQuery } from '@tanstack/react-query';
import { openBrowserAsync } from 'expo-web-browser';
import { Pressable, Text, View } from 'react-native';
import { webPageUrl, webUrl, type WebPage } from '../../links';
import { useTheme } from '../../theme';
import { BottomSheet } from '../../ui/BottomSheet';
import { Button } from '../../ui/Button';
import { Chip } from '../../ui/Chip';
import type { ScreenVoiceSpec, VoiceFailure } from '../contracts';
import type { RetryChange, VoiceSource } from '../internals';
import { HeardQuote } from '../sheets/HeardQuote';
import { heardQuoteId } from '../sheets/HeardQuote.testIds';
import { useVoice } from '../VoiceProvider';
import { closestFromSearch, closestRows, CLOSEST_LIMIT, spanOf, type ClosestRow } from './closest';
import {
  ambiguousCopy,
  candidateLabel,
  commonCopy,
  misheardKeyCopy,
  noMatchCopy,
  noValidActionCopy,
  webOnlyCopy,
  type SheetCopy,
} from './correctionCopy';
import {
  correctionCandidateId,
  correctionClosestId,
  correctionFailureDetailId,
  correctionFailureFixId,
  correctionFailureId,
  correctionFailureLeadId,
  correctionFailureNothingChangedId,
  correctionFailureOpenWebId,
  correctionFailureSheetId,
  correctionFailureWhatCanISayId,
  correctionFailureYesId,
  correctionStateId,
} from './CorrectionFailure.testIds';
import { readSpecContext, type SpecContext } from './specContext';
import { useOffline } from './useOffline';

export type CorrectionFailureKind = Exclude<
  VoiceFailure['kind'],
  'tooShort' | 'nothingHeard' | 'unclear'
>;

export interface CorrectionFailureProps {
  failure: Extract<VoiceFailure, { kind: CorrectionFailureKind }>;
  heard: string | null;
  source: VoiceSource;
  // The one-tap correction: a different value for the span that didn't match.
  onRetry: (change: RetryChange) => void;
  onWhatCanISay: () => void;
  onClose: () => void;
  // The focused screen's spec (its states, rows and focused item) and its project, from the router.
  spec?: ScreenVoiceSpec | null;
  projectKey?: string;
}

interface Body {
  copy: SheetCopy;
  misses: { start: number; end: number }[];
  content: React.ReactNode;
}

// No match, a misheard key, no valid action, web only or ambiguous: nothing was changed, so show the words that were
// heard with the miss underlined and the one-tap correction the kind has (voice D4-D8, D11).
export function CorrectionFailure(props: CorrectionFailureProps) {
  const { heard, source, onWhatCanISay, onClose } = props;
  const theme = useTheme();
  const { colors } = theme;
  const voice = useVoice();
  const common = commonCopy(source);
  const text = heard ?? '';
  const body = useBody(props, text);
  return (
    <View testID={correctionFailureId}>
      <BottomSheet title={body.copy.title} open onClose={onClose} testID={correctionFailureSheetId}>
        <View style={{ gap: theme.space[3] }}>
          <HeardQuote text={text} source={source} misses={body.misses} testID={heardQuoteId} />
          <View>
            <Text
              testID={correctionFailureLeadId}
              style={{
                color: colors.text,
                fontSize: theme.font.size.body15,
                fontWeight: theme.font.weight.semibold,
              }}
            >
              {body.copy.lead}
            </Text>
            {body.copy.detail === null ? null : (
              <Text
                testID={correctionFailureDetailId}
                style={{ color: colors.muted, fontSize: theme.font.size.sm }}
              >
                {body.copy.detail}
              </Text>
            )}
          </View>
          {body.content}
          <Button
            testID={correctionFailureFixId}
            label={common.fix}
            variant="ghost"
            onPress={() => voice.fix()}
          />
          <Text
            testID={correctionFailureNothingChangedId}
            style={{ color: colors.muted, fontSize: theme.font.size.sm, textAlign: 'center' }}
          >
            {common.nothingChanged}
          </Text>
          <Pressable
            testID={correctionFailureWhatCanISayId}
            accessibilityRole="link"
            onPress={onWhatCanISay}
            style={{
              minHeight: theme.touch.control,
              justifyContent: 'center',
              alignItems: 'center',
            }}
          >
            <Text style={{ color: colors.accent, fontSize: theme.font.size.base }}>
              {common.whatCanISay}
            </Text>
          </Pressable>
        </View>
      </BottomSheet>
    </View>
  );
}

// The words to correct: where they sit in the heard text, or the whole sentence when the failure doesn't say.
function wholeOf(text: string) {
  return { start: 0, end: text.length };
}

function useBody(props: CorrectionFailureProps, text: string): Body {
  const { failure, source, onRetry, spec = null, projectKey } = props;
  const context = readSpecContext(spec);
  const offline = useOffline();
  const search = useQuery({
    queryKey: ['voice-failure-closest', failure.kind === 'noMatch' ? failure.detail.spoken : null],
    queryFn: ({ signal }) =>
      fetchSearch(
        {
          q: failure.kind === 'noMatch' ? failure.detail.spoken : '',
          prefix: false,
          closed: false,
          limit: 20,
        },
        signal,
      ),
    enabled: failure.kind === 'noMatch' && !offline && projectKey !== undefined,
  });

  switch (failure.kind) {
    case 'noMatch': {
      const span = failure.detail.span ?? spanOf(text, failure.detail.spoken);
      const closest: ClosestRow[] = offline
        ? closestRows(failure.detail.spoken, context.rows, CLOSEST_LIMIT)
        : projectKey === undefined
          ? []
          : closestFromSearch(search.data?.items ?? [], projectKey, CLOSEST_LIMIT);
      return {
        copy: noMatchCopy(failure.detail.spoken, closest.length, offline),
        misses: span === null ? [] : [span],
        content: (
          <Rows
            rows={closest.map((row, i) => ({
              id: correctionClosestId(i),
              label: `${row.key} ${row.title}`,
              onPress: () => onRetry({ span: span ?? wholeOf(text), value: row.key }),
            }))}
          />
        ),
      };
    }
    case 'misheardKey': {
      const span = spanOf(text, failure.detail.spoken);
      const copy = misheardKeyCopy(failure.detail.spoken, failure.detail.suggestion);
      return {
        copy,
        misses: span === null ? [] : [span],
        content: (
          <Button
            testID={correctionFailureYesId}
            label={copy.yes}
            onPress={() =>
              onRetry({ span: span ?? wholeOf(text), value: failure.detail.suggestion })
            }
          />
        ),
      };
    }
    case 'ambiguous': {
      const candidates = [...failure.detail.candidates].sort(
        (a, b) => (a.rank ?? Infinity) - (b.rank ?? Infinity),
      );
      return {
        copy: ambiguousCopy(source),
        misses: [],
        content: (
          <Rows
            rows={candidates.map((c, i) => ({
              id: correctionCandidateId(i),
              label: candidateLabel(i + 1, c.key, c.title),
              onPress: () => onRetry({ span: wholeOf(text), value: c.key }),
            }))}
          />
        ),
      };
    }
    case 'noValidAction': {
      const states = failure.detail.reason === 'noSuchState' ? context.states : [];
      return {
        copy: noValidActionCopy(failure.detail.reason, states.length),
        misses: [],
        content: (
          <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: 8 }}>
            {states.map((state) => (
              <Chip
                key={state}
                testID={correctionStateId(state)}
                label={state}
                selected={false}
                onPress={() => onRetry({ span: wholeOf(text), value: state })}
              />
            ))}
          </View>
        ),
      };
    }
    case 'webOnly': {
      const copy = webOnlyCopy(failure.detail.where);
      const target = webTarget(failure.detail.page, context, projectKey);
      return {
        copy,
        misses: [],
        content: (
          <Button
            testID={correctionFailureOpenWebId}
            label={copy.open}
            disabled={target === null}
            onPress={() => {
              if (target !== null) void openBrowserAsync(target);
            }}
          />
        ),
      };
    }
  }
}

// Where Open on the web goes: the action's own page (a backlog page at the level the screen shows), else the focused
// item's page, else nowhere.
function webTarget(
  page: WebPage | undefined,
  context: SpecContext,
  projectKey: string | undefined,
): string | null {
  if (projectKey === undefined) return null;
  const origin = platform().apiOrigin();
  if (page?.page === 'backlog' && page.level === undefined && context.level !== null)
    return webPageUrl(origin, projectKey, { page: 'backlog', level: context.level });
  if (page !== undefined) return webPageUrl(origin, projectKey, page);
  return context.focusKey === null ? null : webUrl(origin, projectKey, context.focusKey);
}

function Rows({ rows }: { rows: { id: string; label: string; onPress: () => void }[] }) {
  const theme = useTheme();
  return (
    <View style={{ gap: theme.space[2] }}>
      {rows.map((row) => (
        <Button
          key={row.id}
          testID={row.id}
          label={row.label}
          variant="ghost"
          onPress={row.onPress}
        />
      ))}
    </View>
  );
}
