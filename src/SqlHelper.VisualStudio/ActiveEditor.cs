using System;
using Microsoft;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Task = System.Threading.Tasks.Task;

namespace SqlHelper.VisualStudio
{
    /// <summary>What the commands share: the editor the user is in, its line breaks, and message boxes.</summary>
    internal static class ActiveEditor
    {
        const string Title = "SQL Helper";

        static AsyncPackage _package;
        static IVsTextManager _textManager;
        static IVsEditorAdaptersFactoryService _editorAdapters;

        public static async Task InitializeAsync(AsyncPackage package)
        {
            _package = package;
            _textManager = (IVsTextManager)await package.GetServiceAsync(typeof(SVsTextManager));
            var componentModel = (IComponentModel)await package.GetServiceAsync(typeof(SComponentModel));
            Assumes.Present(_textManager);
            Assumes.Present(componentModel);
            _editorAdapters = componentModel.GetService<IVsEditorAdaptersFactoryService>();
        }

        /// <summary>The active text view, or null (with a message shown) if there isn't one.</summary>
        public static IWpfTextView ViewOrExplain()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _textManager.GetActiveView(1, null, out IVsTextView activeView);
            var view = activeView == null ? null : _editorAdapters.GetWpfTextView(activeView);
            if (view == null) ShowMessage("Open a query window or .sql file first.", OLEMSGICON.OLEMSGICON_INFO);
            return view;
        }

        /// <summary>The document's own line break, so edits don't mix CRLF and LF.</summary>
        public static string LineBreakUsedIn(ITextSnapshot snapshot)
        {
            foreach (var line in snapshot.Lines)
                if (line.LineBreakLength > 0) return line.GetLineBreakText();
            return Environment.NewLine;
        }

        public static void ShowMessage(string message, OLEMSGICON icon)
        {
            VsShellUtilities.ShowMessageBox(_package, message, Title, icon, OLEMSGBUTTON.OLEMSGBUTTON_OK, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
        }
    }
}