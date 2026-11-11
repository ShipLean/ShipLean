import { useRouter } from 'expo-router';
import { useCallback, useEffect, useRef, useState } from 'react';
import { Platform, Pressable, Text, View } from 'react-native';
import { systemNow } from '../../actions/clock';
import type { MicTuningFrom } from '../../links';
import { useTheme } from '../../theme';
import { Button } from '../../ui/Button';
import { PHONE_INPUT_ID, type RecognitionSession } from '../../voice/contracts';
import { engines } from '../../voice/engines';
import { micTuningResult } from '../../voice/micTuning/events';
import { activeLine, classify, DEFAULT_FLOOR_RMS } from '../../voice/micTuning/line';
import type { TuningResult } from '../../voice/micTuning/line';
import { requestTypeInstead } from '../../voice/micTuning/returnIntent';
import { LevelMeter } from './LevelMeter';
import { copy, hotwordsFor, phraseFor, QUIET_SECONDS } from './micTuningCopy';
import { micTuningTestId, micTuningTestIds as ids } from './MicTuningTest.testIds';
import { Result } from './test/Result';
import { useLiveLevel } from './useLiveLevel';

export interface MicTuningTestProps {
  from: MicTuningFrom;
  // The item the person came from; the test's phrase uses a key from it.
  itemKey?: string;
  // The clock for the test's timestamp.
  now?: () => Date;
}

type Phase =
  | { kind: 'start' }
  | { kind: 'quiet' }
  | { kind: 'speak'; room: number[] }
  | { kind: 'result'; result: TuningResult; heard: string; oldLine: number };

