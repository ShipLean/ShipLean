import { createContext, useContext } from 'react';

/** The phone board's pager: the column on screen, turning one page and turning to a column by id. */
export interface BoardPager {
  columnId: string;
  turn(dir: 'left' | 'right'): void;
  goTo(columnId: string): void;
}

export const BoardPagerContext = createContext<BoardPager | null>(null);

/** The phone board's pager. Only the phone board (900px and below) provides one. */
export function useBoardPager(): BoardPager {
  const pager = useContext(BoardPagerContext);
  if (pager === null) throw new Error('useBoardPager is only available inside the phone board.');
  return pager;
}
