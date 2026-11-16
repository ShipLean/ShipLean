/** "1 item", "2 items": a section header's count. */
export function itemCount(count: number): string {
  return `${count} ${count === 1 ? 'item' : 'items'}`;
}
