using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Xunit;

namespace SqlHelper.Formatting.Tests
{
    /// <summary>
    /// The add-semicolons spec cases: every SemicolonCases/name.sql must become SemicolonCases/name.expected.sql, and the
    /// expected file must come through unchanged. On a mismatch the actual output is written to name.received.sql.
    /// </summary>
    public class SemicolonCaseTests
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

            var result = Semicolons.Add(Read(casePath + ".sql"));
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

            var result = Semicolons.Add(expected);

            Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Errors));
            Assert.Equal(expected, result.Text);
        }

        [Fact]
        public void UnparseableSqlIsLeftAlone()
        {
            var result = Semicolons.Add("SELECT FROM WHERE");

            Assert.False(result.Succeeded);
            Assert.Equal("SELECT FROM WHERE", result.Text);
        }

        // Git may check the cases out with CRLF line endings; compare as LF.
        static string Read(string path) => File.ReadAllText(path).Replace("\r\n", "\n");

        static string CasesDirectory([CallerFilePath] string thisFilePath = "") =>
            Path.Combine(Path.GetDirectoryName(thisFilePath), "SemicolonCases");
    }
}