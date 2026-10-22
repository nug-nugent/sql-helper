using System.Collections.Generic;

namespace SqlHelper.Formatting
{
    /// <summary>
    /// The outcome of <see cref="CommentHeader.Apply"/>. When <see cref="Succeeded"/> is false, <see cref="Text"/> is the
    /// original input unchanged and <see cref="Errors"/> says why.
    /// </summary>
    public sealed class HeaderResult
    {
        HeaderResult(bool succeeded, string text, int caretOffset, IReadOnlyList<string> errors)
        {
            Succeeded = succeeded;
            Text = text;
            CaretOffset = caretOffset;
            Errors = errors;
        }

        public bool Succeeded { get; }
        public string Text { get; }

        /// <summary>
        /// Where an editor should put the cursor in <see cref="Text"/>: the end of the history line just added, or of the
        /// empty description line of a new header. -1 when the call failed or there was nothing to convert.
        /// </summary>
        public int CaretOffset { get; }

        public IReadOnlyList<string> Errors { get; }

        internal static HeaderResult Success(string text, int caretOffset) => new HeaderResult(true, text, caretOffset, new string[0]);

        internal static HeaderResult Failure(string originalText, IReadOnlyList<string> errors) => new HeaderResult(false, originalText, -1, errors);
    }
}