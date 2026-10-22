using System;
using System.ComponentModel.Design;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using SqlHelper.Formatting;

namespace SqlHelper.VisualStudio
{
    /// <summary>
    /// SQL Helper > Convert to Dynamic SQL and Convert from Dynamic SQL: convert the selected statements, or the whole
    /// document when nothing is selected, as a single undo step. Both get the whole document, so declarations outside
    /// the selection are found, and may change it outside the selection (the DECLARE of an @Sql variable that goes).
    /// </summary>
    internal static class DynamicSqlCommands
    {
        public static void Initialize(OleMenuCommandService commandService)
        {
            commandService.AddCommand(new MenuCommand(ExecuteToDynamic, new CommandID(PackageIds.CommandSet, PackageIds.ToDynamicCommand)));
            commandService.AddCommand(new MenuCommand(ExecuteFromDynamic, new CommandID(PackageIds.CommandSet, PackageIds.FromDynamicCommand)));
        }

        static void ExecuteToDynamic(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Execute(DynamicSql.ToDynamic);
        }

        static void ExecuteFromDynamic(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Execute(DynamicSql.FromDynamic);
        }

        static void Execute(Func<string, int, int, FormatResult> convert)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var view = ActiveEditor.ViewOrExplain();
            if (view == null) return;

            string text = view.TextBuffer.CurrentSnapshot.GetText();
            var selection = view.Selection.StreamSelectionSpan.SnapshotSpan.Span;
            var result = view.Selection.IsEmpty ? convert(text, 0, 0) : convert(text, selection.Start, selection.Length);
            if (!result.Succeeded)
            {
                ActiveEditor.ShowMessage(string.Join(Environment.NewLine, result.Errors), OLEMSGICON.OLEMSGICON_WARNING);
                return;
            }

            ActiveEditor.ReplaceChanged(view, 0, text, result.Text);
        }
    }
}