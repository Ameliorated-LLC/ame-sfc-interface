using System;
using System.IO;
using System.Management;
using System.Timers;
using System.Diagnostics;
using System.Reflection;
using System.Security.Principal;
using System.Text.RegularExpressions;

namespace sfcInterface
{
    class Program
    {
        // v1.1
        private static Process _selfProcess;
        private static string _cmdLine;
        
        static void Main(string[] args)
        {
            if (!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))
            {
                Console.WriteLine("\nYou must be an administrator running a console session in order to\nuse the sfc utility.");
                Environment.Exit(1631);
            }
            
            _cmdLine = string.Join(" ", args);
            var selfPath = Assembly.GetExecutingAssembly().Location;
            var selfDir = Path.GetDirectoryName(selfPath);
            var winDir = Environment.GetEnvironmentVariable("WINDIR");
            
            var sfcPath = "";
            if (!File.Exists($"{selfDir}\\sfc1.exe"))
            {
                if (!File.Exists($"{winDir}\\System32\\sfc1.exe"))
                {
                    Console.WriteLine("\nsfc utility is unavailable.");
                    Environment.Exit(1632);
                } else
                {
                    sfcPath = $"{winDir}\\System32\\sfc1.exe";
                }
            } else
            {
                sfcPath = $"{selfDir}\\sfc1.exe";
            }
            var sfcDir = Path.GetDirectoryName(sfcPath);
            _selfProcess = Process.GetCurrentProcess();
            
            if (Regex.Match(_cmdLine, "[ ]*/scannow[ ]*", RegexOptions.IgnoreCase).Success)
            {
                Console.Write("\nThis command will cause de-amelioration! DO NOT RUN!\nAre you sure you want to run this command?\n\nEnter 'Cancel' to Exit\nEnter 'I know what I'm doing' to Confirm: ");
                
                Timer readWait = new Timer(90000);
                try
                {
                    readWait.Enabled = true;
                    readWait.AutoReset = false;
                    readWait.Elapsed += new ElapsedEventHandler(Warning);
                    readWait.Start();
                }
                catch
                {
                    Console.WriteLine("\n\nTimer error.");
                    Environment.Exit(1638);
                }
                var confirm = Console.ReadLine();
                readWait.Dispose();
                
                if (String.Equals(confirm, "I know what I'm doing"))
                {
                    try
                    {
                        ProcessStartInfo destructInfo = new ProcessStartInfo
                        {
                            FileName = "cmd.exe",
                            Arguments =
                                $"/c \"PowerShell -NoP -C \"Get-Process -Id '{_selfProcess.Id}' | Where-Object {{$_.ProcessName -EQ '{_selfProcess.ProcessName}'}}; takeown /f '{selfPath}' /a; takeown /f '{sfcPath}' /a; takeown /f '{sfcDir}\\en-US\\sfc1.exe.mui' /a; icacls '{selfPath}' /grant Administrators:F; icacls /grant Administrators:F; icacls '{sfcPath}' /grant Administrators:F; icacls /grant Administrators:F; icacls '{sfcDir}\\en-US\\sfc1.exe.mui' /grant Administrators:F; Remove-Item '{selfPath}' -Force; Rename-Item '{sfcPath}' 'sfc.exe' -Force; Rename-Item '{sfcDir}\\en-US\\sfc1.exe.mui' 'sfc.exe.mui' -Force; $SecAcl = Get-Acl '{winDir}\\System32\\diskmgmt.msc'; Set-Acl '{sfcDir}\\sfc.exe' $SecAcl; Set-Acl '{sfcDir}\\en-US\\sfc.exe.mui' $SecAcl\">NUL 2>&1 & \"{sfcDir}\\sfc.exe\" /scannow & echo. & echo Press any key to exit... & pause>NUL\"",
                            UseShellExecute = true,
                            CreateNoWindow = false
                        };
                        Process.Start(destructInfo);
                    }
                    catch
                    {
                        Console.WriteLine("\nFailed to run command.");
                        Environment.Exit(1635);
                    }
                    Environment.Exit(0);
                } else if (String.Equals(confirm, "Cancel", StringComparison.CurrentCultureIgnoreCase))
                {
                    Environment.Exit(0);
                } else
                {
                    Console.WriteLine("\nIncorrect input entered.");
                    Environment.Exit(1637);
                }
            }

            if (_cmdLine.IndexOf("/scanfile=", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Console.Write("\nThis command can cause de-amelioration if Windows Update or Defender files are scanned.\nAre you sure you want to run this command?\n\nEnter 'Cancel' to Exit\nEnter 'Yes' to Confirm: ");
                
                Timer readWait = new Timer(90000);
                try
                {
                    readWait.Enabled = true;
                    readWait.AutoReset = false;
                    readWait.Elapsed += new ElapsedEventHandler(Warning);
                    readWait.Start();
                }
                catch
                {
                    Console.WriteLine("\n\nTimer error.");
                    Environment.Exit(1638);
                }
                var confirm = Console.ReadLine();
                readWait.Dispose();
                
                if (!String.Equals(confirm, "Yes", StringComparison.CurrentCultureIgnoreCase))
                {
                    if (String.Equals(confirm, "Cancel", StringComparison.CurrentCultureIgnoreCase))
                    {
                        Environment.Exit(0);
                    }
                    else
                    {
                        Console.WriteLine("\nIncorrect input entered.");
                        Environment.Exit(1637);
                    }
                }
            }

            ProcessStartInfo sfcInfo = new ProcessStartInfo(sfcPath, _cmdLine);
            sfcInfo.UseShellExecute = false;
            sfcInfo.CreateNoWindow = false;

            Process sfc = new Process
            {
                StartInfo = sfcInfo,
                EnableRaisingEvents = true,
            };
            
            try
            {
                sfc.Start();
            }
            catch
            {
                Console.WriteLine("\nFailed to start sfc utility.");
                Environment.Exit(1636);
            }

            sfc.WaitForExit();
            
            Environment.Exit(sfc.ExitCode);
        }
        static void Warning(object sender, ElapsedEventArgs e)
        {
            try
            {
                var search = new ManagementObjectSearcher("root\\CIMV2", string.Format($"SELECT ParentProcessId FROM Win32_Process WHERE ProcessId = {_selfProcess.Id}"));
                var results = search.Get().GetEnumerator();
                results.MoveNext();
                var parentId = (uint)results.Current["ParentProcessId"];
                var parentProcess = Process.GetProcessById((int)parentId);
                
                ProcessStartInfo warningMsg = new ProcessStartInfo("msg.exe", $"* /time:86400 \"{parentProcess.ProcessName}.exe was prevented from running:\nsfc {_cmdLine}\"");
                warningMsg.UseShellExecute = false;
                warningMsg.CreateNoWindow = true;

                Process.Start(warningMsg);
            }
            catch
            {
                Console.WriteLine("\n\nMessage error.\nTimed out.");
                Environment.Exit(1634);
            }
            Console.WriteLine("\n\nTimed out.");
            Environment.Exit(1633);
        }
    }
}