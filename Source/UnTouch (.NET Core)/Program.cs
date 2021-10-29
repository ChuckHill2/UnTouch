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
                //File.SetCreationTime(dst, File.GetCreationTime(src));
                //File.SetLastWriteTime(dst, File.GetLastWriteTime(src));
                //File.SetLastAccessTime(dst, File.GetLastAccessTime(src));

                if (!Win32.GetFileTime(src, out long creationTime, out long lastAccessTime, out long lastWriteTime)) ErrorExit("Error getting source file time.");
                if (!Win32.SetFileTime(dst, creationTime, lastAccessTime, lastWriteTime)) ErrorExit("Error setting destination file time.");
            }
            else
            {
                //if ((fileDates & FileDates.CreateTime) != 0) File.SetCreationTime(dst, nuDateTime);
                //if ((fileDates & FileDates.LastWriteTime) != 0) File.SetLastWriteTime(dst, nuDateTime);
                //if ((fileDates & FileDates.LastAccessTime) != 0) File.SetLastAccessTime(dst, nuDateTime);

                long creationTime = 0, lastAccessTime = 0, lastWriteTime = 0;
                long ftDateTime = nuDateTime.ToFileTime();
                if ((fileDates & FileDates.CreateTime) != 0) creationTime = ftDateTime;
                if ((fileDates & FileDates.LastWriteTime) != 0) lastWriteTime = ftDateTime;
                if ((fileDates & FileDates.LastAccessTime) != 0) lastAccessTime = ftDateTime;
                if (!Win32.SetFileTime(dst, creationTime, lastAccessTime, lastWriteTime)) ErrorExit("Error setting destination file time.");
            }

            Environment.Exit(0); //success
        }

        private static void Parse(string[] args)
        {
            if (args == null || args.Length < 1) ErrorExit("Missing arguments.");
            for(int i=0; i<args.Length; i++)
            {
                string arg = args[i];
                if (arg.Length == 0) ErrorExit("Arg must not be empty."); //may  be """ on the command-line
                if (arg[0]=='-' || arg[0]=='/') //command-line switch
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

                //must be a datetime if it starts with a number
                if (arg[0] >= '0' && arg[0] <= '9' && nuDateTime==DateTime.MinValue && DateTimeTryParse(arg, out nuDateTime)) continue;

                if (src == null) //get first filename
                {
                    src = Path.GetFullPath(arg); 
                    if (!File.Exists(src)) ErrorExit($@"File ""{src}"" not found.");
                    continue;
                }

                if (dst == null) //get 2nd filename
                {
                    dst = Path.GetFullPath(arg);
                    if (!File.Exists(dst)) ErrorExit($@"File ""{dst}"" not found.");
                    continue;
                }
            }

            if (dst == null) { dst = src; src = null; } 
            if (dst == null) ErrorExit("File to modify is not defined.");
            if (src == null && nuDateTime == DateTime.MinValue)
            {
                Console.WriteLine("A specific datetime is not defined. Defaulting to today's date.");
                nuDateTime = DateTime.Now;
            }

            if (src != null &&
                dst != null &&
                nuDateTime != DateTime.MinValue)
                ErrorExit("If source and destination files exist, a specified datetime cannot be defined");
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
           -t filedates - Specify which date fields to update.
              If undefined, sets all 3 fields to this new value.
              Filedate keywords - Created, Modified, Accessed or C, M, A.
              Multiple keywords may be specified delimited by space. Do not quote.
           datetime - format: Year-part must be 4 digits.
              yyyy-mm-dd [hh:mm[:ss[.fff]] [am|pm]] (formatted with spaces)
              yyyy-mm-dd[Thh:mm[:ss[.fff]][am|pm]] (formatted w/o spaces)
              mm/dd/yyyy ...
              If datetime contains spaces, it must be quoted.
              If ampm is not specified, assumes 24-hr clock.
              'am' and 'pm' may be abbreviated to 'a' and 'p'
              If datetime undefined, defaults to today's date.
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

    /// <summary>
    /// This is a low-level alternative to:
    ///    • System.IO.File.GetCreationTime()
    ///    • System.IO.File.GetLastWriteTime()
    ///    • System.IO.File.GetLastAccessTime()
    ///    and
    ///    • System.IO.File.SetCreationTime()
    ///    • System.IO.File.SetLastWriteTime()
    ///    • System.IO.File.SetLastAccessTime()
    /// The reason is sometimes some fields do not get set properly. File open/close 3 times in rapid succession?
    /// </summary>
    internal static class Win32
    {
        [DllImport("kernel32.dll")] private static extern bool SetFileTime(IntPtr hFile, ref long creationTime, ref long lastAccessTime, ref long lastWriteTime);
        [DllImport("kernel32.dll")] private static extern bool SetFileTime(IntPtr hFile, IntPtr creationTime, ref long lastAccessTime, ref long lastWriteTime);
        [DllImport("kernel32.dll")] private static extern bool SetFileTime(IntPtr hFile, ref long creationTime, IntPtr lastAccessTime, ref long lastWriteTime);
        [DllImport("kernel32.dll")] private static extern bool SetFileTime(IntPtr hFile, ref long creationTime, ref long lastAccessTime, IntPtr lastWriteTime);
        [DllImport("kernel32.dll")] private static extern bool SetFileTime(IntPtr hFile, IntPtr creationTime, IntPtr lastAccessTime, ref long lastWriteTime);
        [DllImport("kernel32.dll")] private static extern bool SetFileTime(IntPtr hFile, ref long creationTime, IntPtr lastAccessTime, IntPtr lastWriteTime);
        [DllImport("kernel32.dll")] private static extern bool SetFileTime(IntPtr hFile, IntPtr creationTime, ref long lastAccessTime, IntPtr lastWriteTime);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetFileTime(IntPtr hFile, out long creationTime, out long lastAccessTime, out long lastWriteTime);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateFileW(string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);
        private static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hFile);

        /// <summary>
        /// Get all 3 datetime fields for a given file in FileTime (64-bit) format.
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="creationTime"></param>
        /// <param name="lastAccessTime"></param>
        /// <param name="lastWriteTime"></param>
        /// <returns>True if successful</returns>
        public static bool GetFileTime(string filename, out long creationTime, out long lastAccessTime, out long lastWriteTime)
        {
            creationTime = lastAccessTime = lastWriteTime = 0;
            var hFile = CreateFileW(filename, 0x0080, 0x00000003, IntPtr.Zero, 3, 0x80, IntPtr.Zero);
            if (hFile == INVALID_HANDLE_VALUE) return false;
            bool success = GetFileTime(hFile, out creationTime, out lastAccessTime, out lastWriteTime);
            CloseHandle(hFile);
            return success;
        }

        /// <summary>
        /// Set datetime fields for a given file in FileTime (64-bit) format. Time field value 0 == not modified.
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="creationTime"></param>
        /// <param name="lastAccessTime"></param>
        /// <param name="lastWriteTime"></param>
        /// <returns>True if successful</returns>
        public static bool SetFileTime(string filename, long creationTime, long lastAccessTime, long lastWriteTime)
        {
            bool success;
            var hFile = CreateFileW(filename, 0x0100, 0x00000003, IntPtr.Zero, 3, 0x80, IntPtr.Zero);
            if (hFile == INVALID_HANDLE_VALUE) return false;

            var fields = (creationTime == 0 ? 0 : 1) | (lastAccessTime == 0 ? 0 : 2) | (lastWriteTime == 0 ? 0 : 4);

            switch (fields)
            {
                case 0x01: success = SetFileTime(hFile, ref creationTime, IntPtr.Zero, IntPtr.Zero); break;
                case 0x02: success = SetFileTime(hFile, IntPtr.Zero, ref lastAccessTime, IntPtr.Zero); break;
                case 0x03: success = SetFileTime(hFile, ref creationTime, ref lastAccessTime, IntPtr.Zero); break;
                case 0x04: success = SetFileTime(hFile, IntPtr.Zero, IntPtr.Zero, ref lastWriteTime); break;
                case 0x05: success = SetFileTime(hFile, ref creationTime, IntPtr.Zero, ref lastWriteTime); break;
                case 0x06: success = SetFileTime(hFile, IntPtr.Zero, ref lastAccessTime, ref lastWriteTime); break;
                case 0x07: success = SetFileTime(hFile, ref creationTime, ref lastAccessTime, ref lastWriteTime); break;
                default: success = false; break;
            }

            CloseHandle(hFile);
            return success;
        }
    }
}
