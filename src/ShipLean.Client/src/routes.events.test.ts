import { expect, test } from 'vitest';
import events from '../../ShipLean.TestSupport/DtoFixtures/events/events.json';
import { routeIds } from './routes';

// The core types a presence event's screenId as a string, because it cannot import the SPA's routes. The wire value
// is one of the SPA's route ids.
test('presenceChanged_WhenFromFixture_ShouldNameAKnownRoute', () => {
  expect(routeIds).toContain(events.presenceChanged.screenId);
});
