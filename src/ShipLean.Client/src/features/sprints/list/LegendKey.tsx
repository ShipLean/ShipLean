/** A colour swatch and its name in the velocity chart's legend. */
export function LegendKey({ colour, label }: { colour: string; label: string }) {
  return (
    <span className="inline-flex items-center gap-[5px]">
      <i className="inline-block size-[10px] rounded-[2px]" style={{ background: colour }} />
      {label}
    </span>
  );
}
