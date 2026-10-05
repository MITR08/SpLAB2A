using System.Globalization;

namespace GilbParser;

public sealed class KotlinGilbAnalyzer
{
    private readonly string _src;
    private readonly bool _countLoopNesting;
    private int _i;

    private int _cl;
    private int _total;
    private int _maxDepth;
    private bool _anyBranch;
    private int _logicalOps;
    private readonly List<string> _details = [];

    private KotlinGilbAnalyzer(string source, bool countLoopNesting)
    {
        _src = source.Replace("\r\n", "\n").Replace('\r', '\n');
        _countLoopNesting = countLoopNesting;
    }

    public static GilbMetrics Analyze(string source, bool countLoopNesting = true)
    {
        var analyzer = new KotlinGilbAnalyzer(source, countLoopNesting);
        analyzer.ParseProgram();
        int total = analyzer._total;
        return new GilbMetrics
        {
            CL = analyzer._cl,
            RelativeCl = total == 0 ? 0.0 : (double)analyzer._cl / total,
            CLI = analyzer._anyBranch ? analyzer._maxDepth : 0,
            TotalOperators = total,
            McCabe = 1 + analyzer._cl + analyzer._logicalOps,
            Details = analyzer._details
        };
    }

    private bool Eof => _i >= _src.Length;
    private char Ch => _src[_i];

    private void ParseProgram()
    {
        SkipTrivia();
        while (!Eof)
        {
            if (MatchKeyword("package") || MatchKeyword("import"))
            {
                ConsumeSimpleStatement();
                SkipTrivia();
                continue;
            }

            ParseStatement(0);
            SkipTrivia();
        }
    }

    private void ParseStatement(int depth)
    {
        SkipTrivia();
        if (Eof || Ch == '}')
            return;

        SkipModifiers();
        SkipTrivia();
        if (Eof || Ch == '}')
            return;

        if (MatchKeyword("fun"))
        {
            ParseFunction(depth);
            return;
        }
        if (MatchKeyword("class") || MatchKeyword("interface") || MatchKeyword("object"))
        {
            ParseTypeBody(depth);
            return;
        }

        _total++;

        if (MatchKeyword("if"))
        {
            ParseIf(depth);
            return;
        }
        if (MatchKeyword("when"))
        {
            ParseWhen(depth);
            return;
        }
        if (MatchKeyword("for"))
        {
            ParseLoop(depth, "for");
            return;
        }
        if (MatchKeyword("while"))
        {
            ParseLoop(depth, "while");
            return;
        }
        if (MatchKeyword("do"))
        {
            ParseDoWhile(depth);
            return;
        }

        ConsumeSimpleStatement();
    }

    private void ParseStatementOrBlock(int depth)
    {
        SkipTrivia();
        if (!Eof && Ch == '{')
            ParseBlock(depth);
        else
            ParseStatement(depth);
    }

    private void ParseBlock(int depth)
    {
        Expect('{');
        SkipTrivia();
        while (!Eof && Ch != '}')
        {
            ParseStatement(depth);
            SkipTrivia();
        }
        Expect('}');
    }

    private void ParseIf(int depth)
    {
        _cl++;
        _anyBranch = true;
        _maxDepth = Math.Max(_maxDepth, depth);
        _details.Add($"if  (уровень {depth})");

        SkipTrivia();
        CountLogicalsInParens();
        ParseStatementOrBlock(depth + 1);

        SkipTrivia();
        if (MatchKeyword("else"))
            ParseStatementOrBlock(depth + 1);
    }

    private void ParseLoop(int depth, string kind)
    {
        _cl++;
        _anyBranch = true;
        int newDepth = depth;
        if (_countLoopNesting)
        {
            _maxDepth = Math.Max(_maxDepth, depth);
            newDepth = depth + 1;
        }

        _details.Add($"{kind}  (уровень {depth})");
        SkipTrivia();
        CountLogicalsInParens();
        ParseStatementOrBlock(newDepth);
    }

