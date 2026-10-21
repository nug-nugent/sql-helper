using System.Collections.Generic;

namespace SqlHelper.Formatting
{
    /// <summary>
    /// The outcome of <see cref="SqlFormatter.Format"/>. When <see cref="Succeeded"/> is false, <see cref="Text"/> is the
    /// original input unchanged and <see cref="Errors"/> says why.
    /// </summary>
    public sealed class FormatResult
    {
        FormatResult(bool succeeded, string text, IReadOnlyList<string> errors)
        {
            Succeeded = succeeded;
            Text = text;
            Errors = errors;
        }

        public bool Succeeded { get; }
        public string Text { get; }
        public IReadOnlyList<string> Errors { get; }

        internal static FormatResult Success(string text) => new FormatResult(true, text, new string[0]);

        internal static FormatResult Failure(string originalText, IReadOnlyList<string> errors) => new FormatResult(false, originalText, errors);
    }
}
