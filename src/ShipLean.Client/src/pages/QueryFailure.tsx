import { ApiRequestError } from '../api/client';
import { LoadFailedState } from './LoadFailedState';
import { NotFoundState } from './NotFoundState';

// A failed read of the project or its sprints: not found is the not-found state, anything else the load-failed one.
export function QueryFailure({ error }: { error: unknown }) {
  return error instanceof ApiRequestError && error.code === 'not_found' ? (
    <NotFoundState />
  ) : (
    <LoadFailedState />
  );
}