    private void ParseDoWhile(int depth)
    {
        _cl++;
        _anyBranch = true;
        int newDepth = depth;
        if (_countLoopNesting)
        {
            _maxDepth = Math.Max(_maxDepth, depth);
            newDepth = depth + 1;
        }

        _details.Add($"do-while  (уровень {depth})");
        ParseStatementOrBlock(newDepth);
        SkipTrivia();
        if (!MatchKeyword("while"))
            throw Error("После do ожидается while");
        SkipTrivia();
        CountLogicalsInParens();
        SkipTrivia();
        if (!Eof && Ch == ';')
            _i++;
    }

    private void ParseWhen(int depth)
    {
        SkipTrivia();
        if (!Eof && Ch == '(')
            CountLogicalsInParens();

        SkipTrivia();
        Expect('{');

        var bodies = new List<(bool IsElse, string Source)>();
        SkipTrivia();
        while (!Eof && Ch != '}')
        {
            bool isElse = IsElseWhenBranch();
            if (isElse)
                MatchKeyword("else");

            SkipTrivia();
            ConsumeUntilArrow();
            SkipTrivia();
            bodies.Add((isElse, CaptureWhenBody()));
            SkipTrivia();
        }
        Expect('}');

        var real = bodies.Where(b => !b.IsElse).ToList();
        var defs = bodies.Where(b => b.IsElse).ToList();
        int nReal = real.Count;
        int totalBranches = nReal + (defs.Count > 0 ? 1 : 0);
        bool branching = totalBranches >= 2;

        if (branching)
        {
            _cl += totalBranches - 1;
            _anyBranch = true;
            _maxDepth = Math.Max(_maxDepth, depth + (totalBranches - 2));
            _details.Add(
                $"when: {totalBranches} ветвей → CL += {totalBranches - 1}, вклад в CLI = {totalBranches - 2} (уровень {depth})");
        }
        else
            _details.Add($"when без ветвления (уровень {depth})");

        int iCase = 0;
        foreach (var body in real)
        {
            int caseDepth = branching ? depth + iCase + 1 : depth;
            AnalyzeEmbedded(body.Source, caseDepth);
            iCase++;
        }

        foreach (var body in defs)
        {
            int defaultDepth = branching ? depth + nReal : depth;
            AnalyzeEmbedded(body.Source, defaultDepth);
        }
    }

    private bool IsElseWhenBranch()
    {
        int saved = _i;
        if (!MatchKeyword("else"))
        {
            _i = saved;
            return false;
        }
        SkipTrivia();
        bool arrow = PeekArrow();
        _i = saved;
        return arrow;
    }

    private bool PeekArrow() =>
        _i + 1 < _src.Length && _src[_i] == '-' && _src[_i + 1] == '>';

    private bool MatchArrow()
    {
        if (!PeekArrow())
            return false;
        _i += 2;
        return true;
    }

    private void ConsumeUntilArrow()
    {
        int paren = 0, brace = 0, bracket = 0;
        while (!Eof)
        {
            if (paren == 0 && brace == 0 && bracket == 0 && MatchArrow())
                return;
            AdvanceBalanced(ref paren, ref brace, ref bracket);
        }
        throw Error("В ветке when не найдена стрелка ->");
    }

    private string CaptureWhenBody()
    {
        int start = _i;
        SkipTrivia();
        if (!Eof && Ch == '{')
        {
            ConsumeBalanced('{', '}');
            return _src[start.._i];
        }

        int paren = 0, brace = 0, bracket = 0;
        bool seen = false;
        while (!Eof)
        {
            if (paren == 0 && brace == 0 && bracket == 0)
            {
                if (Ch == '}')
                    break;
                if (Ch == ';' || (Ch == '\n' && seen))
                {
                    if (Ch == ';')
                        _i++;
                    break;
                }
            }
            if (!char.IsWhiteSpace(Ch))
                seen = true;
            AdvanceBalanced(ref paren, ref brace, ref bracket);
        }
        return _src[start.._i];
    }

    private void AnalyzeEmbedded(string source, int depth)
    {
        var nested = new KotlinGilbAnalyzer(source, _countLoopNesting);
        nested.SkipTrivia();
        if (nested.Eof)
            return;

        if (nested.Ch == '{')
            nested.ParseBlock(depth);
        else
            nested.ParseStatement(depth);

        _cl += nested._cl;
        _total += nested._total;
        _maxDepth = Math.Max(_maxDepth, nested._maxDepth);
        _anyBranch |= nested._anyBranch;
        _logicalOps += nested._logicalOps;
        _details.AddRange(nested._details);
    }

