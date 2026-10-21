using System.ComponentModel;
using Microsoft.VisualStudio.Shell;

namespace SqlHelper.VisualStudio
{
    /// <summary>Tools > Options > SQL Helper > General.</summary>
    public sealed class GeneralOptionsPage : DialogPage
    {
        [Category("Comment header")]
        [DisplayName("Your initials")]
        [Description("Written on the history lines that Add Header adds, e.g. DH. Filled in from your Windows account name the first time, if it can be.")]
        public string Initials { get; set; } = "";
    }
}