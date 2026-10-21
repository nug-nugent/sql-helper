using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Xunit;

namespace SqlHelper.Formatting.Tests
{
    /// <summary>
    /// The comment header spec cases: every HeaderCases/name.sql must become HeaderCases/name.expected.sql, run as DH on
    /// 21/10/2026 with the note below (see HeaderCases/README.md). On a mismatch the actual output is written to
    /// name.received.sql.
    /// </summary>
    public class HeaderCaseTests
    {
        const string ExpectedSuffix = ".expected.sql";
        const string ReceivedSuffix = ".received.sql";

        internal static readonly HeaderOptions Options = new HeaderOptions
        {
            Initials = "DH",
            Note = "#12345: Example change",
            Today = new DateTime(2026, 10, 21),
            NewLine = "\n",
        };

        public static IEnumerable<object[]> Cases() =>
            Directory.GetFiles(CasesDirectory(), "*.sql")
                .Where(path => !path.EndsWith(ExpectedSuffix, StringComparison.OrdinalIgnoreCase)
                    && !path.EndsWith(ReceivedSuffix, StringComparison.OrdinalIgnoreCase))
                .Select(path => Path.GetFileNameWithoutExtension(path))
                .OrderBy(name => name, StringComparer.Ordinal)
                .Select(name => new object[] { name });

        [Theory]
        [MemberData(nameof(Cases))]
        public void BecomesExpected(string caseName)
        {
            string casePath = Path.Combine(CasesDirectory(), caseName);
            string receivedPath = casePath + ReceivedSuffix;

            var result = new CommentHeader(Options).Apply(Read(casePath + ".sql"));
            Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Errors));

            string expected = Read(casePath + ExpectedSuffix);
            if (result.Text == expected) File.Delete(receivedPath);
            else File.WriteAllText(receivedPath, result.Text);

            Assert.Equal(expected, result.Text);
        }

        // Git may check the cases out with CRLF line endings; the command under test writes LF.
        static string Read(string path) => File.ReadAllText(path).Replace("\r\n", "\n");

        static string CasesDirectory([CallerFilePath] string thisFilePath = "") =>
            Path.Combine(Path.GetDirectoryName(thisFilePath), "HeaderCases");
    }
}