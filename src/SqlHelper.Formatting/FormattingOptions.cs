using System;

namespace SqlHelper.Formatting
{
    /// <summary>
    /// Settings for <see cref="SqlFormatter"/>. The defaults are the house style described in docs/style-guide.md.
    /// </summary>
    public sealed class FormattingOptions
    {
        /// <summary>Text written once per indent level.</summary>
        public string IndentString { get; set; } = "\t";

        /// <summary>Line break written between lines. There is never one after the last line.</summary>
        public string NewLine { get; set; } = Environment.NewLine;
    }
}
