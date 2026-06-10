using System;
using winPEAS.Checks;

namespace winPEAS
{
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            // 1. Статичен анализ на winPEAS
            Checks.Checks.Run(args);

            // 2. DLL Digger runtime сканиране
            try
            {
                DllDigger.CheckProcmonLogs();  // <-- ПРОМЕНЕНО
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[!] DLL Digger runtime scan failed: {ex.Message}");
            }

            // 3. Край
            Console.WriteLine("\n[*] winPEAS scan completed.");
        }
    }
}