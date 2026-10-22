using System;
using System.ComponentModel.Design;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using SqlHelper.Formatting;

namespace SqlHelper.VisualStudio
{
    /// <summary>
    /// SQL Helper > Clean and Format: Clean Up, Add Semicolons and Format SQL in one, on the selection or the whole
    /// document when nothing is selected, as a single undo step. On the whole document an SSMS template header or dash
    /// history block is also converted to the house header, and the cursor left on the history line added; a house
    /// header, or none, is left alone.
    /// </summary>
    internal static class CleanAndFormatCommand
    {
        public static void Initialize(OleMenuCommandService commandService)
        {
            commandService.AddCommand(new MenuCommand(Execute, new CommandID(PackageIds.CommandSet, PackageIds.CleanAndFormatCommand)));
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
            string newLine = ActiveEditor.LineBreakUsedIn(snapshot);

            var header = wholeDocument ? new HeaderOptions { Initials = HeaderCommand.Initials(), NewLine = newLine } : null;
            var result = CleanAndFormat.Apply(text, new FormattingOptions { NewLine = newLine }, header);
            if (!result.Succeeded)
            {
                string where = wholeDocument ? "" : "In the selected text:" + Environment.NewLine;
                ActiveEditor.ShowMessage(where + string.Join(Environment.NewLine, result.Errors), OLEMSGICON.OLEMSGICON_WARNING);
                return;
            }

            if (!wholeDocument)
            {
                // As Format SQL: the selection keeps the whitespace around it.
                string replacement = text.Substring(0, text.Length - text.TrimStart().Length) + result.Text + text.Substring(text.TrimEnd().Length);
                ActiveEditor.ReplaceChanged(view, span.Start.Position, text, replacement);
                return;
            }

            ActiveEditor.ReplaceChanged(view, 0, text, result.Text);
            if (result.CaretOffset < 0) return;

            var caret = new SnapshotPoint(view.TextBuffer.CurrentSnapshot, result.CaretOffset);
            view.Selection.Clear();
            view.Caret.MoveTo(caret);
            view.ViewScroller.EnsureSpanVisible(new SnapshotSpan(caret, 0), EnsureSpanVisibleOptions.None);
        }
    }
}