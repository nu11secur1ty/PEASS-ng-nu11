using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace winPEAS
{
    public static class DllDigger
    {
        public static void Check()
        {
            Console.WriteLine("\n[*] DLL Digger Runtime Scan started...");

            string procmonPath = FindProcmon();
            if (string.IsNullOrEmpty(procmonPath))
            {
                Console.WriteLine("[!] Procmon not found. Install it from Sysinternals.");
                return;
            }

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string pmlPath = Path.Combine(Environment.CurrentDirectory, $"capture_{timestamp}.pml");
            string csvPath = Path.Combine(Environment.CurrentDirectory, $"capture_{timestamp}.csv");

            Console.WriteLine("[*] Starting background capture (60 sec)...");
            var capture = Process.Start(new ProcessStartInfo
            {
                FileName = procmonPath,
                Arguments = $"/AcceptEula /BackingFile \"{pmlPath}\" /Quiet",
                UseShellExecute = false,
                CreateNoWindow = true
            });

            for (int i = 60; i > 0; i -= 10)
            {
                Console.WriteLine($"    {i} sec remaining...");
                Thread.Sleep(10000);
            }

            Console.WriteLine("[*] Stopping capture...");
            Process.Start(procmonPath, "/Terminate").WaitForExit(5000);
            Thread.Sleep(2000);

            Console.WriteLine("[*] Converting to CSV...");
            Process.Start(procmonPath, $"/OpenLog \"{pmlPath}\" /SaveAs \"{csvPath}\" /Quiet").WaitForExit(30000);
            Thread.Sleep(2000);

            if (!File.Exists(csvPath))
            {
                Console.WriteLine("[-] CSV not created.");
                return;
            }

            Console.WriteLine("[*] Scanning for LPE vectors...");
            int found = 0;
            using (var reader = new StreamReader(csvPath))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Contains("NAME NOT FOUND") &&
                        (line.Contains(".sys") || line.Contains(".dll")) &&
                        (line.Contains("\\Temp\\") || line.Contains("\\en-US\\") || line.Contains("\\en-GB\\") || line.Contains("\\AppData\\")))
                    {
                        Console.WriteLine($"[!!!] POSSIBLE LPE: {line}");
                        found++;
                        if (found >= 50) break;
                    }
                }
            }

            Console.WriteLine($"[+] Found {found} potential LPE vectors.");
            Console.WriteLine($"[*] Capture saved: {pmlPath}");
            Console.WriteLine($"[*] CSV saved: {csvPath}");
        }

        private static string FindProcmon()
        {
            string[] paths = {
                Path.Combine(Environment.CurrentDirectory, "procmon.exe"),
                "C:\\Windows\\System32\\procmon.exe"
            };
            return paths.FirstOrDefault(File.Exists);
        }
    }
}