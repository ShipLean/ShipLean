export function PasteStatus({
  pasting,
  error,
  ids,
}: {
  pasting: boolean;
  error: string | null;
  ids: { pasting: string; error: string };
}) {
  if (error !== null) {
    return (
      <p role="alert" data-testid={ids.error} className="text-[length:var(--text-sm)] text-alert">
        {error}
      </p>
    );
  }
  if (!pasting) return null;
  return (
    <p
      role="status"
      data-testid={ids.pasting}
      className="text-[length:var(--text-sm)] text-ink-muted"
    >
      Attaching image…
    </p>
  );
}
