import { View } from 'react-native';
import type { ScreenVoiceSpec } from '../contracts';
import { whatCanISayId } from './WhatCanISay.testIds';

export interface WhatCanISayProps {
  spec: ScreenVoiceSpec | null;
  onClose: () => void;
}

// The commands the focused screen understands.
// TODO: the spec's actions with an example each (web-only ones say where) replace this empty view.
export function WhatCanISay({ spec, onClose }: WhatCanISayProps) {
  void spec;
  void onClose;
  return <View testID={whatCanISayId} />;
}
