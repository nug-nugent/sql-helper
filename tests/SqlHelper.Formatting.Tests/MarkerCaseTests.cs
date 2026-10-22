using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Xunit;

namespace SqlHelper.Formatting.Tests
{
    /// <summary>
    /// The server-side markers spec cases: every MarkerCases/name.sql must become MarkerCases/name.expected.sql, and the
    /// expected file must come through unchanged. On a mismatch the actual output is written to name.received.sql.
    /// </summary>
    public class MarkerCaseTests
    {
        const string ExpectedSuffix = ".expected.sql";
        const string ReceivedSuffix = ".received.sql";

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

            var result = ServerSideMarkers.Add(Read(casePath + ".sql"));
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

            var result = ServerSideMarkers.Add(expected);

            Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Errors));
            Assert.Equal(expected, result.Text);
        }

        [Theory]
        [InlineData("SELECT 1", "There's no procedure")]
        [InlineData("CREATE PROCEDURE dbo.p\nAS\nBEGIN\n\tSELECT 1\n\tRETURN\nEND", "last statement has to be the grid's SELECT")]
        [InlineData("CREATE PROCEDURE dbo.p\nAS\nBEGIN\n\tIF 1 = 1\n\t\tSELECT 1\nEND", "last statement has to be the grid's SELECT")]
        [InlineData("CREATE PROCEDURE dbo.p\nAS\nBEGIN\n\tSET NOCOUNT ON\nEND", "last statement has to be the grid's SELECT")]
        [InlineData("CREATE PROCEDURE dbo.p\nAS\nBEGIN\n\tSET NOCOUNT ON; SELECT 1\nEND", "has to start on a line of its own")]
        [InlineData("CREATE PROCEDURE dbo.p\nAS\nBEGIN\n\tSELECT 1 END", "has to end its line")]
        [InlineData("CREATE PROCEDURE dbo.p\nAS SET NOCOUNT ON\nSELECT 1", "on the line after its AS")]
        [InlineData("SELECT FROM WHERE", "Line 1")]
        public void RefusesAndLeavesTextAlone(string sql, string reason)
        {
            var result = ServerSideMarkers.Add(sql);

            Assert.False(result.Succeeded);
            Assert.Equal(sql, result.Text);
            Assert.Contains(reason, string.Join(" ", result.Errors));
        }

        // Git may check the cases out with CRLF line endings; compare as LF.
        static string Read(string path) => File.ReadAllText(path).Replace("\r\n", "\n");

        static string CasesDirectory([CallerFilePath] string thisFilePath = "") =>
            Path.Combine(Path.GetDirectoryName(thisFilePath), "MarkerCases");
    }
}