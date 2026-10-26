import { renderToStaticMarkup } from 'react-dom/server';
import { MemoryRouter } from 'react-router-dom';
import { expect, test } from 'vitest';
import { ItemLink } from './ItemLink';

const render = (at: string, props: { projectKey?: string }) =>
  renderToStaticMarkup(
    <MemoryRouter initialEntries={[at]}>
      <ItemLink itemKey="SHIP-12" {...props}>
        Pay
      </ItemLink>
    </MemoryRouter>,
  );

test('ItemLink_WhenNoProjectKey_ShouldDefaultToTheOpenRoutesProject', () => {
  expect(render('/p/SHIP/boards/stories', {})).toContain('href="/p/SHIP/items/SHIP-12"');
});

test('ItemLink_WhenNotOnAProjectRoute_ShouldUseTheGivenKeyOrRenderPlainText', () => {
  expect(render('/', { projectKey: 'PAY' })).toContain('href="/p/PAY/items/SHIP-12"');
  expect(render('/', {})).toBe('Pay');
});
