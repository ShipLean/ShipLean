import { pagesTestIds } from '../features/pages/testIds';

// What a page shows for a project, sprint or address that does not exist or that you cannot see (the server answers
// both the same way).
export function NotFoundState() {
  return (
    <section data-testid={pagesTestIds.notFound} className="p-6">
      <h1 className="page-title">Not found</h1>
      <p className="text-ink-muted">This page does not exist, or you do not have access to it.</p>
    </section>
  );
}
