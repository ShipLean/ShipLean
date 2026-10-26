import type { AttachmentDto } from '../../../api/attachments';
import { Dialog } from '../../../components/Dialog';
import { featureTestIds as ids } from '../testIds';

export function ImagePreview({
  attachment,
  onClose,
}: {
  attachment: AttachmentDto;
  onClose: () => void;
}) {
  return (
    <Dialog title={attachment.name} onClose={onClose} width="720px">
      <div data-testid={ids.imagePreview} className="grid place-items-center">
        <img
          data-testid={ids.imagePreviewImage}
          src={attachment.downloadUrl}
          alt={attachment.name}
          className="max-h-[70dvh] max-w-full object-contain"
        />
        <button
          type="button"
          data-testid={ids.imagePreviewClose}
          className="mt-3 h-7 rounded-control border border-edge px-3 text-[length:var(--text-sm)] text-ink hover:bg-signal-soft"
          onClick={onClose}
        >
          Close
        </button>
      </div>
    </Dialog>
  );
}
