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
    [InstalledProductRegistration("SQL Helper", "T-SQL formatting in your house style.", "2.0.0")]
    [ProvideMenuResource("Menus.ctmenu", 1)]
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
            await FormatSqlCommand.InitializeAsync(this, commandService);
        }
    }
}
