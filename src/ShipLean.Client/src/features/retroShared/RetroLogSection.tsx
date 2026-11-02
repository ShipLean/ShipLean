/** One heading and its verbatim lines in the failure log. */
export function RetroLogSection({ label, lines }: { label: string; lines: string[] }) {
  return (
    <>
      <dt data-log-section={label} className="font-medium text-ink-muted">
        {label}
      </dt>
      <dd className="m-0 font-mono text-[length:var(--text-xs)]">
        {lines.length > 0 ? (
          lines.map((line, i) => <div key={i}>{line}</div>)
        ) : (
          <span className="text-ink-muted">None reported.</span>
        )}
      </dd>
    </>
  );
}
