import { sendForm } from '../../../api/client';
import type { AttachmentDto } from '../../../api/attachments';

/** Attaches one file as multipart form data. */
export function uploadAttachment(workItemId: string, file: File): Promise<AttachmentDto> {
  const form = new FormData();
  form.append('file', file, file.name);
  return sendForm<AttachmentDto>(
    'POST',
    `/api/work-items/${encodeURIComponent(workItemId)}/attachments`,
    form,
    'That file is too large to attach.',
  );
}