// The two-step test: the room for 3 s, then the voice held on the big mic. The result sets the speech line, for
// good and too quiet. The phrase is shown only; it is never parsed or applied.
export function MicTuningTest({ from, itemKey, now = systemNow }: MicTuningTestProps) {
  const theme = useTheme();
  const router = useRouter();
  const [phase, setPhase] = useState<Phase>({ kind: 'start' });
  const [failedStart, setFailedStart] = useState(false);
  const [level, setLevel] = useState(0);
  const [partial, setPartial] = useState('');
  const [remaining, setRemaining] = useState(QUIET_SECONDS);
  const [holding, setHolding] = useState(false);
  const live = useLiveLevel(phase.kind === 'start');
  const notReady = live.failed || failedStart;

  const sessions = useRef<Set<RecognitionSession>>(new Set());
  const pressed = useRef(false);
  const held = useRef<RecognitionSession | null>(null);
  const alive = useRef(true);
  useEffect(() => {
    alive.current = true;
    const open = sessions.current;
    return () => {
      alive.current = false;
      for (const s of open) s.cancel();
      open.clear();
    };
  }, []);

  const open = (promise: Promise<RecognitionSession>): Promise<RecognitionSession> =>
    promise.then((s) => {
      sessions.current.add(s);
      return s;
    });
  const close = (s: RecognitionSession) => sessions.current.delete(s);

  // Step 1.
  const quiet = phase.kind === 'quiet';
  useEffect(() => {
    if (!quiet) return undefined;
    let elapsed = 0;
    const started = open(engines().recognizer.start({ hotwords: [] }));
    started.then(
      (s) => s.onLevel((l) => alive.current && setLevel(l)),
      () => {
        if (alive.current) {
          setFailedStart(true);
          setPhase({ kind: 'start' });
        }
      },
    );
    const timer = setInterval(() => {
      elapsed += 1;
      setRemaining(QUIET_SECONDS - elapsed);
      if (elapsed < QUIET_SECONDS) return;
      clearInterval(timer);
      started.then(
        (s) => {
          const room = s.frames();
          s.cancel();
          close(s);
          if (alive.current) setPhase({ kind: 'speak', room });
        },
        () => {},
      );
    }, 1000);
    return () => clearInterval(timer);
  }, [quiet]);

  const finish = useCallback(
    (room: number[], voice: number[], heard: string) => {
      const { tuning, recognizer } = engines();
      const result = classify(room, voice);
      const inputId = recognizer.inputInUse()?.id ?? PHONE_INPUT_ID;
      const oldLine = activeLine(tuning.tuning(inputId)) ?? DEFAULT_FLOOR_RMS;
      if (result.kind === 'good' || result.kind === 'tooQuiet') {
        tuning.save({
          inputId,
          roomRms: result.roomRms,
          voiceRms: result.voiceRms,
          lineRms: result.lineRms,
          testedAt: now().toISOString(),
          automatic: true,
          manualStep: null,
        });
      }
      micTuningResult(result.kind, Platform.OS);
      setPhase({ kind: 'result', result, heard, oldLine });
    },
    [now],
  );

  // Step 2: hold to talk.
  const holdStart = () => {
    if (phase.kind !== 'speak') return;
    pressed.current = true;
    setHolding(true);
    setPartial('');
    setLevel(0);
    open(engines().recognizer.start({ hotwords: hotwordsFor(itemKey), decodeAlways: true })).then(
      (s) => {
        if (!pressed.current) {
          // Let go before the stream was up: nothing to measure.
          s.cancel();
          close(s);
          return;
        }
        held.current = s;
        s.onPartial((t) => alive.current && setPartial(t));
        s.onLevel((l) => alive.current && setLevel(l));
      },
      () => {
        pressed.current = false;
        if (alive.current) {
          setHolding(false);
          setFailedStart(true);
          setPhase({ kind: 'start' });
        }
      },
    );
  };
  const holdEnd = () => {
    if (phase.kind !== 'speak' || !pressed.current) return;
    pressed.current = false;
    setHolding(false);
    const s = held.current;
    held.current = null;
    if (s === null) return;
    const room = phase.room;
    s.stop().then((transcript) => {
      close(s);
      if (alive.current) finish(room, s.frames(), transcript.text);
    });
  };

  const begin = () => {
    setFailedStart(false);
    setLevel(0);
    setRemaining(QUIET_SECONDS);
    setPhase({ kind: 'quiet' });
  };
  const typeInstead = () => {
    requestTypeInstead();
    router.back();
  };
  const typeButton =
    from === 'settings' ? null : (
      <Button
        testID={ids.typeInstead}
        variant="ghost"
        label={copy.typeInstead}
        onPress={typeInstead}
      />
    );
  const title = { color: theme.colors.text, fontSize: theme.font.size.lg } as const;
  const muted = { color: theme.colors.muted, fontSize: theme.font.size.base } as const;

  let body;
  if (phase.kind === 'start') {
    body = notReady ? (
      <View testID={ids.notReady} style={{ gap: theme.space[3] }}>
        <Text style={muted}>{copy.notReady}</Text>
        {typeButton}
      </View>
    ) : (
      <View testID={ids.start} style={{ gap: theme.space[3] }}>
        <Text style={title}>{copy.startTitle}</Text>
        <Text style={muted}>{copy.startBody}</Text>
        <LevelMeter testID={ids.meter} level={live.level} />
        <Button testID={ids.startButton} label={copy.startButton} onPress={begin} />
        {typeButton}
      </View>
    );
  } else if (phase.kind === 'quiet') {
    body = (
      <View testID={ids.stepQuiet} style={{ gap: theme.space[3] }}>
        <Text style={title}>{copy.quietTitle}</Text>
        <Text style={muted}>{copy.quietStep}</Text>
        <Text testID={ids.countdown} style={title}>
          {copy.quietLeft(remaining)}
        </Text>
        <LevelMeter testID={ids.meter} level={level} />
      </View>
    );
  } else if (phase.kind === 'speak') {
    body = (
      <View testID={ids.stepSpeak} style={{ gap: theme.space[3] }}>
        <Text style={title}>{copy.speakTitle}</Text>
        <Text style={muted}>{copy.speakStep}</Text>
        <Text style={muted}>{copy.speakRead}</Text>
        <Text testID={ids.phrase} style={{ ...title, fontWeight: theme.font.weight.semibold }}>
          {`“${phraseFor(itemKey)}”`}
        </Text>
        <Text testID={ids.partial} style={{ color: theme.colors.text }}>
          {partial}
        </Text>
        <LevelMeter testID={ids.meter} level={level} />
        <Pressable
          testID={ids.hold}
          accessibilityRole="button"
          accessibilityLabel={copy.holdHint}
          onPressIn={holdStart}
          onPressOut={holdEnd}
          style={{
            alignSelf: 'center',
            minWidth: 88,
            minHeight: 88,
            borderRadius: 44,
            alignItems: 'center',
            justifyContent: 'center',
            backgroundColor: holding ? theme.colors.danger : theme.colors.accent,
          }}
        >
          <Text style={{ color: theme.colors.accentFg }}>{copy.holdHint}</Text>
        </Pressable>
        <Text style={{ ...muted, textAlign: 'center' }}>{holding ? copy.holdRelease : ''}</Text>
      </View>
    );
  } else {
    const failure = phase.result.kind === 'noSignal' || phase.result.kind === 'noisy';
    body = (
      <View style={{ gap: theme.space[3] }}>
        <Result result={phase.result} heard={phase.heard} oldLine={phase.oldLine} />
        <Button
          testID={ids.tryAgain}
          label={copy.tryAgain}
          onPress={() => setPhase({ kind: 'start' })}
        />
        {failure ? typeButton : null}
      </View>
    );
  }
  return (
    <View testID={micTuningTestId} style={{ gap: theme.space[3] }}>
      {body}
    </View>
  );
}
