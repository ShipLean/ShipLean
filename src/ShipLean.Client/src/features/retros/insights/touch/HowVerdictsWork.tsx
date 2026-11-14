import { useSearchParams } from 'react-router-dom';
import { retrosTestIds as ids } from '../../testIds';

/** The Changes & impact explanation on a phone: one 32px button that writes `sheet=defs` (the definitions sheet reads it). */
export function HowVerdictsWork() {
  const [params, setParams] = useSearchParams();
  const open = () => {
    const next = new URLSearchParams(params);
    next.set('sheet', 'defs');
    setParams(next, { replace: true });
  };
  return (
    <button
      type="button"
      data-testid={ids.retroImpactHowVerdicts}
      className="btn-ghost cursor-pointer self-start whitespace-nowrap touch:min-h-[var(--touch-target)]"
      onClick={open}
    >
      How verdicts work
    </button>
  );
}
