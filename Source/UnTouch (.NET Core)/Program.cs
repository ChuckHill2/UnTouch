using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace UnTouch
{
    [Flags] public enum FileDates
    {
        CreateTime = 0x01,
        LastWriteTime = 0x02,
        LastAccessTime = 0x04,
        All = CreateTime | LastWriteTime | LastAccessTime
    }

    class Program
    {
        static string src;
        static string dst;
        static FileDates fileDates = FileDates.All;
        static DateTime nuDateTime;

        static void Main(string[] args)
        {
            Parse(args);

            if (src!=null)
            {
                File.SetCreationTime(dst, File.GetCreationTime(src));
                File.SetLastWriteTime(dst, File.GetLastWriteTime(src));
                File.SetLastAccessTime(dst, File.GetLastAccessTime(src));
            }
            else
            {
                if ((fileDates & FileDates.CreateTime) != 0) File.SetCreationTime(dst, nuDateTime);
                if ((fileDates & FileDates.LastWriteTime) != 0) File.SetLastWriteTime(dst, nuDateTime);
                if ((fileDates & FileDates.LastAccessTime) != 0) File.SetLastAccessTime(dst, nuDateTime);
            }

            Environment.Exit(0); //success
        }

        private static void Parse(string[] args)
        {
            if (args == null || args.Length < 2) Exit();
            for(int i=0; i<args.Length; i++)
            {
                string arg = args[i];
                if (arg.Length == 0) Exit("Arg must not be empty.");
                if (arg[0]=='-' || arg[0]=='/')
                {
                    arg = arg.Substring(1).ToLower();
                    switch(arg)
                    {
                        case "t":
                            i = ParseTimeFields(i + 1, args);
                            break;
                        default: 
                            continue;
                    }
                }

                if (arg[0] >= '0' && arg[0] <= '9' && DateTime.TryParse(arg, out nuDateTime)) continue;

                if (File.Exists(arg) && src == null)
                {
                    src = Path.GetFullPath(arg);
                    continue;
                }

                if (File.Exists(arg) && dst == null)
                {
                    dst = Path.GetFullPath(arg);
                    continue;
                }
            }

            if (dst==null) { dst = src; src = null; }

            if (src==null && nuDateTime==DateTime.MinValue) Exit("If datetime is undefined the source file must not be undefined.");
            if (src!=null && dst!= null && nuDateTime != DateTime.MinValue) Exit("If source and destination files exist, specified datetime must be undefined");
        }

        private static int ParseTimeFields(int i, string[] args)
        {
            FileDates fd = 0;
            for (; i<args.Length;)
            {
                if (Regex.IsMatch(args[i], @"^(C|Cr|Cre|Crea|Creat|Create|CreateT|CreateTi|CreateTim|CreateTime)$", RegexOptions.IgnoreCase))
                {
                    fd |= FileDates.CreateTime;
                    i++;
                    continue;
                }
                if (Regex.IsMatch(args[i], @"^(LW|LastW|LastWr|LastWri|LastWrit|LastWrite|LastWriteT|LastWriteTi|LastWriteTim|LastWriteTime)$", RegexOptions.IgnoreCase))
                {
                    fd |= FileDates.LastWriteTime;
                    i++;
                    continue;
                }
                if (Regex.IsMatch(args[i], @"^(LA|LastA|LastAc|LastAcc|LastAcce|LastAcces|LastAccess|LastAccessT|LastAccessTi|LastAccessTim|LastAccessTime)$", RegexOptions.IgnoreCase))
                {
                    fd |= FileDates.LastAccessTime;
                    i++;
                    continue;
                }

                break;
            }

            fileDates = fd == 0 ? FileDates.All : fd;

            return i-1;
        }

        private static void Exit(string msg=null)
        {
            if (msg != null)
            {
                Console.WriteLine();
                Console.WriteLine(msg);
            }

            var exe = Path.GetFileName(Process.GetCurrentProcess().MainModule.FileName);

            Console.WriteLine($@"
Assign a new date to a file or directory.

(1) Copy all filetimes from source file to destination file:
    Usage: {exe} sourcefile destfile
        sourcefile - File to copy dates from.
        destfile - File to copy dates to.

(2) Set specific time for specific date field:
    Usage: {exe} [-t CreateTime|LastWriteTime|LastAccessTime | C | LW | LA] ""yyyy-mm-dd hh:mm:ss.fff [am|pm]"" destfile
       -t CreateTime|LastWriteTime|LastAccessTime | C | LW | LA (undefined==set all three fields)
       ""yyyy-mm-dd hh:mm:ss.fff [am|pm]"" (if ampm not specified, assumes 24-hr clock)
       destfile - File to update.

Everything is case insensitive.
");
            Environment.Exit(1);
        }
    }
}
