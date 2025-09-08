멋진 목표입니다. 요구사항을 그대로 구현 가능한 **Windows / Linux 데스크톱 번역 툴** 아키텍처와 실전 구현 계획을 아래처럼 정리했습니다. 핵심은 **xTranslator에서 추출한 XML을 잃어버리는 태그 없이 정밀 파싱**하고, **태그/게임 변수는 번역 모델에 전혀 노출하지 않으면서**(텍스트만 보내기) **원래 위치로 100% 복원**하는 것입니다. 초장문(장편 대사/책/스크립트)도 **스트리밍**으로 끊김 없이 처리합니다.

---

## 0) 한눈에 보는 설계 요약

* **언어/프레임워크 추천**: **.NET 8 + C# + Avalonia UI** (Windows & Linux 모두 네이티브 배포, 고성능 가상화 그리드 지원) ([avaloniaui.net][1], [docs.avaloniaui.net][2])
* **XML 파서**: System.Xml **XmlReader(스트리밍)** + 필요 범위에 한해 **LINQ to XML (XElement)** 혼용 — 매우 큰 XML에서도 OOM 방지.
* **태그/변수 보존 방식(모델 비노출)**:

  1. **텍스트-태그 분리 토크나이즈** →
  2. **태그를 경계로 텍스트 조각만** 번역 호출(SRX 문장분할 옵션) →
  3. **원래 순서로 태그/변수를 삽입 복원** (포지션·스택 기반 리어셈블).
     (XLIFF의 inline code 개념(ph/sc/ec)과 ITS 2.0 ‘translate=no’ 원칙을 응용한 자체 파이프라인) ([OASIS Open][3], [W3C][4])
* **초장문 실시간 번역**: Gemini **2.5 Flash** 스트리밍 + 청크/파이프라이닝(동시성 제한/재시도/백오프). *2.5 Flash* **입력 32k 토큰/출력 32k 토큰** 기준으로 세그먼트 크기 동적 조절. ([Google Cloud][5], [Google AI for Developers][6])
* **게임 변수/포맷 보존**: Creation Kit 메시지에서 쓰는 **printf 스타일(예: `%.0f`, `%s`)**·자리표시자·Alias/Global 치환 등은 **비번역 토큰으로 분리**하여 위치 고정. ([Reddit][7])
* **GUI**: xTranslator와 유사한 3열(원문/번역문/컨텍스트) + 상태 컬럼(자동/검수/잠금/태그체크), **어두운 회색** 기반의 최신 다크테마 가이드 준수(명도/콘트라스트 기준). ([docs.avaloniaui.net][8], [Android Developers][9], [Algoworks][10], [The Interaction Design Foundation][11])
* **프롬프트 시스템**: 기본 **vec** 프롬프트 + 사용자 **커스텀 프롬프트 편집기** + **용어집(강제/권고) 편집기**.
* **검색 리서치 도구**: 디자인 트렌드/레퍼런스 수집에 **ddgr**(DuckDuckGo CLI) 운영 가이드 제공. ([GitHub][12])

---

## 1) 전체 아키텍처

```
[XML Loader]
   └─ XmlReader (stream) → NodeTokenizer(텍스트/태그/변수 토큰화)
        └─ Segmenter (SRX 규칙 적용·문장분리/길이조절)
             └─ Task Queue (동시성 N)
                  └─ Gemini 2.5 Flash (스트리밍)
                       └─ Post-Processor (용어집/표기규칙/띄어쓰기)
        └─ Reassembler (원래 태그/변수/개행·화이트스페이스 복원)
   └─ Validator (태그 대응/자리표시자 개수·순서 체크)
   └─ Cache/DB (세그먼트 캐시·TM)
[UI (Avalonia DataGrid, Dark Theme)]
   └─ 원문/번역문/컨텍스트/상태/품질 경고
   └─ 실시간 스트리밍 표시/취소/재시도
   └─ 프롬프트·용어집 편집기
```

