import { Platform, Text, View } from 'react-native';
import type { TuningResult } from '../../../voice/micTuning/line';
import { lineToSlider } from '../../../voice/micTuning/line';
import { useTheme } from '../../../theme';
import { LevelMeter, rmsToLevel } from '../LevelMeter';
import { copy, noSignalCauses, resultCopy } from '../micTuningCopy';
import { micTuningTestIds as ids } from '../MicTuningTest.testIds';

const RESULT_ID = {
  good: ids.resultGood,
  tooQuiet: ids.resultTooQuiet,
  noisy: ids.resultNoisy,
  noSignal: ids.resultNoSignal,
} as const;

export interface ResultProps {
  result: TuningResult;
  heard: string;
  // The line before this test, for the too-quiet note.
  oldLine: number;
}

// One result of the test: its headline, then what the numbers say. Words heard, never played back.
export function Result({ result, heard, oldLine }: ResultProps) {
  const theme = useTheme();
  const { colors } = theme;
  const { kind } = result;
  const text = resultCopy[kind];
  const tone = kind === 'noSignal' ? colors.danger : kind === 'good' ? colors.ok : colors.warn;
  const causes = noSignalCauses[Platform.OS === 'ios' ? 'ios' : 'android'];
  return (
    <View testID={RESULT_ID[kind]} style={{ gap: theme.space[3] }}>
      <View
        style={{
          borderWidth: 1,
          borderColor: tone,
          borderRadius: theme.radius.lg,
          padding: theme.space[3],
          gap: theme.space[1],
        }}
      >
        <Text
          style={{
            color: colors.text,
            fontSize: theme.font.size.md,
            fontWeight: theme.font.weight.semibold,
          }}
        >
          {text.title}
        </Text>
        <Text style={{ color: colors.muted, fontSize: theme.font.size.base }}>{text.body}</Text>
      </View>
      {kind === 'noSignal' ? (
        <View style={{ gap: theme.space[2] }}>
          <Text style={{ color: colors.muted, fontSize: theme.font.size.sm }}>
            CHECK THESE, THEN TEST AGAIN
          </Text>
          {causes.map((cause, i) => (
            <View
              key={cause.title}
              testID={ids.cause(i + 1)}
              style={{
                borderWidth: 1,
                borderColor: colors.border,
                borderRadius: theme.radius.md,
                padding: theme.space[3],
              }}
            >
              <Text style={{ color: colors.text, fontWeight: theme.font.weight.semibold }}>
                {`${i + 1}. ${cause.title}`}
              </Text>
              <Text style={{ color: colors.muted, fontSize: theme.font.size.base }}>
                {cause.body}
              </Text>
            </View>
          ))}
        </View>
      ) : (
        <>
          {kind === 'good' || kind === 'tooQuiet' ? (
            <View testID={ids.heard} style={{ gap: theme.space[1] }}>
              <Text style={{ color: colors.muted, fontSize: theme.font.size.sm }}>
                {copy.heard}
              </Text>
              <Text style={{ color: colors.text, fontSize: theme.font.size.md }}>
                {heard === '' ? copy.nothingHeard : `“${heard}”`}
              </Text>
            </View>
          ) : null}
          <LevelMeter
            testID={ids.meter}
            level={rmsToLevel(result.voiceRms)}
            roomRms={result.roomRms}
            voiceRms={result.voiceRms}
            lineRms={kind === 'noisy' ? undefined : result.lineRms}
          />
          {kind === 'tooQuiet' ? (
            <Text style={{ color: colors.muted, fontSize: theme.font.size.base }}>
              {`You peaked at ${Math.round(result.voiceRms)}. The old line was at ${Math.round(oldLine)}.`}
            </Text>
          ) : null}
          {kind === 'good' || kind === 'tooQuiet' ? (
            <Text testID={ids.sensitivity} style={{ color: colors.muted }}>
              {copy.sensitivity(lineToSlider(result.lineRms))}
            </Text>
          ) : null}
        </>
      )}
    </View>
  );
}
