using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using XtrXmlTranslator.Core.Tokenizing;

namespace XtrXmlTranslator.Benchmarks;

public class TokenizerBench
{
    private readonly string _s = "Hello <Alias=Player> %.0f {0} %{name} world!";

    [Benchmark]
    public object Tokenize() => InlineTokenizer.Tokenize(_s);
}

public class Program
{
    public static void Main(string[] args)
    {
        BenchmarkRunner.Run<TokenizerBench>();
    }
}

