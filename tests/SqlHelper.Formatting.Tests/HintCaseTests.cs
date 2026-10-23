using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using SqlHelper.Formatting.Hints;
using Xunit;

namespace SqlHelper.Formatting.Tests
{
    /// <summary>
    /// The Performance Hints spec cases: checking HintCases/name.sql must give exactly the hints in
    /// HintCases/name.expected.txt, one per line as the command line prints them (after the file name), in script order.
    /// On a mismatch the actual hints are written to name.received.txt.
    /// </summary>
    public class HintCaseTests
    {
        const string ExpectedSuffix = ".expected.txt";
        const string ReceivedSuffix = ".received.txt";

        public static IEnumerable<object[]> Cases() =>
            Directory.GetFiles(CasesDirectory(), "*.sql")
                .Select(path => Path.GetFileNameWithoutExtension(path))
                .OrderBy(name => name, StringComparer.Ordinal)
                .Select(name => new object[] { name });

        [Theory]
        [MemberData(nameof(Cases))]
        public void GivesExpectedHints(string caseName)
        {
            string casePath = Path.Combine(CasesDirectory(), caseName);
            string receivedPath = casePath + ReceivedSuffix;

            var result = PerformanceHints.Check(Read(casePath + ".sql"));
            Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Errors));

            string actual = string.Join("\n", result.Hints);
            string expected = Read(casePath + ExpectedSuffix);
            if (actual == expected) File.Delete(receivedPath);
            else File.WriteAllText(receivedPath, actual);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ReadUncommittedIsolationIsFlaggedOnlyWhenAskedFor()
        {
            const string sql = "SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;\nSELECT 1;";

            Assert.Empty(PerformanceHints.Check(sql).Hints);

            var hints = PerformanceHints.Check(sql, new HintOptions { FlagReadUncommittedIsolation = true }).Hints;
            var hint = Assert.Single(hints);
            Assert.Equal("SH402", hint.Code);
            Assert.Equal(HintSeverity.Warning, hint.Severity);
            Assert.Equal((1, 1), (hint.Line, hint.Column));
        }

        [Fact]
        public void HintsKnowWhereTheirCodeIs()
        {
            const string sql = "SELECT a\nFROM dbo.T\nWHERE YEAR(d) = 2024;";

            var hint = Assert.Single(PerformanceHints.Check(sql).Hints);

            Assert.Equal("YEAR(d)", sql.Substring(hint.Offset, hint.Length));
        }

        [Theory]
        [InlineData("SELECT FROM WHERE", "Line 1")]
        public void RefusesWhatDoesNotParse(string sql, string reason)
        {
            var result = PerformanceHints.Check(sql);

            Assert.False(result.Succeeded);
            Assert.Empty(result.Hints);
            Assert.Contains(reason, string.Join(" ", result.Errors));
        }

        [Fact]
        public void EmptyInputHasNoHints()
        {
            Assert.True(PerformanceHints.Check("").Succeeded);
            Assert.Empty(PerformanceHints.Check("  \n").Hints);
        }

        // Git may check the cases out with CRLF line endings; compare as LF.
        static string Read(string path) => File.ReadAllText(path).Replace("\r\n", "\n");

        static string CasesDirectory([CallerFilePath] string thisFilePath = "") =>
            Path.Combine(Path.GetDirectoryName(thisFilePath), "HintCases");
    }
}