    private void ParseFunction(int depth)
    {
        _details.Add($"fun  (уровень {depth})");
        int paren = 0, angle = 0;
        while (!Eof)
        {
            SkipTrivia();
            if (Eof)
                return;

            if (paren == 0 && angle == 0 && Ch == '{')
            {
                ParseBlock(depth);
                return;
            }
            if (paren == 0 && angle == 0 && Ch == '=')
            {
                _i++;
                ConsumeSimpleStatement();
                return;
            }

            if (Ch == '(')
            {
                paren++;
                _i++;
            }
            else if (Ch == ')')
            {
                paren = Math.Max(0, paren - 1);
                _i++;
            }
            else if (Ch == '<' && paren == 0)
            {
                angle++;
                _i++;
            }
            else if (Ch == '>' && paren == 0)
            {
                angle = Math.Max(0, angle - 1);
                _i++;
            }
            else
            {
                int p = paren, b = 0, k = 0;
                AdvanceBalanced(ref p, ref b, ref k);
                paren = p;
            }
        }
    }

    private void ParseTypeBody(int depth)
    {
        _details.Add($"объявление типа  (уровень {depth})");
        int paren = 0;
        while (!Eof)
        {
            if (paren == 0 && Ch == '{')
            {
                ParseBlock(depth);
                return;
            }
            if (paren == 0 && (Ch == '\n' || Ch == ';'))
            {
                if (Ch == ';')
                    _i++;
                return;
            }
            if (Ch == '(')
            {
                paren++;
                _i++;
            }
            else if (Ch == ')')
            {
                paren = Math.Max(0, paren - 1);
                _i++;
            }
            else
            {
                int p = paren, b = 0, k = 0;
                AdvanceBalanced(ref p, ref b, ref k);
                paren = p;
            }
        }
    }

    private void ConsumeSimpleStatement()
    {
        int paren = 0, brace = 0, bracket = 0;
        bool seen = false;
        while (!Eof)
        {
            if (paren == 0 && brace == 0 && bracket == 0)
            {
                if (Ch == '}')
                    return;
                if (Ch == ';')
                {
                    _i++;
                    return;
                }
                if (Ch == '\n' && seen)
                    return;
            }
            if (!char.IsWhiteSpace(Ch))
                seen = true;
            AdvanceBalanced(ref paren, ref brace, ref bracket);
        }
    }

    private void SkipModifiers()
    {
        while (true)
        {
            SkipTrivia();
            int saved = _i;
            if (!ReadIdentifier(out string ident) || !IsModifier(ident))
            {
                _i = saved;
                return;
            }
        }
    }

    private static bool IsModifier(string ident) => ident is
        "public" or "private" or "protected" or "internal" or
        "open" or "override" or "abstract" or "final" or
        "suspend" or "inline" or "data" or "inner" or
        "sealed" or "annotation" or "const" or "lateinit" or
        "actual" or "expect" or "tailrec" or "operator" or
        "infix" or "external" or "companion" or "enum";

    private void CountLogicalsInParens()
    {
        SkipTrivia();
        if (Eof || Ch != '(')
            return;
        int start = _i;
        ConsumeBalanced('(', ')');
        _logicalOps += CountLogicalOperators(_src[(start + 1)..(_i - 1)]);
    }

    private static int CountLogicalOperators(string text)
    {
        int n = 0;
        var tmp = new KotlinGilbAnalyzer(text, true);
        while (!tmp.Eof)
        {
            if (tmp.Ch == '"' || tmp.Ch == '\'')
            {
                tmp.SkipString();
                continue;
            }
            if (tmp._i + 1 < tmp._src.Length &&
                (tmp._src[tmp._i] == '&' && tmp._src[tmp._i + 1] == '&' ||
                 tmp._src[tmp._i] == '|' && tmp._src[tmp._i + 1] == '|'))
            {
                n++;
                tmp._i += 2;
                continue;
            }
            tmp._i++;
        }
        return n;
    }

