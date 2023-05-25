using SevenZip;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.IO;
using System.Net.Http;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Linq;
namespace YuzuEAUpdater
{


    public class BananaMod
    {
        public int _idRow { get; set; }
        public string _sModelName { get; set; }
        public string _sSingularTitle { get; set; }
        public string _sIconClasses { get; set; }
        public string _sName { get; set; }
        public string _sProfileUrl { get; set; }
        public long _tsDateAdded { get; set; }
        public long _tsDateModified { get; set; }
        public bool _bHasFiles { get; set; }
        public string[] _aTags { get; set; }
        public string _sVersion { get; set; }
        public long _tsDateUpdated { get; set; }
        public bool _bIsObsolete { get; set; }
        public string _sInitialVisibility { get; set; }
        public bool _bHasContentRatings { get; set; }
        public int _nLikeCount { get; set; }
        public int _nPostCount { get; set; }
        public bool _bWasFeatured { get; set; }
        public int _nViewCount { get; set; }
        public bool _bIsOwnedByAccessor { get; set; }

        public List<BananaFile> files;

        public string pathApp;


        public void loadFiles()
        {
            HttpClient _httpClient = new HttpClient();
            String uri = "https://gamebanana.com/apiv11/Mod/" + _idRow + "/Files";
            String src = _httpClient.GetAsync(uri).Result.Content.ReadAsStringAsync().Result;
            files = JsonSerializer.Deserialize<List<BananaFile>>(src);

        }

        public void download()
        {
            if (!Directory.Exists("_tempMod"))
            {
                Directory.CreateDirectory("_tempMod");
            }

            if (files == null)
                loadFiles();

            foreach (BananaFile file in files)
            {
                if (file._sClamAvResult == "clean")
                {
                    using (var client = new WebClient())
                    {
                        if (file._sFile.EndsWith(".zip") || file._sFile.EndsWith(".7z"))
                        {
                            UI.addTextConsole("       -Downloading " + file._sFile + "\n");
                            if (System.IO.File.Exists("_tempMod/" + file._sFile))
                            {
                                System.IO.File.Delete("_tempMod/" + file._sFile);
                            }

                            try
                            {
                                client.DownloadFile(file._sDownloadUrl, "_tempMod/" + file._sFile);
                            }
                            catch (Exception e)
                            {
                                UI.addTextConsole("Error while downloading " + file._sFile + " : " + e.Message + "\n");
                            }
                        }
                    }
                }
            }

        }

        public void extract()
        {

            foreach (BananaFile f in files)
            {
                if (f._sFile.EndsWith(".zip") || f._sFile.EndsWith(".7z"))
                {
                    UI.addTextConsole("       -Extracting " + f._sFile + "\n");
                    try
                    {
                        if (f._sFile.EndsWith(".zip"))
                        {
                            ZipArchive zipArchive = ZipFile.OpenRead("_tempMod/" + f._sFile);
                            bool isCorrectModFolder = zipArchive.Entries.ToList().FirstOrDefault(f => f.FullName.Contains("romfs") || f.FullName.Contains("exefs") || f.FullName.Contains("cheats")) != null;
                            if (isCorrectModFolder)
                                ZipFile.ExtractToDirectory("_tempMod/" + f._sFile, pathApp, true);

                            zipArchive.Dispose();
                        }
                        else
                        {
                            SevenZipExtractor extractor = new SevenZipExtractor("_tempMod/" + f._sFile);
                            bool isCorrectModFolder = extractor.ArchiveFileNames.ToList().FirstOrDefault(f => f.Contains("romfs") || f.Contains("exefs") || f.Contains("cheats")) != null;
                            if (isCorrectModFolder)
                                extractor.ExtractArchive(pathApp);

                            extractor.Dispose();
                        }
                        System.Threading.Thread.Sleep(1000);
                    }
                    catch (Exception e)
                    {
                        UI.addTextConsole("Error while extracting " + f._sFile + " : " + e.Message);
                    }
                    finally
                    {
                        if (System.IO.File.Exists("_tempMod/" + f._sFile))
                        {
                            System.IO.File.Delete("_tempMod/" + f._sFile);
                        }
                    }
                }
            }
        }
    
            override
            public string ToString()
            {
                return _sName;
            }

    }
}
