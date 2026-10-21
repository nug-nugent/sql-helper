using System;
using System.ComponentModel.Design;
using System.DirectoryServices.AccountManagement;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using SqlHelper.Formatting;

namespace SqlHelper.VisualStudio
{
    /// <summary>
    /// SQL Helper > Add Header: adds the house comment header to the procedure, function or view in the document, or a
    /// dated history line to the header it already has (converting an SSMS template header), as a single undo step. The
    /// cursor is left where the user types next: after the dash of the new history line, or on an empty description.
    /// </summary>
    internal static class HeaderCommand
    {
        static AsyncPackage _package;

        public static void Initialize(AsyncPackage package, OleMenuCommandService commandService)
        {
            _package = package;
            commandService.AddCommand(new MenuCommand(Execute, new CommandID(PackageIds.CommandSet, PackageIds.HeaderCommand)));
        }

        static void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var view = ActiveEditor.ViewOrExplain();
            if (view == null) return;

            string initials = Initials();
            if (initials == null)
            {
                ActiveEditor.ShowMessage("Set your initials first, under Tools > Options > SQL Helper > General.", OLEMSGICON.OLEMSGICON_INFO);
                return;
            }

            var snapshot = view.TextBuffer.CurrentSnapshot;
            string text = snapshot.GetText();
            var options = new HeaderOptions { Initials = initials, NewLine = ActiveEditor.LineBreakUsedIn(snapshot) };
            var result = new CommentHeader(options).Apply(text);
            if (!result.Succeeded)
            {
                ActiveEditor.ShowMessage(string.Join(Environment.NewLine, result.Errors), OLEMSGICON.OLEMSGICON_WARNING);
                return;
            }

            // Replace only the part that changed, so the rest of the document (and its scroll position) is undisturbed.
            int prefix = 0;
            while (prefix < text.Length && prefix < result.Text.Length && text[prefix] == result.Text[prefix]) prefix++;
            int suffix = 0;
            while (suffix < text.Length - prefix && suffix < result.Text.Length - prefix
                && text[text.Length - 1 - suffix] == result.Text[result.Text.Length - 1 - suffix]) suffix++;

            using (var edit = view.TextBuffer.CreateEdit())
            {
                edit.Replace(prefix, text.Length - prefix - suffix, result.Text.Substring(prefix, result.Text.Length - prefix - suffix));
                edit.Apply();
            }

            var caret = new SnapshotPoint(view.TextBuffer.CurrentSnapshot, result.CaretOffset);
            view.Selection.Clear();
            view.Caret.MoveTo(caret);
            view.ViewScroller.EnsureSpanVisible(new SnapshotSpan(caret, 0), EnsureSpanVisibleOptions.None);
        }

        /// <summary>
        /// The initials setting. When it's empty, the first time through it is filled in from the Windows account's
        /// display name (if that is a plain "First Last" name) and saved, so it shows in Tools > Options to change.
        /// </summary>
        static string Initials()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var page = (GeneralOptionsPage)_package.GetDialogPage(typeof(GeneralOptionsPage));
            if (!string.IsNullOrWhiteSpace(page.Initials)) return page.Initials.Trim();

            string derived = CommentHeader.InitialsOf(WindowsDisplayName());
            if (derived == null) return null;
            page.Initials = derived;
            page.SaveSettingsToStorage();
            return derived;
        }

        static string WindowsDisplayName()
        {
            try
            {
                return UserPrincipal.Current.DisplayName;
            }
            catch (Exception)
            {
                // No domain controller reachable, or an account type it can't read; the user sets initials by hand.
                return null;
            }
        }
    }
}