namespace DWPCardScanNet.Core.Configuration
{
    /// <summary>
    /// Definition of one card group / transaction type. Ported from VB6 clsGroupDef.
    /// A group ties a set of cards to a NIST output directory, transaction type (TOT),
    /// verification file, key tag/prompt and optional post-process DLL.
    /// </summary>
    public sealed class GroupDefinition
    {
        public string GroupName { get; set; } = "";
        public string NISTOutDir { get; set; } = "";
        public string PostProcessDLL { get; set; } = "";
        public string PostProcessDLLcfg { get; set; } = "";
        public string VerificationFile { get; set; } = "";
        public string TOT { get; set; } = "";
        public string AwareKeyTag { get; set; } = "";
        public string KeyPrompt { get; set; } = "";
        public string KeyPrefix { get; set; } = "";
    }
}
