import { useState } from 'react';
import { Pressable, Switch, Text, View } from 'react-native';
import { useTheme } from '../../theme';
import { Button } from '../../ui/Button';
import { engines } from '../../voice/engines';
import {
  DEFAULT_FLOOR_RMS,
  activeLine,
  lineToSlider,
  sliderToLine,
} from '../../voice/micTuning/line';
import {
  adjustAutomaticId,
  adjustByHandId,
  adjustFailedId,
  adjustMeterId,
  adjustSaveId,
  adjustStepId,
  adjustToggleId,
} from './AdjustByHand.testIds';
import { LevelMeter } from './LevelMeter';
import { useVoiceInputs } from './input/useVoiceInputs';
import { useLiveLevel } from './useLiveLevel';

const STEPS = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];

// Collapsed by default. Automatic uses the tested line; off, ten steps move the line on a live meter.
export function AdjustByHand() {
  const { inUse } = useVoiceInputs();
  // One panel per input, so a draft never carries over to another input.
  return <Panel key={inUse.id} inputId={inUse.id} />;
}

function Panel({ inputId }: { inputId: string }) {
  const theme = useTheme();
  const { colors } = theme;
  const { tuningOf } = useVoiceInputs();
  const tuning = tuningOf(inputId);
  const [expanded, setExpanded] = useState(false);
  const [automatic, setAutomatic] = useState(tuning?.automatic ?? true);
  const [step, setStep] = useState(
    tuning?.manualStep ?? lineToSlider(activeLine(tuning) ?? DEFAULT_FLOOR_RMS),
  );
  // A test (or Settings) saving a new line replaces the draft: the panel shows what is stored.
  const stored = `${tuning?.automatic}|${tuning?.manualStep}|${tuning?.testedAt}|${tuning?.lineRms}`;
  const [seen, setSeen] = useState(stored);
  if (seen !== stored) {
    setSeen(stored);
    setAutomatic(tuning?.automatic ?? true);
    setStep(tuning?.manualStep ?? lineToSlider(activeLine(tuning) ?? DEFAULT_FLOOR_RMS));
  }
  const { level, failed } = useLiveLevel(expanded);
  const store = engines().tuning;

  const base = (t: typeof tuning) =>
    t ?? {
      inputId,
      roomRms: DEFAULT_FLOOR_RMS,
      voiceRms: DEFAULT_FLOOR_RMS,
      lineRms: DEFAULT_FLOOR_RMS,
      // Not tested: an empty time reads as never tested.
      testedAt: '',
    };

  const onAutomatic = (on: boolean) => {
    setAutomatic(on);
    // Back on: the tested line comes back. Nothing to save when there was never a test or a manual line.
    if (on && tuning !== null && !tuning.automatic) {
      store.save({ ...tuning, automatic: true, manualStep: null });
    }
  };
  const save = () => {
    const t = base(tuning);
    store.save({ ...t, automatic: false, manualStep: step });
  };
  const lineRms = automatic ? tuning?.lineRms : sliderToLine(step);

  return (
    <View
      testID={adjustByHandId}
      style={{
        gap: theme.space[3],
        padding: theme.space[3],
        borderRadius: theme.radius.md,
        borderWidth: 1,
        borderColor: colors.border,
        backgroundColor: colors.surface,
      }}
    >
      <Pressable
        testID={adjustToggleId}
        accessibilityRole="button"
        accessibilityState={{ expanded }}
        onPress={() => setExpanded((e) => !e)}
      >
        <Text
          style={{
            color: colors.text,
            fontSize: theme.font.size.md,
            fontWeight: theme.font.weight.semibold,
          }}
        >
          Adjust by hand {expanded ? '▴' : '▾'}
        </Text>
      </Pressable>
      {expanded ? (
        <View style={{ gap: theme.space[3] }}>
          <Text style={{ color: colors.muted, fontSize: theme.font.size.base }}>
            Talk and move the line until your voice clears it but the room doesn't.
          </Text>
          {failed ? (
            <Text testID={adjustFailedId} style={{ color: colors.warn }}>
              The meter can't listen right now.
            </Text>
          ) : null}
          <LevelMeter
            testID={adjustMeterId}
            level={level}
            lineRms={lineRms}
            roomRms={tuning?.roomRms}
          />
          <View style={{ flexDirection: 'row', alignItems: 'center', gap: theme.space[3] }}>
            <View style={{ flex: 1 }}>
              <Text style={{ color: colors.text, fontWeight: theme.font.weight.semibold }}>
                Automatic
              </Text>
              <Text style={{ color: colors.muted, fontSize: theme.font.size.base }}>
                Set by the test
              </Text>
            </View>
            <Switch
              testID={adjustAutomaticId}
              accessibilityLabel="Automatic"
              value={automatic}
              onValueChange={onAutomatic}
            />
          </View>
          {automatic ? null : (
            <View style={{ gap: theme.space[2] }}>
              <View style={{ flexDirection: 'row', gap: 4 }}>
                {STEPS.map((s) => (
                  <Pressable
                    key={s}
                    testID={adjustStepId(s)}
                    accessibilityRole="button"
                    accessibilityLabel={`Sensitivity ${s} of 10`}
                    accessibilityState={{ selected: s === step }}
                    onPress={() => setStep(s)}
                    style={{
                      flex: 1,
                      minHeight: theme.touch.row,
                      alignItems: 'center',
                      justifyContent: 'center',
                      borderRadius: theme.radius.md,
                      backgroundColor: s <= step ? colors.accent : colors.surface2,
                    }}
                  >
                    <Text style={{ color: s <= step ? colors.surface : colors.text }}>{s}</Text>
                  </Pressable>
                ))}
              </View>
              <View style={{ flexDirection: 'row', justifyContent: 'space-between' }}>
                <Text style={{ color: colors.muted, fontSize: theme.font.size.base }}>
                  Less sensitive
                </Text>
                <Text style={{ color: colors.muted, fontSize: theme.font.size.base }}>
                  More sensitive
                </Text>
              </View>
              <Button testID={adjustSaveId} label="Save" onPress={save} />
            </View>
          )}
        </View>
      ) : null}
    </View>
  );
}
