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
            if (args == null || args.Length < 2) ErrorExit("Missing arguments.");
            for(int i=0; i<args.Length; i++)
            {
                string arg = args[i];
                if (arg.Length == 0) ErrorExit("Arg must not be empty.");
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
                    continue;
                }

                if (arg[0] >= '0' && arg[0] <= '9' && nuDateTime==DateTime.MinValue && DateTimeTryParse(arg, out nuDateTime)) continue;

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

            if (src==null && nuDateTime==DateTime.MinValue) ErrorExit("If datetime is undefined the source file must not be undefined.");
            if (dst == null) ErrorExit("File to update is undefined or does not exist.");
            if (src!=null && dst!= null && nuDateTime != DateTime.MinValue) ErrorExit("If source and destination files exist, specified datetime must be undefined");
        }

        private static int ParseTimeFields(int i, string[] args)
        {
            FileDates fd = 0;
            for (; i<args.Length;)
            {
                if (Regex.IsMatch(args[i], @"^(C|Cr|Cre|Crea|Creat|Create|Created)$", RegexOptions.IgnoreCase))
                {
                    fd |= FileDates.CreateTime;
                    i++;
                    continue;
                }
                if (Regex.IsMatch(args[i], @"^(M|Mo|Mod|Modi|Modif|Modifi|Modifie|Modified)$", RegexOptions.IgnoreCase))
                {
                    fd |= FileDates.LastWriteTime;
                    i++;
                    continue;
                }
                if (Regex.IsMatch(args[i], @"^(A|Ac|Acc|Acce|Acces|Access|Accesse|Accessed)$", RegexOptions.IgnoreCase))
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

        private static void ErrorExit(string msg=null)
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
    Usage: {exe} [-t filedates] datetime destfile
           -t filedates - Specify which date fields to update. If undefined, sets all 3 fields to this new value.
              Filedate keywords - Created, Modified, Accessed or C, M, A.
              Multiple keywords may be specified delimited by space. Do not quote.
           datetime - format: Year-part must be 4 digits.
              yyyy-mm-dd [hh:mm[:ss[.fff]] [am|pm]]
              yyyy-mm-dd[Thh:mm[:ss[.fff]][am|pm]] (formatted w/o spaces)
              mm/dd/yyyy ...
              If datetime contains spaces, it must be quoted.
              If ampm is not specified, assumes 24-hr clock.
              'am' and 'pm' may be abbreviated to 'a' and 'p'
           destfile - File to update. If file contains spaces, it must be quoted.

       Arguments may be in any order.
       Everything is case insensitive.
");
            Environment.Exit(1);
        }

        private static bool DateTimeTryParse(string s, out DateTime result)
        {
            //Regex numeric pattern more forgiving than .NET DateTime parser.
            //Date: We support both YMD and MDY format with delimiters: '-', '\', and '/'.
            //Optional Time: We support a space or 'T' delimiter between date and time, with optional seconds, milliseconds, and a/p or am/pm with or without a space delimiter.
            const string pattern = @"^([0-9]{1,4})[\\/-]([0-9]{1,2})[\\/-]([0-9]{1,4})(?:[ T]([0-9]{1,2}):([0-9]{1,2})(?::([0-9]{1,2})(?:\.([0-9]{1,3}))?)? ?(am|pm|a|p)?)?$";
            Match match = Regex.Match(s, pattern, RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                //if match fails, we try official .NET datetime parser.
                if (!DateTime.TryParse(s, out result)) return false;
            }

            result = DateTime.MinValue;

            int.TryParse(match.Groups[1].Value, out int year);   //assume yyyy-mm-dd format
            int.TryParse(match.Groups[2].Value, out int month);
            int.TryParse(match.Groups[3].Value, out int day);

            if (day > 31)    //mm-dd-yyyy format
            {
                var y = day;
                var d = month;
                var m = year;
                month = m;
                day = d;
                year = y;
            }

            if (year < 1000 || month < 1 || month > 12 || day < 1 || day > DateTime.DaysInMonth(year, month)) return false;

            int hour = 0;
            int minute = 0;
            int second = 0;
            int ms = 0;

            if (match.Groups[4].Success)
            {
                int.TryParse(match.Groups[4].Value, out hour);
                int.TryParse(match.Groups[5].Value, out minute);
                int.TryParse(match.Groups[6].Value, out second);
                var msStr = match.Groups[7].Value;
                if (msStr.Length < 3) msStr += "0";
                if (msStr.Length < 3) msStr += "0";
                int.TryParse(msStr, out ms);

                var ampm = match.Groups[8].Value;
                if (ampm.Length>0 && (ampm[0]=='p' || ampm[0] == 'P'))
                {
                    hour += 12;
                }
            }

            if (hour >= 24 || minute >= 60 || second >= 60) return false;

            result = new DateTime(year, month, day, hour, minute, second, ms);

            return true;
        }
    }
}
