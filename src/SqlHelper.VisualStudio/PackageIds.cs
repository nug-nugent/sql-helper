using System;

namespace SqlHelper.VisualStudio
{
    /// <summary>GUIDs and command IDs; keep in step with the Symbols section of SqlHelperPackage.vsct.</summary>
    internal static class PackageIds
    {
        public const string PackageGuidString = "d476471b-7c48-48a7-8be0-451c4290ae08";

        public static readonly Guid CommandSet = new Guid("2de80807-30c5-4fd7-81ef-a3cd5102261d");

        public const int FormatSqlCommand = 0x0100;
        public const int HeaderCommand = 0x0101;
        public const int SemicolonsCommand = 0x0102;
        public const int MarkersCommand = 0x0103;
        public const int CleanupCommand = 0x0104;
        public const int ToDynamicCommand = 0x0105;
        public const int FromDynamicCommand = 0x0106;
        public const int CleanAndFormatCommand = 0x0107;
    }
}
