import { useRouter } from 'expo-router';
import { Text } from 'react-native';
import { links, type SetupStep } from '../links';
import { DownloadingStep } from '../setup/DownloadingStep';
import { MicrophoneStep } from '../setup/MicrophoneStep';
import { ModelsStep } from '../setup/ModelsStep';
import { useModelStatus } from '../setup/useModelStatus';
import { engines } from '../voice/engines';
import { useTheme } from '../theme';
import { Screen } from '../ui/Screen';
import { TopBar } from '../ui/TopBar';
import { screenSetup } from './testIds';

export const setupSubId = 'setup-sub-line';

export interface SetupScreenProps {
  step: SetupStep;
}

const TITLE: Record<SetupStep, string> = {
  models: 'Set up voice',
  microphone: 'Microphone',
  downloading: 'Voice models',
};

// Steps 2 and 3 of first run (sign-in is step 1), and the Downloading page. Touch only: no voice here.
export function SetupScreen({ step }: SetupScreenProps) {
  const router = useRouter();
  const theme = useTheme();
  const toProjects = () => router.dismissTo(links.projects());
  const status = useModelStatus();
  const phone = `${engines().models.deviceMemory().soldGb} GB phone`;
  const stepNumber = step === 'models' ? 2 : step === 'microphone' ? 3 : null;
  return (
    <Screen testID={screenSetup(step)}>
      <TopBar title={TITLE[step]} />
      {step !== 'downloading' ? null : (
        <Text
          testID={setupSubId}
          style={{
            color: theme.colors.muted,
            fontSize: theme.font.size.base,
            paddingHorizontal: theme.space[4],
            paddingTop: theme.space[2],
          }}
        >
          {`${phone} · ${status.wifiOnly ? 'Wi-Fi only' : 'mobile data'}`}
        </Text>
      )}
      {stepNumber === null ? null : (
        <Text
          style={{
            color: theme.colors.muted,
            fontSize: theme.font.size.base,
            paddingHorizontal: theme.space[4],
            paddingTop: theme.space[2],
          }}
        >
          {`Step ${stepNumber} of 3`}
        </Text>
      )}
      {step === 'models' ? (
        <ModelsStep
          onContinue={() => router.replace(links.setup('microphone'))}
          onSkip={toProjects}
        />
      ) : step === 'microphone' ? (
        <MicrophoneStep
          onContinue={() => router.replace(links.setup('downloading'))}
          onNotNow={() => router.replace(links.setup('downloading'))}
        />
      ) : (
        <DownloadingStep
          onUseApp={toProjects}
          onSetUp={() => router.replace(links.setup('models'))}
        />
      )}
    </Screen>
  );
}
