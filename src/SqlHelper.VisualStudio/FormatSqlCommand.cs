using System;
using System.ComponentModel.Design;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using SqlHelper.Formatting;

namespace SqlHelper.VisualStudio
{
    /// <summary>
    /// SQL Helper > Format SQL: formats the selection, or the whole document when nothing is selected, as a single undo
    /// step. If the SQL can't be formatted the text is left alone and the reason is shown.
    /// </summary>
    internal static class FormatSqlCommand
    {
        public static void Initialize(OleMenuCommandService commandService)
        {
            commandService.AddCommand(new MenuCommand(Execute, new CommandID(PackageIds.CommandSet, PackageIds.FormatSqlCommand)));
        }

        static void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var view = ActiveEditor.ViewOrExplain();
            if (view == null) return;

            var snapshot = view.TextBuffer.CurrentSnapshot;
            bool wholeDocument = view.Selection.IsEmpty;
            var span = wholeDocument
                ? new SnapshotSpan(snapshot, 0, snapshot.Length)
                : new SnapshotSpan(view.Selection.Start.Position, view.Selection.End.Position);
            string text = span.GetText();

            var result = new SqlFormatter(new FormattingOptions { NewLine = ActiveEditor.LineBreakUsedIn(snapshot) }).Format(text);
            if (!result.Succeeded)
            {
                string where = wholeDocument ? "" : "In the selected text:" + Environment.NewLine;
                ActiveEditor.ShowMessage(where + string.Join(Environment.NewLine, result.Errors), OLEMSGICON.OLEMSGICON_WARNING);
                return;
            }

            // A selection keeps the whitespace around it so it doesn't run into the lines either side; a whole document
            // ends where its last line ends.
            string replacement = wholeDocument
                ? result.Text
                : text.Substring(0, text.Length - text.TrimStart().Length) + result.Text + text.Substring(text.TrimEnd().Length);
            if (replacement == text) return;

            using (var edit = view.TextBuffer.CreateEdit())
            {
                edit.Replace(span, replacement);
                edit.Apply();
            }
        }
    }
}