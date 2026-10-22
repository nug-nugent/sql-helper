using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Xunit;

namespace SqlHelper.Formatting.Tests
{
    /// <summary>
    /// The dynamic SQL spec cases: every ToDynamicCases/name.sql must become ToDynamicCases/name.expected.sql under
    /// Convert to Dynamic SQL, and likewise FromDynamicCases under Convert from Dynamic SQL. The text between
    /// <c>/*[*/</c> and <c>/*]*/</c> is the selection; a case without them converts the whole document. On a mismatch
    /// the actual output is written to name.received.sql.
    /// </summary>
    public class DynamicSqlCaseTests
    {
        const string ExpectedSuffix = ".expected.sql";
        const string ReceivedSuffix = ".received.sql";
        const string SelectionStart = "/*[*/";
        const string SelectionEnd = "/*]*/";

        public static IEnumerable<object[]> ToCases() => Cases("ToDynamicCases");

        public static IEnumerable<object[]> FromCases() => Cases("FromDynamicCases");

        [Theory]
        [MemberData(nameof(ToCases))]
        public void ToDynamicBecomesExpected(string caseName) => BecomesExpected("ToDynamicCases", caseName, DynamicSql.ToDynamic);

        [Theory]
        [MemberData(nameof(FromCases))]
        public void FromDynamicBecomesExpected(string caseName) => BecomesExpected("FromDynamicCases", caseName, DynamicSql.FromDynamic);

        [Theory]
        [MemberData(nameof(ToCases))]
        public void ToDynamicKeepsCrlf(string caseName) => KeepsCrlf("ToDynamicCases", caseName, DynamicSql.ToDynamic);

        [Theory]
        [MemberData(nameof(FromCases))]
        public void FromDynamicKeepsCrlf(string caseName) => KeepsCrlf("FromDynamicCases", caseName, DynamicSql.FromDynamic);

        [Theory]
        [InlineData("SELECT * FROM dbo.Orders WHERE OrderId = @Id", "Can't find where @Id is declared")]
        [InlineData("DECLARE @t TABLE (Id INT)\n\n/*[*/SELECT Id FROM @t/*]*/", "@t is a table variable")]
        [InlineData("/*[*/SELECT 1\nGO\nSELECT 2/*]*/", "more than one batch")]
        [InlineData("SELECT /*[*/OrderId/*]*/ FROM dbo.Orders", "Select whole statements")]
        [InlineData("SELECT FROM WHERE", "Line 1")]
        public void ToDynamicRefuses(string sql, string reason) => Refuses(sql, reason, DynamicSql.ToDynamic);

        [Theory]
        [InlineData("EXEC sys.sp_executesql N'SELECT TOP (@n) * FROM dbo.Orders', N'@n INT', @n = 10", "passed a value rather than a variable")]
        [InlineData("EXEC sys.sp_executesql N'SELECT * FROM dbo.Orders WHERE OrderId = @Id', N'@Id INT'", "@Id isn't passed")]
        [InlineData("DECLARE @Sql NVARCHAR(MAX) = N'SELECT 1'\n\nPRINT @Sql\n\nEXEC (@Sql)", "@Sql is also used on line 3")]
        [InlineData("DECLARE @x INT = 1\n\nEXEC (N'DECLARE @x INT = 2')", "@x is declared both")]
        [InlineData("DECLARE @Sql NVARCHAR(MAX) = N'SELECT 1'\n\nEXEC @Sql", "There's no sp_executesql or EXEC (…) call")]
        [InlineData("DECLARE @t SYSNAME = OBJECT_NAME(1)\n\nEXEC (N'SELECT * FROM ' + @t)", "builds its SQL from more than string literals")]
        [InlineData("IF 1 = 1 EXEC (N'SELECT 1')", "whole body of an IF")]
        public void FromDynamicRefuses(string sql, string reason) => Refuses(sql, reason, DynamicSql.FromDynamic);

        static void BecomesExpected(string directory, string caseName, Func<string, int, int, FormatResult> convert)
        {
            string casePath = Path.Combine(CasesDirectory(directory), caseName);
            string receivedPath = casePath + ReceivedSuffix;

            var (sql, start, length) = WithSelection(Read(casePath + ".sql"));
            var result = convert(sql, start, length);
            Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Errors));

            // A one-line document has no line break of its own to copy, so new lines are Environment.NewLine.
            string text = sql.Contains('\n') ? result.Text : result.Text.Replace("\r\n", "\n");
            string expected = Read(casePath + ExpectedSuffix);
            if (text == expected) File.Delete(receivedPath);
            else File.WriteAllText(receivedPath, text);

            Assert.Equal(expected, text);
        }

        /// <summary>SSMS documents use CRLF: the output must use it throughout, inside the string too.</summary>
        static void KeepsCrlf(string directory, string caseName, Func<string, int, int, FormatResult> convert)
        {
            string casePath = Path.Combine(CasesDirectory(directory), caseName);
            var (sql, start, length) = WithSelection(Read(casePath + ".sql").Replace("\n", "\r\n"));
            if (!sql.Contains('\n')) return;

            var result = convert(sql, start, length);

            Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Errors));
            Assert.Equal(Read(casePath + ExpectedSuffix).Replace("\n", "\r\n"), result.Text);
        }

        static void Refuses(string sql, string reason, Func<string, int, int, FormatResult> convert)
        {
            var (text, start, length) = WithSelection(sql);
            var result = convert(text, start, length);

            Assert.False(result.Succeeded);
            Assert.Equal(text, result.Text);
            Assert.Contains(reason, string.Join(" ", result.Errors));
        }

        /// <summary>The text without its selection markers, and where they were.</summary>
        static (string Sql, int Start, int Length) WithSelection(string sql)
        {
            int start = sql.IndexOf(SelectionStart, StringComparison.Ordinal);
            if (start < 0) return (sql, 0, 0);
            sql = sql.Remove(start, SelectionStart.Length);
            int end = sql.IndexOf(SelectionEnd, StringComparison.Ordinal);
            return (sql.Remove(end, SelectionEnd.Length), start, end - start);
        }

        static IEnumerable<object[]> Cases(string directory) =>
            Directory.GetFiles(CasesDirectory(directory), "*.sql")
                .Where(path => !path.EndsWith(ExpectedSuffix, StringComparison.OrdinalIgnoreCase)
                    && !path.EndsWith(ReceivedSuffix, StringComparison.OrdinalIgnoreCase))
                .Select(path => Path.GetFileNameWithoutExtension(path))
                .OrderBy(name => name, StringComparer.Ordinal)
                .Select(name => new object[] { name });

        // Git may check the cases out with CRLF line endings; compare as LF.
        static string Read(string path) => File.ReadAllText(path).Replace("\r\n", "\n");

        static string CasesDirectory(string directory, [CallerFilePath] string thisFilePath = "") =>
            Path.Combine(Path.GetDirectoryName(thisFilePath), directory);
    }
}