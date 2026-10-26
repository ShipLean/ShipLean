import { pagesTestIds } from '../features/pages/testIds';

// What a page shows when the project or its sprints could not be read for a reason other than not existing.
export function LoadFailedState() {
  return (
    <section data-testid={pagesTestIds.loadFailed} className="p-6">
      <h1 className="page-title">Could not load this page</h1>
      <p className="text-ink-muted">Reload the page to try again.</p>
    </section>
  );
}
