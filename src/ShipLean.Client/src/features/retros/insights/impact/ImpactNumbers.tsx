const oneDecimal = (n: number) => n.toFixed(1);

/** A fraction as a signed whole percentage: -0.5 is "-50%", 1 is "+100%". */
export const signedPercent = (fraction: number) => {
  const pct = Math.round(fraction * 100);
  return `${pct > 0 ? '+' : ''}${pct}%`;
};

function Figure({ name, value, caption }: { name: string; value: string; caption: string }) {
  return (
    <div data-figure={name} className="grid">
      <b className="text-[18px] tabular-nums">{value}</b>
      <span className="text-[length:var(--text-xs)] text-ink-muted">{caption}</span>
    </div>
  );
}

/** Per sprint before and after the action closed, and the change; the change is left out when it has no base. */
export function ImpactNumbers({
  beforeAvg,
  afterAvg,
  changePct,
}: {
  beforeAvg: number;
  afterAvg: number | null;
  changePct: number | null;
}) {
  return (
    <div className="flex gap-4">
      <Figure name="before" value={oneDecimal(beforeAvg)} caption="per sprint before" />
      <Figure
        name="after"
        value={afterAvg === null ? '–' : oneDecimal(afterAvg)}
        caption="per sprint after"
      />
      {changePct === null ? null : (
        <Figure name="change" value={signedPercent(changePct)} caption="change" />
      )}
    </div>
  );
}
