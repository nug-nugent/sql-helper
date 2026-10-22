using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Utilities.UnifiedSettings;

namespace SqlHelper.VisualStudio
{
    /// <summary>
    /// The extension's settings. Where the host has Unified Settings (VS 2026, SSMS 22) they are read and written there,
    /// under the monikers in registration.json; otherwise (Visual Studio 2022, or Options in classic mode) through
    /// <see cref="GeneralOptionsPage"/>, whose registry values Unified Settings keeps in step with its own.
    /// </summary>
    internal sealed class Settings
    {
        const string InitialsMoniker = "sqlHelper.general.initials";

        readonly AsyncPackage _package;

        public Settings(AsyncPackage package)
        {
            _package = package;
        }

        public string Initials
        {
            get
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                string value = null;
                if (TryUnified(() => TryRead(InitialsMoniker, out value))) return value;
                var page = Page();
                // The page keeps what it read when first created; reload in case Options has changed it since.
                page.LoadSettingsFromStorage();
                return page.Initials;
            }
            set
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                if (TryUnified(() => TryWrite(InitialsMoniker, value, "Initials from the Windows account name"))) return;
                var page = Page();
                page.Initials = value;
                page.SaveSettingsToStorage();
            }
        }

        GeneralOptionsPage Page() => (GeneralOptionsPage)_package.GetDialogPage(typeof(GeneralOptionsPage));

        /// <summary>
        /// Runs an operation against Unified Settings, which earlier Visual Studio 2022 builds don't have at all (its
        /// types fail to load): true if it ran and succeeded.
        /// </summary>
        static bool TryUnified(Func<bool> operation)
        {
            try
            {
                return operation();
            }
            catch (TypeLoadException)
            {
                return false;
            }
        }

        /// <summary>The Unified Settings service; its type is in an assembly that isn't part of the SDK, so declared here.</summary>
        [Guid("e3684f31-344e-42ea-9047-b620fdc7ac25")]
        interface SVsUnifiedSettingsManager { }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static ISettingsManager Manager()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            return Package.GetGlobalService(typeof(SVsUnifiedSettingsManager)) as ISettingsManager;
        }

        // Classic mode (Unified Settings turned off) reports NotSupportedInClassicMode; the page is used then.
        [MethodImpl(MethodImplOptions.NoInlining)]
        static bool TryRead(string moniker, out string value)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var read = Manager()?.GetReader().GetValue<string>(moniker, SettingReadOptions.NoRequirements);
            value = read?.Outcome == SettingRetrievalOutcome.Success ? read.Value ?? "" : null;
            return value != null;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static bool TryWrite(string moniker, string value, string description)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var writer = Manager()?.GetWriter("SQL Helper");
            if (writer == null) return false;
            var change = writer.EnqueueChange(moniker, value).Outcome;
            if (change != SettingChangeOutcome.PendingCommit && change != SettingChangeOutcome.PendingCommitWithoutValidation) return false;
            var commit = writer.RequestCommit(description).Outcome;
            return commit == SettingCommitOutcome.Success || commit == SettingCommitOutcome.PendingApproval;
        }
    }
}