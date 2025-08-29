using LanguageExt;
using static LanguageExt.Prelude;

namespace NetworkAnalyzer
{
    public static class GlobalVariables
    {
        public static Option<NetworkAnalysisResult> lastAnalysisResult { get; set; } = None;
    }
}
