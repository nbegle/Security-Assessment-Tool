using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace SecurityAssessmentTool
{
    class Program
    {
       
        static async Task InstallNMAP()
        {
            try
            {
                string tempDir = @"C:\Temp";
                string nmapInstallerUrl = "https://nmap.org/dist/nmap-7.94-setup.exe";
                string installerPath = Path.Combine(tempDir, "nmap-setup.exe");

                // Ensure temp directory exists
                if (!Directory.Exists(tempDir))
                {
                    Directory.CreateDirectory(tempDir);
                }

                Console.WriteLine("⬇️ Downloading Nmap installer...");

                using (var client = new HttpClient())
                {
                    var response = await client.GetAsync(nmapInstallerUrl);
                    response.EnsureSuccessStatusCode();

                    using (var fs = new FileStream(installerPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        await response.Content.CopyToAsync(fs);
                    }
                }

                Console.WriteLine("✅ Download complete.");
                Console.WriteLine("🚀 Running Nmap installer...");

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = installerPath,
                        Arguments = "/S", // silent install switch (may vary based on installer)
                        UseShellExecute = true,
                        Verb = "runas" // run as administrator
                    }
                };

                process.Start();
                process.WaitForExit();

                Console.WriteLine("Nmap installation completed.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error installing Nmap: {ex.Message}");
            }
        }

        private static readonly HttpClient httpClient = new HttpClient();

        static async Task Main(string[] args)
        {
            try
            {
                Console.WriteLine("Checking external IP address...");
                string externalIP = await GetExternalIPAddress();
                
                if (!string.IsNullOrEmpty(externalIP))
                {
                    Console.WriteLine($"External IP: {externalIP}");
                    Console.WriteLine("Running nmap scan...");
                    await RunNmapScan(externalIP);
                }       
                else
                {
                    Console.WriteLine("Failed to retrieve external IP address.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
            finally
            {
                httpClient.Dispose();
            }
        }

        static async Task<string> GetExternalIPAddress()
        {
            try
            {
                var response = await httpClient.GetStringAsync("https://api.ipify.org");
                return response.Trim();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting external IP: {ex.Message}");
                return string.Empty;
            }
        }

        static async Task RunNmapScan(string ipAddress)
        {
            try
            {
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = "nmap",
                    Arguments = $"-sV {ipAddress}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processStartInfo);
                if (process != null)
                {
                    string output = await process.StandardOutput.ReadToEndAsync();
                    string error = await process.StandardError.ReadToEndAsync();
                    
                    await process.WaitForExitAsync();

                    if (!string.IsNullOrEmpty(output))
                    {
                        string scanResults = output;
                        string htmlContent = $@"
<!DOCTYPE html>
<html>
<head>
    <title>NMAPScanInformation</title>
    <style>
        body {{ font-family: Arial; background-color: #f0f0f0; }}
        h1 {{ color: #333; }}
        pre {{ background-color: white; padding: 10px; border: 1px solid #ccc; }}
    </style>
</head>
<body>
    <h1>Nmap Scan Results for {ipAddress}</h1>
    <pre>{WebUtility.HtmlEncode(scanResults)}</pre>
</body>
</html>";

                        string outputPath = "output.html";
                        await File.WriteAllTextAsync(outputPath, htmlContent);
                        Console.WriteLine($"HTML file written to: {Path.GetFullPath(outputPath)}");
                    }

                    if (!string.IsNullOrEmpty(error))
                    {
                        Console.WriteLine("Nmap errors:");
                        Console.WriteLine(error);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error running nmap scan: {ex.Message}");
                Console.WriteLine("Make sure nmap is installed and available in PATH.");
            }
        }
    }
}
