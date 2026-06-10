using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Collections.Generic;

namespace winPEAS
{
    public static class DllDigger
    {
        public static void CheckProcmonLogs()
        {
            PrintColor("\n[*] DLL Digger Runtime Scan started...", ConsoleColor.Cyan);

            List<string> procmonPaths = FindAllProcmon();
            if (procmonPaths.Count == 0)
            {
                PrintColor("[!] Procmon not found. Download from Sysinternals.", ConsoleColor.Red);
                PrintColor("[!] Tried: C:\\, C:\\Windows\\System32, C:\\Windows\\System32\\drivers", ConsoleColor.Yellow);
                return;
            }

            PrintColor($"[+] Found {procmonPaths.Count} Procmon instance(s):", ConsoleColor.Green);
            foreach (var path in procmonPaths)
            {
                PrintColor($"    {path}", ConsoleColor.Green);
            }

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            int captureCount = 0;

            foreach (string procmonPath in procmonPaths)
            {
                captureCount++;
                string suffix = procmonPath.Contains("System32") ? "sys32" :
                               (procmonPath.Contains("drivers") ? "drv" : "root");

                string pmlPath = Path.Combine(Environment.CurrentDirectory, $"capture_{timestamp}_{suffix}_{captureCount}.pml");

                PrintColor($"\n[*] Capture #{captureCount} using: {procmonPath}", ConsoleColor.Cyan);

                // DELETE OLD FILE IF EXISTS
                try { if (File.Exists(pmlPath)) File.Delete(pmlPath); } catch { }

                // START CAPTURE
                PrintColor("[*] Starting Procmon capture (60 seconds)...", ConsoleColor.Cyan);
                var captureProc = Process.Start(new ProcessStartInfo
                {
                    FileName = procmonPath,
                    Arguments = $"/AcceptEula /BackingFile \"{pmlPath}\" /Quiet",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });

                if (captureProc == null)
                {
                    PrintColor($"    ❌ Failed to start Procmon from: {procmonPath}", ConsoleColor.Red);
                    continue;
                }

                // WAIT - GIVE IT FULL TIME
                for (int i = 60; i > 0; i -= 10)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write($"\r    [{i,2} seconds remaining]  ");
                    Console.ResetColor();
                    Thread.Sleep(10000);
                }
                Console.WriteLine();

                // STOP CAPTURE
                PrintColor("[*] Stopping capture...", ConsoleColor.Cyan);
                var stopProc = Process.Start(procmonPath, "/Terminate");
                if (stopProc != null)
                {
                    stopProc.WaitForExit(15000);
                    stopProc.Close();
                }

                // WAIT FOR FILE TO BE WRITTEN
                Thread.Sleep(5000);

                // SHOW RESULT
                if (File.Exists(pmlPath))
                {
                    FileInfo fi = new FileInfo(pmlPath);
                    PrintColor($"    ✅ Capture saved: {pmlPath} ({fi.Length:N0} bytes)", ConsoleColor.Green);
                }
                else
                {
                    PrintColor($"    ❌ Capture #{captureCount} failed - no PML file", ConsoleColor.Red);
                }

                // CLOSE PROCESS
                if (captureProc != null && !captureProc.HasExited)
                {
                    try { captureProc.Kill(); } catch { }
                    captureProc.Close();
                }
                PrintColor($"    ℹ️ Procmon instance #{captureCount} closed", ConsoleColor.Cyan);
            }

            // FINAL RESULTS
            Console.WriteLine();
            PrintSeparator();
            PrintColor($"DLL DIGGER RESULTS - {captureCount} capture(s) completed", ConsoleColor.Yellow);
            PrintSeparator();
            PrintColor("[✓] Scan complete. All capture files are saved.", ConsoleColor.Green);
            PrintColor("[*] The researcher can now open the files with Procmon GUI.", ConsoleColor.Cyan);
            PrintColor("[*] Files are located in the current directory.", ConsoleColor.Cyan);
            PrintSeparator();
        }

        private static void PrintColor(string text, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ResetColor();
        }

        private static void PrintSeparator()
        {
            Console.WriteLine(new string('=', 80));
        }

        private static List<string> FindAllProcmon()
        {
            List<string> found = new List<string>();
            string[] paths = {
                @"C:\Windows\System32\procmon.exe",
                @"C:\Windows\System32\procmon64.exe",
                @"C:\Windows\System32\drivers\procmon.exe",
                @"C:\Windows\System32\drivers\procmon64.exe",
                @"C:\procmon.exe",
                @"C:\procmon64.exe",
                Path.Combine(Environment.CurrentDirectory, "procmon.exe"),
                Path.Combine(Environment.CurrentDirectory, "procmon64.exe")
            };
            foreach (var p in paths)
                if (File.Exists(p)) found.Add(p);
            return found;
        }
    }
}