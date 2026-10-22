using System;
using Xunit;

namespace SqlHelper.Formatting.Tests
{
    public class HeaderTests
    {
        const string Procedure = "CREATE PROCEDURE dbo.P AS SELECT 1";

        static HeaderResult Apply(string sql, string note = "#12345: Example change") =>
            new CommentHeader(new HeaderOptions { Initials = "DH", Note = note, Today = new DateTime(2026, 10, 21), NewLine = "\n" }).Apply(sql);

        [Fact]
        public void NewHeaderPutsTheCaretOnTheEmptyDescriptionLine()
        {
            var result = Apply(Procedure);

            string beforeCaret = result.Text.Substring(0, result.CaretOffset);
            Assert.EndsWith("\tDescription\n\t\t", beforeCaret);
        }

        [Fact]
        public void UpdateWithNoNoteLeavesTheLineForTheUserToFinish()
        {
            string withHeader = Apply(Procedure).Text;

            var result = Apply(withHeader, note: "");

            Assert.EndsWith("\t\t21/10/2026 : DH - Created\n\t\t21/10/2026 : DH - ", result.Text.Substring(0, result.CaretOffset));
        }

        [Fact]
        public void ObjectNotAtTheStartOfItsLineGetsTheHeaderOnTheLineAbove()
        {
            var result = Apply("/* note */ " + Procedure);

            Assert.True(result.Succeeded);
            Assert.StartsWith("/* note */ \n/* ===", result.Text);
            Assert.EndsWith("=== */\n" + Procedure, result.Text);
        }

        [Fact]
        public void ScriptWithNoProcedureFunctionOrViewIsLeftAlone()
        {
            var result = Apply("SELECT 1");

            Assert.False(result.Succeeded);
            Assert.Equal("SELECT 1", result.Text);
            Assert.Equal(-1, result.CaretOffset);
        }

        [Fact]
        public void InvalidSqlIsLeftAloneWithTheParseError()
        {
            var result = Apply("CREATE PROCEDURE dbo.P AS SELECT (");

            Assert.False(result.Succeeded);
            Assert.Contains(result.Errors, e => e.StartsWith("Line 1"));
        }

        [Fact]
        public void MissingInitialsAreAnError()
        {
            var result = new CommentHeader(new HeaderOptions()).Apply(Procedure);

            Assert.False(result.Succeeded);
            Assert.Equal(Procedure, result.Text);
        }

        [Fact]
        public void SsmsHeaderWithAnUnknownLabelIsKeptAndANewHeaderAdded()
        {
            const string ssms = "-- ===\n-- Author:\t\tDavid Huggett\n-- Create date: 02/12/2013\n-- Modified by: TJ\n-- ===\n";

            var result = Apply(ssms + Procedure);

            Assert.StartsWith(ssms + "/* ===", result.Text);
        }

        [Fact]
        public void SsmsHeaderWithAuthorButNoDateIsKept()
        {
            const string ssms = "-- ===\n-- Author:\t\tDavid Huggett\n-- Description:\tThing\n-- ===\n";

            Assert.StartsWith(ssms + "/* ===", Apply(ssms + Procedure).Text);
        }

        const string Dashes = "----------------------------------------";

        [Fact]
        public void DashHistorySkipsEmptyLinesAndLeavesTheCaretOnTheAddedLine()
        {
            var result = Apply($"{Dashes}\n-- AB - 01/06/2023 - First\n--\n-- CD-28/06/2024-Second\n{Dashes}\n" + Procedure);

            Assert.Contains("\t\t01/06/2023 : AB - First\n\t\t28/06/2024 : CD - Second\n\t\t21/10/2026 : DH - #12345: Example change\n", result.Text);
            Assert.EndsWith("21/10/2026 : DH - #12345: Example change", result.Text.Substring(0, result.CaretOffset));
        }

        [Theory]
        [InlineData("-- David Huggett - 01/06/2023 - Full name instead of initials\n")]
        [InlineData("-- AB - 1/6/23 - Short date\n")]
        [InlineData("-- AB - 01/06/2023 -\n")]
        [InlineData("-- Fixed the paging\n")]
        public void DashBlockWithAnyOtherLineIsKeptAndANewHeaderAdded(string line)
        {
            string block = $"{Dashes}\n-- AB - 01/06/2023 - First\n{line}{Dashes}\n";

            Assert.StartsWith(block + "/* ===", Apply(block + Procedure).Text);
        }

        [Fact]
        public void DashLinesWithNoClosingRuleAreKept()
        {
            string block = $"{Dashes}\n-- AB - 01/06/2023 - First\n";

            Assert.StartsWith(block + "/* ===", Apply(block + Procedure).Text);
        }

        [Fact]
        public void CrLfScriptsGetCrLfHeaders()
        {
            var result = new CommentHeader(new HeaderOptions { Initials = "DH" }).Apply("SELECT 1\r\nGO\r\n" + Procedure);

            Assert.DoesNotMatch("[^\r]\n", result.Text);
        }

        [Theory]
        [InlineData("David Huggett", "DH")]
        [InlineData("  Mary  Ann Smith ", "MAS")]
        [InlineData("dhuggett", null)]
        [InlineData("J. Smith", null)]
        [InlineData("Smith-Jones Tom", null)]
        public void InitialsComeOnlyFromPlainNames(string name, string initials)
        {
            Assert.Equal(initials, CommentHeader.InitialsOf(name));
        }
    }
}