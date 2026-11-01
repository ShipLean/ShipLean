import { analyticsTestIds } from './testIds';

// TODO: the provider, host, site key and names settings, a Save button and a test-event button replace this placeholder.
export function InstanceAnalytics() {
  return (
    <section data-testid={analyticsTestIds.container} aria-label="Analytics">
      <h2 className="text-[length:var(--text-sm)] font-semibold text-ink">Analytics</h2>
    </section>
  );
}
