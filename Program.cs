using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32;

namespace SecurityAssessmentTool
{
    class Program
    {
        private static readonly HttpClient httpClient = new HttpClient();

        static async Task Main(string[] args)
        {
            var assessmentResults = new StringBuilder();
            
            try
            {
                // Check Windows Update automation first
                Console.WriteLine("Checking Windows Update automation...");
                string windowsUpdateResults = await CheckWindowsUpdateAutomation();
                assessmentResults.AppendLine("Windows Update Assessment:");
                assessmentResults.AppendLine(windowsUpdateResults);
                Console.WriteLine("Windows Update check completed.");

                // Download and install Nmap
                await DownloadAndInstallNmap();

                // Check admin privileges
                Console.WriteLine("Checking admin privileges...");
                string adminStatus = CheckAdminPrivileges();
                assessmentResults.AppendLine($"Admin Privileges: {adminStatus}");
                Console.WriteLine($"Admin status: {adminStatus}");

                // Check password policy
                Console.WriteLine("Checking password policy...");
                string passwordPolicy = await CheckPasswordPolicy();
                assessmentResults.AppendLine($"Password Policy:\n{passwordPolicy}");
                Console.WriteLine("Password policy check completed.");

                // Check external IP and run nmap scan
                Console.WriteLine("Checking external IP address...");
                string externalIP = await GetExternalIPAddress();
                
                if (!string.IsNullOrEmpty(externalIP))
                {
                    Console.WriteLine($"External IP: {externalIP}");
                    Console.WriteLine("Running nmap scan...");
                    string nmapResults = await RunNmapScan(externalIP);
                    assessmentResults.AppendLine($"Nmap Scan Results:\n{nmapResults}");
                    
                    // Generate comprehensive HTML report
                    await GenerateSecurityReport(externalIP, adminStatus, passwordPolicy, nmapResults, windowsUpdateResults);
                }       
                else
                {
                    Console.WriteLine("Failed to retrieve external IP address.");
                    assessmentResults.AppendLine("Failed to retrieve external IP address for nmap scan.");
                }

                Console.WriteLine("\nSecurity assessment completed.");
                Console.WriteLine(assessmentResults.ToString());
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

        static async Task<string> CheckWindowsUpdateAutomation()
        {
            try
            {
                var results = new StringBuilder();

                // Check Windows Update service status
                var serviceCheckInfo = new ProcessStartInfo
                {
                    FileName = "sc",
                    Arguments = "query wuauserv",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var serviceProcess = Process.Start(serviceCheckInfo);
                if (serviceProcess != null)
                {
                    string serviceOutput = await serviceProcess.StandardOutput.ReadToEndAsync();
                    await serviceProcess.WaitForExitAsync();

                    results.AppendLine("Windows Update Service Status:");
                    results.AppendLine(serviceOutput);
                }

                // Check Windows Update settings via registry
                try
                {
                    using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU"))
                    {
                        if (key != null)
                        {
                            var noAutoUpdate = key.GetValue("NoAutoUpdate");
                            var auOptions = key.GetValue("AUOptions");

                            results.AppendLine("\nWindows Update Policy Settings:");
                            results.AppendLine($"NoAutoUpdate: {noAutoUpdate ?? "Not set"}");
                            results.AppendLine($"AUOptions: {auOptions ?? "Not set"}");

                            // Interpret the settings
                            if (noAutoUpdate != null && noAutoUpdate.ToString() == "1")
                            {
                                results.AppendLine("\n? HIGH RISK: Automatic updates are disabled");
                            }
                            else if (auOptions != null)
                            {
                                switch (auOptions.ToString())
                                {
                                    case "2":
                                        results.AppendLine("\nWARNING: Updates download but require manual installation");
                                        break;
                                    case "3":
                                        results.AppendLine("\nWARNING: Updates download and notify for installation");
                                        break;
                                    case "4":
                                        results.AppendLine("\nGOOD: Automatic updates are enabled");
                                        break;
                                    case "5":
                                        results.AppendLine("\nGOOD: Local admin can choose settings, auto-updates enabled");
                                        break;
                                    default:
                                        results.AppendLine($"\nUNKNOWN: AUOptions value {auOptions} - manual review needed");
                                        break;
                                }
                            }
                            else
                            {
                                results.AppendLine("\nLIKELY GOOD: Default Windows Update behavior (typically automatic)");
                            }
                        }
                        else
                        {
                            results.AppendLine("\nNo Windows Update policies found - using default settings");
                            results.AppendLine("LIKELY GOOD: Default Windows Update behavior (typically automatic)");
                        }
                    }
                }
                catch (Exception regEx)
                {
                    results.AppendLine($"\nError reading registry settings: {regEx.Message}");
                }

                // Check for pending updates
                var updateCheckInfo = new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = "-Command \"Get-WUList -MicrosoftUpdate | Select-Object Title, Size | Format-Table -AutoSize\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                try
                {
                    using var updateProcess = Process.Start(updateCheckInfo);
                    if (updateProcess != null)
                    {
                        string updateOutput = await updateProcess.StandardOutput.ReadToEndAsync();
                        string updateError = await updateProcess.StandardError.ReadToEndAsync();
                        await updateProcess.WaitForExitAsync();

                        if (!string.IsNullOrEmpty(updateOutput) && !updateOutput.Contains("not recognized"))
                        {
                            results.AppendLine("\nPending Updates Check:");
                            results.AppendLine(updateOutput);
                        }
                        else
                        {
                            results.AppendLine("\nPending Updates: Unable to check (PSWindowsUpdate module may not be installed)");
                        }
                    }
                }
                catch
                {
                    results.AppendLine("\nPending Updates: Unable to check via PowerShell");
                }

                return results.ToString();
            }
            catch (Exception ex)
            {
                return $"Error checking Windows Update automation: {ex.Message}";
            }
        }

        static async Task DownloadAndInstallNmap()
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

                Console.WriteLine("Downloading Nmap installer...");

                using (var client = new HttpClient())
                {
                    var response = await client.GetAsync(nmapInstallerUrl);
                    response.EnsureSuccessStatusCode();

                    using (var fs = new FileStream(installerPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        await response.Content.CopyToAsync(fs);
                    }
                }

                Console.WriteLine("Download complete.");
                Console.WriteLine("Running Nmap installer...");

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

        static string CheckAdminPrivileges()
        {
            try
            {
                using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                {
                    WindowsPrincipal principal = new WindowsPrincipal(identity);
                    bool isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
                    
                    if (isAdmin)
                    {
                        return "Current user has LOCAL ADMINISTRATOR privileges - HIGH SECURITY RISK";
                    }
                    else
                    {
                        return "Current user does not have administrator privileges - OK";
                    }
                }
            }
            catch (Exception ex)
            {
                return $"Error checking admin privileges: {ex.Message}";
            }
        }

        static async Task<string> CheckPasswordPolicy()
        {
            try
            {
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = "net",
                    Arguments = "accounts",
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
                        return output;
                    }
                    else if (!string.IsNullOrEmpty(error))
                    {
                        return $"Error retrieving password policy: {error}";
                    }
                }
                
                return "Unable to retrieve password policy information.";
            }
            catch (Exception ex)
            {
                return $"Error checking password policy: {ex.Message}";
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

        static async Task<string> RunNmapScan(string ipAddress)
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
                        return output;
                    }

                    if (!string.IsNullOrEmpty(error))
                    {
                        Console.WriteLine("Nmap errors:");
                        Console.WriteLine(error);
                        return $"Nmap scan errors: {error}";
                    }
                }
                
                return "No nmap output received.";
            }
            catch (Exception ex)
            {
                string errorMsg = $"Error running nmap scan: {ex.Message}\nMake sure nmap is installed and available in PATH.";
                Console.WriteLine(errorMsg);
                return errorMsg;
            }
        }

