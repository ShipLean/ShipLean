/** The warn callout above the recurring table; hidden when no theme is recurring. */
export function RecurringCallout({ count }: { count: number }) {
  if (count === 0) return null;
  const noun = count === 1 ? 'theme keeps' : 'themes keep';
  return (
    <div
      role="alert"
      data-callout="recurring"
      className="rounded-panel border border-edge border-l-[3px] border-l-caution bg-sunken px-3 py-2 text-[length:var(--text-sm)]"
    >
      <b className="block">
        {count} {noun} coming back
      </b>
      They were mentioned in several recent sprints without a change that helped.
    </div>
  );
}
