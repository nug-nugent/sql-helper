namespace SqlHelper.Formatting.Hints
{
    public enum HintSeverity
    {
        /// <summary>Very likely a real performance or correctness problem.</summary>
        Warning,

        /// <summary>Worth a look: whether it matters depends on the data or the intent.</summary>
        Suggestion,
    }

    /// <summary>One thing <see cref="PerformanceHints.Check"/> found, at a place in the script.</summary>
    public sealed class Hint
    {
        internal Hint(string code, HintSeverity severity, int line, int column, int offset, int length, string message)
        {
            Code = code;
            Severity = severity;
            Line = line;
            Column = column;
            Offset = offset;
            Length = length;
            Message = message;
        }

        /// <summary>The rule's stable code, such as <c>SH101</c> (listed in docs/style-guide.md).</summary>
        public string Code { get; }

        public HintSeverity Severity { get; }

        /// <summary>1-based line of the start of the code the hint is about.</summary>
        public int Line { get; }

        /// <summary>1-based column of the start of the code the hint is about.</summary>
        public int Column { get; }

        /// <summary>0-based character offset of the code the hint is about.</summary>
        public int Offset { get; }

        /// <summary>Length in characters of the code the hint is about.</summary>
        public int Length { get; }

        public string Message { get; }

        /// <summary><c>line:column: warning SH101: message</c>, as the command line prints it after the file name.</summary>
        public override string ToString() =>
            $"{Line}:{Column}: {(Severity == HintSeverity.Warning ? "warning" : "suggestion")} {Code}: {Message}";
    }
}