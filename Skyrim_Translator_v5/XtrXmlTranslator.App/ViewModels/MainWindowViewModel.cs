using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using XtrXmlTranslator.Core.Srx;
using XtrXmlTranslator.Core.Tokenizing;
using XtrXmlTranslator.Core.Translate;
using XtrXmlTranslator.Core.TM;
using System.Xml;
using XtrXmlTranslator.Core.Xml;
using XtrXmlTranslator.Core.Prompt;
using XtrXmlTranslator.Core.Glossary;
using XtrXmlTranslator.Core.Validation;
using XtrXmlTranslator.App.Services;
using System.Text.Json;
using Serilog;
using Serilog.Context;
using System.Diagnostics;

namespace XtrXmlTranslator.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ISecretsProvider _secrets;
    private readonly ITranslatorFactory _translatorFactory;
    private readonly IFullTextDialogService _fullTextService;
    private readonly IRowFilterService _rowFilterService;
    private readonly IValidationOrchestrator _validation;
    private readonly TranslationSessionService _sessionService;
    private readonly IConfigService _config;
    private readonly IRuntimeSettings _runtimeSettings;
    public ObservableCollection<TranslationRowVM> Rows { get; } = new();
    public ObservableCollection<TranslationRowVM> ViewRows { get; } = new();

    [ObservableProperty]
    private TranslationRowVM? selectedRow;

    // STEP 6: 프롬프트/용어집 상태 (디스크 영속화 지원)
    public PromptConfig Prompt { get; set; } = new();
    public GlossaryStore Glossary { get; set; } = new();

    // 언어 코드 (SRX 엔진 선택)
    public string LanguageCode { get; set; } = "en-US";

    private XtrXmlTranslator.Core.Glossary.GlossaryCompiled? _glossaryCompiled;

    public void LoadProjectConfig()
    {
        try
        {
            var loaded = _config.Load();
            Prompt = loaded.Prompt;
            Glossary = loaded.Glossary;
            _glossaryCompiled = _config.Compile(Glossary);
            StatusText = "설정 로드 완료";
            Log.Information("Config loaded: glossaryCount={Count}", Glossary.Entries.Count);
        }
        catch (Exception ex)
        {
            StatusText = $"설정 로드 오류: {ex.Message}";
            Log.Error(ex, "Failed to load project config");
        }
    }

    public void SavePromptToDisk()
    {
        try
        {
            _config.SavePrompt(Prompt);
            StatusText = "프롬프트 저장됨";
            Log.Information("Prompt saved to {Path}", AppConfigPaths.PromptPath);
        }
        catch (Exception ex)
        {
            StatusText = $"프롬프트 저장 오류: {ex.Message}";
            Log.Error(ex, "Failed to save prompt to {Path}", AppConfigPaths.PromptPath);
        }
    }

    public void SaveGlossaryToDisk()
    {
        try
        {
            _config.SaveGlossary(Glossary);
            StatusText = $"용어집 저장됨: {Glossary.Entries.Count} 항목";
            Log.Information("Glossary saved to {Path} items={Count}", AppConfigPaths.GlossaryPath, Glossary.Entries.Count);
        }
        catch (Exception ex)
        {
            StatusText = $"용어집 저장 오류: {ex.Message}";
            Log.Error(ex, "Failed to save glossary to {Path}", AppConfigPaths.GlossaryPath);
        }
    }

    private string _statusText = "대기 중";
    public string StatusText { get => _statusText; set { _statusText = value; OnPropertyChanged(); } }

    private double _progress;
    public double Progress { get => _progress; set { _progress = value; OnPropertyChanged(); } }

    private string _progressDetail = string.Empty;
    public string ProgressDetail { get => _progressDetail; set { _progressDetail = value; OnPropertyChanged(); } }

    private string _sessionSummary = string.Empty;
    public string SessionSummary { get => _sessionSummary; set { _sessionSummary = value; OnPropertyChanged(); } }

    private bool _isAutoRowHeight = false; // 기본: 한 줄 고정
    public bool IsAutoRowHeight
    {
        get => _isAutoRowHeight;
        set
        {
            if (_isAutoRowHeight == value) return;
            _isAutoRowHeight = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsFixedRowHeight));
            RowHeight = value ? double.NaN : _fixedRowHeight;
        }
    }
    public bool IsFixedRowHeight => !_isAutoRowHeight;

    private double _rowHeight = 44; // 기본 한 줄 높이
    public double RowHeight
    {
        get => _rowHeight;
        set
        {
            _rowHeight = value;
            OnPropertyChanged();
            if (!_isAutoRowHeight)
                _fixedRowHeight = value;
        }
    }

    private double _fixedRowHeight = 44;
    public double FixedRowHeight
    {
        get => _fixedRowHeight;
        set
        {
            _fixedRowHeight = value;
            OnPropertyChanged();
            if (!_isAutoRowHeight)
                RowHeight = value;
        }
    }

    private string _query = string.Empty;
    public string Query { get => _query; set { _query = value; OnPropertyChanged(); UpdateViewRows(); } }

    private bool _filterErrors;
    public bool FilterErrors
    {
        get => _filterErrors;
        set
        {
            if (_filterErrors == value) return;
            _filterErrors = value;
            OnPropertyChanged();
            UpdateViewRows();
        }
    }

    public bool CanTranslate => Rows.Count > 0;

    private bool _skipTagHeavyOption;
    public bool SkipTagHeavyOption
    {
        get => _skipTagHeavyOption;
        set
        {
            if (_skipTagHeavyOption == value) return;
            _skipTagHeavyOption = value;
            OnPropertyChanged();
            // 런타임 설정(세션 범위)
            _runtimeSettings.SkipTagHeavyEnabled = value;
        }
    }

    // 자동 교정 실행(경고있는 행 대상으로 보수적 오토픽스 적용)
    public void AutoFixSelected()
    {
        int affected = 0;
        foreach (var row in Rows.Where(r => !r.TagOk))
        {
            AutoFixRow(row);
            affected++;
        }
        Log.Information("AutoFixSelected executed for {Count} rows", affected);
    }

    public void AutoFixRow(TranslationRowVM row)
    {
        _validation.AutoFixRow(row);
        Log.Debug("AutoFix applied for row {Index}", row.Index);
        RevalidateRow(row);
    }

    [RelayCommand]
    private void AutoFixRowCmd(TranslationRowVM row) => AutoFixRow(row);

    [RelayCommand]
    private void RevalidateRowCmd(TranslationRowVM row) => RevalidateRow(row);

    [RelayCommand]
    private void RevalidateAll()
    {
        foreach (var r in Rows) RevalidateRow(r);
        Log.Information("RevalidateAll executed: rows={Count}", Rows.Count);
    }

    [RelayCommand]
    private void NextWarning()
    {
        var list = FilterErrors ? ViewRows : Rows;
        if (list.Count == 0) return;
        int start = 0;
        if (SelectedRow is not null)
        {
            var cur = list.IndexOf(SelectedRow);
            start = Math.Max(0, cur + 1);
        }
        int idx = -1;
        for (int i = 0; i < list.Count; i++)
        {
            int j = (start + i) % list.Count;
            if (!list[j].TagOk)
            {
                idx = j; break;
            }
        }
        if (idx >= 0) SelectedRow = list[idx];
    }

    [RelayCommand]
    private void PrevWarning()
    {
        var list = FilterErrors ? ViewRows : Rows;
        if (list.Count == 0) return;
        int start = SelectedRow is null ? list.Count - 1 : Math.Max(0, list.IndexOf(SelectedRow) - 1);
        int idx = -1;
        for (int i = 0; i < list.Count; i++)
        {
            int j = (start - i + list.Count) % list.Count;
            if (!list[j].TagOk)
            {
                idx = j; break;
            }
        }
        if (idx >= 0) SelectedRow = list[idx];
    }

    [RelayCommand]
    private async Task OpenSourceFullText(TranslationRowVM row)
    {
        Log.Debug("OpenSourceFullText: ctx={Context} srcLen={Len}", row.Context, row.SourceText?.Length ?? 0);
        await _fullTextService.OpenAsync(row.Context, row.SourceText ?? string.Empty, row.TranslationText ?? string.Empty, false);
    }

    [RelayCommand]
    private async Task OpenTranslationFullText(TranslationRowVM row)
    {
        Log.Debug("OpenTranslationFullText: ctx={Context} tgtLen={Len}", row.Context, row.TranslationText?.Length ?? 0);
        var result = await _fullTextService.OpenAsync(row.Context, row.SourceText ?? string.Empty, row.TranslationText ?? string.Empty, true);
        if (result is not null)
        {
            row.TranslationText = result;
            var issues = Validator.Validate(row.SourceText ?? string.Empty, row.TranslationText ?? string.Empty);
            if (issues.Count == 0)
            {
                row.TagOk = true; row.Warning = string.Empty;
            }
            else
            {
                row.TagOk = false; row.Status = RowStatus.Error;
                row.Warning = string.Join(" | ", issues.Select(i => i.Message));
            }
        }
    }

    public void RevalidateRow(TranslationRowVM row)
    {
        _validation.RevalidateRow(row);
        if (row.TagOk)
            Log.Debug("Revalidate OK for row {Index}", row.Index);
        else
            Log.Warning("Revalidate failed for row {Index}", row.Index);
        UpdateViewRows();
    }

    private CancellationTokenSource? _cts;

    public void RebuildGlossaryCompiled()
    {
        _glossaryCompiled = _config.Compile(Glossary);
        Log.Information("Glossary compiled: entries={Count}", Glossary.Entries.Count);
    }

