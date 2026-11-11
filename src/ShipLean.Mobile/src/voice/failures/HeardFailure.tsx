import { useRouter } from 'expo-router';
import { useEffect, useRef } from 'react';
import { Pressable, Text, View } from 'react-native';
import { links } from '../../links';
import { useTheme } from '../../theme';
import { BottomSheet } from '../../ui/BottomSheet';
import { Button } from '../../ui/Button';
import { useToast } from '../../ui/Toast';
import type { VoiceFailure } from '../contracts';
import { engines } from '../engines';
import type { RetryChange, VoiceSource } from '../internals';
import { HeardQuote } from '../sheets/HeardQuote';
import { heardQuoteId } from '../sheets/HeardQuote.testIds';
import { useVoiceInternals } from '../internals';
import { useVoice } from '../VoiceProvider';
import {
  heardFailureFixId,
  heardFailureId,
  heardFailureSheetId,
  heardFailureLeadId,
  heardFailureNoteId,
  heardFailureNothingChangedId,
  heardFailureWhatCanISayId,
} from './HeardFailure.testIds';
import { nothingHeardToast, tooShortToast, unclearCopy } from './heardCopy';
import { readSpecContext } from './specContext';
import {
  resetSilentHolds,
  SILENT_HOLDS_FOR_TUNE_ACTION,
  SILENT_HOLDS_FOR_MIC_TUNING,
} from './silentHolds';
import { useSilentHolds } from './useSilentHolds';

export type HeardFailureKind = 'tooShort' | 'nothingHeard' | 'unclear';

export interface HeardFailureProps {
  failure: Extract<VoiceFailure, { kind: HeardFailureKind }>;
  heard: string | null;
  source: VoiceSource;
  // { asNote: true } keeps an unclear sentence as a note.
  onRetry: (change: RetryChange) => void;
  onTypeInstead: () => void;
  onWhatCanISay: () => void;
  onClose: () => void;
}

// Too short, nothing heard, or heard but unclear: nothing was changed, and the words are kept. A tap or a silent hold
// is a toast and no sheet (voice D1, D2); the second silent hold offers Tune mic and the third opens the Mic tuning page; unclear is a sheet
// with the heard words and a way on (D9, typed D6). A failure over an open preview never reaches here: the provider
// restores the preview without a message.
export function HeardFailure(props: HeardFailureProps) {
  return (
    <View testID={heardFailureId}>
      {props.failure.kind === 'unclear' ? (
        <UnclearSheet {...props} />
      ) : (
        <SilentFailure {...props} />
      )}
    </View>
  );
}

// A tap or silence: a toast and no sheet. The second silent hold in a row adds a Tune mic action; the third pushes the
// Mic tuning page instead of another toast (voice D1, D2). The page is pushed, so Back restores the screen as it was.
function SilentFailure({ failure, onClose }: HeardFailureProps) {
  const toast = useToast();
  const router = useRouter();
  const internals = useVoiceInternals();
  const holds = useSilentHolds(failure);
  const handled = useRef<VoiceFailure | null>(null);
  const silent = failure.kind === 'nothingHeard';

  useEffect(() => {
    if (handled.current === failure) return;
    handled.current = failure;
    // The page's Back label names an item: the focused one, else the first the screen shows.
    const context = readSpecContext(internals.spec());
    const key = context.focusKey ?? context.rows[0]?.key;
    if (silent && holds >= SILENT_HOLDS_FOR_MIC_TUNING) {
      resetSilentHolds();
      router.push(links.micTuning('third_hold', key));
      onClose();
      return;
    }
    const copy =
      failure.kind === 'tooShort'
        ? tooShortToast()
        : nothingHeardToast(engines().recognizer.inputName());
    toast.show(
      silent && holds === SILENT_HOLDS_FOR_TUNE_ACTION
        ? {
            ...copy,
            action: {
              label: 'Tune mic',
              onPress: () => {
                resetSilentHolds();
                router.push(links.micTuning('second_hold', key));
              },
            },
          }
        : copy,
    );
    onClose();
  }, [silent, holds, failure, toast, router, internals, onClose]);

  return null;
}

function UnclearSheet({
  failure,
  heard,
  source,
  onRetry,
  onWhatCanISay,
  onClose,
}: HeardFailureProps) {
  const theme = useTheme();
  const { colors } = theme;
  const voice = useVoice();
  const copy = unclearCopy(source);
  const text = heard ?? (failure.kind === 'unclear' ? failure.detail.heard : '');
  const keepAsNote = () => {
    onRetry({ asNote: true });
    // A typed note is posted at once (typed D6); a voice note comes back as a preview first.
    if (source === 'typed') voice.apply();
  };
  return (
    <BottomSheet title={copy.title} open onClose={onClose} testID={heardFailureSheetId}>
      <View style={{ gap: theme.space[3] }}>
        <HeardQuote text={text} source={source} misses={[]} testID={heardQuoteId} />
        <View>
          <Text
            testID={heardFailureLeadId}
            style={{
              color: colors.text,
              fontSize: theme.font.size.body15,
              fontWeight: theme.font.weight.semibold,
            }}
          >
            {copy.lead}
          </Text>
          <Text style={{ color: colors.muted, fontSize: theme.font.size.sm }}>{copy.detail}</Text>
        </View>
        <View style={{ flexDirection: 'row', gap: theme.space[2] }}>
          <View style={{ flex: 1 }}>
            <Button
              testID={heardFailureFixId}
              label={copy.fix}
              variant="ghost"
              onPress={() => voice.fix()}
            />
          </View>
          <View style={{ flex: 1 }}>
            <Button testID={heardFailureNoteId} label={copy.note} onPress={keepAsNote} />
          </View>
        </View>
        <Text
          testID={heardFailureNothingChangedId}
          style={{ color: colors.muted, fontSize: theme.font.size.sm, textAlign: 'center' }}
        >
          {copy.nothingChanged}
        </Text>
        <Pressable
          testID={heardFailureWhatCanISayId}
          accessibilityRole="link"
          onPress={onWhatCanISay}
          style={{ minHeight: theme.touch.control, justifyContent: 'center', alignItems: 'center' }}
        >
          <Text style={{ color: colors.accent, fontSize: theme.font.size.base }}>
            {copy.whatCanISay}
          </Text>
        </Pressable>
      </View>
    </BottomSheet>
  );
}
