import { Text, View } from 'react-native';
import { useTheme } from '../../theme';
import { layout } from '../../ui/layout';
import { highlight } from '../highlight';
import type { VoiceUiStateOf } from '../internals';
import {
  listeningContextId,
  listeningHintId,
  listeningHotId,
  listeningPartialId,
  listeningSheetId,
} from './ListeningSheet.testIds';

export interface ListeningSheetProps {
  state: VoiceUiStateOf<'listening'>;
}

// While the mic is held: what it applies to, the words so far with the screen's keys, states and names lit up, and
// the way to send. A plain card over the screen, not a Modal: a Modal would take the touch the hold depends on.
// TODO: the level meter and the input name join the card.
// Recorded PCM stays in memory for recognition only; never write, keep or upload it.
export function ListeningSheet({ state }: ListeningSheetProps) {
  const theme = useTheme();
  const { colors } = theme;
  const parts = highlight(state.partial, state.hotwords);
  return (
    <View
      testID={listeningSheetId}
      pointerEvents="none"
      style={{
        position: 'absolute',
        left: theme.space[4],
        right: theme.space[4],
        bottom: layout.tabBar + layout.dockMic + theme.space[5],
        padding: theme.space[4],
        gap: theme.space[2],
        borderRadius: layout.sheetRadius,
        backgroundColor: colors.surface,
        borderWidth: 1,
        borderColor: colors.border,
      }}
    >
      <Text
        testID={listeningContextId}
        style={{ color: colors.muted, fontSize: theme.font.size.sm }}
      >
        {state.screen}
      </Text>
      <Text
        testID={listeningPartialId}
        style={{ color: colors.text, fontSize: theme.font.size.body15 }}
      >
        {parts.length === 0
          ? 'Listening…'
          : parts.map((p, i) =>
              p.matched ? (
                <Text
                  key={i}
                  testID={listeningHotId}
                  style={{ color: colors.accent, fontWeight: theme.font.weight.semibold }}
                >
                  {p.text}
                </Text>
              ) : (
                p.text
              ),
            )}
      </Text>
      <Text testID={listeningHintId} style={{ color: colors.muted, fontSize: theme.font.size.sm }}>
        Release to send
      </Text>
    </View>
  );
}
