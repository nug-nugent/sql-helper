using System.Collections.Generic;

namespace SqlHelper.Formatting.Hints
{
    /// <summary>
    /// The outcome of <see cref="PerformanceHints.Check"/>. When <see cref="Succeeded"/> is false the script couldn't be
    /// checked (it doesn't parse) and <see cref="Errors"/> says why.
    /// </summary>
    public sealed class HintResult
    {
        HintResult(bool succeeded, IReadOnlyList<Hint> hints, IReadOnlyList<string> errors)
        {
            Succeeded = succeeded;
            Hints = hints;
            Errors = errors;
        }

        public bool Succeeded { get; }

        /// <summary>In script order.</summary>
        public IReadOnlyList<Hint> Hints { get; }

        public IReadOnlyList<string> Errors { get; }

        internal static HintResult Success(IReadOnlyList<Hint> hints) => new HintResult(true, hints, new string[0]);

        internal static HintResult Failure(IReadOnlyList<string> errors) => new HintResult(false, new Hint[0], errors);
    }
}