using System;
using System.ComponentModel.Design;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using SqlHelper.Formatting;
using SqlHelper.Formatting.Hints;

namespace SqlHelper.VisualStudio
{
    /// <summary>
    /// SQL Helper > Show Performance Hints: checks the selection, or the whole document when nothing is selected, for
    /// patterns that are often slow or wrong, and lists them in the Error List. Double-clicking one selects the code it
    /// is about. The text is never changed.
    /// </summary>
    internal static class PerformanceHintsCommand
    {
        static AsyncPackage _package;
        static ErrorListProvider _errorList;

        public static void Initialize(AsyncPackage package, OleMenuCommandService commandService)
        {
            _package = package;
            commandService.AddCommand(new MenuCommand(Execute, new CommandID(PackageIds.CommandSet, PackageIds.PerformanceHintsCommand)));
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

            var options = new HintOptions { FlagReadUncommittedIsolation = new Settings(_package).FlagReadUncommittedIsolation };
            var result = PerformanceHints.Check(span.GetText(), options);
            var errorList = ErrorList();
            errorList.Tasks.Clear();
            if (!result.Succeeded)
            {
                string where = wholeDocument ? "" : "In the selected text:" + Environment.NewLine;
                ActiveEditor.ShowMessage(where + string.Join(Environment.NewLine, result.Errors), OLEMSGICON.OLEMSGICON_WARNING);
                return;
            }
            if (result.Hints.Count == 0)
            {
                ActiveEditor.ShowMessage(wholeDocument ? "No performance hints for this script." : "No performance hints for the selected text.",
                    OLEMSGICON.OLEMSGICON_INFO);
                return;
            }

            string document = view.TextBuffer.Properties.TryGetProperty(typeof(ITextDocument), out ITextDocument textDocument)
                ? textDocument.FilePath
                : "";
            errorList.SuspendRefresh();
            try
            {
                foreach (var hint in result.Hints)
                {
                    var target = snapshot.CreateTrackingSpan(span.Start.Position + hint.Offset, hint.Length, SpanTrackingMode.EdgeInclusive);
                    var start = target.GetStartPoint(snapshot);
                    var line = start.GetContainingLine();
                    var task = new ErrorTask
                    {
                        Category = TaskCategory.BuildCompile,
                        ErrorCategory = hint.Severity == HintSeverity.Warning ? TaskErrorCategory.Warning : TaskErrorCategory.Message,
                        Text = $"{hint.Code}: {hint.Message}",
                        Document = document,
                        Line = line.LineNumber,
                        Column = start.Position - line.Start.Position,
                    };
                    task.Navigate += (s, args) => GoTo(view, document, target);
                    errorList.Tasks.Add(task);
                }
            }
            finally
            {
                errorList.ResumeRefresh();
            }
            errorList.BringToFront();
        }

        static ErrorListProvider ErrorList()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            return _errorList ?? (_errorList = new ErrorListProvider(_package) { ProviderName = "SQL Helper Performance Hints" });
        }

        /// <summary>Selects the hint's code in the editor it came from, wherever edits since have moved it.</summary>
        static void GoTo(IWpfTextView view, string document, ITrackingSpan target)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (view.IsClosed)
            {
                ActiveEditor.ShowMessage("That query window has been closed. Run Show Performance Hints again.", OLEMSGICON.OLEMSGICON_INFO);
                return;
            }
            var span = target.GetSpan(view.TextBuffer.CurrentSnapshot);
            view.Selection.Select(span, isReversed: false);
            view.Caret.MoveTo(span.Start);
            view.ViewScroller.EnsureSpanVisible(span, EnsureSpanVisibleOptions.AlwaysCenter);
            ActiveEditor.Focus(view, document);
        }
    }
}