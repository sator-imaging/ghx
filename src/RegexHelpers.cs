using System.Text.RegularExpressions;

namespace GitHubWorkflow;

internal static partial class RegexHelpers
{
    //lang=regex
    const string PlaceholderPatternString = @"\$\{\{\s*(matrix|inputs)\s*\.\s*([A-Za-z0-9_-]+)\s*\}\}";
    //lang=regex
    const string StepSummaryRedirectPatternString = @"\s*>>?\s*\$GITHUB_STEP_SUMMARY";
    //lang=regex
    const string InlineCommentPatternString = @"#.*$";
    //lang=regex
    const string DollarPositionalPatternString = @"\$([0-9])";
    //lang=regex
    const string SleepCommandPatternString = @"^\s*sleep\s+([0-9]+)\s*;?\s*$";

#if NET9_0_OR_GREATER
    [GeneratedRegex(PlaceholderPatternString)]
    public static partial Regex PlaceholderPattern { get; }

    [GeneratedRegex(StepSummaryRedirectPatternString)]
    public static partial Regex StepSummaryRedirectPattern { get; }

    [GeneratedRegex(InlineCommentPatternString)]
    public static partial Regex InlineCommentPattern { get; }

    [GeneratedRegex(DollarPositionalPatternString)]
    public static partial Regex DollarPositionalPattern { get; }

    [GeneratedRegex(SleepCommandPatternString, RegexOptions.Multiline)]
    public static partial Regex SleepCommandPattern { get; }
#else
    public static Regex PlaceholderPattern { get; } =
        new Regex(PlaceholderPatternString, RegexOptions.Compiled);

    public static Regex StepSummaryRedirectPattern { get; } =
        new Regex(StepSummaryRedirectPatternString, RegexOptions.Compiled);

    public static Regex InlineCommentPattern { get; } =
        new Regex(InlineCommentPatternString, RegexOptions.Compiled);

    public static Regex DollarPositionalPattern { get; } =
        new Regex(DollarPositionalPatternString, RegexOptions.Compiled);

    public static Regex SleepCommandPattern { get; } =
        new Regex(SleepCommandPatternString, RegexOptions.Compiled | RegexOptions.Multiline);
#endif
}
