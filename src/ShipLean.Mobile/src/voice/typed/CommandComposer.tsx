import { View } from 'react-native';
import type { ComposerReason } from './composerRequest';
import { commandComposerId } from './CommandComposer.testIds';

export interface CommandComposerProps {
  text: string;
  onChangeText: (text: string) => void;
  // Why the composer opened: the mic is off, the mic check failed, or a command is being edited.
  reason: ComposerReason;
  // Called after the command is sent.
  onSent: () => void;
}

// The dock's Command mode: a typed command goes through the same preview as a spoken one.
// TODO: the field, the reason line and Send (through useVoice().submitTyped) replace this empty view.
export function CommandComposer({ text, onChangeText, reason, onSent }: CommandComposerProps) {
  void text;
  void onChangeText;
  void reason;
  void onSent;
  return <View testID={commandComposerId} />;
}
