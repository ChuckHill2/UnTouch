# UnTouch

Simple console application with command line arguments to assign a file a new created/modified/accessed date.
* AND used as a comparison to the much hyped .NET Core. 

### Features

Three different versions of the same application. All 3 are functionally equivalent.<br />
Coding content is identical (or as close as could be).<br />
All three use the Regex library for parsing plus CPP version includes Unicode support for unicode filenames.

1. Compiled and published as x64 .NET Core 3.1 Console app. (27339 KB)
   - Will run on only Windows x64 machine, but may be 'Published' for any non-windows OS as long one does not use any OS-specific code. No system dependencies.
   - Slowest and 3 orders of magnitude bigger in size. (Why is this better?)
2. Compiled as .NET Framework 4.5 AnyCpu Console App. (10 KB)
   - Will transparently run on both Windows x32/x64 machines, but requires .NET environment to be pre-installed. However, this is built-in for all windows machines anyway.
   - Reasonably fast and smallest in size.
3. Compiled as C++ Win32 x64 Console App. (74 KB)
   - Will run on only Windows x64 machine. No other dependencies.
   - Very, very fast but 5X larger.

### My Opinion
* .NET Core is good for Web server applications but not anything else. I consider it not ready for prime-time. The functionality is not complete.
* .NET Framework is the most mature for Console, Forms, and WPF local applications.
* Win32 CPP is best for raw speed and interfacing to anything, but is slow to develop on and more prone to bugs.

Comments appreciated. You may change my mind!