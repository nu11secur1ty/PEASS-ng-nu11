using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace winPEAS
{
    public static class RuntimeScanner
    {
        public static void CheckProcmonLogs()
        {
            Console.WriteLine("\n[*] DLL Digger Runtime Scan started...");

            string procmonPath = FindProcmon();
            if (string.IsNullOrEmpty(procmonPath))
            {
                Console.WriteLine("[!] Procmon not found.");
                return;
            }

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string pmlPath = Path.Combine(Environment.CurrentDirectory, $"capture_{timestamp}.pml");
            string csvPath = Path.Combine(Environment.CurrentDirectory, $"capture_{timestamp}.csv");

            // 1. START CAPTURE (background, no GUI)
            Console.WriteLine("[*] Starting Procmon capture (60 seconds)...");
            var captureProc = Process.Start(new ProcessStartInfo
            {
                FileName = procmonPath,
                Arguments = $"/AcceptEula /BackingFile \"{pmlPath}\" /Quiet",
                UseShellExecute = false,
                CreateNoWindow = true
            });

            // 2. WAIT - DO NOTHING, JUST WAIT
            for (int i = 60; i > 0; i -= 10)
            {
                Console.WriteLine($"    {i} seconds remaining...");
                Thread.Sleep(10000);
            }

            // 3. STOP CAPTURE PROPERLY (no killing)
            Console.WriteLine("[*] Stopping capture...");
            Process.Start(procmonPath, "/Terminate").WaitForExit(5000);

            // Wait a bit for file to be written
            Thread.Sleep(2000);

            // 4. SAVE AS CSV (without opening anything)
            Console.WriteLine("[*] Saving as CSV...");
            Process.Start(procmonPath, $"/OpenLog \"{pmlPath}\" /SaveAs \"{csvPath}\" /Quiet").WaitForExit(30000);

            Thread.Sleep(2000);

            // 5. DONE - files are saved
            if (File.Exists(pmlPath))
                Console.WriteLine($"[+] Capture saved: {pmlPath}");
            if (File.Exists(csvPath))
                Console.WriteLine($"[+] CSV saved: {csvPath}");

            Console.WriteLine("[*] Scan complete. Files are ready for analysis.");
        }

        private static string FindProcmon()
        {
            string[] paths = {
                Path.Combine(Environment.CurrentDirectory, "procmon.exe"),
                "C:\\Windows\\System32\\procmon.exe",
                "C:\\procmon.exe"
            };
            foreach (var p in paths)
                if (File.Exists(p)) return p;
            return null;
        }
    }
}