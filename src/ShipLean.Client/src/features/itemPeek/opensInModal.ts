import type { RouteId, matchRoute } from '../../routes';

// The three Backlogs pages hard-navigate to the item page; everywhere else an item opens in the modal.
const hardNavigates: readonly RouteId[] = ['storiesBacklog', 'featuresBacklog', 'epicsBacklog'];

// Whether an item of `projectKey` opens in the modal over the page you are on: not on a Backlogs page, and only for
// the project the page belongs to. `here` is what `matchRoute` returned for the address, `openKey` its project key.
export function opensInModal(
  here: ReturnType<typeof matchRoute>,
  openKey: string | undefined,
  projectKey: string,
): boolean {
  return here !== undefined && !hardNavigates.includes(here.id) && openKey === projectKey;
}
