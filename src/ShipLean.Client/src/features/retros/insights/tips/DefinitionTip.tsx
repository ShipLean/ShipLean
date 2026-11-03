/** A tooltip body: one intro line over a term and meaning grid. */
export function DefinitionTip({
  intro,
  rows,
}: {
  intro: string;
  rows: { term: string; meaning: string }[];
}) {
  return (
    <>
      <p className="m-0 mb-1">{intro}</p>
      <dl className="m-0 grid grid-cols-[auto_minmax(0,1fr)] gap-x-2 gap-y-[3px]">
        {rows.map((row) => (
          <div key={row.term} className="contents">
            <dt className="font-semibold">{row.term}</dt>
            <dd className="m-0">{row.meaning}</dd>
          </div>
        ))}
      </dl>
    </>
  );
}
