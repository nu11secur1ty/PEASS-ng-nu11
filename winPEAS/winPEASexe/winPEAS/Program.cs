using System;
using winPEAS.Checks;  // Добави, за да може да извикаме Run

namespace winPEAS
{
    public static class Program
    {
        // Static blacklists        
        //static string goodSoft = "Windows Phone Kits|Windows Kits|Windows Defender|Windows Mail|Windows Media Player|Windows Multimedia Platform|windows nt|Windows Photo Viewer|Windows Portable Devices|Windows Security|Windows Sidebar|WindowsApps|WindowsPowerShell| Windows$|Microsoft|WOW6432Node|internet explorer|Internet Explorer|Common Files";                       

        [STAThread]
        public static void Main(string[] args)
        {
            // Запазваме оригиналното поведение на winPEAS
            Checks.Checks.Run(args);
            
            // Добавяме DLL Digger runtime скенер (допълнително)
            try
            {
                RuntimeScanner.CheckProcmonLogs();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[!] DLL Digger runtime scan failed: {ex.Message}");
            }
            
            // Край – winPEAS си продължава
            Console.WriteLine("\n[*] winPEAS scan completed.");
        }
    }
}
