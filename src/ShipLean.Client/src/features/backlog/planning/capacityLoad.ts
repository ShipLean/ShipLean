/** A sprint section's load: committed against its limit, the capacity entered or else the velocity the forecast plans at. */
export interface CapacityLoad {
  /** The limit the bar is drawn against; null when there is neither capacity nor velocity. */
  limit: number | null;
  /** No capacity was entered: the muted velocity bar and chip. */
  fromVelocity: boolean;
  /** Points over the limit, or null when within it. */
  over: number | null;
  /** Points free under the limit, or null when over or without a limit. */
  free: number | null;
}

export function capacityLoad(
  committed: number,
  capacity: number | null,
  velocity: number | null,
): CapacityLoad {
  const limit = capacity ?? velocity;
  const exceeded = limit !== null && committed > limit;
  return {
    limit,
    fromVelocity: capacity === null,
    over: exceeded ? committed - limit : null,
    free: limit !== null && !exceeded ? limit - committed : null,
  };
}
