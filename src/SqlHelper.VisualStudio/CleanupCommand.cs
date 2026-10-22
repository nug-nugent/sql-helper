using System;
using System.ComponentModel.Design;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using SqlHelper.Formatting;

namespace SqlHelper.VisualStudio
{
    /// <summary>
    /// SQL Helper > Clean Up: rewrites joins, abbreviated keywords, aliases and brackets in the selection, or the
    /// whole document when nothing is selected, into the house's preferred form, as a single undo step. The layout is
    /// left as it is.
    /// </summary>
    internal static class CleanupCommand
    {
        public static void Initialize(OleMenuCommandService commandService)
        {
            commandService.AddCommand(new MenuCommand(Execute, new CommandID(PackageIds.CommandSet, PackageIds.CleanupCommand)));
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

            var result = Cleanup.Apply(text);
            if (!result.Succeeded)
            {
                string where = wholeDocument ? "" : "In the selected text:" + Environment.NewLine;
                ActiveEditor.ShowMessage(where + string.Join(Environment.NewLine, result.Errors), OLEMSGICON.OLEMSGICON_WARNING);
                return;
            }

            ActiveEditor.ReplaceChanged(view, span.Start.Position, text, result.Text);
        }
    }
}