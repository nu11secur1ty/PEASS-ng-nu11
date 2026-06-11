using System;
using winPEAS.Checks;

namespace winPEAS
{
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            // 1. ПЪРВО: winPEAS статичен анализ (със своя clipboard output)
            Console.WriteLine("[*] Running winPEAS Static Analysis...");
            Checks.Checks.Run(args);

            // 2. СЛЕД КАТО winPEAS СВЪРШИ - разделител
            Console.WriteLine("\n" + new string('=', 80));
            Console.WriteLine("[*] winPEAS Static Analysis COMPLETED");
            Console.WriteLine(new string('=', 80) + "\n");

            // 3. ВТОРО: DLL Digger (чист, самостоятелен, без да пречи на никой)
            try
            {
                DllDigger.CheckProcmonLogs();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[!] DLL Digger runtime scan failed: {ex.Message}");
                Console.ResetColor();
            }

            // 4. КРАЙ
            Console.WriteLine("\n[*] Full scan completed.");
            Console.WriteLine("[*] Press Enter to exit...");
            Console.ReadLine();
        }
    }
}