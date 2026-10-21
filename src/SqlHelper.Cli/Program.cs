using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using SqlHelper.Formatting;

namespace SqlHelper.Cli
{
    public static class Program
    {
        const int ExitOk = 0;
        const int ExitUsage = 1;
        const int ExitNotFormatted = 2;

        const string Usage =
@"Usage: sqlhelper format [file]
       sqlhelper header [file] --initials XX [--note text] [--date dd/MM/yyyy]

format  Formats T-SQL in the house style.
header  Adds the house comment header to the first procedure, function or view, or
        adds a history line to the one it has. An SSMS template header is converted.
        --note is the text of the history line (default: none, left for you to finish);
        --date defaults to today.

Both read the given file, or standard input if no file is given, and write the
result to standard output. The file itself is never changed.

Exit codes:
  0  done
  1  bad arguments or unreadable file
  2  the SQL was left unchanged (it doesn't parse, there's nothing to put a header
     on, or the result failed SQL Helper's safety check); the reason goes to
     standard error and nothing to standard output";

        public static int Main(string[] args)
        {
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = new UTF8Encoding(false);

            if (args.Length == 0 || (args[0] != "format" && args[0] != "header")
                || !TryParseArguments(args, out string file, out Dictionary<string, string> options)
                || !TryCreateCommand(args[0], options, out Func<string, (bool Succeeded, string Text, IReadOnlyList<string> Errors)> run))
            {
                Console.Error.WriteLine(Usage);
                return ExitUsage;
            }

            string input;
            try
            {
                input = file != null ? File.ReadAllText(file) : Console.In.ReadToEnd();
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Console.Error.WriteLine(ex.Message);
                return ExitUsage;
            }

            var result = run(input);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors) Console.Error.WriteLine(error);
                return ExitNotFormatted;
            }

            Console.Out.Write(result.Text);
            return ExitOk;
        }

        /// <summary>At most one file name, plus <c>--name value</c> pairs.</summary>
        static bool TryParseArguments(string[] args, out string file, out Dictionary<string, string> options)
        {
            file = null;
            options = new Dictionary<string, string>();
            for (int i = 1; i < args.Length; i++)
            {
                if (args[i].StartsWith("--"))
                {
                    if (i + 1 == args.Length || options.ContainsKey(args[i])) return false;
                    options[args[i]] = args[++i];
                }
                else if (file == null) file = args[i];
                else return false;
            }
            return true;
        }

        static bool TryCreateCommand(string command, Dictionary<string, string> options,
            out Func<string, (bool Succeeded, string Text, IReadOnlyList<string> Errors)> run)
        {
            run = null;
            if (command == "format")
            {
                if (options.Count > 0) return false;
                run = sql =>
                {
                    var result = new SqlFormatter().Format(sql);
                    return (result.Succeeded, result.Text, result.Errors);
                };
                return true;
            }

            var headerOptions = new HeaderOptions();
            foreach (var option in options)
            {
                switch (option.Key)
                {
                    case "--initials":
                        headerOptions.Initials = option.Value;
                        break;
                    case "--note":
                        headerOptions.Note = option.Value;
                        break;
                    case "--date":
                        if (!DateTime.TryParseExact(option.Value, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date))
                            return false;
                        headerOptions.Today = date;
                        break;
                    default:
                        return false;
                }
            }
            if (string.IsNullOrWhiteSpace(headerOptions.Initials)) return false;

            run = sql =>
            {
                var result = new CommentHeader(headerOptions).Apply(sql);
                return (result.Succeeded, result.Text, result.Errors);
            };
            return true;
        }
    }
}