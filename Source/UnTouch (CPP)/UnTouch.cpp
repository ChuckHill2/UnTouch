#include <regex>
#include <string>
#include <windows.h>
#include <timezoneapi.h>
using namespace std;

#pragma region Constants/Enums
const int FileDate_CreateTime = 0x01;
const int FileDate_LastWriteTime = 0x02;
const int FileDate_LastAccessTime = 0x04;
const int FileDate_All = FileDate_CreateTime | FileDate_LastWriteTime | FileDate_LastAccessTime;
#pragma endregion

static wchar_t* src = NULL;
static wchar_t* dst = NULL;
static int fileDates = FileDate_All;
static FILETIME nuDateTime = { 0,0 };

#pragma region Function Prototypes
void Parse(int argc, wchar_t* argv[]);
int ParseTimeFields(int i, int argc, wchar_t* argv[]);
void ErrorExit(const char* msg);
BOOL FileExists(wchar_t* file);
BOOL GetFileDates(wchar_t* file, FILETIME* createTime, FILETIME* lastWriteTime, FILETIME* lastAccessTime);
BOOL SetFileDates(wchar_t* file, FILETIME* createTime, FILETIME* lastWriteTime, FILETIME* lastAccessTime);
BOOL SystemTimeFromStr(__in LPCWSTR psz, LCID lcid, __out LPSYSTEMTIME pst);
int DaysInMonth(int year, int month);
BOOL FileTimeFromStr(__in LPCWSTR psz, LCID lcid, __out LPFILETIME pft);
#pragma endregion

int wmain(int argc, wchar_t* argv[])
{
    Parse(argc, argv);

    if (src != 0)
    {
        FILETIME cr,lw,la;
        GetFileDates(src, &cr, &lw, &la);
        SetFileDates(dst, &cr, &lw, &la);
    }
    else
    {
        FILETIME *cr=NULL, *lw=NULL, *la=NULL;
        if ((fileDates & FileDate_CreateTime) != 0) cr = &nuDateTime;
        if ((fileDates & FileDate_LastWriteTime) != 0) lw = &nuDateTime;
        if ((fileDates & FileDate_LastAccessTime) != 0) la = &nuDateTime;
        SetFileDates(dst, cr, lw, la);
    }

    exit(0); //success
}

void Parse(int argc, wchar_t* argv[])
{
    if (argv == 0 || argc < 3) ErrorExit("Missing arguments.");
    for (int i = 1; i < argc; i++)
    {
        wchar_t* arg = argv[i];
        if (argc == 0) ErrorExit("Arg must not be empty.");

        if (arg[0] == '-' || arg[0] == '/')
        {
            arg = (arg + 1);
            if (wcslen(arg) == 1 && arg[0] == 't' || arg[0] == 'T')
            {
                i = ParseTimeFields(i + 1, argc, argv);
            }
            continue;
        }

        if (arg[0] >= '0' && arg[0] <= '9' && nuDateTime.dwHighDateTime == 0 && nuDateTime.dwLowDateTime == 0 && FileTimeFromStr(arg, 0x0409, &nuDateTime)) continue;

        if (FileExists(arg) && src == 0)
        {
            src = arg;
            continue;
        }

        if (FileExists(arg) && dst == 0)
        {
            dst = arg;
            continue;
        }
    }

    if (dst == 0) { dst = src; src = 0; }

    if (src == 0 && nuDateTime.dwHighDateTime == 0 && nuDateTime.dwLowDateTime == 0) ErrorExit("Datetime is undefined or invalid.");
    if (dst == 0) ErrorExit("File to update is undefined or does not exist.");
    if (src != 0 && dst != 0 && (nuDateTime.dwHighDateTime != 0 || nuDateTime.dwLowDateTime != 0)) ErrorExit("If source and destination files exist, specified datetime must be undefined");
}

