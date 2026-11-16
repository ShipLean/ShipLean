import { describe, expect, it } from 'vitest';
import { highlightJson } from './highlightJson';

describe('highlightJson', () => {
  it('ShouldEscapeHtml', () => {
    const html = highlightJson('{"t": "<script>alert(1)</script> & co"}');
    expect(html).not.toContain('<script>');
    expect(html).toContain('&lt;script&gt;');
    expect(html).toContain('&amp; co');
  });

  it('ShouldMarkKeysStringsNumbersAndLiterals', () => {
    const html = highlightJson('{"a": "x", "b": 1.5, "c": null, "d": true}');
    expect(html).toContain('<span class="json-k">&quot;a&quot;</span>:');
    expect(html).toContain('<span class="json-s">&quot;x&quot;</span>');
    expect(html).toContain('<span class="json-n">1.5</span>');
    expect(html).toContain('<span class="json-b">null</span>');
    expect(html).toContain('<span class="json-b">true</span>');
  });
});
