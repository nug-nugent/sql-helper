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

        /// <summary>
        /// Replaces <paramref name="text"/>, found at <paramref name="start"/> in the view's buffer, with
        /// <paramref name="newText"/> as a single undo step, touching only the part that differs so the rest of the
        /// document (and its scroll position) is undisturbed.
        /// </summary>
        public static void ReplaceChanged(ITextView view, int start, string text, string newText)
        {
            int prefix = 0;
            while (prefix < text.Length && prefix < newText.Length && text[prefix] == newText[prefix]) prefix++;
            int suffix = 0;
            while (suffix < text.Length - prefix && suffix < newText.Length - prefix
                && text[text.Length - 1 - suffix] == newText[newText.Length - 1 - suffix]) suffix++;
            if (prefix == text.Length && prefix == newText.Length) return;

            using (var edit = view.TextBuffer.CreateEdit())
            {
                edit.Replace(start + prefix, text.Length - prefix - suffix, newText.Substring(prefix, newText.Length - prefix - suffix));
                edit.Apply();
            }
        }

        /// <summary>Brings the view's document window to the front (when its path finds it) and gives the view focus.</summary>
        public static void Focus(ITextView view, string documentPath)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (!string.IsNullOrEmpty(documentPath)
                && VsShellUtilities.IsDocumentOpen(_package, documentPath, Guid.Empty, out _, out _, out IVsWindowFrame frame))
                frame.Show();
            _editorAdapters.GetViewAdapter(view)?.SendExplicitFocus();
        }

        public static void ShowMessage(string message, OLEMSGICON icon)
        {
            VsShellUtilities.ShowMessageBox(_package, message, Title, icon, OLEMSGBUTTON.OLEMSGBUTTON_OK, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
        }
    }
}