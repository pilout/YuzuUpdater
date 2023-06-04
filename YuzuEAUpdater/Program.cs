using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Terminal.Gui;
using YuzuEAUpdater.UI;

namespace YuzuEAUpdater
{
    public class Program
    {
        static void Main(string[] args)
        {

            Application.Run<MainWindow>();
            Application.Shutdown();

        }
    }

    public class MainWindow : Window
    {
        private  List<Release> releases = new List<Release>();
        private  List<PR> prList = new List<PR>();

        private  string currentVersion = null;
        private  Release myCurrentRelease = null;
        private  string currentExe = "yuzu.exe";
        public  List<Game> games = new List<Game>();
        public static object lockObj = new object();
        private string pathApp = "";
        public Boolean optimizePerf = true;
        public Boolean killCpuProccess = false;
        public bool autoStartYuzu;
        public bool confirmDownload;
        public bool backupSave = false;
        public static MainUI MainUI;



        

        private void InitializeComponent()
        {
            Application.MainLoop.Invoke(() =>
            {
                Title = "YuzuTool 1.8";
                this.AutoSize = true;
                X = 0;
                Y = 1;
                Width = Dim.Fill();
                Height = Dim.Fill();

                MainUI = new MainUI(this);
                
                Application.Init();
                Console.SetWindowSize(Console.LargestWindowWidth, Console.LargestWindowHeight);
                this.SetNeedsDisplay();
            });

        }


        public  MainWindow()
        {
            getSettings();
            InitializeComponent();
            Task.Run(() =>
            {
                try
                {

                    while (MainUI.mainConsole == null || MainUI.mainConsole.SuperView == null)
                    {
                        System.Threading.Thread.Sleep(1500);
                        Console.WriteLine("Initialise UI...");
                    }
                    purgeUncessaryFiles();
                    _saveBackup();
                    getCurrentVersion();
                    checkVersion();
                    waitYuzuLaunch();
                }
                catch (Exception ex)
                {
                    Application.Shutdown();
                    Console.Write(ex.StackTrace);
                    System.Threading.Thread.Sleep(10000);
                    Console.ReadLine();
                }

            });


        }

        private  void killYuzus()
        {
            Process[] processes = new Process[0];

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                processes = Process.GetProcessesByName(currentExe.Replace(".exe", ""));
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                processes = Process.GetProcessesByName("yuzu");

            if (processes.Length > 0)
                MainUI.addTextConsole("Kill yuzu process\n");

            foreach (Process p in processes)
                p.Kill();
             
        }
		
		private  void waitYuzuLaunch(){
            var timer = 10000;

            while (!autoStartYuzu)
            {
                System.Threading.Thread.Sleep(1000);
            }

            if(!System.IO.File.Exists(currentExe))
            {
                MainUI.addTextConsole("Yuzu not found\n");
                return;
            }

            MainUI.addTextConsole("Starting Yuzu...\n");
            Process p = new Process();
            p.StartInfo.FileName = currentExe;
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = true;

            if(this.killCpuProccess)
                Utils.KillProcessesByCpuUsage(3);

            p.Start();
            p.WaitForInputIdle();
            if (this.optimizePerf)
            {
                Utils.SetPowerSavingMode(false);
                Utils.setAffinityMask(p);
                p.PriorityClass = ProcessPriorityClass.RealTime;
            }


            while (p.MainWindowHandle==IntPtr.Zero && RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||( timer > 0))
            {
				System.Threading.Thread.Sleep(1000);
                timer -= 1000;
				Console.Write(".");
                p = Process.GetProcessById(p.Id);
            }
            
            System.Environment.Exit(0);
            Application.Shutdown();
        

        }

