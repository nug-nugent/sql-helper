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

        /// <summary>The commands that take no options: text in, text out.</summary>
        static readonly Dictionary<string, Func<string, FormatResult>> TextCommands = new Dictionary<string, Func<string, FormatResult>>
        {
            ["format"] = sql => new SqlFormatter().Format(sql),
            ["cleanup"] = Cleanup.Apply,
            ["semicolons"] = Semicolons.Add,
            ["markers"] = ServerSideMarkers.Add,
            ["todynamic"] = sql => DynamicSql.ToDynamic(sql),
            ["fromdynamic"] = sql => DynamicSql.FromDynamic(sql),
        };

        const string Usage =
@"Usage: sqlhelper format [file]
       sqlhelper cleanup [file]
       sqlhelper semicolons [file]
       sqlhelper markers [file]
       sqlhelper todynamic [file]
       sqlhelper fromdynamic [file]
       sqlhelper header [file] --initials XX [--note text] [--date dd/MM/yyyy]

format      Formats T-SQL in the house style.
cleanup     Rewrites joins, abbreviated keywords, aliases and brackets into the
            house's preferred form, leaving the layout as it is.
semicolons  Ends every statement with a semicolon. The statement before a
            --##ServerSideFinalSqlEnd comment is left without one.
markers     Adds the server-side grid markers that are missing from the first
            procedure, whose last statement must be the grid's SELECT.
todynamic   Turns the script into a parameterised sp_executesql call, typing each
            parameter from its variable's declaration.
fromdynamic Replaces each sp_executesql or EXEC (...) call whose SQL is built only
            from string literals with the statements it runs.
header     Adds the house comment header to the first procedure, function or view,
            or adds a history line to the one it has. An SSMS template header is
            converted. --note is the text of the history line (default: none, left
            for you to finish); --date defaults to today.

All read the given file, or standard input if no file is given, and write the
result to standard output. The file itself is never changed.

Exit codes:
  0  done
  1  bad arguments or unreadable file
  2  the SQL was left unchanged (it doesn't parse, there's nothing to put a header
     or markers on or nothing to convert, or the result failed SQL Helper's safety check); the reason
     goes to standard error and nothing to standard output";

        public static int Main(string[] args)
        {
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = new UTF8Encoding(false);

            if (args.Length == 0 || (!TextCommands.ContainsKey(args[0]) && args[0] != "header")
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
            if (TextCommands.TryGetValue(command, out Func<string, FormatResult> apply))
            {
                if (options.Count > 0) return false;
                run = sql =>
                {
                    var result = apply(sql);
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