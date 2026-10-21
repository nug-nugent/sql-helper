using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Xunit;

namespace SqlHelper.Formatting.Tests
{
    /// <summary>
    /// The spec cases: every Cases/name.sql must format to exactly Cases/name.expected.sql (line-ending style aside).
    /// On a mismatch the actual output is written to name.received.sql so the two can be diffed.
    /// </summary>
    public class SpecCaseTests
    {
        const string ExpectedSuffix = ".expected.sql";
        const string ReceivedSuffix = ".received.sql";

        static readonly SqlFormatter Formatter = new SqlFormatter(new FormattingOptions { NewLine = "\n" });

        public static IEnumerable<object[]> Cases() =>
            Directory.GetFiles(CasesDirectory(), "*.sql")
                .Where(path => !path.EndsWith(ExpectedSuffix, StringComparison.OrdinalIgnoreCase)
                    && !path.EndsWith(ReceivedSuffix, StringComparison.OrdinalIgnoreCase))
                .Select(path => Path.GetFileNameWithoutExtension(path))
                .OrderBy(name => name, StringComparer.Ordinal)
                .Select(name => new object[] { name });

        [Theory]
        [MemberData(nameof(Cases))]
        public void FormatsToExpected(string caseName)
        {
            string casePath = Path.Combine(CasesDirectory(), caseName);
            string receivedPath = casePath + ReceivedSuffix;

            var result = Formatter.Format(Read(casePath + ".sql"));
            Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Errors));

            string expected = Read(casePath + ExpectedSuffix);
            if (result.Text == expected) File.Delete(receivedPath);
            else File.WriteAllText(receivedPath, result.Text);

            Assert.Equal(expected, result.Text);
        }

        [Theory]
        [MemberData(nameof(Cases))]
        public void ExpectedOutputIsAlreadyFormatted(string caseName)
        {
            string expected = Read(Path.Combine(CasesDirectory(), caseName) + ExpectedSuffix);

            var result = Formatter.Format(expected);

            Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Errors));
            Assert.Equal(expected, result.Text);
        }

        // Git may check the cases out with CRLF line endings; the formatter under test writes LF.
        static string Read(string path) => File.ReadAllText(path).Replace("\r\n", "\n");

        // The source folder rather than the build output, so received files land next to the cases.
        static string CasesDirectory([CallerFilePath] string thisFilePath = "") =>
            Path.Combine(Path.GetDirectoryName(thisFilePath), "Cases");
    }
}
