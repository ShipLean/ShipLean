import { useEffect } from 'react';
import { ScrollView } from 'react-native';
import { useLocalSearchParams, useRouter } from 'expo-router';
import { Text } from 'react-native';
import type { MicTuningFrom } from '../../links';
import { micTuningOpened } from '../../voice/micTuning/events';
import { useTheme } from '../../theme';
import { Screen } from '../../ui/Screen';
import { TopBar } from '../../ui/TopBar';
import { screenIds } from '../testIds';
import { AdjustByHand } from './AdjustByHand';
import { InputCard } from './InputCard';
import { MicTuningTest } from './MicTuningTest';
import { micTuningBackId } from './MicTuningScreen.testIds';

const FROMS: readonly MicTuningFrom[] = ['second_hold', 'third_hold', 'settings'];
const fromOf = (value: unknown): MicTuningFrom => FROMS.find((f) => f === value) ?? 'settings';

// The Mic tuning page: the input in use, the two-step test, and Adjust by hand. Full screen, pushed over everything.
// Touch only: no voice here.
export function MicTuningScreen() {
  const router = useRouter();
  const theme = useTheme();
  const params = useLocalSearchParams<{ from?: string; key?: string }>();
  const from = fromOf(params.from);
  const key = params.key !== undefined && params.key !== '' ? params.key : undefined;

  useEffect(() => {
    micTuningOpened(from);
  }, [from]);

  return (
    <Screen testID={screenIds.micTuning}>
      <TopBar
        title="Mic tuning"
        left={
          <Text
            testID={micTuningBackId}
            accessibilityRole="button"
            onPress={() => router.back()}
            style={{ color: theme.colors.accent, fontSize: theme.font.size.md }}
          >
            {key === undefined ? 'Back' : `Back to ${key}`}
          </Text>
        }
      />
      <ScrollView contentContainerStyle={{ padding: theme.space[4], gap: theme.space[4] }}>
        <InputCard />
        <MicTuningTest from={from} itemKey={key} />
        <AdjustByHand />
      </ScrollView>
    </Screen>
  );
}
