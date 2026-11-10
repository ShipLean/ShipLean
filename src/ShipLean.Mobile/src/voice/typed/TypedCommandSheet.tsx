import { TypedCommandSheetBody } from './TypedCommandSheetBody';
import { useComposerRequest, useDockFocused } from './composerRequest';

// Screens with no dock (the tab roots) have nowhere for a Command composer request to open. The same composer shows
// in a sheet there, so "Type instead" is never a dead button; Preview hands the text to the voice preview as the
// dock's Command mode does.
export function TypedCommandSheet() {
  const { request, consume } = useComposerRequest();
  const docked = useDockFocused();
  if (request === null || request.mode !== 'command' || docked) return null;
  return (
    <TypedCommandSheetBody
      key={request.text + String(request.reason)}
      request={request}
      close={consume}
    />
  );
}
