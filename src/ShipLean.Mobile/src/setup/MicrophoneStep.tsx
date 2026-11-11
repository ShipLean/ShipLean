import { useState, type FunctionComponent } from 'react';
import { Platform, Text, View } from 'react-native';
import { useTheme } from '../theme';
import { Button } from '../ui/Button';
import { engines } from '../voice/engines';
import { downloadView } from './downloadView';
import { microphoneStepIds } from './MicrophoneStep.testIds';
import { Body, Heading, ProgressLine, StepBody, StepFoot, TickedList } from './SetupParts';
import { useModelStatus } from './useModelStatus';

export interface MicrophoneStepProps {
  // After Continue, once the phone's prompt has been answered.
  onContinue: () => void;
  // Not now: the phone's prompt is not used up.
  onNotNow: () => void;
}

const PROMISES = [
  {
    id: 'hold',
    title: 'Listens only while you hold the button',
    body: "There's no wake word and nothing listens in the background.",
  },
  {
    id: 'phone',
    title: 'Turned into text on this phone',
    body: 'The models run here, so commands work without a voice service.',
  },
  {
    id: 'privacy',
    title: 'Nothing you say is kept; only the text you approve is posted',
  },
];

// Step 3: says what the permission is for before the phone asks. Continue asks once; Not now never asks, so the
// phone's own prompt is still there the first time the mic is held (microphone D3).
export const MicrophoneStep: FunctionComponent<MicrophoneStepProps> = ({
  onContinue,
  onNotNow,
}) => {
  const theme = useTheme();
  const status = useModelStatus();
  const [asking, setAsking] = useState(false);
  const downloading = status.phase === 'downloading';
  const percent = downloadView(status).percent;

  const ask = async () => {
    setAsking(true);
    try {
      await engines().mic.request();
    } finally {
      setAsking(false);
    }
    onContinue();
  };

  return (
    <>
      <StepBody testID={microphoneStepIds.root}>
        {downloading ? (
          <View testID={microphoneStepIds.downloadLine} style={{ gap: theme.space[1] }}>
            <Text style={{ color: theme.colors.muted, fontSize: theme.font.size.base }}>
              {`Voice models downloading · ${percent}%`}
            </Text>
            <ProgressLine
              testID={`${microphoneStepIds.downloadLine}-bar`}
              fraction={percent / 100}
              height={3}
            />
          </View>
        ) : null}
        <Heading>Allow the microphone</Heading>
        <Body muted>Voice needs it to hear your commands.</Body>
        <TickedList lines={PROMISES} testID={microphoneStepIds.promises} />
        <Text
          testID={microphoneStepIds.next}
          style={{ color: theme.colors.muted, fontSize: theme.font.size.base }}
        >
          {`Next, your phone asks for permission. You can change it any time in ${
            Platform.OS === 'android' ? "the app's settings" : 'Settings'
          }.`}
        </Text>
      </StepBody>
      <StepFoot>
        <Button
          testID={microphoneStepIds.continue}
          label="Continue"
          busy={asking}
          onPress={() => void ask()}
        />
        <Button
          testID={microphoneStepIds.notNow}
          label="Not now"
          variant="ghost"
          onPress={onNotNow}
        />
      </StepFoot>
    </>
  );
};