        static async Task GenerateSecurityReport(string ipAddress, string adminStatus, string passwordPolicy, string nmapResults, string windowsUpdateResults)
        {
            try
            {
                string htmlContent = $@"
<!DOCTYPE html>
<html>
<head>
    <title>Security Assessment Report</title>
    <style>
        body {{ font-family: Arial, sans-serif; background-color: #f5f5f5; margin: 20px; }}
        .container {{ max-width: 1200px; margin: 0 auto; background-color: white; padding: 20px; border-radius: 8px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }}
        h1 {{ color: #d32f2f; text-align: center; border-bottom: 2px solid #d32f2f; padding-bottom: 10px; }}
        h2 {{ color: #1976d2; margin-top: 30px; }}
        .section {{ margin-bottom: 30px; padding: 15px; border-left: 4px solid #1976d2; background-color: #f8f9fa; }}
        .admin-risk {{ background-color: #ffebee; border-left-color: #d32f2f; }}
        .admin-ok {{ background-color: #e8f5e8; border-left-color: #4caf50; }}
        pre {{ background-color: #f4f4f4; padding: 15px; border: 1px solid #ddd; border-radius: 4px; overflow-x: auto; white-space: pre-wrap; }}
        .timestamp {{ text-align: center; color: #666; font-size: 14px; margin-top: 20px; }}
        .risk-high {{ color: #d32f2f; font-weight: bold; }}
        .risk-ok {{ color: #4caf50; font-weight: bold; }}
    </style>
</head>
<body>
    <div class='container'>
        <h1>Security Assessment Report</h1>
        
        <div class='section'>
            <h2>Windows Update Assessment</h2>
            <pre>{WebUtility.HtmlEncode(windowsUpdateResults)}</pre>
        </div>
        
        <div class='section {(adminStatus.Contains("HIGH SECURITY RISK") ? "admin-risk" : "admin-ok")}'>
            <h2>Administrator Privileges Check</h2>
            <p class='{(adminStatus.Contains("HIGH SECURITY RISK") ? "risk-high" : "risk-ok")}'>{WebUtility.HtmlEncode(adminStatus)}</p>
        </div>

        <div class='section'>
            <h2>Password Policy Assessment</h2>
            <pre>{WebUtility.HtmlEncode(passwordPolicy)}</pre>
        </div>

        <div class='section'>
            <h2>Network Scan Results (External IP: {WebUtility.HtmlEncode(ipAddress)})</h2>
            <pre>{WebUtility.HtmlEncode(nmapResults)}</pre>
        </div>

        <div class='timestamp'>
            Report generated on: {DateTime.Now:yyyy-MM-dd HH:mm:ss}
        </div>
    </div>
</body>
</html>";

                string outputPath = "security_assessment_report.html";
                await File.WriteAllTextAsync(outputPath, htmlContent);
                Console.WriteLine($"Security assessment report written to: {Path.GetFullPath(outputPath)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating security report: {ex.Message}");
            }
        }
    }
}