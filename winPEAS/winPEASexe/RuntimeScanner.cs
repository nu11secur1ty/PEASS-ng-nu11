using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace winPEAS
{
    public static class RuntimeScanner
    {
        public static void CheckProcmonLogs()
        {
            // 1. Стартираме Procmon (ако е наличен)
            string procmonPath = FindProcmon();
            if (string.IsNullOrEmpty(procmonPath))
            {
                Console.WriteLine("[!] Procmon not found. Skipping runtime scan.");
                return;
            }

            // 2. Пускаме capture за 30 секунди
            RunProcmonCapture(procmonPath);

            // 3. Анализираме CSV за NAME NOT FOUND
            AnalyzeCapture();

            // 4. Почистваме
            Cleanup();
        }

        private static string FindProcmon()
        {
            string[] possiblePaths = {
                "C:\\Windows\\System32\\procmon.exe",
                "C:\\procmon.exe",
                Path.Combine(Environment.CurrentDirectory, "procmon.exe")
            };
            return possiblePaths.FirstOrDefault(File.Exists);
        }

        private static void RunProcmonCapture(string procmonPath)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = procmonPath,
                Arguments = "/AcceptEula /BackingFile capture.pml /Quiet",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using (var proc = Process.Start(startInfo))
            {
                System.Threading.Thread.Sleep(30000); // 30 seconds capture
                proc.Kill();
            }
        }

        private static void AnalyzeCapture()
        {
            // Конвертиране на PML в CSV
            Process.Start("procmon.exe", "/OpenLog capture.pml /SaveAs capture.csv").WaitForExit();

            // Четене на CSV и търсене на NAME NOT FOUND
            if (!File.Exists("capture.csv")) return;

            var lines = File.ReadAllLines("capture.csv");
            foreach (var line in lines)
            {
                if (line.Contains("NAME NOT FOUND") && 
                    (line.Contains(".sys") || line.Contains(".dll")) &&
                    (line.Contains("\\Temp\\") || line.Contains("\\en-US\\") || line.Contains("\\en-GB\\")))
                {
                    Console.WriteLine($"[!!!] POSSIBLE LPE: {line}");
                }
            }
        }

        private static void Cleanup()
        {
            foreach (var f in new[] { "capture.pml", "capture.csv" })
                if (File.Exists(f)) File.Delete(f);
        }
    }
}