        public  void getSettings()
        {
            string[] data =  new string[0];
            if (System.IO.File.Exists("launchUpdater.txt"))
            {
                StreamReader reader = new StreamReader("launchUpdater.txt");
                 data = reader.ReadToEnd().Replace("\r\n", "").Split("|");
                reader.Close();
            }


            if (data.Length > 0)
                currentExe = data[0];

            autoStartYuzu = data.Length > 1 ? bool.Parse(data[1]) : true;
            confirmDownload = data.Length > 2 ? bool.Parse(data[2]) : true;
            backupSave = data.Length > 3 ? bool.Parse(data[3]) : true;
            optimizePerf = data.Length > 4 ? bool.Parse(data[4]) : true;
            killCpuProccess = data.Length > 5 ? bool.Parse(data[5]) : false;

            Utils.init7ZipPaht();
            initAppPath();
        }

        public void setSettings()
        {
            StreamWriter writer = new StreamWriter("launchUpdater.txt");
            writer.Write(currentExe + "|" + autoStartYuzu + "|" + confirmDownload + "|" + backupSave + "|" + optimizePerf + "|" + killCpuProccess);
            writer.Close();
        }

        private  HttpClient httpClient()
        {

            HttpClientHandler httpClientHandler = new HttpClientHandler();
            httpClientHandler.ClientCertificateOptions = ClientCertificateOption.Manual;
            httpClientHandler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            HttpClient client = new HttpClient(httpClientHandler);

            return client ;
        }

