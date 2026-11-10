import { useState } from 'react';
import { KeyboardAvoidingView, Platform } from 'react-native';
import { BottomSheet } from '../../ui/BottomSheet';
import { CommandComposer } from './CommandComposer';
import { typedCommandSheetId } from './TypedCommandSheet.testIds';
import type { ComposerRequest } from './composerRequest';

// The sheet around one Command composer request. Its text starts from the request, so the parent keys it by the request
// to start again when a new one arrives.
export function TypedCommandSheetBody({
  request,
  close,
}: {
  request: ComposerRequest;
  close: () => void;
}) {
  const [text, setText] = useState(request.text);
  return (
    <BottomSheet testID={typedCommandSheetId} title="Command" open onClose={close}>
      <KeyboardAvoidingView
        testID={`${typedCommandSheetId}-keyboard`}
        behavior={Platform.OS === 'ios' ? 'padding' : undefined}
      >
        <CommandComposer
          text={text}
          onChangeText={setText}
          reason={request.reason}
          onSent={close}
        />
      </KeyboardAvoidingView>
    </BottomSheet>
  );
}
