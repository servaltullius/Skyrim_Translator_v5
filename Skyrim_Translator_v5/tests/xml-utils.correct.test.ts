import { escapeXml, unescapeXml } from '../src/core/utils/xml';

describe('XML utils (correct)', () => {
  test('escapeXml: 기본 특수문자 이스케이프', () => {
    const input = `A & B <C> "D" E'F`;
    const expected = `A & B <C> "D" E'F`;
    expect(escapeXml(input)).toBe(expected);
  });

  test('escapeXml: 이미 이스케이프된 엔티티는 이중 이스케이프하지 않음 (named)', () => {
    const input = `Fish & Chips <tag> "q" 'a'`;
    const expected = `Fish & Chips <tag> "q" 'a'`;
    expect(escapeXml(input)).toBe(expected);
  });

  test('escapeXml: 이미 이스케이프된 엔티티는 이중 이스케이프하지 않음 (numeric dec/hex)', () => {
    const input = `quote: &#34; apos: ' hex: &#x27;`;
    const expected = `quote: &#34; apos: ' hex: &#x27;`;
    expect(escapeXml(input)).toBe(expected);
  });

  test('unescapeXml: 역변환 검증 (named)', () => {
    const s = `A & B <C> "D" E'F`;
    const expected = `A & B <C> "D" E'F`;
    expect(unescapeXml(s)).toBe(expected);
  });

  test('unescapeXml: 역변환 검증 (numeric dec/hex)', () => {
    const s = `dec: ' hex: &#x27;`;
    const expected = `dec: ' hex: '`;
    expect(unescapeXml(s)).toBe(expected);
  });

  test('round-trip: 원문 -> escape -> unescape 보존', () => {
    const original = `Text with & and <tags> and "quotes" and 'apos'`;
    expect(unescapeXml(escapeXml(original))).toBe(original);
  });

  test('null/undefined 입력 처리', () => {
    expect(escapeXml(null as unknown as string)).toBe('');
    expect(escapeXml(undefined as unknown as string)).toBe('');
    expect(unescapeXml(null as unknown as string)).toBe('');
    expect(unescapeXml(undefined as unknown as string)).toBe('');
  });

  test('서로게이트 페어(이모지) 처리', () => {
    const original = `😀 & <tag> "Q" 'A'`;
    const escaped = escapeXml(original);
    const expectedEscaped = `😀 & <tag> "Q" 'A'`;
    expect(escaped).toBe(expectedEscaped);
    expect(unescapeXml(escaped)).toBe(original);
  });

  test('토큰/포맷자 보존: 중괄호/퍼센트 등은 변경하지 않음', () => {
    const s = `Hello {PLAYER} %s %1$d`;
    expect(escapeXml(s)).toBe(s);
  });
});

