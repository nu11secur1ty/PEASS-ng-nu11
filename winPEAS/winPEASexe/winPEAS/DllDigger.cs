using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace winPEAS
{
    public static class DllDigger
    {
        private static StringBuilder htmlReport = new StringBuilder();
        private static List<string> memoryAnomalies = new List<string>();

        public static void CheckProcmonLogs()
        {
            InitHTMLReport();

            PrintColor("\n[*] DLL Digger Runtime Scan started...", ConsoleColor.Cyan);
            AddToHTMLReport("h2", "DLL Digger Runtime Scan", ConsoleColor.Cyan);

            // MEMORY LEAK CHECK - BEFORE CAPTURE
            PrintColor("\n[*] Checking memory state before capture...", ConsoleColor.Cyan);
            AddToHTMLReport("h3", "Memory Analysis - Pre-Scan", ConsoleColor.Cyan);
            CheckMemoryLeaks("pre_scan");

            List<string> procmonPaths = FindAllProcmon();
            if (procmonPaths.Count == 0)
            {
                PrintColor("[!] Procmon not found. Download from Sysinternals.", ConsoleColor.Red);
                AddToHTMLReport("error", "Procmon not found. Download from Sysinternals.", ConsoleColor.Red);
                return;
            }

            PrintColor($"[+] Found {procmonPaths.Count} Procmon instance(s):", ConsoleColor.Green);
            AddToHTMLReport("info", $"Found {procmonPaths.Count} Procmon instance(s)", ConsoleColor.Green);

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            int captureCount = 0;
            List<string> captureFiles = new List<string>();

            foreach (string procmonPath in procmonPaths)
            {
                captureCount++;
                string suffix = procmonPath.Contains("System32") ? "sys32" :
                               (procmonPath.Contains("drivers") ? "drv" : "root");

                string pmlPath = Path.Combine(Environment.CurrentDirectory, $"capture_{timestamp}_{suffix}_{captureCount}.pml");
                captureFiles.Add(pmlPath);

                PrintColor($"\n[*] Capture #{captureCount} using: {procmonPath}", ConsoleColor.Cyan);
                AddToHTMLReport("info", $"Capture #{captureCount} using: {procmonPath}", ConsoleColor.Cyan);

                try { if (File.Exists(pmlPath)) File.Delete(pmlPath); } catch { }

                // CHECK MEMORY BEFORE START
                long memBefore = GetCurrentProcessMemory();

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
                    AddToHTMLReport("error", $"Failed to start Procmon from: {procmonPath}", ConsoleColor.Red);
                    continue;
                }

                // Monitor memory during capture
                for (int i = 60; i > 0; i -= 10)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write($"\r    [{i,2} seconds remaining]  ");
                    Console.ResetColor();

                    // Sample memory every 10 seconds
                    if (i % 20 == 0)
                    {
                        long currentMem = GetCurrentProcessMemory();
                        AddToHTMLReport("info", $"Memory usage at {i}s: {FormatBytes(currentMem)}", ConsoleColor.Cyan);
                    }

                    Thread.Sleep(10000);
                }
                Console.WriteLine();

                PrintColor("[*] Stopping capture...", ConsoleColor.Cyan);
                var stopProc = Process.Start(procmonPath, "/Terminate");
                if (stopProc != null)
                {
                    stopProc.WaitForExit(15000);
                    stopProc.Close();
                }

                Thread.Sleep(5000);

                // CHECK MEMORY AFTER STOP
                long memAfter = GetCurrentProcessMemory();
                long memDiff = memAfter - memBefore;

                if (memDiff > 10 * 1024 * 1024) // More than 10MB leak
                {
                    string leakMsg = $"⚠️ POTENTIAL MEMORY LEAK: {FormatBytes(memDiff)} increase";
                    PrintColor(leakMsg, ConsoleColor.Red);
                    memoryAnomalies.Add(leakMsg);
                    AddToHTMLReport("error", leakMsg, ConsoleColor.Red);
                }
                else
                {
                    string okMsg = $"✅ Memory stable: {FormatBytes(memDiff)} change";
                    PrintColor(okMsg, ConsoleColor.Green);
                    AddToHTMLReport("info", okMsg, ConsoleColor.Green);
                }

                if (File.Exists(pmlPath))
                {
                    FileInfo fi = new FileInfo(pmlPath);
                    string successMsg = $"Capture saved: {pmlPath} ({fi.Length:N0} bytes)";
                    PrintColor($"    ✅ {successMsg}", ConsoleColor.Green);
                    AddToHTMLReport("success", successMsg, ConsoleColor.Green);
                }
                else
                {
                    string failMsg = $"Capture #{captureCount} failed - no PML file";
                    PrintColor($"    ❌ {failMsg}", ConsoleColor.Red);
                    AddToHTMLReport("error", failMsg, ConsoleColor.Red);
                }

                if (captureProc != null && !captureProc.HasExited)
                {
                    try { captureProc.Kill(); } catch { }
                    captureProc.Close();
                }
                PrintColor($"    ℹ️ Procmon instance #{captureCount} closed", ConsoleColor.Cyan);
            }

            // DRIVER MEMORY AUDIT
            PrintColor("\n[*] Starting Driver Memory Audit...", ConsoleColor.Cyan);
            AddToHTMLReport("h3", "Driver Memory Audit", ConsoleColor.Cyan);
            AuditDriverMemory();

            // FINAL RESULTS
            Console.WriteLine();
            PrintSeparator();
            PrintColor($"DLL DIGGER RESULTS - {captureCount} capture(s) completed", ConsoleColor.Yellow);
            PrintSeparator();

            if (memoryAnomalies.Count > 0)
            {
                PrintColor($"\n⚠️ MEMORY ANOMALIES DETECTED: {memoryAnomalies.Count}", ConsoleColor.Red);
                foreach (var anomaly in memoryAnomalies)
                {
                    PrintColor($"    - {anomaly}", ConsoleColor.Red);
                }
            }
            else
            {
                PrintColor("\n✅ No memory anomalies detected", ConsoleColor.Green);
            }

            PrintColor("[✓] Scan complete. All capture files are saved.", ConsoleColor.Green);
            PrintColor("[*] The researcher can now open the files with Procmon GUI.", ConsoleColor.Cyan);
            PrintColor("[*] Files are located in the current directory.", ConsoleColor.Cyan);

            // GENERATE HTML REPORT
            string htmlFile = GenerateHTMLReport(timestamp);
            PrintColor($"\n📊 HTML Report generated: {htmlFile}", ConsoleColor.Cyan);

            PrintSeparator();
        }

        // ==================== MEMORY LEAK DETECTION ====================
        private static void CheckMemoryLeaks(string phase)
        {
            try
            {
                var process = Process.GetCurrentProcess();
                long memoryMB = process.PrivateMemorySize64 / (1024 * 1024);
                string memMsg = $"Process memory usage ({phase}): {memoryMB} MB ({FormatBytes(process.PrivateMemorySize64)})";
                PrintColor(memMsg, ConsoleColor.Cyan);
                AddToHTMLReport("info", memMsg, ConsoleColor.Cyan);

                // Check for high memory usage
                if (memoryMB > 500) // More than 500MB
                {
                    string warning = $"⚠️ High memory usage: {memoryMB} MB";
                    memoryAnomalies.Add(warning);
                    AddToHTMLReport("warning", warning, ConsoleColor.Yellow);
                }
            }
            catch (Exception ex)
            {
                AddToHTMLReport("error", $"Memory check failed: {ex.Message}", ConsoleColor.Red);
            }
        }

        private static long GetCurrentProcessMemory()
        {
            try
            {
                var process = Process.GetCurrentProcess();
                return process.PrivateMemorySize64;
            }
            catch
            {
                return 0;
            }
        }

        private static void AuditDriverMemory()
        {
            string driverPath = @"C:\Windows\System32\drivers\";
            if (!Directory.Exists(driverPath))
            {
                AddToHTMLReport("error", "Driver directory not found", ConsoleColor.Red);
                return;
            }

            string[] driverFiles = Directory.GetFiles(driverPath, "*.sys");
            AddToHTMLReport("info", $"Scanning {driverFiles.Length} drivers for memory anomalies", ConsoleColor.Cyan);

            int suspiciousCount = 0;

            foreach (string driver in driverFiles.Take(50)) // Limit to 50 for performance
            {
                try
                {
                    FileInfo fi = new FileInfo(driver);
                    string driverName = Path.GetFileName(driver);

                    // Check file size anomalies (potential memory mapping issues)
                    if (fi.Length > 10 * 1024 * 1024) // Larger than 10MB
                    {
                        string anomaly = $"⚠️ {driverName}: Unusually large driver ({FormatBytes(fi.Length)}) - possible memory issue";
                        memoryAnomalies.Add(anomaly);
                        AddToHTMLReport("warning", anomaly, ConsoleColor.Yellow);
                        suspiciousCount++;
                    }

                    // Check for recently modified drivers (last 7 days)
                    if (fi.LastWriteTime > DateTime.Now.AddDays(-7))
                    {
                        string anomaly = $"⚠️ {driverName}: Recently modified ({fi.LastWriteTime:yyyy-MM-dd}) - review recommended";
                        memoryAnomalies.Add(anomaly);
                        AddToHTMLReport("warning", anomaly, ConsoleColor.Yellow);
                        suspiciousCount++;
                    }
                }
                catch { }
            }

            AddToHTMLReport("info", $"Found {suspiciousCount} drivers with potential memory issues",
                suspiciousCount > 0 ? ConsoleColor.Yellow : ConsoleColor.Green);
        }

        // ==================== HTML REPORT GENERATION ====================
        private static void InitHTMLReport()
        {
            htmlReport.Clear();
            htmlReport.AppendLine("<!DOCTYPE html>");
            htmlReport.AppendLine("<html lang='en'>");
            htmlReport.AppendLine("<head>");
            htmlReport.AppendLine("    <meta charset='UTF-8'>");
            htmlReport.AppendLine("    <meta name='viewport' content='width=device-width, initial-scale=1.0'>");
            htmlReport.AppendLine("    <title>DLL Digger - Memory Audit Report</title>");
            htmlReport.AppendLine("    <style>");
            htmlReport.AppendLine("        body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background: #0a0e27; color: #00ffcc; margin: 0; padding: 20px; }");
            htmlReport.AppendLine("        .container { max-width: 1200px; margin: 0 auto; background: #0f1235; border-radius: 10px; padding: 20px; box-shadow: 0 0 20px rgba(0,255,204,0.3); }");
            htmlReport.AppendLine("        h1 { color: #ff3366; border-left: 4px solid #ff3366; padding-left: 15px; }");
            htmlReport.AppendLine("        h2 { color: #ffcc00; border-bottom: 2px solid #ffcc00; padding-bottom: 10px; }");
            htmlReport.AppendLine("        h3 { color: #00ffcc; margin-top: 20px; }");
            htmlReport.AppendLine("        .success { color: #00ff88; background: #003322; padding: 10px; border-left: 4px solid #00ff88; margin: 10px 0; }");
            htmlReport.AppendLine("        .error { color: #ff6666; background: #330000; padding: 10px; border-left: 4px solid #ff6666; margin: 10px 0; }");
            htmlReport.AppendLine("        .warning { color: #ffaa00; background: #332200; padding: 10px; border-left: 4px solid #ffaa00; margin: 10px 0; }");
            htmlReport.AppendLine("        .info { color: #88ccff; background: #001133; padding: 10px; border-left: 4px solid #88ccff; margin: 10px 0; }");
            htmlReport.AppendLine("        .summary { background: #1a1f4e; padding: 15px; border-radius: 8px; margin: 20px 0; }");
            htmlReport.AppendLine("        .summary h3 { margin-top: 0; }");
            htmlReport.AppendLine("        table { width: 100%; border-collapse: collapse; margin: 15px 0; }");
            htmlReport.AppendLine("        th, td { border: 1px solid #00ffcc33; padding: 8px; text-align: left; }");
            htmlReport.AppendLine("        th { background: #00ffcc22; color: #00ffcc; }");
            htmlReport.AppendLine("        .timestamp { color: #888; font-size: 0.9em; text-align: right; }");
            htmlReport.AppendLine("        hr { border-color: #00ffcc33; }");
            htmlReport.AppendLine("    </style>");
            htmlReport.AppendLine("</head>");
            htmlReport.AppendLine("<body>");
            htmlReport.AppendLine("<div class='container'>");
            htmlReport.AppendLine($"    <h1>🐭 DLL Digger - Memory & Driver Audit Report</h1>");
            htmlReport.AppendLine($"    <div class='timestamp'>Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}</div>");
            htmlReport.AppendLine("    <hr>");
        }

        private static void AddToHTMLReport(string type, string message, ConsoleColor color = ConsoleColor.White)
        {
            string cssClass = type switch
            {
                "success" => "success",
                "error" => "error",
                "warning" => "warning",
                "h1" => "",
                "h2" => "",
                "h3" => "",
                _ => "info"
            };

            if (type == "h1")
                htmlReport.AppendLine($"    <h1>{message}</h1>");
            else if (type == "h2")
                htmlReport.AppendLine($"    <h2>{message}</h2>");
            else if (type == "h3")
                htmlReport.AppendLine($"    <h3>{message}</h3>");
            else
                htmlReport.AppendLine($"    <div class='{cssClass}'>📌 {message}</div>");
        }

        private static string GenerateHTMLReport(string timestamp)
        {
            // Add summary section
            htmlReport.AppendLine("    <div class='summary'>");
            htmlReport.AppendLine("        <h3>📊 Memory Audit Summary</h3>");
            htmlReport.AppendLine($"        <p>Total memory anomalies detected: <strong>{memoryAnomalies.Count}</strong></p>");
            htmlReport.AppendLine("    </div>");

            if (memoryAnomalies.Count > 0)
            {
                htmlReport.AppendLine("    <h3>⚠️ Detailed Memory Anomalies</h3>");
                htmlReport.AppendLine("    <table>");
                htmlReport.AppendLine("        <tr><th>#</th><th>Anomaly Description</th></tr>");
                for (int i = 0; i < memoryAnomalies.Count; i++)
                {
                    htmlReport.AppendLine($"        <tr><td>{i + 1}</td><td>{memoryAnomalies[i]}</td></tr>");
                }
                htmlReport.AppendLine("    </table>");
            }
            else
            {
                htmlReport.AppendLine("    <div class='success'>✅ No memory anomalies detected during scan</div>");
            }

            // Close HTML
            htmlReport.AppendLine("    <hr>");
            htmlReport.AppendLine("    <div class='timestamp'>🐭 DLL Digger by nu11secur1ty | Memory Protection Active</div>");
            htmlReport.AppendLine("</div>");
            htmlReport.AppendLine("</body>");
            htmlReport.AppendLine("</html>");

            string reportFile = Path.Combine(Environment.CurrentDirectory, $"DLLDigger_MemoryReport_{timestamp}.html");
            File.WriteAllText(reportFile, htmlReport.ToString(), Encoding.UTF8);
            return reportFile;
        }

        private static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
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
