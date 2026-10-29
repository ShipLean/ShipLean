import { sprintsTestIds as ids } from '../testIds';
import type { VelocityComparison } from './compareToVelocity';

/** Says how the velocity typed for the sprint sits against the history; far from it (either way) is a warning. */
export function VelocityCallout({
  comparison,
  typed,
  velocity,
  overridden,
  pointsOnly,
  entered,
}: {
  comparison: VelocityComparison;
  typed: number | undefined;
  /** The history: the project's override, else the recent sprints' average. */
  velocity: number;
  overridden: boolean;
  /** Old points with no velocity: there is nothing to compare until one is typed. */
  pointsOnly: boolean;
  /** The table has rows: "No capacity entered" must not show then. */
  entered: boolean;
}) {
  const [tone, title, text] = pointsOnly
    ? ([
        'info',
        'Points only',
        'This sprint has points but no velocity. Enter a velocity to recompute them.',
      ] as const)
    : describe(comparison, typed, velocity, overridden, entered);
  return (
    <div
      data-testid={ids.capacityCallout}
      data-tone={tone}
      role={tone === 'warn' ? 'alert' : undefined}
      className={`rounded-panel border border-edge border-l-[3px] bg-sunken px-3 py-2 text-[length:var(--text-sm)] ${
        tone === 'warn' ? 'border-l-caution' : 'border-l-edge'
      }`}
    >
      <b className="block">{title}</b>
      {text}
    </div>
  );
}

function describe(
  comparison: VelocityComparison,
  typed: number | undefined,
  velocity: number,
  overridden: boolean,
  entered: boolean,
): ['info' | 'warn', string, string] {
  switch (comparison.kind) {
    case 'none':
      if (entered) {
        return typed === undefined
          ? ['info', 'No velocity typed', 'Enter a velocity to compare it with recent sprints.']
          : [
              'info',
              'No velocity history',
              'There are no earlier sprints to compare this velocity with.',
            ];
      }
      return [
        'info',
        'No capacity entered',
        `Planning uses velocity (${velocity}) for this sprint.`,
      ];
    case 'inLine':
      return [
        'info',
        'In line with recent velocity',
        overridden
          ? 'The velocity is within 10% of the project override.'
          : 'The velocity is within 10% of the recent sprints.',
      ];
    case 'above':
      return [
        'warn',
        `Velocity ${comparison.percent}% above ${overridden ? 'the override' : 'recent sprints'}`,
        overridden
          ? `You typed ${typed}; the project plans against ${velocity}. Worth a second look.`
          : `You typed ${typed}; the team usually completes about ${velocity}. Worth a second look.`,
      ];
    case 'below':
      return [
        'warn',
        `Velocity ${comparison.percent}% below ${overridden ? 'the override' : 'recent sprints'}`,
        `You typed ${typed}; ${overridden ? 'the project plans against' : 'the team usually completes about'} ${velocity}. Fine if the team is smaller; otherwise plan less than usual.`,
      ];
  }
}
