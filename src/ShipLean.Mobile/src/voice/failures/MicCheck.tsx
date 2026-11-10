import { useEffect, useState } from 'react';
import { Text, View } from 'react-native';
import { useTheme } from '../../theme';
import { BottomSheet } from '../../ui/BottomSheet';
import { Button } from '../../ui/Button';
import { engines } from '../engines';
import { MIC_CHECK_COPY } from './heardCopy';
import {
  micCheckDoneId,
  micCheckId,
  micCheckInputId,
  micCheckLeadId,
  micCheckLevelId,
  micCheckLevelLabelId,
  micCheckTypeInsteadId,
} from './MicCheck.testIds';

export interface MicCheckProps {
  onTypeInstead: () => void;
  onClose: () => void;
}

const BARS = 16;
// Under this the meter reads Quiet.
const QUIET = 0.05;

// After the third silent hold in a row: a live level meter and the input in use, so the person can see whether the
// phone hears anything at all. It runs its own recognition session and cancels it when it closes; the audio stays in
// memory for the meter and is never kept. Switching input isn't something the phone's speech engine offers, so
// there's no "Use iPhone mic".
export function MicCheck({ onTypeInstead, onClose }: MicCheckProps) {
  const theme = useTheme();
  const { colors } = theme;
  const [level, setLevel] = useState(0);
  const [input, setInput] = useState<string | null>(() => engines().recognizer.inputName());
  const [unavailable, setUnavailable] = useState(false);

  useEffect(() => {
    let closed = false;
    let cancel: (() => void) | null = null;
    const recognizer = engines().recognizer;
    recognizer.start({ hotwords: [] }).then(
      (session) => {
        if (closed) {
          session.cancel();
          return;
        }
        cancel = () => session.cancel();
        session.onLevel((l) => setLevel(Math.min(1, Math.max(0, l))));
        setInput(recognizer.inputName());
      },
      () => {
        if (!closed) setUnavailable(true);
      },
    );
    return () => {
      closed = true;
      cancel?.();
    };
  }, []);

  const lit = Math.ceil(level * BARS);
  const copy = MIC_CHECK_COPY;
  return (
    <BottomSheet title={copy.title} open onClose={onClose} testID={micCheckId}>
      <View style={{ gap: theme.space[3] }}>
        <View>
          <Text
            testID={micCheckLeadId}
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
        {unavailable ? (
          <Text style={{ color: colors.warn, fontSize: theme.font.size.sm }}>
            {copy.unavailable}
          </Text>
        ) : (
          <View
            style={{ flexDirection: 'row', alignItems: 'center', gap: theme.space[2] }}
            accessible
            accessibilityLabel="Microphone level"
          >
            <View
              testID={micCheckLevelId}
              accessibilityRole="progressbar"
              accessibilityValue={{ min: 0, max: 100, now: Math.round(level * 100) }}
              style={{ flex: 1, flexDirection: 'row', gap: 2, height: 20, alignItems: 'flex-end' }}
            >
              {Array.from({ length: BARS }, (_, i) => (
                <View
                  key={i}
                  style={{
                    flex: 1,
                    height: 8 + (12 * i) / (BARS - 1),
                    borderRadius: 2,
                    backgroundColor: i < lit ? colors.accent : colors.border,
                  }}
                />
              ))}
            </View>
            <Text
              testID={micCheckLevelLabelId}
              style={{ color: colors.muted, fontSize: theme.font.size.sm }}
            >
              {level <= QUIET ? copy.levelQuiet : copy.levelHearing}
            </Text>
          </View>
        )}
        <View testID={micCheckInputId} style={{ gap: theme.space[1] }}>
          {input === null ? (
            <Text style={{ color: colors.muted, fontSize: theme.font.size.sm }}>
              {copy.noInput}
            </Text>
          ) : (
            <>
              <Text
                style={{
                  color: colors.text,
                  fontSize: theme.font.size.body15,
                  fontWeight: theme.font.weight.semibold,
                }}
              >
                {input}
              </Text>
              <Text style={{ color: colors.muted, fontSize: theme.font.size.sm }}>
                {copy.inputDetail}
              </Text>
            </>
          )}
        </View>
        <View style={{ flexDirection: 'row', gap: theme.space[2] }}>
          <View style={{ flex: 1 }}>
            <Button
              testID={micCheckTypeInsteadId}
              label={copy.typeInstead}
              variant="ghost"
              onPress={onTypeInstead}
            />
          </View>
          <View style={{ flex: 1 }}>
            <Button testID={micCheckDoneId} label={copy.done} onPress={onClose} />
          </View>
        </View>
      </View>
    </BottomSheet>
  );
}
