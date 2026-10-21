using System;
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

Formats T-SQL from the given file, or from standard input if no file is given,
and writes the result to standard output.

Exit codes:
  0  formatted successfully
  1  bad arguments or unreadable file
  2  the SQL was not formatted (it doesn't parse, or the result failed SQL Helper's
     safety check); the reason goes to standard error and nothing to standard output";

        public static int Main(string[] args)
        {
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = new UTF8Encoding(false);

            if (args.Length == 0 || args.Length > 2 || args[0] != "format")
            {
                Console.Error.WriteLine(Usage);
                return ExitUsage;
            }

            string input;
            try
            {
                input = args.Length == 2 ? File.ReadAllText(args[1]) : Console.In.ReadToEnd();
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Console.Error.WriteLine(ex.Message);
                return ExitUsage;
            }

            var result = new SqlFormatter().Format(input);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors) Console.Error.WriteLine(error);
                return ExitNotFormatted;
            }

            Console.Out.Write(result.Text);
            return ExitOk;
        }
    }
}
