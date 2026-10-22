using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace SqlHelper.VisualStudio
{
    /// <summary>
    /// Tools > Options > SQL Helper > General, as a classic options page. In VS 2026 and SSMS 22 the same settings
    /// are shown by Unified Settings instead (see registration.json, which names this page's GUID); this page remains
    /// for Visual Studio 2022 and for classic-mode Options. Read and write the settings through <see cref="Settings"/>.
    /// </summary>
    [Guid("6f1c3e2a-9b4d-4a7e-8c55-2d0b7e9a41f3")]
    public sealed class GeneralOptionsPage : DialogPage
    {
        [Category("Comment header")]
        [DisplayName("Your initials")]
        [Description("Written on the history lines that Add Header adds, e.g. DH. Filled in from your Windows account name the first time, if it can be.")]
        public string Initials { get; set; } = "";
    }
}