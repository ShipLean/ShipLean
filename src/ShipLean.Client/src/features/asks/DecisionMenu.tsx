import { FieldMenu } from '../workItem/form/FieldMenu';
import { routePath } from '../../routes';
import { showToast } from '../../components/toastStore';
import type { DecisionDto } from '../../api/asks';
import type { AsksItem } from './regions';
import { asksTestIds as ids } from './testIds';

const optionClass =
  'flex h-7 items-center rounded-control px-2 text-left text-[length:var(--text-sm)] whitespace-nowrap hover:bg-signal-soft';

// The ⋯ menu of a decision that is not withdrawn: Withdraw and Copy link.
export function DecisionMenu({
  item,
  decision,
  onWithdraw,
}: {
  item: AsksItem;
  decision: DecisionDto;
  onWithdraw(): void;
}) {
  const copyLink = async () => {
    const path = routePath('workItem', { key: item.projectKey, itemKey: item.key });
    await navigator.clipboard.writeText(
      new URL(`${path}#${decision.label}`, document.baseURI).href,
    );
    showToast(`Link to ${decision.label} copied`);
  };
  return (
    <FieldMenu
      buttonTestId={ids.decisionMore}
      buttonLabel={`More for ${decision.label}`}
      button={<span aria-hidden="true">⋯</span>}
    >
      {(close) => (
        <>
          <button
            type="button"
            role="menuitem"
            data-testid={ids.decisionWithdraw}
            className={optionClass}
            onClick={() => {
              close();
              onWithdraw();
            }}
          >
            Withdraw {decision.label}
          </button>
          <button
            type="button"
            role="menuitem"
            data-testid={ids.decisionCopyLink}
            className={optionClass}
            onClick={() => {
              close();
              void copyLink();
            }}
          >
            Copy link to {decision.label}
          </button>
          <p className="m-0 border-t border-edge px-2 pt-1.5 pb-0.5 text-[length:var(--text-xs)] text-ink-muted">
            Withdrawn decisions stay listed, struck through. Nothing is deleted.
          </p>
        </>
      )}
    </FieldMenu>
  );
}
