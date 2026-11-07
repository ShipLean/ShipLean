using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Harness;

// The touch rules (K9) a phone screen follows, as readable lines, one per violation; empty means the rules hold.
// - no sideways scroll;
// - every visible input, textarea and select has a computed font size of at least 16px;
// - every visible button, link (a[href]) and [role=button] is at least 32×32px, except links inside Markdown.
// Visible means the element has a client rect and is not visibility:hidden. The script is the one place in the
// acceptance tests that queries by tag and role.
public static class TouchRules
{
    // (root, pageScroll, markdownTestId) => string[]: pageScroll checks the page's sideways scroll, otherwise root's own.
    private const string Script = """
        (root, pageScroll, markdownTestId) => {
          const lines = [];
          const describe = (el) => {
            const id = el.getAttribute('data-testid');
            if (id) return `${el.tagName.toLowerCase()} [data-testid=${id}]`;
            const text = (el.getAttribute('aria-label') || el.textContent || '').trim().replace(/\s+/g, ' ').slice(0, 40);
            return `${el.tagName.toLowerCase()} without data-testid "${text}"`;
          };
          const visible = (el) => el.getClientRects().length > 0 && getComputedStyle(el).visibility !== 'hidden';
          const within = (selector) => [...(root.matches(selector) ? [root] : []), ...root.querySelectorAll(selector)].filter(visible);
          if (pageScroll) {
            const doc = document.documentElement;
            if (doc.scrollWidth > window.innerWidth) lines.push(`page scrolls sideways: scrollWidth ${doc.scrollWidth} > innerWidth ${window.innerWidth}`);
          } else if (root.scrollWidth > root.clientWidth) {
            lines.push(`${describe(root)} scrolls sideways: scrollWidth ${root.scrollWidth} > clientWidth ${root.clientWidth}`);
          }
          for (const el of within('input, textarea, select')) {
            const size = parseFloat(getComputedStyle(el).fontSize);
            if (size < 16) lines.push(`${describe(el)}: font ${size}px < 16px`);
          }
          const markdown = `[data-testid="${markdownTestId}"] a`;
          for (const el of within('button, a[href], [role=button]')) {
            if (el.matches(markdown)) continue;
            const r = el.getBoundingClientRect();
            if (r.width < 32 || r.height < 32) lines.push(`${describe(el)}: ${Math.round(r.width)}×${Math.round(r.height)} < 32×32`);
          }
          return lines;
        }
        """;

    // The whole page (the route sweep's form): the page-level scroll check plus every element on the page.
    public static async Task<IReadOnlyList<string>> ViolationsAsync(IPage page) =>
        await page.EvaluateAsync<string[]>(
            $"(markdownTestId) => ({Script})(document.documentElement, true, markdownTestId)",
            ComponentTestIds.Markdown);

    // Only the elements inside within, and within's own sideways scroll instead of the page's (a lane's form: siblings
    // build the rest of the screen, so their overflow must not fail it).
    public static async Task<IReadOnlyList<string>> ViolationsAsync(IPage page, ILocator within)
    {
        await Assertions.Expect(within).ToBeVisibleAsync();
        var root = await within.ElementHandleAsync();
        try
        {
            return await page.EvaluateAsync<string[]>(
                $"([root, markdownTestId]) => ({Script})(root, false, markdownTestId)",
                new object[] { root, ComponentTestIds.Markdown });
        }
        finally
        {
            await root.DisposeAsync();
        }
    }
}
