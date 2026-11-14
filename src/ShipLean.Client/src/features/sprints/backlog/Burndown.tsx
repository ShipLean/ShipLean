import { useLayoutEffect, useRef, useState, type PointerEvent } from 'react';
import type { SprintMetricsDto } from '@shiplean/core/api/sprints';
import { useTouchShell } from '../../../touch/useTouchShell';
import { sprintsTestIds as ids } from '../testIds';
import { BURNDOWN_HEIGHT, BURNDOWN_WIDTH, burndownShape } from './burndownGeometry';
import { burndownDotAt, burndownReadout } from './burndownReadout';

// At 900px and below the chart fills the card: about 118px tall, as wide as its box.
const TOUCH_HEIGHT = 118;
const TOUCH_FALLBACK_WIDTH = 366;

/** Story points remaining at the end of each working day so far, over the whole sprint's working days, against the ideal line. Inline SVG, no chart library. At 900px and below, a tap or drag along the chart reads a day in the header. */
export function Burndown({
  metrics,
  days,
}: {
  metrics: SprintMetricsDto;
  /** Every working day of the sprint (yyyy-MM-dd), the chart's x-axis. */
  days: readonly string[];
}) {
  const { touch } = useTouchShell();
  const box = useRef<HTMLDivElement>(null);
  const [boxWidth, setBoxWidth] = useState(TOUCH_FALLBACK_WIDTH);
  const [picked, setPicked] = useState<string | null>(null);

  useLayoutEffect(() => {
    const element = box.current;
    if (!touch || !element) return;
    const read = () => setBoxWidth(Math.floor(element.clientWidth) || TOUCH_FALLBACK_WIDTH);
    read();
    const observer = new ResizeObserver(read);
    observer.observe(element);
    return () => observer.disconnect();
  }, [touch, metrics.burndown.length]);

  const width = touch ? boxWidth : BURNDOWN_WIDTH;
  const height = touch ? TOUCH_HEIGHT : BURNDOWN_HEIGHT;
  const shape = burndownShape(metrics.burndown, days, metrics.committed, { width, height });
  const dotPicked = touch ? shape.dots.find((dot) => dot.date === picked) : undefined;

  // The pointer's x in the chart's own units; the chart is drawn 1:1 but scaled anyway if the box is not.
  const pick = (event: PointerEvent<SVGSVGElement>) => {
    const rect = event.currentTarget.getBoundingClientRect();
    const x = ((event.clientX - rect.left) * width) / (rect.width || width);
    setPicked(burndownDotAt(shape.dots, x)?.date ?? null);
  };

  return (
    <div className="rounded-md border border-edge bg-panel px-2.5 py-1.5 touch:w-full touch:px-2.5 touch:pt-2 touch:pb-1">
      <header className="flex items-baseline justify-between gap-2 text-[length:var(--text-sm)] font-semibold">
        <span>Burndown</span>
        <span data-testid={ids.burndownRemaining} className="font-medium text-ink-muted">
          {metrics.remaining} pts left
        </span>
        {touch && metrics.burndown.length > 0 ? (
          <span
            data-testid={ids.burndownReadout}
            className="ml-auto text-[length:var(--text-xs)] font-medium text-ink-muted tabular-nums"
          >
            {dotPicked ? burndownReadout(dotPicked) : 'Tap a day'}
          </span>
        ) : null}
      </header>
      {metrics.burndown.length === 0 ? (
        <p
          data-testid={ids.burndownEmpty}
          className="m-0 text-[length:var(--text-sm)] text-ink-muted"
        >
          Starts with the first working day.
        </p>
      ) : (
        <div ref={box} className="touch:w-full touch:min-w-0 touch:overflow-hidden">
          <svg
            data-testid={ids.burndown}
            viewBox={`0 0 ${width} ${height}`}
            width={width}
            height={height}
            role="img"
            aria-label="Burndown: story points remaining per working day against the ideal line"
            className="block text-[length:9px] touch:touch-pan-y touch:text-[length:10px]"
            fill="var(--color-muted)"
            onPointerDown={
              touch
                ? (event) => {
                    event.currentTarget.setPointerCapture(event.pointerId);
                    pick(event);
                  }
                : undefined
            }
            onPointerMove={touch ? (event) => event.buttons > 0 && pick(event) : undefined}
          >
            <line
              x1={24}
              x2={width - 6}
              y1={shape.baselineY}
              y2={shape.baselineY}
              stroke="var(--color-border)"
            />
            <text x={2} y={9}>
              {shape.max}
            </text>
            {dotPicked ? (
              <line
                x1={dotPicked.x}
                x2={dotPicked.x}
                y1={6}
                y2={shape.baselineY}
                stroke="var(--color-accent)"
              />
            ) : null}
            <polyline
              data-testid={ids.burndownIdeal}
              points={shape.idealPoints}
              fill="none"
              stroke="var(--color-muted)"
              strokeWidth={1.5}
              strokeDasharray="3 3"
            />
            <polyline
              data-testid={ids.burndownActual}
              points={shape.actualPoints}
              fill="none"
              stroke="var(--chart-completed)"
              strokeWidth={2}
              strokeLinejoin="round"
            />
            {shape.dots.map((dot) => (
              <g key={dot.date}>
                <circle
                  data-testid={ids.burndownPoint}
                  data-day={dot.day}
                  data-remaining={dot.remaining}
                  data-ideal={dot.ideal}
                  cx={dot.x}
                  cy={dot.y}
                  r={dot === dotPicked ? 5 : 3}
                  fill="var(--chart-completed)"
                  stroke="var(--color-surface)"
                  strokeWidth={2}
                >
                  {touch ? null : (
                    <title>{`Day ${dot.day}: ${dot.remaining} pts left (ideal ${dot.ideal})`}</title>
                  )}
                </circle>
                <text x={dot.x} y={height - 4} textAnchor="middle">
                  {dot.day}
                </text>
              </g>
            ))}
          </svg>
        </div>
      )}
    </div>
  );
}
