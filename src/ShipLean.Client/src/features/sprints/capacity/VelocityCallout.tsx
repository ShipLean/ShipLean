import { sprintsTestIds as ids } from '../testIds';
import type { VelocityComparison } from './compareToVelocity';

/** Says how the team total sits against velocity; far from it (either way) is a warning. */
export function VelocityCallout({
  comparison,
  velocity,
  overridden,
}: {
  comparison: VelocityComparison;
  velocity: number;
  overridden: boolean;
}) {
  const [tone, title, text] = describe(comparison, velocity, overridden);
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
  velocity: number,
  overridden: boolean,
): ['info' | 'warn', string, string] {
  switch (comparison.kind) {
    case 'none':
      return [
        'info',
        'No capacity entered',
        `Planning uses velocity (${velocity}) for this sprint.`,
      ];
    case 'inLine':
      return [
        'info',
        'In line with velocity',
        overridden ? 'Within 10% of the velocity override.' : 'Within 10% of the recent sprints.',
      ];
    case 'above':
      return [
        'warn',
        `${comparison.percent}% above velocity`,
        overridden
          ? `The project plans against a velocity of ${velocity}. Worth a second look.`
          : `The team usually completes about ${velocity}. Worth a second look.`,
      ];
    case 'below':
      return [
        'warn',
        `${comparison.percent}% below velocity`,
        'Expected when people are away. Plan less than usual.',
      ];
  }
}
