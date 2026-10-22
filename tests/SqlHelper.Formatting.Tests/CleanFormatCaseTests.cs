using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Xunit;

namespace SqlHelper.Formatting.Tests
{
    /// <summary>
    /// The clean-and-format spec cases: every CleanFormatCases/name.sql must become CleanFormatCases/name.expected.sql,
    /// run with the header cases' options, and the expected file must come through unchanged. On a mismatch the actual
    /// output is written to name.received.sql.
    /// </summary>
    public class CleanFormatCaseTests
    {
        const string ExpectedSuffix = ".expected.sql";
        const string ReceivedSuffix = ".received.sql";

        static readonly FormattingOptions Formatting = new FormattingOptions { NewLine = "\n" };

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

            var result = CleanAndFormat.Apply(Read(casePath + ".sql"), Formatting, HeaderCaseTests.Options);
            Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Errors));

            string expected = Read(casePath + ExpectedSuffix);
            if (result.Text == expected) File.Delete(receivedPath);
            else File.WriteAllText(receivedPath, result.Text);

            Assert.Equal(expected, result.Text);
        }

        [Theory]
        [MemberData(nameof(Cases))]
        public void ExpectedIsUnchanged(string caseName)
        {
            string expected = Read(Path.Combine(CasesDirectory(), caseName + ExpectedSuffix));

            var result = CleanAndFormat.Apply(expected, Formatting, HeaderCaseTests.Options);

            Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Errors));
            Assert.Equal(expected, result.Text);
            Assert.Equal(-1, result.CaretOffset);
        }

        [Fact]
        public void CaretIsLeftOnTheAddedHistoryLine()
        {
            var result = CleanAndFormat.Apply(Read(Path.Combine(CasesDirectory(), "convert-ssms-header.sql")), Formatting, HeaderCaseTests.Options);

            Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Errors));
            Assert.EndsWith("21/10/2026 : DH - #12345: Example change", result.Text.Substring(0, result.CaretOffset));
        }

        [Fact]
        public void WithoutHeaderOptionsTheHeaderIsLeftAlone()
        {
            string sql = Read(Path.Combine(CasesDirectory(), "convert-ssms-header.sql"));

            var result = CleanAndFormat.Apply(sql, Formatting, null);

            Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Errors));
            Assert.StartsWith("-- =============================================\n-- Author:", result.Text);
            Assert.Equal(-1, result.CaretOffset);
        }

        [Fact]
        public void InitialsAreNeededOnlyToConvertAHeader()
        {
            var noInitials = new HeaderOptions { Today = HeaderCaseTests.Options.Today, NewLine = "\n" };

            var plain = CleanAndFormat.Apply(Read(Path.Combine(CasesDirectory(), "no-header.sql")), Formatting, noInitials);
            Assert.True(plain.Succeeded, string.Join(Environment.NewLine, plain.Errors));

            string ssms = Read(Path.Combine(CasesDirectory(), "convert-ssms-header.sql"));
            var converted = CleanAndFormat.Apply(ssms, Formatting, noInitials);
            Assert.False(converted.Succeeded);
            Assert.Equal(ssms, converted.Text);
        }

        [Fact]
        public void UnparseableSqlIsLeftAlone()
        {
            var result = CleanAndFormat.Apply("SELECT FROM WHERE", Formatting, HeaderCaseTests.Options);

            Assert.False(result.Succeeded);
            Assert.Equal("SELECT FROM WHERE", result.Text);
        }

        // Git may check the cases out with CRLF line endings; the command under test writes LF.
        static string Read(string path) => File.ReadAllText(path).Replace("\r\n", "\n");

        static string CasesDirectory([CallerFilePath] string thisFilePath = "") =>
            Path.Combine(Path.GetDirectoryName(thisFilePath), "CleanFormatCases");
    }
}