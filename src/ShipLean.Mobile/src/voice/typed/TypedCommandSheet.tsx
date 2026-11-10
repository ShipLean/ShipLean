import { useState } from 'react';
import { BottomSheet } from '../../ui/BottomSheet';
import { CommandComposer } from './CommandComposer';
import { typedCommandSheetId } from './TypedCommandSheet.testIds';
import { useComposerRequest, useDockFocused, type ComposerRequest } from './composerRequest';

// Screens with no dock (the tab roots) have nowhere for a Command composer request to open. The same composer shows
// in a sheet there, so "Type instead" is never a dead button; Preview hands the text to the voice preview as the
// dock's Command mode does.
export function TypedCommandSheet() {
  const { request, consume } = useComposerRequest();
  const docked = useDockFocused();
  if (request === null || request.mode !== 'command' || docked) return null;
  return <Sheet key={request.text + String(request.reason)} request={request} close={consume} />;
}

function Sheet({ request, close }: { request: ComposerRequest; close: () => void }) {
  const [text, setText] = useState(request.text);
  return (
    <BottomSheet testID={typedCommandSheetId} title="Command" open onClose={close}>
      <CommandComposer text={text} onChangeText={setText} reason={request.reason} onSent={close} />
    </BottomSheet>
  );
}