    private void ConsumeBalanced(char open, char close)
    {
        Expect(open);
        int depth = 1;
        while (!Eof && depth > 0)
        {
            if (Ch == '"' || Ch == '\'')
            {
                SkipString();
                continue;
            }
            if (Ch == '/' && _i + 1 < _src.Length && (_src[_i + 1] == '/' || _src[_i + 1] == '*'))
            {
                SkipCommentOnly();
                continue;
            }
            if (Ch == open)
                depth++;
            else if (Ch == close)
                depth--;
            _i++;
        }
    }

    private void AdvanceBalanced(ref int paren, ref int brace, ref int bracket)
    {
        if (Eof)
            return;
        if (Ch == '"' || Ch == '\'')
        {
            SkipString();
            return;
        }
        if (Ch == '/' && _i + 1 < _src.Length && (_src[_i + 1] == '/' || _src[_i + 1] == '*'))
        {
            SkipCommentOnly();
            return;
        }
        if (Ch == '(') paren++;
        else if (Ch == ')') paren = Math.Max(0, paren - 1);
        else if (Ch == '{') brace++;
        else if (Ch == '}') brace = Math.Max(0, brace - 1);
        else if (Ch == '[') bracket++;
        else if (Ch == ']') bracket = Math.Max(0, bracket - 1);
        _i++;
    }

    private void SkipCommentOnly()
    {
        if (Ch == '/' && _i + 1 < _src.Length && _src[_i + 1] == '/')
        {
            _i += 2;
            while (!Eof && Ch != '\n')
                _i++;
            return;
        }
        if (Ch == '/' && _i + 1 < _src.Length && _src[_i + 1] == '*')
        {
            _i += 2;
            while (_i + 1 < _src.Length && !(_src[_i] == '*' && _src[_i + 1] == '/'))
                _i++;
            if (_i + 1 < _src.Length)
                _i += 2;
        }
    }

    private void SkipTrivia()
    {
        while (!Eof)
        {
            if (char.IsWhiteSpace(Ch))
            {
                _i++;
                continue;
            }
            if (Ch == '/' && _i + 1 < _src.Length && (_src[_i + 1] == '/' || _src[_i + 1] == '*'))
            {
                SkipCommentOnly();
                continue;
            }
            break;
        }
    }

    private void SkipString()
    {
        if (Eof)
            return;
        if (_i + 2 < _src.Length && _src[_i] == '"' && _src[_i + 1] == '"' && _src[_i + 2] == '"')
        {
            _i += 3;
            while (_i + 2 < _src.Length &&
                   !(_src[_i] == '"' && _src[_i + 1] == '"' && _src[_i + 2] == '"'))
                _i++;
            if (_i + 2 < _src.Length)
                _i += 3;
            return;
        }

        char q = Ch;
        _i++;
        while (!Eof)
        {
            if (Ch == '\\')
            {
                _i += 2;
                continue;
            }
            if (Ch == q)
            {
                _i++;
                return;
            }
            _i++;
        }
    }

    private bool MatchKeyword(string kw)
    {
        SkipTrivia();
        int saved = _i;
        if (!ReadIdentifier(out string ident) || ident != kw)
        {
            _i = saved;
            return false;
        }
        return true;
    }

    private bool ReadIdentifier(out string ident)
    {
        ident = "";
        if (Eof || !(char.IsLetter(Ch) || Ch == '_'))
            return false;
        int start = _i;
        _i++;
        while (!Eof && (char.IsLetterOrDigit(Ch) || Ch == '_'))
            _i++;
        ident = _src[start.._i];
        return true;
    }

    private void Expect(char c)
    {
        SkipTrivia();
        if (Eof || Ch != c)
            throw Error($"Ожидался символ '{c}'");
        _i++;
    }

    private Exception Error(string message)
    {
        int line = 1, col = 1;
        for (int k = 0; k < Math.Min(_i, _src.Length); k++)
        {
            if (_src[k] == '\n')
            {
                line++;
                col = 1;
            }
            else
                col++;
        }
        return new InvalidOperationException($"{message} (строка {line}, позиция {col})");
    }
}

public sealed class GilbMetrics
{
    public int CL { get; init; }
    public double RelativeCl { get; init; }
    public int CLI { get; init; }
    public int TotalOperators { get; init; }
    public int McCabe { get; init; }
    public IReadOnlyList<string> Details { get; init; } = [];

    public string RelativeFormatted => RelativeCl.ToString("0.000", CultureInfo.InvariantCulture);
}
