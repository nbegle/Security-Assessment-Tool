# SecurityAssessmentTool

## Overview

**SecurityAssessmentTool** is a C# console application designed to automate the installation of Nmap, retrieve the machine’s external IP address, and perform a basic Nmap service/version scan against that IP. The results are saved in a styled HTML report for easy viewing and distribution.

This tool is ideal for security engineers or administrators who want a quick way to assess their public-facing services and verify exposure.

---

## Features

- ✅ Automatically downloads and silently installs Nmap
- 🌐 Retrieves external IP address using `api.ipify.org`
- 🔍 Executes an Nmap scan (`-sV`) to enumerate services and versions
- 📄 Generates a clean, formatted HTML report of the scan results

---

## Prerequisites

- Windows OS
- [.NET SDK 6.0+](https://dotnet.microsoft.com/download)
- Internet connection
- Administrator privileges (for silent Nmap installation)

---

## How It Works

1. **Installation Phase**
   - Downloads the Nmap Windows installer to `C:\Temp`
   - Executes a silent install as administrator

2. **Scanning Phase**
   - Uses `https://api.ipify.org` to detect the external IP address
   - Executes `nmap -sV [IP]` using the installed binary
   - Captures and parses the output

3. **Reporting Phase**
   - Encodes the output into an HTML file (`output.html`)
   - Saves the report in the application's working directory

---

## How to Use

### 1. Clone or Download the Repo

```bash
git clone https://github.com/your-org/SecurityAssessmentTool.git
cd SecurityAssessmentTool
```

### 2. Build the Project

Use the .NET CLI:

```bash
dotnet build
```

Or open the `.csproj` in Visual Studio and build the solution.

### 3. Run the Tool

```bash
dotnet run
```

> 🛑 **Important:** Run the terminal as an Administrator to ensure the Nmap installer executes successfully.

---

## Output

- `output.html`: A self-contained, readable report containing the scan results.
- Logs and status messages are printed directly to the console.

---

## Example

```bash
Checking external IP address...
External IP: 198.51.100.12
Running nmap scan...
HTML file written to: C:\Users\YourUser\output.html
```

Open `output.html` in your browser to review the scan output.

---

## Troubleshooting

- If Nmap fails to install, ensure the machine has internet access and that UAC allows elevation.
- If the scan fails, verify that Nmap is installed and that the binary path is available in the system's `PATH`.
- Ensure firewall settings allow outbound HTTP requests to `api.ipify.org`.

---


## Disclaimer

This tool is intended for **authorized use only**. Scanning systems without explicit permission is illegal and unethical. Always comply with your organization's policy and applicable laws.
