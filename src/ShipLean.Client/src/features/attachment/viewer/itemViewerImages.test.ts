import { expect, it } from 'vitest';
import type { AttachmentDto } from '@shiplean/core/api/attachments';
import { itemViewerImages } from './itemViewerImages';

const file = (name: string, kind: AttachmentDto['kind']): AttachmentDto => ({
  id: name,
  workItemId: 'w',
  name,
  contentType: 'x',
  sizeBytes: 1,
  uploadedBy: null,
  createdAt: '',
  createdLabel: '',
  downloadUrl: `/api/attachments/${name}/content`,
  kind,
  replacedCount: 0,
  agentName: null,
  via: null,
});

it('itemViewerImages_ShouldListEveryImageOfTheItem', () => {
  const images = itemViewerImages([
    file('a.png', 'image'),
    file('notes.md', 'markdown'),
    file('b.jpg', 'image'),
    file('c.zip', 'file'),
  ]);
  expect(images).toEqual([
    { src: '/api/attachments/a.png/content', alt: 'a.png', name: 'a.png' },
    { src: '/api/attachments/b.jpg/content', alt: 'b.jpg', name: 'b.jpg' },
  ]);
});
