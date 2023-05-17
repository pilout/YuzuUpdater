using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace YuzuEAUpdater
{
    internal class Program
    {
        private static List<Release> releases = new List<Release>();
        private static List<PR> prList = new List<PR>();

        private static  string currentVersion = null;
        private static Release myCurrentRelease = null;
        private static string currentExe = "yuzu.exe";



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
                Process.GetProcessesByName(currentExe.Replace(".exe", ""));
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
                    if(currentVersion.StartsWith("0"))
                        currentVersion = currentVersion.Substring(1);

                    currentVersion = "EA-" + currentVersion;
                    Console.WriteLine("Yuzu EA version found : " + currentVersion);
                }
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
                Console.WriteLine("Nothing , launch yuzu");
                var buildId = Regex.Match(Console.ReadLine(), @"-switch (\d+)").Groups[1].Value;
                if (buildId != "")
                {
                    downloadRelease(new Release(buildId, true));
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
                    if (File.Exists(currentExe))
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
                    if (File.Exists(currentExe))
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


}
