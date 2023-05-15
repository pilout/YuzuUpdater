using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
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
		
		private static void waitYuzuLaunch(){
			Console.Write("Starting Yuzu...");
            Process p = Process.Start(currentExe);
			
			while(p.MainWindowHandle==IntPtr.Zero){
				System.Threading.Thread.Sleep(1000);
				Console.Write(".");
			}
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

        static void getCurrentVersion()
        {
            if(System.IO.File.Exists(currentExe))
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

        public static void checkVersion()
        {

            Console.WriteLine("Retrives releases from github");
            WebClient client = new WebClient();
            String src = client.DownloadString("https://github.com/pineappleEA/pineapple-src/releases/");

            string[] releaseVersions = src.Split(new String[] { "h2 class=\"sr-only\"" }, StringSplitOptions.RemoveEmptyEntries).Skip(1).ToArray(); 
            releaseVersions = releaseVersions.Take(releaseVersions.Length - 1).ToArray();

            for(int i = 0;  i < releaseVersions.Length; i++)
            {
                releases.Add(new Release(releaseVersions[i]));
            }

            if(currentVersion == null)
            {
                Console.WriteLine("No version found, but find latest version");

      
            }
            else
            {
                myCurrentRelease = releases.Where(x => x.version == currentVersion).FirstOrDefault();
                if (myCurrentRelease != null)
                {
                    if (myCurrentRelease != releases.FirstOrDefault())
                    {
                        Console.WriteLine("Retrieve PRs from github");
                        for (var p = 0; p < 4; p++)
                        {
                            src = client.DownloadString("https://github.com/yuzu-emu/yuzu/issues?page=" + p + "&q=sort%3Acreated-desc");

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

                    }
                    else
                    {
                        Console.WriteLine("Yuzu is up to date");
                        return;
                    }
                }
                Console.WriteLine("New Version found , pass from " + currentVersion + " to " + releases[0].version + "\n" + getChangeLog());

            }

            Console.WriteLine("Do you want to download it ? (y/n)");
            string answer = Console.ReadLine();
            if (answer == "y")
            {
                downloadLastedVersion();
            }
            else
            {
                Console.WriteLine("Ok, bye");
                System.Environment.Exit(0);
            }

        }

        private static  void  downloadLastedVersion()
        {
			try{
				
			
				Console.WriteLine("Downloading latest version");
				WebClient client = new WebClient();
				client.DownloadFile(releases[0].downloadUrl, "YuzuEA.zip");
				ZipArchive zip = ZipFile.OpenRead("YuzuEA.zip");
				zip.ExtractToDirectory(System.Environment.CurrentDirectory,true);
				zip.Dispose();
				Console.WriteLine("Remove zip file");
				System.IO.File.Delete("YuzuEA.zip");
				string[] files = Directory.GetFiles(System.Environment.CurrentDirectory + "/yuzu-windows-msvc-early-access");
				Console.WriteLine("Move files and directory to root directory");
				Utils.DirectoryCopyAndDelete(System.Environment.CurrentDirectory + "/yuzu-windows-msvc-early-access", System.Environment.CurrentDirectory);
				if(File.Exists(currentExe))
					System.IO.File.Delete(currentExe);
				System.IO.File.Move("yuzu.exe", currentExe);
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
    }


    public class Release
    {
        public Release(string releaseVersion)
        {

            this.version = Regex.Match(releaseVersion, @""">(.*)</h2>").Groups[1].Value;
            this.downloadUrl = @"https://github.com/pineappleEA/pineapple-src/releases/download/"+ version + "/Windows-Yuzu-"+ version + ".zip";
            this.releaseDate = DateTime.Parse(Regex.Match(releaseVersion, @"datetime=""(.*)"">").Groups[1].Value);

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
