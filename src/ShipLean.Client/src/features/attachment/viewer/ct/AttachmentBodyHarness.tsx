import { useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { Route, Routes } from 'react-router-dom';
import type { AttachmentDto, AttachmentMarkdownDto } from '@shiplean/core/api/attachments';
import { projectByKeyKey } from '../../../../queries/shared';
import { ImageViewerHost } from '../../../../touch/imageViewer';
import { workItemAttachmentsKey } from '../../../workItem/workItemApi';
import { AttachmentBody } from '../../body/AttachmentBody';

const image = (id: string): AttachmentDto => ({
  id,
  workItemId: 'w1',
  name: `${id}.png`,
  contentType: 'image/png',
  sizeBytes: 100,
  uploadedBy: null,
  createdAt: '2026-11-01T00:00:00Z',
  createdLabel: '1 Nov',
  downloadUrl: `/api/attachments/${id}/content`,
  kind: 'image',
  replacedCount: 0,
  agentName: null,
  via: null,
});

const data = {
  workItem: {
    id: 'w1',
    key: 'SHIP-100',
    title: 'Checkout',
    type: { id: 'feature', name: 'Feature' },
  },
  markdown: '![b](b.png)',
  html: '<figure data-md-image="attached"><img src="/api/attachments/b/content" alt="b"><figcaption>b.png</figcaption></figure>',
} as unknown as AttachmentMarkdownDto;

function Seeded({ seedImages }: { seedImages: boolean }) {
  const client = useQueryClient();
  // The item's three images and its project are already in the cache, as when the page is reached from the item.
  useState(() => {
    client.setQueryData(projectByKeyKey('SHIP'), { id: 'p1', key: 'SHIP' });
    if (seedImages) {
      client.setQueryData(workItemAttachmentsKey('p1', 'SHIP-100'), ['a', 'b', 'c'].map(image));
    }
  });
  return <AttachmentBody data={data} view="rendered" />;
}

/** AttachmentBody on a route with a project key, its item's attachments cached unless `seedImages` is false (CT only). */
export function AttachmentBodyHarness({ seedImages = true }: { seedImages?: boolean }) {
  return (
    <>
      <Routes>
        <Route path="/p/:key/*" element={<Seeded seedImages={seedImages} />} />
      </Routes>
      <ImageViewerHost />
    </>
  );
}
