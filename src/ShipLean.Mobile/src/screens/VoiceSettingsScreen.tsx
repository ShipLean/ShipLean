import { VoiceSettings } from '../setup/VoiceSettings';
import { Screen } from '../ui/Screen';
import { TopBar } from '../ui/TopBar';
import { SettingsBack } from './SettingsBack';
import { screenIds } from './testIds';

// Settings › Voice: this phone's voice models, storage and microphone. Touch only: no voice here.
export function VoiceSettingsScreen() {
  return (
    <Screen testID={screenIds.settingsVoice}>
      <TopBar title="Voice" left={<SettingsBack />} />
      <VoiceSettings />
    </Screen>
  );
}
