import { ApiRequestError } from '@shiplean/core/api/client';
import { ReviewBar } from '../ReviewBar';
import { CHANGES } from './fixtures';

// Approve fails with a 409 conflict. The error is built here, in the browser bundle, because an ApiRequestError made in
// a spec runs in node and loses its class when it crosses to the page.
export function ConflictingReviewBar() {
  return (
    <ReviewBar
      data={CHANGES}
      projectKey="SHIP"
      itemKey="SHIP-171"
      review={{
        approve: () => Promise.reject(new ApiRequestError('stale', 409, 'conflict')),
        requestChanges: () => Promise.reject(new Error('unused')),
      }}
    />
  );
}