int ParseTimeFields(int i, int argc, wchar_t* argv[])
{
    int fd = 0;
    std::wregex reCreateTime(L"(C|Cr|Cre|Crea|Creat|Create|Created)", std::regex_constants::icase);
    std::wregex reLastWriteTime(L"^(M|Mo|Mod|Modi|Modif|Modifi|Modifie|Modified)$", std::regex_constants::icase);
    std::wregex reLastAccessTime(L"^(A|Ac|Acc|Acce|Acces|Access|Accesse|Accessed)$", std::regex_constants::icase);
    std::wstring input;

    for (; i < argc;)
    {
        input = argv[i];
        if (std::regex_match(input, reCreateTime))
        {
            fd |= FileDate_CreateTime;
            i++;
            continue;
        }
        input = argv[i];
        if (std::regex_match(input, reLastWriteTime))
        {
            fd |= FileDate_LastWriteTime;
            i++;
            continue;
        }
        input = argv[i];
        if (std::regex_match(input, reLastAccessTime))
        {
            fd |= FileDate_LastAccessTime;
            i++;
            continue;
        }

        break;
    }

    fileDates = fd == 0 ? FileDate_All : fd;

    return i - 1;
}

void ErrorExit(const char* msg)
{
    if (msg != 0)
    {
        printf("\r\n");
        printf(msg);
    }

    printf(
        "\r\nAssign a new date to a file or directory.\r\n"
        "\t\n"
        "(1) Copy all filetimes from source file to destination file :\r\n"
        "    Usage: UnTouch.exe sourcefile destfile\r\n"
        "           sourcefile - File to copy dates from.\r\n"
        "           destfile - File to copy dates to.\r\n"
        "\r\n"
        "(2) Set specific time for specific date field :\r\n"
        "    Usage: UnTouch.exe [-t filedates] datetime destfile\r\n"
        "           -t filedates - Specify which date fields to update. If undefined, sets all 3 fields to this new value.\r\n"
        "              Filedate keywords - Created, Modified, Accessed or C, M, A.\r\n"
        "           datetime - format: Year-part must be 4 digits.\r\n"
        "              yyyy-mm-dd [hh:mm[:ss[.fff]] [am|pm]]\r\n"
        "              yyyy-mm-dd[Thh:mm[:ss[.fff]][am|pm]] (formatted w/o spaces)\r\n"
        "              mm/dd/yyyy ...\r\n"
        "              If datetime contains spaces, it must be quoted.\r\n"
        "              If ampm is not specified, assumes 24-hr clock.\r\n"
        "              'am' and 'pm' may be abbreviated to 'a' and 'p'\r\n"
        "           destfile = File to update. If file contains spaces, it must be quoted.\r\n"
        "\r\n"
        "    Arguments may be in any order.\r\n"
        "    Everything is case insensitive.\r\n"
    );

    exit(1);
}

BOOL FileExists(wchar_t* file)
{
    WIN32_FIND_DATA FindFileData;
    HANDLE handle = FindFirstFile(file, &FindFileData);
    int found = handle != INVALID_HANDLE_VALUE;
    if (found)
    {
        //FindClose(&handle); this will crash
        FindClose(handle);
    }
    return found;
}

BOOL GetFileDates(wchar_t* file, FILETIME* createTime, FILETIME* lastWriteTime, FILETIME* lastAccessTime)
{
    WIN32_FIND_DATA FindFileData;
    HANDLE handle = FindFirstFile(file, &FindFileData);
    int found = handle != INVALID_HANDLE_VALUE;
    if (found)
    {
        createTime->dwLowDateTime = FindFileData.ftCreationTime.dwLowDateTime;
        createTime->dwHighDateTime = FindFileData.ftCreationTime.dwHighDateTime;
        lastWriteTime->dwLowDateTime = FindFileData.ftLastWriteTime.dwLowDateTime;
        lastWriteTime->dwHighDateTime = FindFileData.ftLastWriteTime.dwHighDateTime;
        lastAccessTime->dwLowDateTime = FindFileData.ftLastAccessTime.dwLowDateTime;
        lastAccessTime->dwHighDateTime = FindFileData.ftLastAccessTime.dwHighDateTime;

        FindClose(handle);
    }
    return found;
}

BOOL SetFileDates(wchar_t* file, FILETIME* createTime, FILETIME* lastWriteTime, FILETIME* lastAccessTime)
{
    HANDLE hFile = CreateFile(file, FILE_WRITE_ATTRIBUTES, FILE_SHARE_READ | FILE_SHARE_WRITE, NULL, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, NULL);
    if (hFile == INVALID_HANDLE_VALUE) return FALSE;

    BOOL ok = SetFileTime(hFile, createTime, lastWriteTime, lastAccessTime);
    CloseHandle(hFile);
    return ok;
}

