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