public MainWindowViewModel() : this(new EnvSecretsProvider(), new ResilientGeminiFactory(), new DummyFullTextDialogService(), new RowFilterService(), new ValidationOrchestrator(), new TranslationSessionService(), new ConfigService(), new RuntimeSettings()) { }

    public MainWindowViewModel(ISecretsProvider secrets, ITranslatorFactory translatorFactory, IFullTextDialogService fullTextService)
        : this(secrets, translatorFactory, fullTextService, new RowFilterService(), new ValidationOrchestrator(), new TranslationSessionService(), new ConfigService(), new RuntimeSettings())
    {
    }

    public MainWindowViewModel(ISecretsProvider secrets,
                               ITranslatorFactory translatorFactory,
                               IFullTextDialogService fullTextService,
                               IRowFilterService rowFilterService,
                               IValidationOrchestrator validation,
                               TranslationSessionService sessionService,
                               IConfigService config,
                               IRuntimeSettings runtimeSettings)
    {
        _secrets = secrets;
        _translatorFactory = translatorFactory;
        _fullTextService = fullTextService;
        _rowFilterService = rowFilterService;
        _validation = validation;
        _sessionService = sessionService;
        _config = config;
        _runtimeSettings = runtimeSettings;
        Rows.CollectionChanged += (_, __) => { OnPropertyChanged(nameof(CanTranslate)); UpdateViewRows(); };
        // 데모용 초기 행
        Rows.Add(new TranslationRowVM("demo", "Hello world."));
        if (Rows.Count > 0) Rows[^1].Index = Rows.Count;
        // 설정 자동 로드
        LoadProjectConfig();
    }

    [RelayCommand]
    private void Open()
    {
        // ViewModel에서 파일 피커 접근은 TopLevel 필요 → 간단히 상태만 갱신(실제 구현은 View로 위임 권장)
        StatusText = "열기: 구현 예정 (View에서 StorageProvider로 위임 권장)";
    }

    [RelayCommand]
    private async Task TranslateSelected()
    {
        _cts = new CancellationTokenSource();
        try
        {
            var callbacks = new TranslationSessionCallbacks(
                OnProgress: (pct, detail) => Dispatcher.UIThread.Post(() => { Progress = pct; ProgressDetail = detail; }),
                OnRowDelta: (rowIdx, delta) => Dispatcher.UIThread.Post(() => { Rows[rowIdx].TranslationText += delta; }),
                OnRowWarning: (rowIdx, msg) => Dispatcher.UIThread.Post(() =>
                {
                    var row = Rows[rowIdx];
                    row.TagOk = false;
                    if (string.IsNullOrEmpty(row.Warning)) row.Warning = msg;
                    else if (!row.Warning.Split(" | ", StringSplitOptions.RemoveEmptyEntries).Contains(msg))
                        row.Warning += " | " + msg;
                }),
                OnRowError: (rowIdx, msg) => Dispatcher.UIThread.Post(() =>
                {
                    var row = Rows[rowIdx];
                    row.Status = RowStatus.Error;
                    if (string.IsNullOrEmpty(row.Warning)) row.Warning = msg;
                    else if (!row.Warning.Split(" | ", StringSplitOptions.RemoveEmptyEntries).Contains(msg))
                        row.Warning += " | " + msg;
                }),
                OnRowFinal: (rowIdx, finalText, issues) => Dispatcher.UIThread.Post(() =>
                {
                    var row = Rows[rowIdx];
                    row.TranslationText = finalText;
                    if (issues.Count == 0)
                    {
                        row.TagOk = true;
                    }
                    else
                    {
                        row.TagOk = false;
                        row.Status = RowStatus.Error;
                        var msg = string.Join(" | ", issues.Select(i => i.Message));
                        row.Warning = string.IsNullOrEmpty(row.Warning) ? msg : row.Warning + " | " + msg;
                    }
                }),
                OnStatus: s => Dispatcher.UIThread.Post(() => StatusText = s),
                OnSummary: (total, warn, err, pace) => Dispatcher.UIThread.Post(() =>
                {
                    SessionSummary = $"세그먼트 {total}, 경고 {warn}, 오류 {err}, 대기 {pace}ms";
                })
            );

            await _sessionService.RunAsync(
                rows: Rows,
                languageCode: LanguageCode,
                prompt: Prompt,
                glossary: Glossary,
                compiled: _glossaryCompiled,
                secrets: _secrets,
                translatorFactory: _translatorFactory,
                ct: _cts.Token,
                cb: callbacks);
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
        }
    }

    private void UpdateViewRows()
    {
        var nextSel = _rowFilterService.Update(Rows, ViewRows, Query, FilterErrors, SelectedRow);
        SelectedRow = nextSel;
    }

    [RelayCommand]
    private void Cancel()
    {
        Log.Information("Translate cancel requested");
        _cts?.Cancel();
    }

    [RelayCommand]
    private void Save()
    {
        StatusText = "저장: 구현 예정";
        Log.Information("Save requested");
    }

    // 기존 코드비하인드 참조 호환용(차후 제거 가능)
    public void LoadFromXmlStream(Stream stream)
    {
        Rows.Clear();
        try
        {
            Log.Information("Loading XML stream");
            var candidates = new HashSet<string>(new[] { "Source", "Dest", "Text", "Entry", "String", "Value" }, StringComparer.OrdinalIgnoreCase);
            var settings = XmlStreamReader.CreateSafeReaderSettings();

            // 루트 판별
            if (stream.CanSeek) stream.Position = 0;
            string? root = null;
            using (var xr0 = XmlReader.Create(stream, settings))
            {
                if (xr0.Read())
                {
                    xr0.MoveToContent();
                    root = xr0.LocalName;
                }
            }

            // xTranslator 형식: SSTXMLRessources/Content/String/{Source,Dest}
            if (string.Equals(root, "SSTXMLRessources", StringComparison.OrdinalIgnoreCase))
            {
                if (stream.CanSeek) stream.Position = 0;
                using var xr = XmlReader.Create(stream, settings);
                while (xr.Read())
                {
                    if (xr.NodeType == XmlNodeType.Element && xr.LocalName.Equals("String", StringComparison.OrdinalIgnoreCase))
                    {
                        string? edid = null, rec = null, src = null, dst = null;
                        using (var sub = xr.ReadSubtree())
                        {
                            sub.Read(); // position on <String>
                            while (sub.Read())
                            {
                                if (sub.NodeType == XmlNodeType.Element)
                                {
                                    if (sub.LocalName.Equals("EDID", StringComparison.OrdinalIgnoreCase)) edid = sub.ReadElementContentAsString();
                                    else if (sub.LocalName.Equals("REC", StringComparison.OrdinalIgnoreCase)) rec = sub.ReadElementContentAsString();
                                    else if (sub.LocalName.Equals("Source", StringComparison.OrdinalIgnoreCase)) src = sub.ReadElementContentAsString();
                                    else if (sub.LocalName.Equals("Dest", StringComparison.OrdinalIgnoreCase)) dst = sub.ReadElementContentAsString();
                                }
                            }
                        }
                        // 바깥 리더를 현재 <String> 전체 스킵
                        xr.Skip();

                        if (!string.IsNullOrWhiteSpace(src) || !string.IsNullOrWhiteSpace(dst))
                        {
                            var ctx = edid ?? rec ?? "Content/String";
                            var row = new TranslationRowVM(ctx, src ?? string.Empty) { TranslationText = dst ?? string.Empty };
                            Rows.Add(row);
                            Rows[^1].Index = Rows.Count;
                        }
                    }
                }
                Log.Information("xTranslator XML loaded: rows={Count}", Rows.Count);
                return;
            }

            // 일반(폴백) 경로: 가장 많은 리프 텍스트 경로를 골라 단일 컬럼으로 로드
            var countCandidates = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var countAll = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            if (stream.CanSeek) stream.Position = 0;
            using (var xr1 = XmlReader.Create(stream, settings))
            {
                foreach (var (path, text) in XmlStreamReader.EnumerateLeafTexts(xr1, _ => true))
                {
                    if (string.IsNullOrWhiteSpace(text)) continue;
                    var lastSep = path.LastIndexOf('/');
                    var elem = lastSep >= 0 ? path.Substring(lastSep + 1) : path;
                    if (candidates.Contains(elem))
                    {
                        countCandidates[path] = countCandidates.TryGetValue(path, out var c1) ? c1 + 1 : 1;
                    }
                    countAll[path] = countAll.TryGetValue(path, out var c2) ? c2 + 1 : 1;
                }
            }

            string? bestPath = null;
            if (countCandidates.Count > 0)
                bestPath = countCandidates.OrderByDescending(kv => kv.Value).First().Key;
            else if (countAll.Count > 0)
                bestPath = countAll.OrderByDescending(kv => kv.Value).First().Key;

            if (bestPath is null)
            {
                StatusText = "XML에 추출 가능한 텍스트가 없습니다.";
                Log.Warning("No extractable text found in XML");
                return;
            }

            // 2nd pass: extract only bestPath texts
            if (stream.CanSeek) stream.Position = 0;
            using (var xr2 = XmlReader.Create(stream, settings))
            {
                foreach (var (path, text) in XmlStreamReader.EnumerateLeafTexts(xr2, el => true))
                {
                    if (!string.Equals(path, bestPath, StringComparison.OrdinalIgnoreCase)) continue;
                    Rows.Add(new TranslationRowVM(path, text));
                    Rows[^1].Index = Rows.Count;
                }
                Log.Information("XML loaded: bestPath={Path} rows={Count}", bestPath, Rows.Count);
            }
        }
        catch (Exception ex)
        {
            StatusText = $"XML 로딩 오류: {ex.Message}";
            Log.Error(ex, "XML loading failed");
        }
        finally
        {
            StatusText = $"XML 로드됨: {Rows.Count} rows";
            Log.Information("XML load finished: rows={Count}", Rows.Count);
        }
    }
}
