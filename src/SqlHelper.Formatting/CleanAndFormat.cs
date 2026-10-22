namespace SqlHelper.Formatting
{
    /// <summary>
    /// Everything at once (see docs/style-guide.md, Clean and Format): <see cref="Cleanup"/>, then
    /// <see cref="Semicolons"/>, then <see cref="SqlFormatter"/>, then <see cref="CommentHeader.Convert"/> for a header
    /// in an old format. Each step checks its own result; if any fails, the input comes back unchanged with its reasons.
    /// </summary>
    /// <remarks>
    /// The header goes last so its caret offset is into the final text. Formatting leaves a house header as it is, so
    /// converting it after formatting gives the same text as before.
    /// </remarks>
    public static class CleanAndFormat
    {
        /// <param name="header">Null leaves any header alone, as for a selection.</param>
        public static HeaderResult Apply(string sql, FormattingOptions formatting, HeaderOptions header)
        {
            sql = sql ?? "";

            var cleaned = Cleanup.Apply(sql);
            if (!cleaned.Succeeded) return HeaderResult.Failure(sql, cleaned.Errors);

            var terminated = Semicolons.Add(cleaned.Text);
            if (!terminated.Succeeded) return HeaderResult.Failure(sql, terminated.Errors);

            var formatted = new SqlFormatter(formatting).Format(terminated.Text);
            if (!formatted.Succeeded) return HeaderResult.Failure(sql, formatted.Errors);

            if (header == null) return HeaderResult.Success(formatted.Text, -1);
            var converted = new CommentHeader(header).Convert(formatted.Text);
            return converted.Succeeded ? converted : HeaderResult.Failure(sql, converted.Errors);
        }
    }
}