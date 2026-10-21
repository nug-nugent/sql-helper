using System;
using System.ComponentModel.Design;
using Microsoft;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using SqlHelper.Formatting;
using Task = System.Threading.Tasks.Task;

namespace SqlHelper.VisualStudio
{
    /// <summary>
    /// SQL Helper > Format SQL: formats the selection, or the whole document when nothing is selected, as a single undo
    /// step. If the SQL can't be formatted the text is left alone and the reason is shown.
    /// </summary>
    internal static class FormatSqlCommand
    {
        const string Title = "SQL Helper";

        static AsyncPackage _package;
        static IVsTextManager _textManager;
        static IVsEditorAdaptersFactoryService _editorAdapters;

        public static async Task InitializeAsync(AsyncPackage package, OleMenuCommandService commandService)
        {
            _package = package;
            _textManager = (IVsTextManager)await package.GetServiceAsync(typeof(SVsTextManager));
            var componentModel = (IComponentModel)await package.GetServiceAsync(typeof(SComponentModel));
            Assumes.Present(_textManager);
            Assumes.Present(componentModel);
            _editorAdapters = componentModel.GetService<IVsEditorAdaptersFactoryService>();

            commandService.AddCommand(new MenuCommand(Execute, new CommandID(PackageIds.CommandSet, PackageIds.FormatSqlCommand)));
        }

        static void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var view = ActiveTextView();
            if (view == null)
            {
                ShowMessage("Open a query window or .sql file first.", OLEMSGICON.OLEMSGICON_INFO);
                return;
            }

            var snapshot = view.TextBuffer.CurrentSnapshot;
            bool wholeDocument = view.Selection.IsEmpty;
            var span = wholeDocument
                ? new SnapshotSpan(snapshot, 0, snapshot.Length)
                : new SnapshotSpan(view.Selection.Start.Position, view.Selection.End.Position);
            string text = span.GetText();

            var result = new SqlFormatter(new FormattingOptions { NewLine = LineBreakUsedIn(snapshot) }).Format(text);
            if (!result.Succeeded)
            {
                string where = wholeDocument ? "" : "In the selected text:" + Environment.NewLine;
                ShowMessage(where + string.Join(Environment.NewLine, result.Errors), OLEMSGICON.OLEMSGICON_WARNING);
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

        static IWpfTextView ActiveTextView()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _textManager.GetActiveView(1, null, out IVsTextView activeView);
            return activeView == null ? null : _editorAdapters.GetWpfTextView(activeView);
        }

        /// <summary>The document's own line break, so formatting doesn't mix CRLF and LF.</summary>
        static string LineBreakUsedIn(ITextSnapshot snapshot)
        {
            foreach (var line in snapshot.Lines)
                if (line.LineBreakLength > 0) return line.GetLineBreakText();
            return Environment.NewLine;
        }

        static void ShowMessage(string message, OLEMSGICON icon)
        {
            VsShellUtilities.ShowMessageBox(_package, message, Title, icon, OLEMSGBUTTON.OLEMSGBUTTON_OK, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
        }
    }
}