         void getCurrentVersion()
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
                        MainUI.addTextConsole("Yuzu EA version found : " + currentVersion + "\n");
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
                        MainUI.addTextConsole("Yuzu EA version found : " + currentVersion + "\n");
                    }
                }
            }
            catch(ArgumentOutOfRangeException e) 
            { 
                MainUI.addTextConsole("Its seem you dont use EA Yuzu version." + "\n");
                MainUI.addTextConsole("You must use EA version from pineapple here : https://github.com/pineappleEA/pineapple-src/releases." + "\n");
            }



        } 

        public  void checkVersion()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
            MainUI.addTextConsole("Check for YUZU EA update" + "\n");


            String src =  httpClient().GetAsync("https://github.com/pineappleEA/pineapple-src/releases/").Result.Content.ReadAsStringAsync().Result;
            string[] releaseVersions = src.Split(new String[] { "h2 class=\"sr-only\"" }, StringSplitOptions.RemoveEmptyEntries).Skip(1).ToArray(); 
            releaseVersions = releaseVersions.Take(releaseVersions.Length - 1).ToArray();

            for(int i = 0;  i < releaseVersions.Length; i++)
            {
                releases.Add(new Release(releaseVersions[i]));
            }

            if(currentVersion == null)
                MainUI.addTextConsole("No version found" + "\n");
            
            myCurrentRelease = releases.Where(x => x.version == currentVersion).FirstOrDefault();
            if(myCurrentRelease == null)
                myCurrentRelease = new Release(currentVersion,true);
            
            if (myCurrentRelease != releases.FirstOrDefault())
            {
                MainUI.addTextConsole("Retrieve PRs from github" + "\n");
                for (var p = 0; p < 4; p++)
                {
                    MainUI.progress.Report((p + 1) * 0.25f);
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

                MainUI.addTextConsole("New Version found , pass from " + currentVersion + " to " + releases[0].version + "\n" + getChangeLog());
                if(confirmDownload)
                {
                    MainUI.addTextConsole("Do you want to download it ? (y/n)" + "\n");
                    string answer = MainUI.waitInput().Result;
                    if (answer != "y")
                        return;
                }
                 downloadRelease(releases[0]);
            }
            else
            {
                MainUI.addTextConsole("Yuzu is up to date" + "\n");
            }
          
        }

        public void  downloadRelease(Release release)
        {
			try{
                killYuzus();
                MainUI.addTextConsole("Downloading "+ release.version + " version" + "\n");

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    String fileName = System.IO.Path.GetFileName(new Uri(release.downloadUrl).LocalPath);
                    download(release.downloadUrl, fileName).GetAwaiter().GetResult();
                    MainUI.addTextConsole("Make executable" + "\n");
                    Process.Start("chmod", "+x " + fileName);
                    MainUI.addTextConsole("Remove old version" + "\n");
                    if (System.IO.File.Exists(currentExe))
                        System.IO.File.Delete(currentExe);

                    currentExe = fileName;
                }
                else
                {
                    download(release.downloadUrl, "YuzuEA.zip").GetAwaiter().GetResult();
                    ZipArchive zip = ZipFile.OpenRead("YuzuEA.zip");
                    zip.ExtractToDirectory(System.Environment.CurrentDirectory, true);
                    zip.Dispose();
                    MainUI.addTextConsole("Remove zip file" + "\n");
                    System.IO.File.Delete("YuzuEA.zip");
                    string[] files = Directory.GetFiles(System.Environment.CurrentDirectory + "/yuzu-windows-msvc-early-access");
                    MainUI.addTextConsole("Move files and directory to root directory" + "\n");
                    if (System.IO.File.Exists(currentExe))
                        System.IO.File.Delete(currentExe);
                    Utils.DirectoryCopyAndDelete(System.Environment.CurrentDirectory + "/yuzu-windows-msvc-early-access", System.Environment.CurrentDirectory);
                    System.IO.File.Move("yuzu.exe", currentExe);
                }
                MainUI.addTextConsole("Install to " + release.version + " success !\n");
                purgeUncessaryFiles();
            }
			catch(Exception ex){
				MainUI.addTextConsole(ex.StackTrace + "  " + ex.Message + "\n");

			}
        }

        private  string getChangeLog()
        {
            string changeLog = "";
            List<PR> prs = prList.Where(x => x.releaseDate > myCurrentRelease.releaseDate  && x.label.Contains("merge")).ToList();

            foreach(PR pr in prs)
            {
                changeLog += pr.idIssue + " - " + pr.description + " - " + pr.label + " - " + pr.releaseDate + "\n";
            }
            return changeLog;
        }

        private async Task download(String uri,String filename)
        {
            HttpClient _httpClient = httpClient();
            _httpClient.BaseAddress = new Uri(uri);
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/octet-stream"));
            using (var file = new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.None))
            {

                await _httpClient.DownloadAsync(uri, file, MainUI.progress);
            }

        }



        public void scanTitlesIdAndGetName()
        {
            if(pathApp == "")
            {
                MainUI.addTextConsole("Path to yuzu data not found" + "\n");
                return;
            }
            else
            {
                MainUI.addTextConsole("Path to yuzu data found: " + pathApp + "\n");
            }
                

            if (games.Count == 0)
            {

                List<String> directorys = Directory.GetDirectories(pathApp ,"010*",SearchOption.AllDirectories).Select(d => Path.GetFileName(d).ToUpper()).ToList();
                directorys = directorys.Distinct().ToList();

                HttpClient _httpClient = httpClient();
                String src = _httpClient.GetAsync("https://switchbrew.org/w/index.php?title=Title_list/Games&mobileaction=toggle_view_desktop").Result.Content.ReadAsStringAsync().Result;

                foreach (String directory in directorys)
                {
                    String id = directory.Substring(0, 16);
                    string name = Regex.Match(src, @"<td>" + id + @"</td>\s*<td>(.*)</td>").Groups[1].Value;

                    if (name != "")
                    {
                        games.Add(new Game(id, name, pathApp));
                    }

                }
            }
        }


        private void _saveBackup()
        {
            try
            {
                MainUI.addTextConsole("Save game backup..." + "\n");
               string sourceDir = Path.Combine(pathApp, "nand", "user", "save");
                DirectoryInfo directoryInfo = new DirectoryInfo(sourceDir);
                if (this.backupSave && directoryInfo.Exists && directoryInfo.GetDirectories().Length>0)
                {
                    if(!Directory.Exists(Path.Combine(Environment.CurrentDirectory, "savesBackup")))
                        Directory.CreateDirectory(Path.Combine(Environment.CurrentDirectory, "savesBackup"));   

                    string zipFilePath = Path.Combine(Environment.CurrentDirectory,"savesBackup", DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".7z");

                    SevenZip.SevenZipCompressor compressor = new SevenZip.SevenZipCompressor();
                    compressor.CompressionLevel = SevenZip.CompressionLevel.Ultra;
                    compressor.CompressionMethod = SevenZip.CompressionMethod.Lzma2;
                    compressor.CompressionMode = SevenZip.CompressionMode.Create;

                    compressor.CompressDirectory(sourceDir, zipFilePath);

                    _deleteOldBackups();

                }

            

            }
            catch (Exception ex)
            {
                MainUI.addTextConsole(ex.StackTrace + "  " + ex.Message + "\n");
                 Console.ReadLine();
            }

            
        }


        private void _deleteOldBackups()
        {
            string backupDir = Path.Combine(Environment.CurrentDirectory, "savesBackup");
            if (!Directory.Exists(backupDir)) return; // backup directory does not exist
            var backupFiles = new DirectoryInfo(backupDir).GetFiles("*.7z", SearchOption.AllDirectories).OrderBy(f => f.LastWriteTime).ToList(); // get all backup files in the directory sorted by date
            if (backupFiles.Count < 4) return; // less than 3 backup files present

            backupFiles[0].Delete(); // delete the oldest backup file
        }


        public void restoreLatestBackup()
        {
            string backupDir = Path.Combine(Environment.CurrentDirectory, "savesBackup");
            DirectoryInfo dirInfo = new DirectoryInfo(backupDir);
            if (dirInfo.Exists)
            {
                FileInfo[] files = dirInfo.GetFiles("*.7z");
                if (files.Length > 0)
                {
                    Array.Sort(files, (x, y) => y.CreationTime.CompareTo(x.CreationTime));
                    FileInfo latestBackup = files[0];
                    MainUI.addTextConsole("Restoring latest backup: " + latestBackup.CreationTime.ToString() + "\n");
                    SevenZip.SevenZipExtractor extractor = new SevenZip.SevenZipExtractor(latestBackup.FullName);
                    extractor.ExtractArchive(Path.Combine(pathApp, "nand", "user", "save"));
                    MainUI.addTextConsole("Restore complete.\n");
                }
                else
                {
                    MainUI.addTextConsole("No backup file found.\n");
                }
            }
            else
            {
                MainUI.addTextConsole("Backup directory does not exist.\n");
            }
        }


        private void initAppPath()
        {
             pathApp = Path.Combine(System.Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) ,"yuzu");

            if (!Directory.Exists(pathApp))
                pathApp = "user";
            if (!Directory.Exists(pathApp))
                pathApp = "";

        }


        private void purgeUncessaryFiles()
        {
            MainUI.addTextConsole("Purging uncessary files...\n");
            //FIND all FILLS THAT BEGIN WITH yuzu-windows-msvc-source- in current directory
           var files = Directory.GetFiles(Environment.CurrentDirectory, "yuzu-windows-msvc-source-*");
            foreach (var file in files)
            {
                try
                {
                    System.IO.File.Delete(file);
                    FileInfo fileInfo = new FileInfo(file);
                    MainUI.addTextConsole(" -Deleted " + fileInfo.Name + "\n");
                }
                catch (Exception ex)
                {
                    MainUI.addTextConsole(ex.StackTrace + "  " + ex.Message + "\n");
                    Console.ReadLine();
                }
            }

        }


        public void checkUpdate()
        {
            MainUI.addTextConsole("W'll restart for updates...\n");

                WebClient webClient = new WebClient();

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    if(!File.Exists("updateUpdater.exe"))
                       File.Delete("updateUpdater.exe");
                    webClient.DownloadFile("https://github.com/pilout/YuzuUpdater/releases/download/updater/updateUpdater.exe", "updateUpdater.exe");
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    if (!File.Exists("updateUpdater"))
                        File.Delete("updateUpdater");
                    webClient.DownloadFile("https://github.com/pilout/YuzuUpdater/releases/download/updater/updateUpdater", "updateUpdater");
                }

            ProcessStartInfo startInfo = null;

             if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
             {
                Process.Start("chmod", "+x updateUpdater");
                startInfo = new ProcessStartInfo("updateUpdater");
             }
             else
                startInfo = new ProcessStartInfo("updateUpdater.exe");
             
                  
            startInfo.UseShellExecute = true;
            Process.Start(startInfo);
            Process.GetCurrentProcess().Kill();
        }
    }


}
