import { StackPlaceholder } from '../shell/StackPlaceholder';
import { screenIds } from './testIds';

// TODO: Settings › Voice replaces this placeholder.
export function VoiceSettingsScreen() {
  return <StackPlaceholder title="Voice" testID={screenIds.settingsVoice} />;
}
