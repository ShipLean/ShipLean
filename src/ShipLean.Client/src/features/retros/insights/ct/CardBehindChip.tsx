import { useState } from 'react';
import { StatusChip } from '../recurring/StatusChip';

/** A tappable card with a status chip in it: the card opens unless the chip's tap stops at the chip. */
export function CardBehindChip() {
  const [opened, setOpened] = useState(false);
  return (
    <div data-testid="card" data-opened={opened} onClick={() => setOpened(true)}>
      <StatusChip status="quiet" mode="sprint" />
    </div>
  );
}