BOOL SystemTimeFromStr(__in LPCWSTR psz, LCID lcid, __out LPSYSTEMTIME pst)
{
    memset(pst, 0, sizeof(SYSTEMTIME));

    //Wide version of std::regex_search() does not exist so we dumb down the wide datetime string to multibyte.
    char sz[256];
    memset(sz, 0, 256);
    size_t retval;
    wcstombs_s(&retval, sz, 256, psz, wcslen(psz));

    //Date: We support both YMD and MDY format with delimiters: '-', '\', and '/'.
    //Optional Time: We support a space or 'T' delimiter between date and time, with optional seconds, milliseconds, and a/p or am/pm with or without a space delimiter.
    //Note: Named capture groups not supported!!!
    std::regex reTime1("^([0-9]{1,4})[\\/-]([0-9]{1,2})[\\/-]([0-9]{1,4})(?:[ T]([0-9]{1,2}):([0-9]{1,2})(?::([0-9]{1,2})(?:\.([0-9]{1,3}))?)? ?(am|pm|a|p)?)?$", std::regex_constants::icase);
    std::string input = sz;
    std::smatch m;

    if (!std::regex_search(input, m, reTime1)) return FALSE;
    int len = m.size();

    pst->wYear = stoi(m[1].str()); //assume yyyy-mm-dd format
    pst->wMonth = stoi(m[2].str());
    pst->wDay = stoi(m[3].str());

    if (pst->wDay > 31)            //mm-dd-yyyy format
    {
        WORD year = pst->wDay; 
        WORD day = pst->wMonth;
        WORD month = pst->wYear;
        pst->wMonth = month;
        pst->wDay = day;
        pst->wYear = year;
    }

    if (pst->wYear < 1000 || pst->wMonth < 1 || pst->wMonth >12 || pst->wDay < 1 || pst->wDay > DaysInMonth(pst->wYear, pst->wMonth)) return FALSE;

    if (m[4].matched) pst->wHour = stoi(m[4].str());
    if (m[5].matched) pst->wMinute = stoi(m[5].str());
    if (m[6].matched) pst->wSecond = stoi(m[6].str());

    if (m[7].matched)
    {
        string ms = m[7].str();
        if (ms.length() < 3) ms += "0";
        if (ms.length() < 3) ms += "0";
        pst->wMilliseconds = stoi(ms);
    }
    if (m[8].matched)
    {
        char ap = m[8].str()[0];
        if (ap == 'p' || ap == 'P' && pst->wHour < 12)
        {
            pst->wHour += 12;
        }
    }

    if (pst->wHour >= 24 || pst->wMinute >= 60 || pst->wSecond >= 60) return FALSE;

    //sscanf not as robust for parsing datetimes when parts are missing.
    //if (swscanf_s(psz, L"%hu-%hu-%hu %hu:%hu:%hu %[apAP]", &pst->wYear, &pst->wMonth, &pst->wDay, &pst->wHour, &pst->wMinute, &pst->wSecond, &ampm) < 3)
    //    if (swscanf_s(psz, L"%hu/%hu/%hu %hu:%hu:%hu %[apAP]", &pst->wYear, &pst->wMonth, &pst->wDay, &pst->wHour, &pst->wMinute, &pst->wSecond, &ampm) < 3) return FALSE;

    //Convert local time to UTC time...FileTime is always UTC
    BOOL success = TzSpecificLocalTimeToSystemTime(NULL, pst, pst);

    return TRUE;

    ////https://devblogs.microsoft.com/oldnewthing/20121102-00/?p=6183
    ////Ostensively limited to a single format and I have not been able to get it to work, so I just give up.
    //DATE date;
    ////lcid = 0x0409;
    //return SUCCEEDED(VarDateFromStr(psz, lcid, 0, &date)) &&
    //    VariantTimeToSystemTime(date, pst);
}

int DaysInMonth(int year, int month)
{
    //leap year condition, if month is 2
    if (month == 2)
    {
        if ((year % 400 == 0) || (year % 4 == 0 && year % 100 != 0))
            return 29;
        else
            return 28;
    }
    //months which has 31 days
    else if (month == 1 || month == 3 || month == 5 || month == 7 || month == 8
        || month == 10 || month == 12)
        return 31;
    else
        return 30;
}

BOOL FileTimeFromStr(__in LPCWSTR psz, LCID lcid, __out LPFILETIME pft)
{
    SYSTEMTIME st;
    memset(pft, 0, sizeof(FILETIME));

    return SystemTimeFromStr(psz, lcid, &st) &&
        SystemTimeToFileTime(&st, pft);
}
