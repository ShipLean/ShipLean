import type { ReactNode } from 'react';
import { Link } from 'react-router-dom';

interface KpiTileProps {
  testId: string;
  value: string;
  unit?: string;
  label: ReactNode;
  /** The whole tile opens this address (the label then holds no link of its own). */
  to?: string;
  /** The test id of that link. */
  linkTestId?: string;
  /** Lets a long value (the time left) wrap onto two lines on a phone instead of overflowing its tile. */
  wrap?: boolean;
}

const TILE =
  'min-w-24 rounded-md border border-edge bg-panel px-3 py-1.5 touch:grid touch:min-w-0 touch:content-start touch:gap-px touch:px-2 touch:py-[7px]';

/** One figure of the Sprint backlog's row: the value with its unit, and what it measures underneath. On a phone the four sit in one row. */
export function KpiTile({ testId, value, unit, label, to, linkTestId, wrap }: KpiTileProps) {
  const body = (
    <>
      <div
        data-testid={testId}
        className={`text-[length:var(--text-xl)] font-semibold tabular-nums touch:text-[19px] touch:leading-tight ${
          wrap
            ? 'touch:text-[length:var(--text-sm)] touch:whitespace-normal'
            : 'touch:whitespace-nowrap'
        }`}
      >
        {value}
        {unit ? (
          <small className="ml-1 text-[length:var(--text-sm)] font-normal text-ink-muted touch:text-xs touch:font-medium">
            {unit}
          </small>
        ) : null}
      </div>
      <div className="text-[length:var(--text-xs)] text-ink-muted touch:truncate touch:text-[11.5px]">
        {label}
      </div>
    </>
  );
  if (to === undefined) return <div className={TILE}>{body}</div>;
  return (
    <Link data-testid={linkTestId} to={to} className={`${TILE} text-inherit no-underline`}>
      {body}
    </Link>
  );
}
