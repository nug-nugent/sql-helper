using System;
using System.ComponentModel.Design;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using SqlHelper.Formatting;

namespace SqlHelper.VisualStudio
{
    /// <summary>
    /// SQL Helper > Add Server-Side Markers: adds the server-side grid markers missing from the procedure in the
    /// document, and takes the semicolon off its final SELECT, as a single undo step. Nothing else in the text changes.
    /// </summary>
    internal static class MarkersCommand
    {
        public static void Initialize(OleMenuCommandService commandService)
        {
            commandService.AddCommand(new MenuCommand(Execute, new CommandID(PackageIds.CommandSet, PackageIds.MarkersCommand)));
        }

        static void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var view = ActiveEditor.ViewOrExplain();
            if (view == null) return;

            string text = view.TextBuffer.CurrentSnapshot.GetText();
            var result = ServerSideMarkers.Add(text);
            if (!result.Succeeded)
            {
                ActiveEditor.ShowMessage(string.Join(Environment.NewLine, result.Errors), OLEMSGICON.OLEMSGICON_WARNING);
                return;
            }

            ActiveEditor.ReplaceChanged(view, 0, text, result.Text);
        }
    }
}