* **SRX 기반 세그먼트**: 업계 표준 SRX(문장 분할 규칙)로 촘촘하게 쪼개되, 태그 경계를 절대 넘지 않게 설계. ([docs.rws.com][13], [AbroadLink][14])
* **스트리밍**: SSE 기반 스트리밍으로 UI 셀에 실시간 표시(부분 번역). Google 공식 샘플/문서 기준. ([Google Cloud][15], [Google AI for Developers][16])
* **토큰한계/요율 관리**: 2.5 Flash 32k 토큰 한계와 레이트리밋 문서 기준으로 **동적 배치/백오프**. ([Google Cloud][5], [Google AI for Developers][17])

---

## 2) 파일 형식과 xTranslator 연계 포인트

* **xTranslator**는 **XML Import/Export**를 제공(레포 내 `TESVT_XMLFunc.pas`, `TESVT_XMLExportOpts.pas` 등) — 즉, **XML 기반 사전/문자열 교환**이 표준 워크플로입니다. 본 툴은 **그 XML을 직접 파싱**합니다. ([GitHub][18])
* xTranslator는 Skyrim/Fallout/Starfield 문자열(STRINGS/DLSTRINGS/ILSTRINGS)과 esp/esm 번역에 쓰이며 **다양한 인코딩/하이브리드 모드**를 지원합니다. (레포 README·릴리즈 노트) ([GitHub][18])

> **스키마 불문 견고성**: XML 구조 변형/확장에 대비해 **스트리밍 파싱 + 관대한 필드 매핑**(알 수 없는 속성/서브태그 보존)으로 설계합니다.

---

## 3) XML 파서 선정

* **.NET 기본 라이브러리**

  * **XmlReader**: 전진형 스트리밍 파서 — 초대형 XML에도 안전, 메모리 제한 환경에서 최적.
  * **LINQ to XML (XElement)**: 특정 노드만 메모리에 잠깐 적재해 조작(속성 수정 등).
* 대안 스택(참고): Rust(**quick-xml**), Node(**saxes/fast-xml-parser**), Python(**lxml + defusedxml**) 등.
* **ITS/XLIFF 지식 반영**: 태그/속성의 번역 가능 여부와 인라인 코드 개념을 내부 규칙으로 흡수. ([W3C][4], [OASIS Open][3])

---

## 4) “태그를 모델에 보여주지 않고” 보존하는 안전한 방식

**문제점**: 태그/변수를 완전히 숨긴 채 통문장을 번역하면, **출력 단어 순서**가 바뀌어 **정확한 삽입 위치**를 잃습니다.

**해결**: **태그 경계 기준 분할 번역**.

1. **Tokenizer**

   * 인라인 **XML/유사 XML 태그**: `<...>`
   * Bethesda/CK **포맷 지정자**: `%s`, `%d`, `%.0f` 등
   * 기타 **플레이스홀더**: `{0}`, `{name}`, `%{username}` 등
   * 이들을 **비번역 토큰**으로 추출(스택·오프셋 기록), 나머지는 **텍스트 토큰**. (플레이스홀더는 로컬라이제이션 업계에서 변경 금지 요소) ([help.smartling.com][19])

2. **Segmenter**

   * 텍스트 토큰을 **SRX 규칙**으로 문장 분할(언어별 마침표/약어 예외 적용). 태그 경계는 절대 넘지 않음. ([docs.rws.com][13])

3. **Translate**

   * **텍스트 세그먼트만** Gemini에 요청. 태그/변수는 모델에 **아예 미전달**.

4. **Reassembler**

   * 원래 **토큰 시퀀스**(텍스트/태그/변수)를 순서대로 재결합 → **태그/변수 1:1 원복**.
   * **검증기**:

     * 포맷 지정자·플레이스홀더 **개수/순서 불변** 확인(예: `%s` 갯수).
     * 필요 시 경고/자동수정. CK 메시지 치환 문법 참고. ([Reddit][7])

