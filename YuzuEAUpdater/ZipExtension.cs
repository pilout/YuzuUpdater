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
            var platForm = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture;
            var OS = RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "Linux" : RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Windows" : "MacOS";
            bool check = File.Exists(Environment.CurrentDirectory + "/7zip/win32/x64/7zxa.dll");
            if (OS == "Windows")
            {
                if (platForm == Architecture.X64)
                {
                    SevenZipBase.SetLibraryPath(Environment.CurrentDirectory+ "/7zip/win32/x64/7zxa.dll");
                }
                else if (platForm == Architecture.X86)
                {
                    SevenZipBase.SetLibraryPath(Environment.CurrentDirectory + "/7zip/win32/ia32/7zxa.dll");
                }
            }
            else if (OS == "Linux")
            {

                if (platForm == Architecture.X64)
                {
                    SevenZipBase.SetLibraryPath(Environment.CurrentDirectory + "/7zip/linux/x64/7zz");
                }
                else if (platForm == Architecture.X86)
                {
                    SevenZipBase.SetLibraryPath(Environment.CurrentDirectory + "/7zip/linux/ia32/7zz");
                }
                else if (platForm == Architecture.Arm)
                {
                    SevenZipBase.SetLibraryPath(Environment.CurrentDirectory + "/7zip/linux/arm/7zz");
                }
                else if (platForm == Architecture.Arm64)
                {
                    SevenZipBase.SetLibraryPath(Environment.CurrentDirectory + "/7zip/linux/arm64/7zz");
                }
            }
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

    }
}
