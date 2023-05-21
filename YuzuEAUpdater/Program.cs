using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using SevenZip;
using SevenZip.Sdk.Compression.Lzma;

namespace YuzuEAUpdater
{
    internal class Program
    {
        private static List<Release> releases = new List<Release>();
        private static List<PR> prList = new List<PR>();

        private static  string currentVersion = null;
        private static Release myCurrentRelease = null;
        private static string currentExe = "yuzu.exe";
        private static List<Game> games = new List<Game>();



        static void Main(string[] args)
        {
            getSettings();
            getCurrentVersion();
            checkVersion();
			waitYuzuLaunch();
        }

        private static void killYuzus()
        {
            Process[] processes = new Process[0];

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                processes = Process.GetProcessesByName(currentExe.Replace(".exe", ""));
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                processes = Process.GetProcessesByName("yuzu");

            if (processes.Length > 0)
                Console.WriteLine("Kill yuzu process");

            foreach (Process p in processes)
            {
                p.Kill();
            }
        }
		
		private static void waitYuzuLaunch(){

            Console.Write("Starting Yuzu...");
            Process p = new Process();
            p.StartInfo.FileName = currentExe;
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = true;
            p.Start();
			
			while(p.MainWindowHandle==IntPtr.Zero && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
				System.Threading.Thread.Sleep(1000);
				Console.Write(".");
                p = Process.GetProcessById(p.Id);
            }

            System.Environment.Exit(0);
		}

        private static void getSettings()
        {
            if (System.IO.File.Exists("launchUpdater.txt"))
            {
                StreamReader reader = new StreamReader("launchUpdater.txt");
                currentExe = reader.ReadToEnd().Replace("\r\n", "");
                reader.Close();
            }

            Utils.init7ZipPaht();
        }

        private static HttpClient httpClient()
        {

            HttpClientHandler httpClientHandler = new HttpClientHandler();
            httpClientHandler.ClientCertificateOptions = ClientCertificateOption.Manual;
            httpClientHandler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            HttpClient client = new HttpClient(httpClientHandler);

            return client ;
        }

