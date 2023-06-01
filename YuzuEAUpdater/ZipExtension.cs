using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Compression;
using System.Net.Security;
using System.Net;
using System.Runtime.InteropServices;
using SevenZip;
using System.Net.Http;
using System.Threading;
using System.Diagnostics;
using System.ServiceProcess;

namespace YuzuEAUpdater
{
        public static class Utils
        {
            public static void ExtractToDirectory(this ZipArchive archive, string destinationDirectoryName, bool overwrite)
            {
             
                if (!overwrite)
                {
                    archive.ExtractToDirectory(destinationDirectoryName);
                    return;
                }

                DirectoryInfo di = Directory.CreateDirectory(destinationDirectoryName);
                string destinationDirectoryFullPath = di.FullName;

                foreach (ZipArchiveEntry file in archive.Entries)
                {
                    string completeFileName = Path.GetFullPath(Path.Combine(destinationDirectoryFullPath, file.FullName));
                    string directory = Path.GetDirectoryName(completeFileName);

                    if (!Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    if (!completeFileName.StartsWith(destinationDirectoryFullPath, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new IOException("Trying to extract file outside of destination directory. See this link for more info: https://snyk.io/research/zip-slip-vulnerability");
                    }

                    

                    if (file.Name == "")
                    {// Assuming Empty for Directory
                        Directory.CreateDirectory(Path.GetDirectoryName(completeFileName));
                        continue;
                    }

                    file.ExtractToFile(completeFileName, true);

                }
            }

        public static void DirectoryCopyAndDelete(string strSource, string Copy_dest)
        {
            DirectoryInfo dirInfo = new DirectoryInfo(strSource);

            DirectoryInfo[] directories = dirInfo.GetDirectories();

            FileInfo[] files = dirInfo.GetFiles();

            foreach (DirectoryInfo tempdir in directories)
            {

                Directory.CreateDirectory(Copy_dest + "/" + tempdir.Name);// creating the Directory   

                var ext = System.IO.Path.GetExtension(tempdir.Name);

                if (System.IO.Path.HasExtension(ext))
                {
                    foreach (FileInfo tempfile in files)
                    {
                        tempfile.CopyTo(Path.Combine(strSource + "/" + tempfile.Name, Copy_dest + "/" + tempfile.Name),true);
                        File.Delete(tempfile.FullName);
                    }
                }
                DirectoryCopyAndDelete(strSource + "/" + tempdir.Name, Copy_dest + "/" + tempdir.Name);
            }

            FileInfo[] files1 = dirInfo.GetFiles();

            foreach (FileInfo tempfile in files1)
            {
                tempfile.CopyTo(Path.Combine(Copy_dest, tempfile.Name),true);
                File.Delete(tempfile.FullName);

            }
            Directory.Delete(dirInfo.FullName);
        }


        public static void init7ZipPaht()
        {
            var platForm = RuntimeInformation.OSArchitecture;
            var OS = RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "Linux" : RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Windows" : "MacOS";
            bool check = File.Exists(Path.Combine(Environment.CurrentDirectory, "7zip", OS == "Windows" ? "win32" : "linux", platForm == Architecture.X64 ? "x64" : platForm == Architecture.Arm64 ? "arm64" : platForm == Architecture.Arm ? "arm" : "ia32", OS == "Windows" ? "7za.dll" : "7zz"));
            SevenZipBase.SetLibraryPath(Path.Combine(Environment.CurrentDirectory, "7zip", OS == "Windows" ? "win32" : "linux", platForm == Architecture.X64 ? "x64" : platForm == Architecture.Arm64 ? "arm64" : platForm == Architecture.Arm ? "arm" : "ia32", OS == "Windows" ? "7za.dll" : "7zz"));
        }


        public static async Task DownloadAsync(this HttpClient client, string requestUri, Stream destination, IProgress<float> progress = null, CancellationToken cancellationToken = default)
        {
            // Get the http headers first to examine the content length
            using (var response = await client.GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead))
            {
                var contentLength = response.Content.Headers.ContentLength;

                using (var download = await response.Content.ReadAsStreamAsync())
                {

                    // Ignore progress reporting when no progress reporter was 
                    // passed or when the content length is unknown
                    if (progress == null || !contentLength.HasValue)
                    {
                        await download.CopyToAsync(destination);
                        return;
                    }

                    // Convert absolute progress (bytes downloaded) into relative progress (0% - 100%)
                    var relativeProgress = new Progress<long>(totalBytes => progress.Report((float)totalBytes / contentLength.Value));
                    // Use extension method to report progress while downloading
                    await download.CopyToAsync(destination, 81920, relativeProgress, cancellationToken);
                    progress.Report(1);
                }
            }
        }

            public static async Task CopyToAsync(this Stream source, Stream destination, int bufferSize, IProgress<long> progress = null, CancellationToken cancellationToken = default)
            {
                if (source == null)
                    throw new ArgumentNullException(nameof(source));
                if (!source.CanRead)
                    throw new ArgumentException("Has to be readable", nameof(source));
                if (destination == null)
                    throw new ArgumentNullException(nameof(destination));
                if (!destination.CanWrite)
                    throw new ArgumentException("Has to be writable", nameof(destination));
                if (bufferSize < 0)
                    throw new ArgumentOutOfRangeException(nameof(bufferSize));

                var buffer = new byte[bufferSize];
                long totalBytesRead = 0;
                int bytesRead;
                while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) != 0)
                {
                    await destination.WriteAsync(buffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);
                    totalBytesRead += bytesRead;
                    progress?.Report(totalBytesRead);
                }
            }


        public static void setAffinityMask(Process p)
        {
            // Récupération du nombre de processeurs logiques sur le système
            int processorCount = Environment.ProcessorCount;

            if (processorCount < 5)
                return;

            // Calcul du masque d'affinité en utilisant tous les threads disponibles
            long affinityMask = 0;
            for (int i = 0; i < processorCount; i++)
            {
                if (i % 2 == 0) // Activer chaque autre thread
                {
                    affinityMask |= 1L << i;
                }
            }
            p.ProcessorAffinity= (IntPtr)affinityMask;
        }

        public static void SetPowerSavingMode(bool enable)
        {
            try
            {
                const int SC_MONITORPOWER = 0xF170;
                const int WM_SYSCOMMAND = 0x0112;

                IntPtr HWND_BROADCAST = new IntPtr(0xffff);

                int monitorState = enable ? 2 : -1; // 2 = POWER_ON, -1 = POWER_OFF

                NativeMethods.SendMessage(HWND_BROADCAST, WM_SYSCOMMAND, SC_MONITORPOWER, monitorState);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error setting power saving mode: " + ex.Message);
            }
        }


        public static void KillProcessesByCpuUsage(float cpuUsageThreshold)
        {
            Process[] processes = Process.GetProcesses();

            foreach (Process process in processes)
            {
                try
                {
                    if (process.Id == Process.GetCurrentProcess().Id)
                        continue;

                    if (process.TotalProcessorTime.TotalSeconds > TimeSpan.FromSeconds(cpuUsageThreshold).TotalSeconds)
                    {
                        UI.addTextConsole($"Terminating process: {process.ProcessName} (ID: {process.Id})\n");
                        process.Kill();
                       
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error terminating process: {process.ProcessName} (ID: {process.Id})");
                    Console.WriteLine(ex.Message);
                }
            }
        }




        internal static class NativeMethods
        {
            [System.Runtime.InteropServices.DllImport("user32.dll")]
            internal static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        }
    }
}
