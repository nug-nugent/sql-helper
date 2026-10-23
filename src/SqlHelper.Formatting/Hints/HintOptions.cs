namespace SqlHelper.Formatting.Hints
{
    public sealed class HintOptions
    {
        /// <summary>
        /// Whether <c>SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED</c> is flagged (SH402). Off by default: unlike a
        /// <c>NOLOCK</c> hint on one table, it is usually a deliberate choice for a whole report or session.
        /// </summary>
        public bool FlagReadUncommittedIsolation { get; set; }
    }
}