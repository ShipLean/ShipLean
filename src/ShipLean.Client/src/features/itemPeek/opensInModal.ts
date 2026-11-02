import type { matchRoute } from '../../routes';

// Whether an item of `projectKey` opens in the modal over the page you are on: on every page of the project the page
// belongs to, Backlogs included, and not on an install-level page or for another project. `here` is what `matchRoute`
// returned for the address, `openKey` its project key.
export function opensInModal(
  here: ReturnType<typeof matchRoute>,
  openKey: string | undefined,
  projectKey: string,
): boolean {
  return here !== undefined && openKey === projectKey;
}
