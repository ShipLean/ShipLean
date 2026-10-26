export function ExternalImageNote({ sources, testId }: { sources: string[]; testId: string }) {
  if (sources.length === 0) return null;
  return (
    <p data-testid={testId} className="text-[length:var(--text-sm)] text-caution">
      External images are not shown, and Save will ask first: {sources.join(', ')}
    </p>
  );
}
