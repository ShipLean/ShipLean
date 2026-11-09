import { describe, expect, test } from 'vitest';
import { addressErrors, hostOfOrigin, normaliseServerAddress } from './serverAddress';

const origin = (input: string) => {
  const result = normaliseServerAddress(input);
  return result.ok ? result.origin : result.error;
};

describe('normaliseServerAddress', () => {
  test.each([
    ['  shiplean.example.com  ', 'https://shiplean.example.com'],
    ['http://localhost:5000', 'http://localhost:5000'],
    ['HTTPS://Example.COM', 'https://example.com'],
    ['https://example.com/', 'https://example.com'],
    ['https://example.com/a/b?x=1#frag', 'https://example.com'],
    ['example.com:8443/path', 'https://example.com:8443'],
    ['192.168.1.5:3000', 'https://192.168.1.5:3000'],
    ['http://[::1]:5000/', 'http://[::1]:5000'],
    ['https://Example.com:443/path', 'https://example.com'],
    ['example.com:443', 'https://example.com'],
    ['http://example.com:80', 'http://example.com'],
    ['http://[::1]:80', 'http://[::1]'],
    ['https://example.com:80', 'https://example.com:80'],
    ['http://example.com:443', 'http://example.com:443'],
    ['https://example.com:65535', 'https://example.com:65535'],
    ['[2001:db8::1]:8443', 'https://[2001:db8::1]:8443'],
    ['https://[::ffff:1.2.3.4]', 'https://[::ffff:1.2.3.4]'],
  ])('%s -> %s', (input, expected) => {
    expect(origin(input)).toBe(expected);
  });

  test.each(['', '   '])('empty %j', (input) => {
    expect(origin(input)).toBe(addressErrors.empty);
  });

  test.each(['ftp://example.com', 'file:///etc/passwd', 'javascript://x', 'shiplean://signed-in'])(
    'wrong scheme %s',
    (input) => {
      expect(origin(input)).toBe(addressErrors.scheme);
    },
  );

  test.each([
    'https://user@example.com',
    'https://user:pass@example.com',
    'https://@example.com',
    'example.com@evil.com',
    'https://evil.com\\@good.com',
  ])('userinfo %s', (input) => {
    expect(origin(input)).toBe(addressErrors.userinfo);
  });

  test.each([
    'https://',
    'https://exa mple.com',
    'https://example.com:abc',
    'https://example.com:',
    'https://.example.com',
    '//example.com',
    'not a url',
    'https://example.com:65536',
    'https://example.com:99999',
    'https://[:::]',
    'https://[::1::2]',
    'https://[1:2:3:4:5:6:7:8:9]',
    'https://[12345::1]',
    'https://[1:2]',
    'https://[::1.2.3]',
  ])('unparsable %s', (input) => {
    expect(origin(input)).toBe(addressErrors.unparsable);
  });

  test('a userinfo-looking path is only a path', () => {
    expect(origin('https://example.com/a@b')).toBe('https://example.com');
  });

  test('says the exact words', () => {
    expect(addressErrors).toEqual({
      empty: 'Enter the address of your ShipLean server.',
      scheme: 'The address must start with http:// or https://.',
      unparsable: "That doesn't look like a server address.",
      userinfo: 'Leave the user name and password out of the address.',
    });
  });

  test('hostOfOrigin drops the scheme', () => {
    expect(hostOfOrigin('http://example.com:5000')).toBe('example.com:5000');
  });
});