> 이 접근은 XLIFF의 **inline code(ph/sc/ec)** 보존 철학을 모델 외부에서 구현한 것입니다. ([OASIS Open][3])

---

## 5) 초장문 실시간 번역(Streaming & Chunking)

* **모델**: *Gemini 2.5 Flash* (텍스트/이미지 지원, 저지연; 입력/출력 **각 32k tokens**). 긴 세그먼트는 **가변 크기 청크**로 쪼개 순차 스트리밍. ([Google Cloud][5], [Google AI for Developers][6])
* **스트리밍**: SSE(EventSource) 패턴으로 부분 결과를 UI에 바로 뿌림(공식 샘플/문서). ([Google Cloud][15], [Google AI for Developers][16])
* **동시성/레이트리밋**: 큐 기반 워커(N개), **백오프/재시도**. Google 문서의 레이트리밋 가이드 준수. ([Google AI for Developers][17])

---

## 6) GUI 설계 (xTranslator 유사 + 현대적 다크 테마)

* **레이아웃**: 상단 도구막대, 좌측 **모듈/파일 트리**, 중앙 **그리드**(FormID/컨텍스트 | **원문(전체 보이기)** | **번역문(전체 보이기)** | 상태/품질), 하단 스트리밍 로그/경고.
* **그리드**: **가상화**(수천\~수만 행) + 멀티라인 셀 + 실시간 셀 업데이트. Avalonia TreeDataGrid/가상화 가이드 권장. ([docs.avaloniaui.net][2])
* **다크 테마**: Avalonia **FluentTheme Dark** + 톤 조절. **절대 흑(#000)/절대 백(#FFF)을 지양**, 접근성 대비 준수(Material 3 권고). ([docs.avaloniaui.net][8], [Android Developers][9], [The Interaction Design Foundation][11])
* **대비/가독성**: 다크 배경(#121212 \~ #1E1E1E 계열) + 본문 텍스트 AA/AAA 대비 확보(참고 가이드). ([Material Design][20])
* **상태 배지**: Auto/Edited/Locked/TagOK/Error 등 명확한 시각 피드백.
* **검색/필터**: Regex/Tag 오류만 보기/길이 초과/용어 위반 등.

> 필요시 FluentAvalonia 활용(Fluent v2 스타일, Dark/HC 테마). ([amwx.github.io][21])

---

## 7) 프롬프트 & 용어집

* **기본 프롬프트: `vec`** (요구하신 키워드)
  예시(시스템 지시문 템플릿, 한국어):

  > **\[vec 기본]** 전문 게임 로컬라이제이션 톤으로 자연스럽고 간결하게 번역합니다. 게임 세계관/용어를 존중합니다. 줄바꿈/공백을 유지합니다. 인용부호·말투는 원문 의도를 따릅니다. (별도 전달된) **용어집(강제/권고)** 을 준수합니다. 숫자/기호/단위 표기는 현지 표준으로 맞추되, **플레이스홀더/변수/태그는 존재하지 않는다고 가정하고 번역**합니다.

* **커스텀 프롬프트 편집기**: 프로젝트 단위/세션 단위로 시스템·유저 프롬프트를 저장/버전관리.

* **용어집 편집기**:

  * **강제**: 정확히 일치 시 강제 치환(Post-Processor)
  * **권고**: 모델 프롬프트에 반영 + 위반 시 경고
  * 경계 인식(단어 경계/접사), 대소문/활용형 처리 규칙.

---

## 8) 품질 보호 장치

* **태그/변수 검증**: 수/순서/중첩 체크(자동 수정 제안). (CK printf 자리표시자 사례) ([Reddit][7])
* **길이/줄바꿈/공백 보존**: `xml:space="preserve"` 등 화이트스페이스 규칙을 유지. (ITS/Okapi 문서 참조) ([okapi.sourceforge.net][22], [okapiframework.org][23])
* **세그먼트 캐시/TM**: 동일 세그먼트 재번역 방지.
* **되돌리기/감사 로그**: 원문-번역 diff, 사용자 수정 이력.
* **오프라인 안전**: API 키는 OS 보안 저장소/암호화 파일에 저장.

---

## 9) 배포

* **Windows**: `dotnet publish -r win-x64 --self-contained` (배포 단일 폴더/설치 프로그램 옵션)
* **Linux**: `dotnet publish -r linux-x64 --self-contained`, 필요 시 `.deb` 패키징 가이드 참고. ([GitHub][24], [DEV Community][25])

---

## 10) 구현 스켈레톤 (C#/.NET 8 + Avalonia)

> 아래 코드는 **핵심 아이디어**를 담은 축약본입니다.

### 10.1 XML 스트리밍 파싱 & 토크나이즈

```csharp
// 모델에 태그를 숨기기 위해, 문자열 값 안의 <...>, %, {0} 등을 "비번역 토큰"으로 분리
public readonly record struct Tok(bool IsTagOrVar, string Value);

public static IEnumerable<Tok> TokenizeInline(string text)
{
    // 1) XML/유사XML 태그
    var tag = new Regex(@"<[^>]+>", RegexOptions.Compiled);
    // 2) CK/printf 스타일 포맷 지정자 (단순화)
    var printf = new Regex(@"%(\d+\$)?[+#0\- ]*(\d+)?(\.\d+)?[cdfgiosuxXeEfp%]", RegexOptions.Compiled);
    // 3) 중괄호/퍼센트 변수
    var brace = new Regex(@"\{\w+(?:\.\w+)?\}|\%\{\w+\}", RegexOptions.Compiled);

    int i = 0;
    while (i < text.Length)
    {
        Match m = null;
        foreach (var rx in new[] { tag, printf, brace })
        {
            m = rx.Match(text, i);
            if (m.Success && m.Index == i) break;
            m = null;
        }
        if (m != null)
        {
            yield return new Tok(true, m.Value);
            i += m.Length;
        }
        else
        {
            int next = text.Length;
            foreach (var rx in new[] { tag, printf, brace })
            {
                var n = rx.Match(text, i);
                if (n.Success) next = Math.Min(next, n.Index);
            }
            var slice = text.Substring(i, next - i);
            yield return new Tok(false, slice);
            i = next;
        }
    }
}
```

### 10.2 SRX 기반 문장 분할(개념)

* SRX 규칙(JSON/YAML) → 정규식 세트로 로드 → `Tok.IsTagOrVar == false` 텍스트만 문장 나눔 → 토큰 시퀀스 단위로 **세그먼트 배치**. (SRX 표준 문서 참고) ([docs.rws.com][13], [AbroadLink][14])

### 10.3 Gemini 2.5 Flash 호출 (스트리밍; SSE 수신 예)

> .NET은 공식 Gemini SDK가 아니어도 **REST + SSE 파싱**으로 충분합니다. 아래 예시는 **Vertex AI Chat Completions SSE** 패턴을 응용한 구조입니다(개념 참고용). ([Google Cloud][15])

```csharp
public async IAsyncEnumerable<string> StreamTranslateAsync(
    IEnumerable<string> lines, // 텍스트 세그먼트(태그 제거 후)
    string apiKey,
    [EnumeratorCancellation] CancellationToken ct = default)
{
    using var http = new HttpClient();
    using var req = new HttpRequestMessage(HttpMethod.Post,
        "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:streamGenerateContent");
    req.Headers.Add("x-goog-api-key", apiKey);
    req.Content = new StringContent(JsonSerializer.Serialize(new {
        contents = new[] { new { parts = lines.Select(x => new { text = x }) } },
        generationConfig = new { temperature = 0.2 }
    }), Encoding.UTF8, "application/json");

    using var res = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
    res.EnsureSuccessStatusCode();

    using var stream = await res.Content.ReadAsStreamAsync(ct);
    using var reader = new StreamReader(stream);
    string line;
    var sb = new StringBuilder();
    while ((line = await reader.ReadLineAsync()) != null)
    {
        if (line.StartsWith("data: "))
        {
            var json = line.Substring(6);
            // 청크 파싱 → 부분 텍스트 추출
            var chunk = JsonDocument.Parse(json);
            var piece = chunk.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text").GetString();
            sb.Append(piece);
            yield return piece; // UI에 스트리밍 표시
        }
    }
}
```

> **토큰 제한**: 2.5 Flash **입력/출력 각 32k** — 세그먼트 길이를 동적으로 조절하고, 너무 길면 추가 문장 단위로 분할하세요. ([Google Cloud][5], [Google AI for Developers][6])

### 10.4 리어셈블 & 검증

```csharp
public static string Reassemble(IReadOnlyList<Tok> seq, Queue<string> translatedSegments)
{
    var sb = new StringBuilder();
    foreach (var t in seq)
    {
        if (t.IsTagOrVar) sb.Append(t.Value);
        else sb.Append(translatedSegments.Dequeue());
    }
    return sb.ToString();
}

// 포맷 지정자 검증(간단 예)
public static void AssertFormatCountUnchanged(string src, string dst)
{
    int Count(MatchCollection m) => m.Count;
    var rx = new Regex(@"%(\d+\$)?[+#0\- ]*(\d+)?(\.\d+)?[cdfgiosuxXeEfp%]");
    if (Count(rx.Matches(src)) != Count(rx.Matches(dst)))
        throw new InvalidOperationException("Format placeholders mismatch");
}
```

---

## 11) UI: Avalonia 다크 테마 & 그리드

* **FluentTheme Dark** 적용 + 커스텀 톤 오버레이 (너무 검은 배경/새하얀 텍스트는 피함). 접근성 기준은 Material 3/NN 권장. ([docs.avaloniaui.net][8], [Android Developers][9], [The Interaction Design Foundation][11])
* **대용량 성능**: TreeDataGrid/Items 가상화 사용, 템플릿 단순화 및 비동기 바인딩(공식 팁). ([docs.avaloniaui.net][2], [avaloniaui.net][26])

---

## 12) Gemini 연동(공식 문서)

* **일반 텍스트 생성/샘플**(Python/JS/Go/Java/REST)과 퀵스타트는 Google **AI for Developers**를 따른다. ([Google AI for Developers][27])
* **스트리밍 예제/가이드**: Vertex AI Chat Completions SSE 샘플. ([Google Cloud][15])
* **레이트리밋 정책**: 요청/분량 제한, 지수 백오프 권장. ([Google AI for Developers][17])

---

## 13) ddgr 기반 리서치(옵션)

디자인/UX 트렌드를 수집할 때 **ddgr**(DuckDuckGo CLI)을 활용하면 터미널에서 빠르게 자료를 모을 수 있습니다(설치/사용 레퍼런스 포함). ([GitHub][12], [Arch Manual Pages][28])

---

## 14) 테스트 체크리스트

1. **태그/변수 보존**: `<b>…</b>`, `<Alias=…>`, `%s/%d/%.0f`, `{0}` 등 케이스별 왕복 테스트. (CK 포맷 자리표시자) ([Reddit][7])
2. **화이트스페이스**: `xml:space="preserve"` 보존 확인. ([okapi.sourceforge.net][22])
3. **토큰 한계**: 32k 경계 근처 세그먼트 스트리밍 확인. ([Google Cloud][5])
4. **레이트리밋/재시도**: 대량 배치에서 백오프 동작. ([Google AI for Developers][17])
5. **UI 가독성**: 다크 모드 대비/색약 모드 시뮬레이션(Material 가이드 준수). ([Android Developers][9])

---

## 15) 로드맵 제안

* **MVP**: XML 로드 → 토큰화 → SRX 분할 → Flash 스트리밍 → 리어셈블 → 그리드 표시/편집
* **후속**: XLIFF 입출력 옵션, TMX/Glossary 익스포트, 번역 메모리, 세그먼트 잠금, 다중 모델 백엔드(Pro/Lite 선택)
* **성능**: 파이프라인 벡프레셔/큐 시각화, 대용량 로더(진행률), 병렬도 자동 튜닝

---

## 16) 왜 이 선택들이 “검증된가”

* **xTranslator**가 애초에 XML Import/Export를 지원하며 레포에 XML 함수/옵션이 명시됨(우리 툴의 기본 인터페이스가 XML이기 때문). ([GitHub][18])
* **XLIFF/ITS/SRX**는 번역 업계 표준으로 **인라인 코드 보존/번역 가능성/문장 분할**의 베이스라인을 제공합니다(우리는 이를 자체 포맷 내부 규칙으로 흡수). ([OASIS Open][3], [W3C][4], [docs.rws.com][13])
* **Gemini 2.5 Flash**는 최신 공식 문서로 입력/출력 토큰 한계와 사용 가이드가 명확합니다. 스트리밍·레이트리밋도 레퍼런스가 있습니다. ([Google Cloud][5], [Google AI for Developers][16])
* **Avalonia**는 Windows/Linux 본격 지원, 다크 테마와 가상화 성능 가이드가 체계적입니다. ([avaloniaui.net][1], [docs.avaloniaui.net][2])
* **다크 테마 접근성**은 Material 3/업계 가이드를 따릅니다(절대 흑/백 회피, 적절한 대비). ([Android Developers][9], [The Interaction Design Foundation][11])

---

### 부록 A — Java 대안(공식 SDK 선호 시)

Google AI for Developers의 **Java 클라이언트**로 2.5 Flash 호출 가능(멀티플랫폼 UI는 **Compose Multiplatform**/JavaFX 택). ([Google AI for Developers][29])

```java
// 간단 호출 예 (공식 문서 스타일)
import com.google.genai.Client;
import com.google.genai.types.GenerateContentResponse;

Client client = new Client();
GenerateContentResponse res =
  client.models.generateContent("gemini-2.5-flash", "텍스트 세그먼트", null);
System.out.println(res.text());
```

---

## 마무리

위 설계는 **태그·게임 변수 완전 보존(모델 비노출)**, **초장문 실시간 스트리밍**, **xTranslator 친화 UI**를 모두 충족합니다. 다음 단계로는 **프로젝트 시드 리포**(솔루션/폴더/레이어/샘플 XML/가짜 번역기 어댑터)까지 바로 찍어낼 수 있습니다. 필요하시면 \*\*초기 템플릿(솔루션 스캐폴딩 + 기본 토크나이저/리어셈블러/더미 UI)\*\*를 곧바로 제공하겠습니다.

[1]: https://avaloniaui.net/?utm_source=chatgpt.com "Avalonia UI – Open-Source .NET XAML Framework | WPF ..."
[2]: https://docs.avaloniaui.net/docs/guides/development-guides/improving-performance?utm_source=chatgpt.com "Improving Performance | Avalonia Docs"
[3]: https://docs.oasis-open.org/xliff/xliff-core/v2.2/xliff-core-v2.2-part1.html?utm_source=chatgpt.com "XLIFF Version 2.2. Part 1: Core - Index of /"
[4]: https://www.w3.org/TR/its20/?utm_source=chatgpt.com "Internationalization Tag Set (ITS) Version 2.0"
[5]: https://cloud.google.com/vertex-ai/generative-ai/docs/models/gemini/2-5-flash?utm_source=chatgpt.com "Gemini 2.5 Flash | Generative AI on Vertex AI"
[6]: https://ai.google.dev/gemini-api/docs/models?utm_source=chatgpt.com "Gemini models | Gemini API | Google AI for Developers"
[7]: https://www.reddit.com/r/skyrimmods/comments/ly02tt/display_propertiesvariables_in_message_box_text/?utm_source=chatgpt.com "display properties/variables in message box text?"
[8]: https://docs.avaloniaui.net/docs/basics/user-interface/styling/themes/fluent?utm_source=chatgpt.com "Fluent Theme"
[9]: https://developer.android.com/develop/ui/compose/designsystems/material3?utm_source=chatgpt.com "Material Design 3 in Compose - Android Developers"
[10]: https://www.algoworks.com/blog/dark-mode-designs-in-2024/?utm_source=chatgpt.com "Mastering Dark Mode Design - A 2024 Guide - Algoworks"
[11]: https://www.interaction-design.org/literature/article/ui-color-palette?srsltid=AfmBOopEKGwoA6SRVvheOSeehQqOM14oIsAnoeIH2TIqpFQqyGI3P1I1&utm_source=chatgpt.com "UI Color Palette 2025: Best Practices, Tips, and Tricks for Designers"
[12]: https://github.com/jarun/ddgr?utm_source=chatgpt.com "jarun/ddgr: :duck: DuckDuckGo from the terminal"
[13]: https://docs.rws.com/en-US/sdl-passolo-help-785448/the-srx-segmenter-413197?utm_source=chatgpt.com "The SRX Segmenter"
[14]: https://abroadlink.com/blog/segmentation-in-translation-and-the-srx-standard-format?utm_source=chatgpt.com "Segmentation in translation and the SRX standard format"
[15]: https://cloud.google.com/vertex-ai/generative-ai/docs/samples/generativeaionvertexai-gemini-chat-completions-streaming?utm_source=chatgpt.com "Generate streaming text by using Gemini and the Chat ..."
[16]: https://ai.google.dev/gemini-api/docs/text-generation?utm_source=chatgpt.com "Text generation | Gemini API | Google AI for Developers"
[17]: https://ai.google.dev/gemini-api/docs/rate-limits?utm_source=chatgpt.com "Rate limits | Gemini API | Google AI for Developers"
[18]: https://github.com/MGuffin/xTranslator "GitHub - MGuffin/xTranslator: Text & translation editor for Skyrim, Fallout4 and Starfield mods"
[19]: https://help.smartling.com/hc/en-us/articles/360008143433-Placeholders-in-Resource-Files?utm_source=chatgpt.com "Placeholders in Resource Files"
[20]: https://m2.material.io/design/usability/accessibility.html?utm_source=chatgpt.com "Accessibility - Material Design"
[21]: https://amwx.github.io/FluentAvaloniaDocs/pages/FATheme/Themes?utm_source=chatgpt.com "Themes - FluentAvalonia Docs"
[22]: https://okapi.sourceforge.net/Release/Filters/Help/xml.htm?utm_source=chatgpt.com "Okapi Components - XML Filter"
[23]: https://okapiframework.org/wiki/index.php/ITS?utm_source=chatgpt.com "ITS"
[24]: https://github.com/AvaloniaUI/Avalonia/discussions/15563?utm_source=chatgpt.com "Problems with avalonia.xplat cross compile Desktop ..."
[25]: https://dev.to/chami/export-avalonia-app-to-linux-ubuntu-step-by-step-guide-40id?utm_source=chatgpt.com "Export Avalonia App To linux Ubuntu step-by-step guide"
[26]: https://avaloniaui.net/blog/10-avalonia-performance-tips-to-supercharge-your-app?utm_source=chatgpt.com "10 Avalonia Performance Tips to Supercharge Your App"
[27]: https://ai.google.dev/gemini-api/docs/quickstart?utm_source=chatgpt.com "Gemini API quickstart | Google AI for Developers"
[28]: https://man.archlinux.org/man/extra/ddgr/ddgr.1.en?utm_source=chatgpt.com "ddgr(1) — Arch manual pages"
[29]: https://ai.google.dev/gemini-api/docs?utm_source=chatgpt.com "Gemini API | Google AI for Developers"