        static void getCurrentVersion()
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {

                    var files = Directory.EnumerateFiles(System.Environment.CurrentDirectory, "*.AppImage");
                    if (files.Count() > 0)
                    {
                        currentExe = files.First();
                        currentVersion = currentExe.Substring(currentExe.LastIndexOf("-") + 1, currentExe.LastIndexOf(".") - currentExe.LastIndexOf("-") - 1);
                        currentVersion = "EA-" + currentVersion;
                        Console.WriteLine("Yuzu EA version found : " + currentVersion);
                    }
                }
                else
                {
                    if (System.IO.File.Exists(currentExe))
                    {
                        StreamReader reader = new StreamReader(currentExe);
                        currentVersion = reader.ReadToEnd();
                        reader.Close();
                        var index = currentVersion.IndexOf("yuzu Early Access");
                        currentVersion = currentVersion.Substring(index - 20, 20).Replace("\0", "").Replace("\00", "");
                        if (currentVersion.StartsWith("0"))
                            currentVersion = currentVersion.Substring(1);

                        currentVersion = "EA-" + currentVersion;
                        Console.WriteLine("Yuzu EA version found : " + currentVersion);
                    }
                }
            }
            catch(ArgumentOutOfRangeException e) 
            { 
                Console.WriteLine("Its seem you dont use EA Yuzu version.");
                Console.WriteLine("You must use EA version from pineapple here : https://github.com/pineappleEA/pineapple-src/releases.");
            }



        }

        public static void checkVersion()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
            Console.WriteLine("Check for YUZU EA update");


            String src =  httpClient().GetAsync("https://github.com/pineappleEA/pineapple-src/releases/").Result.Content.ReadAsStringAsync().Result;
            string[] releaseVersions = src.Split(new String[] { "h2 class=\"sr-only\"" }, StringSplitOptions.RemoveEmptyEntries).Skip(1).ToArray(); 
            releaseVersions = releaseVersions.Take(releaseVersions.Length - 1).ToArray();

            for(int i = 0;  i < releaseVersions.Length; i++)
            {
                releases.Add(new Release(releaseVersions[i]));
            }

            if(currentVersion == null)
                Console.WriteLine("No version found");
            
            myCurrentRelease = releases.Where(x => x.version == currentVersion).FirstOrDefault();
            if(myCurrentRelease == null)
                myCurrentRelease = new Release(currentVersion,true);
            
            if (myCurrentRelease != releases.FirstOrDefault())
            {
                Console.WriteLine("Retrieve PRs from github");
                for (var p = 0; p < 4; p++)
                {
                    src = httpClient().GetAsync("https://github.com/yuzu-emu/yuzu/issues?page=" + p + "&q=sort%3Acreated-desc").Result.Content.ReadAsStringAsync().Result;

                    string[] prs = src.Split(new String[] { "<div class=\"flex-auto min-width-0 p-2 pr-3 pr-md-2\">" }, StringSplitOptions.RemoveEmptyEntries).Skip(1).ToArray();
                    prs = prs.Take(prs.Length - 1).ToArray();

                    for (int i = 0; i < prs.Length; i++)
                    {
                        var pr = new PR(prs[i]);
                        //CHECK IF UNIQUE BECAUSE BUG IDK WHY
                        if(prList.FirstOrDefault(x => x.idIssue == pr.idIssue) == null)
                            prList.Add(pr);
                    }
                }


                string changeLog = getChangeLog();

                Console.WriteLine("New Version found , pass from " + currentVersion + " to " + releases[0].version + "\n" + getChangeLog());
                Console.WriteLine("Do you want to download it ? (y/n)");
                string answer = Console.ReadLine();
                if (answer == "y")
                {
                    downloadRelease(releases[0]);
                }

            }
            else
            {
                Console.WriteLine("Yuzu is up to date");
            }
            
            

            while (true)
            {
                Console.WriteLine();
                Console.WriteLine("Do you want another things ?");
                Console.WriteLine("-switch <buildID>");
                Console.WriteLine("-dmods (Download all available mods in gamebanana)");
                Console.WriteLine("-Nothing , launch yuzu");
                String input = Console.ReadLine();
                Console.WriteLine();
                var buildId = Regex.Match(input, @"-switch (\d+)").Groups[1].Value;
                if (buildId != "")
                {
                    downloadRelease(new Release(buildId, true));
                }
                else if(input == "-dmods")
                {
                    downloadMods();
                }
                else
                    return;
            }
        }

        private static  void  downloadRelease(Release release)
        {
			try{
                killYuzus();
                Console.WriteLine("Downloading "+ release.version + " version");

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    String fileName = System.IO.Path.GetFileName(new Uri(release.downloadUrl).LocalPath);
                    download(release.downloadUrl, fileName);
                    Console.WriteLine("Make executable");
                    Process.Start("chmod", "+x " + fileName);
                    Console.WriteLine("Remove old version");
                    if (System.IO.File.Exists(currentExe))
                        System.IO.File.Delete(currentExe);

                    currentExe = fileName;
                }
                else
                {
                    download(release.downloadUrl, "YuzuEA.zip");
                    ZipArchive zip = ZipFile.OpenRead("YuzuEA.zip");
                    zip.ExtractToDirectory(System.Environment.CurrentDirectory, true);
                    zip.Dispose();
                    Console.WriteLine("Remove zip file");
                    System.IO.File.Delete("YuzuEA.zip");
                    string[] files = Directory.GetFiles(System.Environment.CurrentDirectory + "/yuzu-windows-msvc-early-access");
                    Console.WriteLine("Move files and directory to root directory");
                    if (System.IO.File.Exists(currentExe))
                        System.IO.File.Delete(currentExe);
                    Utils.DirectoryCopyAndDelete(System.Environment.CurrentDirectory + "/yuzu-windows-msvc-early-access", System.Environment.CurrentDirectory);
                    System.IO.File.Move("yuzu.exe", currentExe);
                }
			}
			catch(Exception ex){
				Console.WriteLine(ex.StackTrace + "  " + ex.Message);
				Console.ReadLine();
			}
        }

        private static string getChangeLog()
        {
            string changeLog = "";
            List<PR> prs = prList.Where(x => x.releaseDate > myCurrentRelease.releaseDate  && x.label.Contains("merge")).ToList();

            foreach(PR pr in prs)
            {
                changeLog += pr.idIssue + " - " + pr.description + " - " + pr.label + " - " + pr.releaseDate + "\n";
            }
            return changeLog;
        }

        private static void download(String uri,String filename)
        {
            HttpClient _httpClient = httpClient();
            _httpClient.BaseAddress = new Uri(uri);
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/octet-stream"));
            HttpResponseMessage response = _httpClient.GetAsync(uri).Result;
            response.EnsureSuccessStatusCode();
            using (FileStream fileStream = new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                response.Content.CopyToAsync(fileStream).Wait();
            }
        }


        private static void downloadMods()
        {
            scanTitlesIdAndGetName();
            if (games.Count == 0)
            {
                Console.WriteLine("No game found");
                return;
            }
            else
            {
                Console.WriteLine("Select game to download mods");
                String input = Console.ReadLine();
                int index = 0;
                if (int.TryParse(input, out index))
                {
                    if (index < games.Count)
                    {
                        Game game = games[index];
                        game.loadMods();

                        List<BananaMod> validMods = game.bananaMods.Where(x => x._sModelName == "Mod" && !x._bIsObsolete && x._bHasFiles).ToList();
                        Console.WriteLine("Find " + validMods.Count + " mods for " + game.name);
                        Console.WriteLine("Download all mods ? (y/n)");
                        input = Console.ReadLine();
                        if (input == "y")
                        {
                            List<Task> tasks = new List<Task>();
                            index = 0;
                            foreach (BananaMod mod in validMods)
                            {
                                Task task = new Task(() =>
                                {
                                    mod.download();
                                    mod.extract(game.pathMods);
                                });
                                tasks.Add(task);
                                task.Start();
                                index++;

                                if(index % 3 == 0)
                                 Task.WaitAll(tasks.ToArray());

                            }

                            Task.WaitAll(tasks.ToArray());
                            
                        }
                    }
                    else
                    {
                        Console.WriteLine("Invalid index");
                    }
                }
                else
                {
                    Console.WriteLine("Invalid index");
                }
            }
        }


        private static void scanTitlesIdAndGetName()
        {
            if (games.Count == 0)
            {
                Console.WriteLine("");
                Console.WriteLine("Scanning game");
                var path = System.Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "/yuzu";

                if(!Directory.Exists(path))
                    path = "user";
                if(!Directory.Exists(path + "/sdmc/atmosphere/contents"))
                    return;


               String[] directorys = Directory.GetDirectories(path+"/sdmc/atmosphere/contents").Select(d => Path.GetFileName(d)).ToArray();

                HttpClient _httpClient = httpClient();
                String src = _httpClient.GetAsync("https://switchbrew.org/w/index.php?title=Title_list/Games&mobileaction=toggle_view_desktop").Result.Content.ReadAsStringAsync().Result;

                foreach (String directory in directorys)
                {
                    String id = directory.Substring(0, 16);
                    string name = Regex.Match(src, @"<td>" + id + @"</td>\s*<td>(.*)</td>").Groups[1].Value;

                    if (name != "")
                    {
                        games.Add(new Game(id, name,path));
                        Console.WriteLine(games.Count-1 + ")  " + name);
                    }

                }
            }
        }

 

      
    }


    public class Release
    {
        public Release(string releaseVersion)
        {

            this.version = Regex.Match(releaseVersion, @""">(.*)</h2>").Groups[1].Value;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                this.downloadUrl = @"https://github.com/pineappleEA/pineapple-src/releases/download/" + this.version + "/Linux-Yuzu-" + this.version + ".AppImage";
            else
                this.downloadUrl = @"https://github.com/pineappleEA/pineapple-src/releases/download/" + this.version + "/Windows-Yuzu-" + this.version + ".zip";
           

            this.releaseDate = DateTime.Parse(Regex.Match(releaseVersion, @"datetime=""(.*)"">").Groups[1].Value);

        }

        public Release(string version,bool none)
        {
            if(version == null)
            {
                this.version = "EA-0";
            }
            else
            {
                this.version = "EA-" + version;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                this.downloadUrl = @"https://github.com/pineappleEA/pineapple-src/releases/download/" + this.version + "/Linux-Yuzu-" + this.version + ".AppImage";
            else
                this.downloadUrl = @"https://github.com/pineappleEA/pineapple-src/releases/download/" + this.version + "/Windows-Yuzu-" + this.version + ".zip";

            this.releaseDate = DateTime.MinValue;
        }

        public string version;
        public string downloadUrl;
        public DateTime releaseDate;
    }

    public class PR
    {
        public PR(string prVersion)
        {
             idIssue = Regex.Match(prVersion, @"<a id=""issue_(\d*)_link").Groups[1].Value;
             description = Regex.Match(prVersion, @"<a id=""issue_\d*_link"".*>(.*)</a>").Groups[1].Value;
             releaseDate = DateTime.Parse(Regex.Match(prVersion, @"datetime=""(.*)Z""").Groups[1].Value + "Z");

            var labels = Regex.Matches(prVersion, @"data-name=""(.*)"" style=");
            foreach(Match label in labels)
            {
                this.label += label.Groups[1].Value + " ";
            }
        }

        public string idIssue;
        public string description;
        public DateTime releaseDate;
        public string label = "";

    }


    public class Game
    {
        public string id;
        public string name;
        public string pathApp;
        public List<BananaMod> bananaMods = new List<BananaMod>();

        public string pathMods
        {
            get
            {
                return pathApp + "\\load\\" + this.id;
            }
        }

        public Game(string id, string name, string pathApp)
        {
            this.id = id;
            this.name = name;
            this.pathApp = pathApp;
        }

        public void loadMods()
        {
            if (bananaMods.Count == 0)
            {
                HttpClient _httpClient = new HttpClient();

                String src = _httpClient.GetAsync("https://gamebanana.com/apiv11/Util/Game/NameMatch?_sName=" + name.Replace(" ", "+") + "&_nPerpage=10&_nPage=1").Result.Content.ReadAsStringAsync().Result;
                String idGameBanana = Regex.Match(src, @"""_idRow"": (\d+)").Groups[1].Value;
                BananaResponse bananaResponse = null;
                int p = 1;
                while (bananaResponse == null || bananaResponse._aRecords.Count > 0)
                {
                    src = _httpClient.GetAsync("https://gamebanana.com/apiv11/Game/" + idGameBanana + "/Subfeed?_nPage=" + p + "&_sSort=default").Result.Content.ReadAsStringAsync().Result;
                    bananaResponse = JsonSerializer.Deserialize<BananaResponse>(src);
                    bananaMods.AddRange(bananaResponse._aRecords);
                    p++;
                }
            }
        }
    }


    public class BananaResponse
    {
        public List<BananaMod> _aRecords { get; set; }

    }

    public class BananaFile
    {
        public int _idRow { get; set; }
        public string _sFile { get; set; }
        public int _nFilesize { get; set; }
        public string _sDescription { get; set; }
        public long _tsDateAdded { get; set; }
        public int _nDownloadCount { get; set; }
        public string _sAnalysisState { get; set; }
        public string _sDownloadUrl { get; set; }
        public string _sMd5Checksum { get; set; }
        public string _sClamAvResult { get; set; }
        public string _sAnalysisResult { get; set; }
        public bool _bContainsExe { get; set; }

    }

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
        public bool _bHasFiles{ get; set; }
        public string[] _aTags{ get; set; }
        public string _sVersion{ get; set; }
        public long _tsDateUpdated { get; set; }
        public bool _bIsObsolete{ get; set; }
        public string _sInitialVisibility{ get; set; }
        public bool _bHasContentRatings{ get; set; }
        public int _nLikeCount { get; set; }
        public int _nPostCount { get; set; }
        public bool _bWasFeatured{ get; set; }
        public int _nViewCount { get; set; }
        public bool _bIsOwnedByAccessor{ get; set; }

        public List<BananaFile> files;


        public void loadFiles()
        {
            HttpClient _httpClient = new HttpClient();
            String uri = "https://gamebanana.com/apiv11/Mod/" + _idRow + "/Files";
            String src = _httpClient.GetAsync(uri).Result.Content.ReadAsStringAsync().Result;
            files = JsonSerializer.Deserialize<List<BananaFile>>(src);

        }

        public void download()
        {
            if(!Directory.Exists("_tempMod"))
            {
                Directory.CreateDirectory("_tempMod");
            }

            if(files == null) 
                loadFiles();

            foreach (BananaFile file in files)
            {
                if (file._sClamAvResult == "clean")
                {
                    using (var client = new WebClient())
                    {
                        if(file._sFile.EndsWith(".zip") || file._sFile.EndsWith(".7z"))
                        {
                            Console.WriteLine("       -Downloading " + file._sFile);
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
                                Console.WriteLine("Error while downloading " + file._sFile + " : " + e.Message);
                            }
                        }
                    }
                }
            }

        }

        public void extract(String pathToExtract)
        {

            foreach (BananaFile f in files)
            {
                if (f._sFile.EndsWith(".zip") || f._sFile.EndsWith(".7z"))
                {
                    Console.WriteLine("       -Extracting " + f._sFile);
                    try
                    {
                        if (f._sFile.EndsWith(".zip"))
                        {
                            ZipArchive zipArchive = ZipFile.OpenRead("_tempMod/" + f._sFile);
                            bool isCorrectModFolder = zipArchive.Entries.ToList().FirstOrDefault(f => f.FullName.Contains("romfs") || f.FullName.Contains("exefs") || f.FullName.Contains("cheats")) != null;
                            if(isCorrectModFolder)
                                ZipFile.ExtractToDirectory("_tempMod/" + f._sFile, pathToExtract,true);

                            zipArchive.Dispose();
                        }
                        else
                        {
                            SevenZipExtractor extractor = new SevenZipExtractor("_tempMod/" + f._sFile);
                            bool isCorrectModFolder = extractor.ArchiveFileNames.ToList().FirstOrDefault(f => f.Contains("romfs") || f.Contains("exefs") || f.Contains("cheats")) != null;
                            if (isCorrectModFolder)
                                extractor.ExtractArchive(pathToExtract);

                            extractor.Dispose();
                        }
                        System.Threading.Thread.Sleep(1000);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Error while extracting " + f._sFile + " : " + e.Message);
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
    }
}
