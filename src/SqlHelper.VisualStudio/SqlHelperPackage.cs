using System;
using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace SqlHelper.VisualStudio
{
    /// <summary>
    /// The SQL Helper package for Visual Studio and SQL Server Management Studio (both run on the same shell). It loads
    /// the first time one of its commands is used.
    /// </summary>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("SQL Helper", "Formats and cleans up T-SQL, adds comment headers and semicolons, and converts to and from dynamic SQL.", BuildVersion.Value)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    // The classic options page, for Visual Studio 2022 and classic-mode Options. Where Unified Settings is on
    // (VS 2026, SSMS 22) it is hidden and registration.json's settings are shown instead.
    [ProvideOptionPage(typeof(GeneralOptionsPage), "SQL Helper", "General", 0, 0, true, IsInUnifiedSettings = true)]
    [ProvideSettingsManifest]
    // Lets the host load our own copy of ScriptDom when it hasn't already loaded one of its own.
    [ProvideBindingPath]
    [Guid(PackageIds.PackageGuidString)]
    public sealed class SqlHelperPackage : AsyncPackage
    {
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            var commandService = (OleMenuCommandService)await GetServiceAsync(typeof(IMenuCommandService));
            Assumes.Present(commandService);
            await ActiveEditor.InitializeAsync(this);
            FormatSqlCommand.Initialize(commandService);
            CleanAndFormatCommand.Initialize(commandService);
            CleanupCommand.Initialize(commandService);
            HeaderCommand.Initialize(this, commandService);
            SemicolonsCommand.Initialize(commandService);
            MarkersCommand.Initialize(commandService);
            DynamicSqlCommands.Initialize(commandService);
        }
    }
}
