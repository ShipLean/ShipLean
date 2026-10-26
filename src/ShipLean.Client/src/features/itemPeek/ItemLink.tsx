import type { CSSProperties, MouseEvent, ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { routePath } from '../../routes';
import { itemPeekTestIds } from './testIds';

export interface ItemLinkProps {
  projectKey: string;
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

// TODO: stub for the contract push (always a plain link to the full page); the modal behaviour replaces it in the next A0 push.
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
  return (
    <Link
      to={routePath('workItem', { key: projectKey, itemKey })}
      data-testid={testId ?? itemPeekTestIds.link}
      data-item-key={itemKey}
      className={className}
      style={style}
      title={title}
      onClick={onClick}
    >
      {children}
    </Link>
  );
}
