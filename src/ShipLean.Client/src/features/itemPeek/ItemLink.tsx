import type { CSSProperties, MouseEvent, ReactNode } from 'react';
import { Link, useLocation } from 'react-router-dom';
import { matchRoute, routePath, type RouteId } from '../../routes';
import { itemPeekTestIds } from './testIds';
import { useItemPeek } from './useItemPeek';

export interface ItemLinkProps {
  /** Defaults to the open route's project; without either, the children render as plain text. */
  projectKey?: string;
  itemKey: string;
  children: ReactNode;
  className?: string;
  style?: CSSProperties;
  /** Defaults to `itemPeekTestIds.link`. */
  testId?: string;
  title?: string;
  /** Called first; `preventDefault()` cancels opening the item. */
  onClick?: (event: MouseEvent<HTMLAnchorElement>) => void;
}

// The three Backlogs pages hard-navigate to the item page; everywhere else an item opens in the modal.
const hardNavigates: readonly RouteId[] = ['storiesBacklog', 'featuresBacklog', 'epicsBacklog'];

const isPlainLeftClick = (event: MouseEvent) =>
  event.button === 0 && !event.metaKey && !event.ctrlKey && !event.shiftKey && !event.altKey;

// A link to a work item. Its `href` is always the full item page (so Ctrl/Cmd/Shift/middle-click open it in a new tab);
// a plain left click opens the item in the modal over the current screen, except on a Backlogs page or for an item of
// another project than the open one, where it navigates to the full page.
export function ItemLink({
  projectKey,
  itemKey,
  children,
  className,
  style,
  testId,
  title,
  onClick,
}: ItemLinkProps) {
  const { pathname, search } = useLocation();
  const { open } = useItemPeek();
  const here = matchRoute(pathname, search);
  const openKey = here !== undefined && 'key' in here.params ? here.params.key : undefined;
  const key = projectKey ?? openKey;
  if (key === undefined) return <>{children}</>;
  const opensModal = here !== undefined && !hardNavigates.includes(here.id) && openKey === key;

  const click = (event: MouseEvent<HTMLAnchorElement>) => {
    onClick?.(event);
    if (event.defaultPrevented || !opensModal || !isPlainLeftClick(event)) return;
    event.preventDefault();
    open(itemKey);
  };

  return (
    <Link
      to={routePath('workItem', { key, itemKey })}
      data-testid={testId ?? itemPeekTestIds.link}
      data-item-key={itemKey}
      className={className}
      style={style}
      title={title}
      onClick={click}
    >
      {children}
    </Link>
  );
}
