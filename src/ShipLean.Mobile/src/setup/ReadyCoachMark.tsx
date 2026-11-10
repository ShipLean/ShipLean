import { platform } from '@shiplean/core/platform';
import { useEffect, useEffectEvent, useState, type FunctionComponent } from 'react';
import { AppState, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useTheme } from '../theme';
import { Button } from '../ui/Button';
import { layout } from '../ui/layout';
import { engines } from '../voice/engines';
import type { MicStatus } from '../voice/internals';
import { useVoice } from '../voice/VoiceProvider';
import { canAskMic, coachVariant, markGuide, readGuide, type CoachVariant } from './guide';
import { readyCoachMarkIds } from './ReadyCoachMark.testIds';
import { useModelStatus } from './useModelStatus';

export interface ReadyCoachMarkProps {
  // The person started their first hold of the mic.
  onTry: () => void;
  // Skip: the guide is over.
  onSkip: () => void;
}

// Setup D13 and microphone D7: once, when voice is ready, a coach mark at the mic. "Try it" when the microphone is on;
// when it isn't, the turn-it-on one (shown once), and "Try it" follows when the microphone is first allowed. Mounted
// once in the tab bar. Nothing is read aloud. The root stays mounted and empty when there is nothing to show.
export const ReadyCoachMark: FunctionComponent<ReadyCoachMarkProps> = ({ onTry, onSkip }) => {
  const theme = useTheme();
  const insets = useSafeAreaInsets();
  const status = useModelStatus();
  const voice = useVoice();
  const [mic, setMic] = useState<MicStatus | null>(null);
  const [variant, setVariant] = useState<CoachVariant | null>(null);
  // The variant the person has put away: Skip, Not now, or the first hold.
  const [away, setAway] = useState<CoachVariant | null>(null);
  const [held, setHeld] = useState(false);

  // The microphone's state on mount, and again when the app comes back to the front (after Open Settings).
  useEffect(() => {
    let live = true;
    const read = () =>
      void engines()
        .mic.status()
        .then((s) => {
          if (live) setMic(s);
        });
    read();
    const sub = AppState.addEventListener('change', (next) => {
      if (next === 'active') read();
    });
    return () => {
      live = false;
      sub.remove();
    };
  }, []);

  // What to show is worked out here from the phone's flags, and kept while the mark is on screen.
  const next =
    mic === null ? null : coachVariant(status.phase, mic, readGuide(platform().persistentStore()));
  if (next !== null && (variant === null || (variant === 'micOff' && next === 'try'))) {
    setVariant(next);
  }
  // The first hold of the mic puts the "Try it" mark away.
  if (!held && variant === 'try' && voice.state.kind === 'listening') {
    setHeld(true);
    setAway('try');
  }

  useEffect(() => {
    if (variant === null) return;
    markGuide(
      platform().persistentStore(),
      variant === 'try' ? 'voiceReadyShown' : 'micCoachShown',
    );
  }, [variant]);

  const tried = useEffectEvent(onTry);
  useEffect(() => {
    if (held) tried();
  }, [held]);

  const showing = variant !== null && away !== variant;
  const bottom = insets.bottom + layout.tabBar + layout.toastGap;
  const card = {
    position: 'absolute',
    left: theme.space[4],
    right: theme.space[4],
    bottom,
    gap: theme.space[2],
    padding: theme.space[4],
    borderRadius: theme.radius.lg,
    borderWidth: 1,
    borderColor: theme.colors.border,
    backgroundColor: theme.colors.surface,
  } as const;
  const title = {
    color: theme.colors.text,
    fontSize: theme.font.size.body15,
    fontWeight: theme.font.weight.bold,
  } as const;
  const body = { color: theme.colors.text, fontSize: theme.font.size.body15 } as const;

  return (
    <View testID={readyCoachMarkIds.root} pointerEvents="box-none">
      {showing && variant === 'try' ? (
        <View testID={readyCoachMarkIds.card} style={card}>
          <Text testID={readyCoachMarkIds.title} style={title}>
            Voice is ready
          </Text>
          <Text testID={readyCoachMarkIds.body} style={body}>
            Try it: hold the button and say &lsquo;What needs me?&rsquo;
          </Text>
          <Text style={{ color: theme.colors.muted, fontSize: theme.font.size.base }}>
            Let go when you&rsquo;ve finished. Questions just get an answer; changes always wait for
            your OK.
          </Text>
          <Button
            testID={readyCoachMarkIds.skip}
            label="Skip"
            variant="ghost"
            onPress={() => {
              // Skipping ends the guide: no first-result sheet follows.
              markGuide(platform().persistentStore(), 'firstCommandShown');
              setAway('try');
              onSkip();
            }}
          />
        </View>
      ) : null}
      {showing && variant === 'micOff' && mic !== null ? (
        <View testID={readyCoachMarkIds.micOffCard} style={card}>
          <Text testID={readyCoachMarkIds.title} style={title}>
            Voice is ready
          </Text>
          <Text testID={readyCoachMarkIds.body} style={body}>
            Turn on the microphone to use it
          </Text>
          <Text style={{ color: theme.colors.muted, fontSize: theme.font.size.base }}>
            {canAskMic(mic)
              ? "It's off, so voice can't hear you yet. Typed commands work now."
              : "It's off in Settings, so voice can't hear you yet. Typed commands work now."}
          </Text>
          <View style={{ flexDirection: 'row', gap: theme.space[2] }}>
            <Button
              testID={readyCoachMarkIds.notNow}
              label="Not now"
              variant="ghost"
              onPress={() => setAway('micOff')}
            />
            {canAskMic(mic) ? (
              <Button
                testID={readyCoachMarkIds.allowMic}
                label="Allow microphone"
                onPress={() => {
                  void engines()
                    .mic.request()
                    .then(() => engines().mic.status())
                    .then(setMic);
                }}
              />
            ) : (
              <Button
                testID={readyCoachMarkIds.openSettings}
                label="Open Settings"
                onPress={() => engines().mic.openSettings()}
              />
            )}
          </View>
        </View>
      ) : null}
    </View>
  );
};