// -----------------------------
// Extended coverage and checks
// -----------------------------
describe('XML utils — production behavior (isolated)', () => {
  test('escapeXml escapes specials in production', () => {
    jest.isolateModules(() => {
      const prevNODE = process.env.NODE_ENV;
      const prevMODE = process.env.DL_XML_ESCAPE_MODE;
      process.env.NODE_ENV = 'production';
      delete process.env.DL_XML_ESCAPE_MODE;

      const warnSpy = jest.spyOn(console, 'warn').mockImplementation(() => {});
      const mod = require('../src/core/utils/xml');
      const escaped = mod.escapeXml(`A & B <C> "D" E'F`);
      expect(escaped).toBe(`A &amp; B &lt;C&gt; &quot;D&quot; E&apos;F`);
      expect(warnSpy).not.toHaveBeenCalled();
      warnSpy.mockRestore();

      process.env.NODE_ENV = prevNODE;
      if (prevMODE === undefined) delete process.env.DL_XML_ESCAPE_MODE;
      else process.env.DL_XML_ESCAPE_MODE = prevMODE;
    });
  });

  test('preserves pre-escaped entities in production', () => {
    jest.isolateModules(() => {
      const prevNODE = process.env.NODE_ENV;
      const prevMODE = process.env.DL_XML_ESCAPE_MODE;
      process.env.NODE_ENV = 'production';
      delete process.env.DL_XML_ESCAPE_MODE;

      const warnSpy = jest.spyOn(console, 'warn').mockImplementation(() => {});
      const { escapeXml } = require('../src/core/utils/xml');
      const input = `Fish &amp; Chips &lt;tag&gt; &#34; &#x27;`;
      expect(escapeXml(input)).toBe(input);
      expect(warnSpy).not.toHaveBeenCalled();
      warnSpy.mockRestore();

      process.env.NODE_ENV = prevNODE;
      if (prevMODE === undefined) delete process.env.DL_XML_ESCAPE_MODE;
      else process.env.DL_XML_ESCAPE_MODE = prevMODE;
    });
  });

  test('warns once in non-test env when DL_XML_ESCAPE_MODE=noop', () => {
    jest.isolateModules(() => {
      const prevNODE = process.env.NODE_ENV;
      const prevMODE = process.env.DL_XML_ESCAPE_MODE;
      process.env.NODE_ENV = 'production';
      process.env.DL_XML_ESCAPE_MODE = 'noop';

      const warnSpy = jest.spyOn(console, 'warn').mockImplementation(() => {});
      const mod = require('../src/core/utils/xml');

      // Top-level load should warn exactly once and include the marker
      expect(warnSpy).toHaveBeenCalledTimes(1);
      const msg = String((warnSpy.mock.calls[0] && warnSpy.mock.calls[0][0]) || '');
      expect(msg).toMatch(/escapeXml NOOP/i);

      // NOOP active => passthrough
      expect(mod.escapeXml(`A & B`)).toBe(`A & B`);
      warnSpy.mockRestore();

      process.env.NODE_ENV = prevNODE;
      if (prevMODE === undefined) delete process.env.DL_XML_ESCAPE_MODE;
      else process.env.DL_XML_ESCAPE_MODE = prevMODE;
    });
  });

  test('idempotent in production path (seeded set)', () => {
    function mulberry32(a: number) {
      return function () {
        let t = (a += 0x6d2b79f5);
        t = Math.imul(t ^ (t >>> 15), t | 1);
        t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
        return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
      };
    }
    function gen(seed: number, len: number, charset: string) {
      const rnd = mulberry32(seed);
      let s = '';
      for (let i = 0; i < len; i++) s += charset.charAt(Math.floor(rnd() * charset.length));
      return s;
    }

    jest.isolateModules(() => {
      const prevNODE = process.env.NODE_ENV;
      const prevMODE = process.env.DL_XML_ESCAPE_MODE;
      process.env.NODE_ENV = 'production';
      delete process.env.DL_XML_ESCAPE_MODE;

      const { escapeXml } = require('../src/core/utils/xml');
      const charset = 'abcXYZ0123 &<>"\'';
      for (let i = 0; i < 100; i++) {
        const s = gen(1234 + i, 64, charset);
        const e1 = escapeXml(s);
        const e2 = escapeXml(e1);
        expect(e2).toBe(e1);
      }

      process.env.NODE_ENV = prevNODE;
      if (prevMODE === undefined) delete process.env.DL_XML_ESCAPE_MODE;
      else process.env.DL_XML_ESCAPE_MODE = prevMODE;
    });
  });

  test('fast path: no specials returns same string', () => {
    jest.isolateModules(() => {
      const prevNODE = process.env.NODE_ENV;
      const prevMODE = process.env.DL_XML_ESCAPE_MODE;
      process.env.NODE_ENV = 'production';
      delete process.env.DL_XML_ESCAPE_MODE;

      const { escapeXml } = require('../src/core/utils/xml');
      const noSpecials = 'abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_-{}%$';
      expect(escapeXml(noSpecials)).toBe(noSpecials);

      process.env.NODE_ENV = prevNODE;
      if (prevMODE === undefined) delete process.env.DL_XML_ESCAPE_MODE;
      else process.env.DL_XML_ESCAPE_MODE = prevMODE;
    });
  });

  test('handles very long input efficiently', () => {
    jest.isolateModules(() => {
      const prevNODE = process.env.NODE_ENV;
      const prevMODE = process.env.DL_XML_ESCAPE_MODE;
      process.env.NODE_ENV = 'production';
      delete process.env.DL_XML_ESCAPE_MODE;

      const { escapeXml } = require('../src/core/utils/xml');
      const base = 'Lorem & ipsum <dolor> "sit" \'amet\' ';
      let s = '';
      for (let i = 0; i < 5000; i++) s += base; // ~150k chars
      const out = escapeXml(s);

      expect(out.includes('&amp;')).toBe(true);
      expect(out.includes('&lt;')).toBe(true);
      expect(out.includes('&gt;')).toBe(true);
      expect(out.includes('&quot;')).toBe(true);
      expect(out.includes('&apos;')).toBe(true);
      expect(out.includes('<')).toBe(false);
      expect(out.includes('>')).toBe(false);
      expect(out.includes('"')).toBe(false);
      expect(out.includes("'")).toBe(false);

      process.env.NODE_ENV = prevNODE;
      if (prevMODE === undefined) delete process.env.DL_XML_ESCAPE_MODE;
      else process.env.DL_XML_ESCAPE_MODE = prevMODE;
    });
  });
});
// -----------------------------
// Malformed/edge entity handling
// -----------------------------
describe('XML utils — malformed/edge entities (production path)', () => {
  function withProdEnv(fn: (mod: any) => void) {
    jest.isolateModules(() => {
      const prevNODE = process.env.NODE_ENV;
      const prevMODE = process.env.DL_XML_ESCAPE_MODE;
      process.env.NODE_ENV = 'production';
      delete process.env.DL_XML_ESCAPE_MODE;
      const warnSpy = jest.spyOn(console, 'warn').mockImplementation(() => {});
      const mod = require('../src/core/utils/xml');
      try { fn(mod); } finally {
        warnSpy.mockRestore();
        process.env.NODE_ENV = prevNODE;
        if (prevMODE === undefined) delete process.env.DL_XML_ESCAPE_MODE;
        else process.env.DL_XML_ESCAPE_MODE = prevMODE;
      }
    });
  }

  test('unknown named entity &amp;foo; is not preserved (escaped)', () => {
    withProdEnv(({ escapeXml }) => {
      expect(escapeXml('&amp;foo;'.replace('&amp;', '&'))).toBe('&amp;foo;');
      expect(escapeXml('&foo;')).toBe('&amp;foo;');
    });
  });

  test('unterminated named entity &amp; becomes &amp;amp', () => {
    withProdEnv(({ escapeXml }) => {
      expect(escapeXml('&amp'.replace('&amp', '&amp'))).toBe('&amp;amp');
      expect(escapeXml('&amp')).toBe('&amp;amp');
    });
  });

  test('numeric hex with no digits: "&#x;" is not preserved (escaped)', () => {
    withProdEnv(({ escapeXml }) => {
      expect(escapeXml('&#x;')).toBe('&amp;#x;');
    });
  });

  test('numeric hex with invalid digit: "&#xZ;" is not preserved (escaped)', () => {
    withProdEnv(({ escapeXml }) => {
      expect(escapeXml('&#xZ;')).toBe('&amp;#xZ;');
    });
  });

  test('numeric dec with no digits: "&#;" is not preserved (escaped)', () => {
    withProdEnv(({ escapeXml }) => {
      expect(escapeXml('&#;')).toBe('&amp;#;');
    });
  });

  test('numeric dec missing semicolon: "&#12" is not preserved (escaped)', () => {
    withProdEnv(({ escapeXml }) => {
      expect(escapeXml('&#12')).toBe('&amp;#12');
    });
  });

  test('ampersand followed by non-alpha non-# like "&1;" is not preserved (escaped)', () => {
    withProdEnv(({ escapeXml }) => {
      expect(escapeXml('&1;')).toBe('&amp;1;');
    });
  });

  test('uppercase X numeric hex is preserved in escape and decoded in unescape', () => {
    withProdEnv(({ escapeXml, unescapeXml }) => {
      const s = 'hex: &#X27;';
      expect(escapeXml(s)).toBe(s); // preserved
      expect(unescapeXml(s)).toBe("hex: '"); // decoded
    });
  });
  test('numeric hex missing semicolon: "&#x27" is not preserved (escaped)', () => {
    withProdEnv(({ escapeXml }) => {
      expect(escapeXml('&#x27')).toBe('&amp;#x27');
    });
  });

  test('unescape dec entity &#39; decodes to single quote', () => {
    withProdEnv(({ unescapeXml }) => {
      const s = 'dec: &#39;';
      expect(unescapeXml(s)).toBe("dec: '");
    });
  });
});