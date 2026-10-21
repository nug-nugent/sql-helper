using System;

namespace SqlHelper.Formatting
{
    /// <summary>
    /// Settings for <see cref="CommentHeader"/>: who is making the change, when, and what it was.
    /// </summary>
    public sealed class HeaderOptions
    {
        /// <summary>The initials written on the history line this run adds, e.g. <c>DH</c>. Required.</summary>
        public string Initials { get; set; }

        /// <summary>
        /// What changed, written after the dash on the added history line. Empty leaves <c>dd/MM/yyyy : DH - </c> for the
        /// user to finish. Ignored when the script has no history yet: that line always says <c>Created</c>.
        /// </summary>
        public string Note { get; set; } = "";

        /// <summary>The date written on the added history line.</summary>
        public DateTime Today { get; set; } = DateTime.Today;

        /// <summary>Line break for the lines written; null uses the script's own.</summary>
        public string NewLine { get; set; }
    }
}