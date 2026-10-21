using Xunit;

namespace SqlHelper.Formatting.Tests
{
    public class SafetyTests
    {
        readonly SqlFormatter _formatter = new SqlFormatter(new FormattingOptions { NewLine = "\n" });

        [Fact]
        public void InvalidSqlIsReturnedUnchangedWithTheParseError()
        {
            const string sql = "select (a from";

            var result = _formatter.Format(sql);

            Assert.False(result.Succeeded);
            Assert.Equal(sql, result.Text);
            Assert.Contains(result.Errors, e => e.StartsWith("Line 1"));
        }

        [Fact]
        public void UnhandledStatementsKeepTheirOwnLayoutApartFromKeywordCase()
        {
            var result = _formatter.Format("alter database current\n    set recovery simple");

            Assert.True(result.Succeeded);
            Assert.Equal("ALTER DATABASE CURRENT\n    SET RECOVERY SIMPLE", result.Text);
        }

        [Fact]
        public void EmptyInputIsLeftAlone()
        {
            Assert.Equal("", _formatter.Format("").Text);
            Assert.Equal("  \n", _formatter.Format("  \n").Text);
        }
    }
}
