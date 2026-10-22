using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SqlHelper.Formatting.Headers
{
    /// <summary>
    /// Reading and writing header comments: the house header, and the one SSMS's Create Procedure template writes.
    /// </summary>
    internal static class HeaderText
    {
        // Both rule lines are 120 characters wide.
        const int RuleLength = 117;
        public static readonly string TopRule = "/* " + new string('=', RuleLength);
        public static readonly string BottomRule = new string('=', RuleLength) + " */";
        public const string DescriptionHeading = "\tDescription";
        public const string HistoryHeading = "\tUpdate History";
        public const string BodyIndent = "\t\t";

        static readonly Regex HouseTopRule = new Regex(@"^/\*\s*=+\s*$");
        static readonly Regex HouseBottomRule = new Regex(@"^\s*=+\s*\*/$");

        static readonly Regex SsmsRule = new Regex(@"^--\s*=+\s*$");
        static readonly Regex SsmsField = new Regex(@"^--\s*(Author|Create\s*date|Description)\s*:\s*(.*?)\s*$", RegexOptions.IgnoreCase);
        static readonly Regex OtherLabel = new Regex(@"^--\s*[A-Za-z][A-Za-z ]*:(\s|$)");
        // An unfilled template parameter, e.g. <Author,,Name>
        static readonly Regex Placeholder = new Regex(@"^<[^>]*,[^>]*,[^>]*>$");
        static readonly Regex PersonName = new Regex(@"^[A-Za-z]+(\s+[A-Za-z]+)+$");

        static readonly Regex DashRule = new Regex(@"^-{10,}\s*$");
        // -- SR - 01/06/2023 - PBI 13217 - EMR Patient Summary
        static readonly Regex DashHistoryLine = new Regex(@"^--\s*([A-Za-z]{1,4})\s*-\s*(\d{2}/\d{2}/\d{4})\s*-\s*(.*?)\s*$");

        /// <summary>A <c>/* ===</c> … <c>=== */</c> block comment with an Update History section.</summary>
        public static bool IsHouseHeader(string comment)
        {
            var lines = Lines(comment);
            return lines.Count >= 3
                && HouseTopRule.IsMatch(lines[0])
                && HouseBottomRule.IsMatch(lines[lines.Count - 1])
                && lines.Any(l => l.Trim().Equals("Update History", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>A <c>-- ====</c> line, which opens and closes the SSMS template header.</summary>
        public static bool IsSsmsRule(string comment) => SsmsRule.IsMatch(comment.TrimEnd('\r', '\n'));

        /// <summary>
        /// Reads the lines between an SSMS header's rules, or returns null if they contain anything other than
        /// <c>Author</c>, <c>Create date</c> and <c>Description</c> (the last may run on over several lines). Unfilled
        /// template parameters count as empty. Author and date must be both filled or both empty, so a history line can
        /// be made from them without losing either.
        /// </summary>
        public static SsmsHeader ParseSsms(IEnumerable<string> comments)
        {
            string author = "", created = "", field = null;
            var description = new List<string>();
            var seen = new HashSet<string>();

            foreach (string comment in comments)
            {
                string line = comment.TrimEnd('\r', '\n');
                var match = SsmsField.Match(line);
                if (match.Success)
                {
                    field = Regex.Replace(match.Groups[1].Value.ToLowerInvariant(), @"\s+", " ");
                    if (!seen.Add(field)) return null;
                    string value = Placeholder.IsMatch(match.Groups[2].Value) ? "" : match.Groups[2].Value;
                    if (field == "author") author = value;
                    else if (field == "create date") created = value;
                    else if (value.Length > 0) description.Add(value);
                    continue;
                }
                if (OtherLabel.IsMatch(line)) return null;

                string text = line.Substring(2).Trim();
                if (text.Length == 0) continue;
                if (field != "description") return null;
                description.Add(text);
            }

            if ((author.Length == 0) != (created.Length == 0)) return null;
            return new SsmsHeader(author, created, description);
        }

        /// <summary>A line of ten or more dashes, which opens and closes a dash history block.</summary>
        public static bool IsDashRule(string comment) => DashRule.IsMatch(comment.TrimEnd('\r', '\n'));

        /// <summary>
        /// Reads the lines between a dash history block's rules (<c>-- SR - 01/06/2023 - what changed</c>) as house
        /// history lines (<c>01/06/2023 : SR - what changed</c>), or returns null if any line is anything else or
        /// there are none. Lines that are just <c>--</c> are skipped.
        /// </summary>
        public static List<string> ParseDashHistory(IEnumerable<string> comments)
        {
            var history = new List<string>();
            foreach (string comment in comments)
            {
                string line = comment.TrimEnd('\r', '\n');
                if (line.Substring(2).Trim().Length == 0) continue;
                var match = DashHistoryLine.Match(line);
                if (!match.Success || match.Groups[3].Value.Length == 0) return null;
                history.Add($"{match.Groups[2].Value} : {match.Groups[1].Value} - {match.Groups[3].Value}");
            }
            return history.Count > 0 ? history : null;
        }

        /// <summary>Initials of a plain "First Last" name, e.g. DH for David Huggett; null for anything else.</summary>
        public static string InitialsOf(string name)
        {
            name = name?.Trim() ?? "";
            if (!PersonName.IsMatch(name)) return null;
            return string.Concat(Regex.Split(name, @"\s+").Select(word => char.ToUpperInvariant(word[0])));
        }

        /// <summary>A whole house header. An empty description gets one indented line for the user to fill in.</summary>
        public static string Build(IList<string> description, IList<string> history, string newLine)
        {
            var text = new StringBuilder();
            text.Append(TopRule).Append(newLine).Append(DescriptionHeading).Append(newLine);
            if (description.Count == 0) text.Append(BodyIndent).Append(newLine);
            foreach (string line in description) text.Append(BodyIndent).Append(line).Append(newLine);
            text.Append(newLine).Append(HistoryHeading).Append(newLine);
            foreach (string line in history) text.Append(BodyIndent).Append(line).Append(newLine);
            text.Append(BottomRule);
            return text.ToString();
        }

        /// <summary>Offset of the end of the empty description line in a header from <see cref="Build"/>.</summary>
        public static int EmptyDescriptionEnd(string newLine) =>
            TopRule.Length + newLine.Length + DescriptionHeading.Length + newLine.Length + BodyIndent.Length;

        static List<string> Lines(string text) => text.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
    }

    internal sealed class SsmsHeader
    {
        public SsmsHeader(string author, string created, IReadOnlyList<string> description)
        {
            Author = author;
            Created = created;
            Description = description;
        }

        public string Author { get; }
        public string Created { get; }
        public IReadOnlyList<string> Description { get; }
        public bool HasHistory => Author.Length > 0;
    }
}