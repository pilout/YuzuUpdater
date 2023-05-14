using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Compression;

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
    }
}
