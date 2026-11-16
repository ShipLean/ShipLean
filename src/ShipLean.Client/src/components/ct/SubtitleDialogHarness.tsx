import { Dialog } from '../Dialog';
import { LONG_TITLE, SUBTITLE } from './subtitleDialogText';

/** CT-only: a Dialog with an icon, a title too long for one line and a subtitle. */
export function SubtitleDialogHarness() {
  return (
    <Dialog
      title={LONG_TITLE}
      subtitle={SUBTITLE}
      titleIcon={<span aria-hidden="true">●</span>}
      onClose={() => undefined}
    >
      <p className="m-0">Body</p>
    </Dialog>
  );
}
