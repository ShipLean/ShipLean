import { FEATURE_BOARD_PARENT_VIEW, FEATURE_BOARD_VIEW } from './fixtures/featureBoardView';
import {
  FEATURE_BOARD_PARENT_ROUTE,
  FEATURE_BOARD_PEOPLE,
  FEATURE_BOARD_ROUTE,
  arrangeFeatureBoard,
  featureBoardReady,
} from './fixtures/featureBoardStubs';
import { pageShots } from './shoot';

pageShots([
  {
    name: 'feature-board',
    route: FEATURE_BOARD_ROUTE,
    shell: true,
    arrange: (p) => arrangeFeatureBoard(p, FEATURE_BOARD_VIEW),
    ready: featureBoardReady,
  },
  {
    name: 'feature-board-parent',
    route: FEATURE_BOARD_PARENT_ROUTE,
    shell: true,
    arrange: (p) => arrangeFeatureBoard(p, FEATURE_BOARD_PARENT_VIEW),
    ready: featureBoardReady,
  },
  {
    name: 'feature-board-people',
    route: FEATURE_BOARD_PARENT_ROUTE,
    shell: true,
    arrange: (p) => arrangeFeatureBoard(p, FEATURE_BOARD_PARENT_VIEW, FEATURE_BOARD_PEOPLE),
    ready: featureBoardReady,
  },
]);
