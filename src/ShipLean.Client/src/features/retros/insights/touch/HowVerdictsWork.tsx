import { useSearchParams } from 'react-router-dom';
import { retrosTestIds as ids } from '../../testIds';

/** The Changes & impact explanation on a phone: an inline link, a 32px-tall button, that writes `sheet=defs` (the definitions sheet reads it). */
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
      className="inline-flex min-h-[var(--touch-target)] cursor-pointer items-center border-0 bg-transparent p-0 align-middle text-[length:inherit] text-signal underline"
      onClick={open}
    >
      How verdicts work
    </button>
  );
}
