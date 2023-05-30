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
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Terminal.Gui;

namespace YuzuEAUpdater
{
    public class Program
    {
        static void Main(string[] args)
        {

            Application.Run<UI>();
            Application.Shutdown();

        }
    }

    internal class UI : Window
    {
        private  List<Release> releases = new List<Release>();
        private  List<PR> prList = new List<PR>();

        private  string currentVersion = null;
        private  Release myCurrentRelease = null;
        private  string currentExe = "yuzu.exe";
        private  List<Game> games = new List<Game>();
        public static  Label mainConsole ;
        private static ProgressBar progressBar ;
        private static ListView listView;
        private  Boolean _waitInput = false;
        private string _input = "";
        private static List<int> linesIndex = new List<int>();
        private bool autoStartYuzu;
        private bool confirmDownload;
        private bool backupSave = false;
        private static object lockObj = new object();
        private static bool checkall = false;
        private string pathApp = "";


        public async Task<string> waitInput()
        {
            addTextConsole("\n");
            _waitInput = true;
            while (_waitInput)
            {
              await Task.Delay(100);
            }
            var temp = _input;
            _input = "";
            return temp;
        }

        public static void addTextConsole(String text)
        {

            int[] indexs = text.Select((b, i) => b == '\n' ? i + mainConsole.Text.Length : -1).Where(i => i != -1).ToArray();
            linesIndex.AddRange(indexs);

            Application.MainLoop.Invoke(() =>
            {
                mainConsole.Text += (text);

                if(mainConsole.SuperView.Bounds.Height>0)
                {
                    while (linesIndex.Count > 0 && (mainConsole.Frame.Bottom) > mainConsole.SuperView.Bounds.Height)
                    {
                        var index = linesIndex.First();
                        if(index < mainConsole.Text.Length)
                            mainConsole.Text = mainConsole.Text.Substring(index);
                        else
                            mainConsole.Text = "";

                        linesIndex.RemoveAt(0);

                        foreach(var l in linesIndex.ToList())
                        {
                            linesIndex[linesIndex.IndexOf(l)] = l - index;
                        }

                    }
                }


                    mainConsole.SetNeedsDisplay();
            });
        } 

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

                MenuBar = new MenuBar();
                FrameView mainFram = new FrameView();
                FrameView switchFram = new FrameView();
                FrameView modFram = new FrameView();
                List<MenuBarItem> items = new List<MenuBarItem>();
                listView = new ListView()
                {
                    X = 1,
                    Y = 2,
                    Height = Dim.Fill(),
                    Width = Dim.Fill(1),
                    //ColorScheme = Colors.TopLevel,
                    AllowsMarking = true,
                    AllowsMultipleSelection = true
                };

                Label labelFoundMods = new Label();
                labelFoundMods.X = 1;
                labelFoundMods.Y = 1;
                labelFoundMods.Text = "";
                Button btnDownloadMods = new Button("Install");
                btnDownloadMods.Visible = false;
                btnDownloadMods.X = Pos.Percent(60);
                btnDownloadMods.Y = 1;
                Button chkAll = new Button("Check All");
                chkAll.X = Pos.Right(btnDownloadMods) + 1;
                chkAll.Y = 1;
                chkAll.Visible = false;
                chkAll.Clicked += () =>
                {
                    if (listView.Source== null)
                        return;
                    var i = 0;
                    foreach (var mod in listView.Source.ToList())
                    {
                        listView.Source.SetMark(i, !checkall);
                        i++;
                    }

                    checkall = !checkall;
                    if(checkall)
                        chkAll.Text = "Uncheck All";
                    else
                        chkAll.Text = "Check All";
                };
                Task modsTask = null;
                btnDownloadMods.Clicked += () =>
                {
                    if (listView.Source.Count == 0 || (modsTask != null && !modsTask.IsCompleted)  )
                        return;

                    List<BananaMod> mods = listView.Source.ToList() as List<BananaMod>;
                    modsTask = new Task(() =>
                    {
                        List<BananaMod> markedMods = new List<BananaMod>();
                        var i = 0;
                        foreach (var mod in mods)
                        {
                            if (listView.Source.IsMarked(i))
                                markedMods.Add(mod);
                            i++;
                        }

                        List<Task> tasks = new List<Task>();
                        var index = 0;
                        var nbTaskDone = 0;
                        foreach (BananaMod mod in markedMods)
                        {
                            Task task = new Task(() =>
                            {
                                mod.download();
                                mod.extract();
                                lock (lockObj)
                                {
                                    nbTaskDone++;
                                    progress.Report((float)nbTaskDone / (float)markedMods.Count);
                                }
                            });
                            tasks.Add(task);
                            task.Start();
                            index++;

                            if (index % 3 == 0)
                                Task.WaitAll(tasks.ToArray());

                        }

                        Task.WaitAll(tasks.ToArray());

                    });
                    modsTask.Start();
                };


                var autoStartItem= new MenuItem("AutoStart Yuzu", null,null, null, null, Key.Null);
               Action autoStartAction = () =>
               {
                 autoStartItem.Checked = !autoStartItem.Checked;
                 this.autoStartYuzu = autoStartItem.Checked;
                 setSettings();
               };
                autoStartItem.Action = autoStartAction;
                autoStartItem.Checked = this.autoStartYuzu;
                autoStartItem.CheckType = MenuItemCheckStyle.Checked;


                var confirmDownload = new MenuItem("Ask download", null, null, null,null, Key.Null);
                Action actionConfirmD = () =>
                {
                    confirmDownload.Checked = !confirmDownload.Checked;
                    this.confirmDownload = confirmDownload.Checked;
                    setSettings();
                };
                confirmDownload.Action = actionConfirmD;
                confirmDownload.Checked = this.confirmDownload;
                confirmDownload.CheckType = MenuItemCheckStyle.Checked;


                var backupSave = new MenuItem("Backup Save at start", null, null, null, null, Key.Null);
                Action actionBackupSave = () =>
                {
                    backupSave.Checked = !backupSave.Checked;
                    this.backupSave = backupSave.Checked;
                    setSettings();
                };
                backupSave.Action = actionBackupSave;
                backupSave.Checked = this.backupSave;
                backupSave.CheckType = MenuItemCheckStyle.Checked;



                items.Add(new MenuBarItem("[Settings]", new MenuItem[]
                {autoStartItem,
                confirmDownload,
                backupSave
                }));



                Add(MenuBar);
                items.Add(new MenuBarItem("[Restore latest backup]", null, restoreLatestBackup));
                items.Add(new MenuBarItem("[Get last version]", null, checkUpdate));

                MenuBar.Menus = items.ToArray();
       

                Task t = new Task(() =>
                {
                    scanTitlesIdAndGetName();
                    if (this.games.Count > 0)
                        items.Add(new MenuBarItem("[Mods]", this.games.Select(g => new MenuItem(g.name, null, () =>
                        {
                            Task task = new Task(() =>
                            {
                                g.loadMods(this.progress);
                                modFram.Title = "Mods for " + g.name;
                                labelFoundMods.Text = g.validBananaMods.Count + " mods found";
                                listView.SetSource(g.validBananaMods);
                                btnDownloadMods.Visible = true;
                                chkAll.Visible = true;
                            });
                             task.Start();
                        }, null, null, Key.Null)).ToArray()));
                    else
                        items.Add(new MenuBarItem("Mods", new MenuItem[] { new MenuItem("No game found", null, null, null, null, Key.Null) }));
                    MenuBar.Menus = items.ToArray();
                });

                t.Start();

          
                mainFram.X = 0;
                mainFram.Y = 1;
                mainFram.Width = Dim.Fill();
                mainFram.Height = Dim.Percent(49);
                mainFram.Title = "Logs";
                mainConsole = new Label();
                mainConsole.X = 0;
                mainConsole.Y = 0;
                mainFram.Add(mainConsole);
                progressBar = new ProgressBar();
                progressBar.X = 0;
                progressBar.Y = 0;
                progressBar.Width = Dim.Fill();
                progressBar.Height = 1;
                progressBar.Visible = false;
                Add(mainFram);

                switchFram.Title = "Switch build";
                switchFram.X = 0;
                switchFram.Y = Pos.Percent(51);
                switchFram.Width = Dim.Percent(17);
                switchFram.Height = Dim.Percent(49);
                Label switchLabel = new Label();
                switchLabel.X = 0;
                switchLabel.Y = 0;
                switchLabel.Width = Dim.Fill();
                switchLabel.Height = 2;
                switchLabel.Text = "Enter build number: ";
                switchFram.Add(switchLabel);
                TextField textField = new TextField();
                textField.TextChanging += (args) =>
                {
                    if (args.NewText.Any(c => !char.IsDigit((char)c) && !char.IsControl((char)c)))
                        args.Cancel = true;
                };
                textField.X = 0;
                textField.Y = 2;
                textField.Width = Dim.Percent(80);
                textField.Height = 1;
                textField.Text = "";
                switchFram.Add(textField);
                Button button = new Button("Install");
                button.X = 0;
                button.Y = Pos.Bottom(textField)+1;
                button.Width = Dim.Percent(50);
                button.Height = 1;
                button.Clicked +=  () =>
                {
                    Task t = null;
                    if (textField.Text.Length > 0  && (t == null || t.IsCompleted))
                    {
                       t= new Task(() =>
                        {
                            downloadRelease(new Release(textField.Text.ToString(), true));
                        });
                        t.Start();
                    }
                };
                switchFram.Add(button);

                modFram.Title = "Mods";
                modFram.X = Pos.Percent(18);
                modFram.Y = Pos.Percent(51);
                modFram.Width = Dim.Fill();
                modFram.Height = Dim.Percent(49);
                modFram.Visible = true;
                modFram.Add(labelFoundMods);
                modFram.Add(listView);
                modFram.Add(btnDownloadMods);
                modFram.Add(chkAll);


                listView.RowRender += ListView_RowRender;

                var _scrollBar = new ScrollBarView(listView, true);

                _scrollBar.ChangedPosition += () => {
                    listView.TopItem = _scrollBar.Position;
                    if (listView.TopItem != _scrollBar.Position)
                    {
                        _scrollBar.Position = listView.TopItem;
                    }
                    listView.SetNeedsDisplay();
                };

                _scrollBar.OtherScrollBarView.ChangedPosition += () => {
                    listView.LeftItem = _scrollBar.OtherScrollBarView.Position;
                    if (listView.LeftItem != _scrollBar.OtherScrollBarView.Position)
                    {
                        _scrollBar.OtherScrollBarView.Position = listView.LeftItem;
                    }
                    listView.SetNeedsDisplay();
                };

                listView.DrawContent += (e) => {
                    if(listView.Source != null)
                    {
                        _scrollBar.Size = listView.Source.Count - 1;
                        _scrollBar.Position = listView.TopItem;
                        _scrollBar.OtherScrollBarView.Size = listView.Maxlength - 1;
                        _scrollBar.OtherScrollBarView.Position = listView.LeftItem;
                        _scrollBar.Refresh();
                    }
                };


                Add(switchFram);
                Add(modFram);
                Add(progressBar);



    
                Application.RootKeyEvent += Application_RootKeyEvent;
                Application.Init();
                Console.SetWindowSize(Console.LargestWindowWidth, Console.LargestWindowHeight);
                this.SetNeedsDisplay();
            });

        }



        private void ListView_RowRender(ListViewRowEventArgs obj)
        {
            if (obj.Row == listView.SelectedItem)
            {
                return;
            }
            if (listView.AllowsMarking && listView.Source.IsMarked(obj.Row))
            {
                obj.RowAttribute = new Terminal.Gui.Attribute(Color.BrightRed, Color.BrightYellow);
                return;
            }
            if (obj.Row % 2 == 0)
            {
                obj.RowAttribute = new Terminal.Gui.Attribute(Color.BrightGreen, Color.Magenta);
            }
            else
            {
                obj.RowAttribute = new Terminal.Gui.Attribute(Color.BrightMagenta, Color.Green);
            }
        }

        private bool Application_RootKeyEvent(KeyEvent arg)
        {
            if (_waitInput && mainConsole.SuperView.SuperView.Visible)
            {
                if (arg.Key == Key.Enter)
                {
                    _waitInput = false;
                    addTextConsole("\n");
                }
                else if (arg.Key == Key.DeleteChar || arg.Key == Key.Backspace)
                {
                    if (_input.Length >= 1)
                    {
                        mainConsole.Text = mainConsole.Text.Substring(0, mainConsole.Text.Length - 1);
                        _input = _input.Substring(0, _input.Length - 1);
                    }

                }
                else if (!char.IsControl((char)arg.Key))
                {

                    addTextConsole(((char)arg.Key).ToString());
                    _input += ((char)arg.Key).ToString();

                }
                return true;
            }
            return false;
        }

        public  UI()
        {
            getSettings();
            InitializeComponent();
            purgeUncessaryFiles();

            Task.Run(() =>
            {
                try
                {

                    while (mainConsole == null || mainConsole.SuperView == null)
                    {
                        System.Threading.Thread.Sleep(1500);
                        Console.WriteLine("Initialise UI...");
                    }
                    getCurrentVersion();
                    _saveBackup();
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
                addTextConsole("Kill yuzu process\n");

            foreach (Process p in processes)
            {
                p.Kill();
            }
        }
		
		private  void waitYuzuLaunch(){
            var timer = 10000;

            while (!autoStartYuzu)
            {
                System.Threading.Thread.Sleep(1000);
            }

            if(!System.IO.File.Exists(currentExe))
            {
                addTextConsole("Yuzu not found\n");
                return;
            }

            addTextConsole("Starting Yuzu...\n");
            Process p = new Process();
            p.StartInfo.FileName = currentExe;
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = true;
            p.Start();
			
			while(p.MainWindowHandle==IntPtr.Zero && RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||( timer > 0))
            {
				System.Threading.Thread.Sleep(1000);
                timer -= 1000;
				Console.Write(".");
                p = Process.GetProcessById(p.Id);
            }
            
            System.Environment.Exit(0);
            Application.Shutdown();
        

        }

        private  void getSettings()
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

            Utils.init7ZipPaht();
            initAppPath();
        }

        private void setSettings()
        {
            StreamWriter writer = new StreamWriter("launchUpdater.txt");
            writer.Write(currentExe + "|" + autoStartYuzu + "|" + confirmDownload + "|" + backupSave);
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
                        addTextConsole("Yuzu EA version found : " + currentVersion + "\n");
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
                        addTextConsole("Yuzu EA version found : " + currentVersion + "\n");
                    }
                }
            }
            catch(ArgumentOutOfRangeException e) 
            { 
                addTextConsole("Its seem you dont use EA Yuzu version." + "\n");
                addTextConsole("You must use EA version from pineapple here : https://github.com/pineappleEA/pineapple-src/releases." + "\n");
            }



        } 

        public  void checkVersion()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
            addTextConsole("Check for YUZU EA update" + "\n");


            String src =  httpClient().GetAsync("https://github.com/pineappleEA/pineapple-src/releases/").Result.Content.ReadAsStringAsync().Result;
            string[] releaseVersions = src.Split(new String[] { "h2 class=\"sr-only\"" }, StringSplitOptions.RemoveEmptyEntries).Skip(1).ToArray(); 
            releaseVersions = releaseVersions.Take(releaseVersions.Length - 1).ToArray();

            for(int i = 0;  i < releaseVersions.Length; i++)
            {
                releases.Add(new Release(releaseVersions[i]));
            }

            if(currentVersion == null)
                addTextConsole("No version found" + "\n");
            
            myCurrentRelease = releases.Where(x => x.version == currentVersion).FirstOrDefault();
            if(myCurrentRelease == null)
                myCurrentRelease = new Release(currentVersion,true);
            
            if (myCurrentRelease != releases.FirstOrDefault())
            {
                addTextConsole("Retrieve PRs from github" + "\n");
                for (var p = 0; p < 4; p++)
                {
                    progress.Report((p + 1) * 0.25f);
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

                addTextConsole("New Version found , pass from " + currentVersion + " to " + releases[0].version + "\n" + getChangeLog());
                if(confirmDownload)
                {
                    addTextConsole("Do you want to download it ? (y/n)" + "\n");
                    string answer = waitInput().Result;
                    if (answer != "y")
                        return;
                }
                 downloadRelease(releases[0]);
            }
            else
            {
                addTextConsole("Yuzu is up to date" + "\n");
            }

        }

        private void  downloadRelease(Release release)
        {
			try{
                killYuzus();
                addTextConsole("Downloading "+ release.version + " version" + "\n");

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    String fileName = System.IO.Path.GetFileName(new Uri(release.downloadUrl).LocalPath);
                    download(release.downloadUrl, fileName).GetAwaiter().GetResult();
                    addTextConsole("Make executable" + "\n");
                    Process.Start("chmod", "+x " + fileName);
                    addTextConsole("Remove old version" + "\n");
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
                    addTextConsole("Remove zip file" + "\n");
                    System.IO.File.Delete("YuzuEA.zip");
                    string[] files = Directory.GetFiles(System.Environment.CurrentDirectory + "/yuzu-windows-msvc-early-access");
                    addTextConsole("Move files and directory to root directory" + "\n");
                    if (System.IO.File.Exists(currentExe))
                        System.IO.File.Delete(currentExe);
                    Utils.DirectoryCopyAndDelete(System.Environment.CurrentDirectory + "/yuzu-windows-msvc-early-access", System.Environment.CurrentDirectory);
                    System.IO.File.Move("yuzu.exe", currentExe);
                }
			}
			catch(Exception ex){
				addTextConsole(ex.StackTrace + "  " + ex.Message + "\n");
				Console.ReadLine();
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

                await _httpClient.DownloadAsync(uri, file, progress);
            }

        }

        private IProgress<float> progress
        {
            get; set; 
        
        } = new Progress<float>(p => {
                if (progressBar.Visible == false)
                {
                    progressBar.Y = progressBar.SuperView.Bounds.Bottom - 1;
                    progressBar.Visible = true;
                }

                progressBar.Fraction = p;

                if (p == 1)
                    progressBar.Visible = false;
            });

        private  void scanTitlesIdAndGetName()
        {
            if (games.Count == 0)
            {

               String[] directorys = Directory.GetDirectories(pathApp + "/sdmc/atmosphere/contents").Select(d => Path.GetFileName(d)).ToArray();

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
                string sourceDir = Path.Combine(pathApp, "nand", "user", "save");
                if (this.backupSave && Directory.Exists(sourceDir))
                {
                    if(!Directory.Exists(Path.Combine(Environment.CurrentDirectory, "savesBackup")))
                        Directory.CreateDirectory(Path.Combine(Environment.CurrentDirectory, "savesBackup"));   

                    string zipFilePath = Path.Combine(Environment.CurrentDirectory,"savesBackup", DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".7z");

                    SevenZip.SevenZipCompressor compressor = new SevenZip.SevenZipCompressor();
                    compressor.CompressionLevel = SevenZip.CompressionLevel.Ultra;
                    compressor.CompressionMethod = SevenZip.CompressionMethod.Lzma2;
                    compressor.CompressionMode = SevenZip.CompressionMode.Create;

                    compressor.CompressDirectory(sourceDir, zipFilePath);
           
                }

                _deleteOldBackups();

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.StackTrace + "  " + ex.Message + "\n");
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


        private void restoreLatestBackup()
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
                    addTextConsole("Restoring latest backup: " + latestBackup.CreationTime.ToString() + "\n");
                    SevenZip.SevenZipExtractor extractor = new SevenZip.SevenZipExtractor(latestBackup.FullName);
                    extractor.ExtractArchive(Path.Combine(pathApp, "nand", "user", "save"));
                    addTextConsole("Restore complete.\n");
                }
                else
                {
                    addTextConsole("No backup file found.\n");
                }
            }
            else
            {
                addTextConsole("Backup directory does not exist.\n");
            }
        }


        private void initAppPath()
        {
             pathApp = Path.Combine(System.Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) ,"yuzu");

            if (!Directory.Exists(pathApp))
                pathApp = "user";
            if (!Directory.Exists(pathApp + "/sdmc/atmosphere/contents"))
                return;
        }


        private void purgeUncessaryFiles()
        {
            //FIND all FILLS THAT BEGIN WITH yuzu-windows-msvc-source- in current directory
           var files = Directory.GetFiles(Environment.CurrentDirectory, "yuzu-windows-msvc-source-*");
            foreach (var file in files)
            {
                try
                {
                    System.IO.File.Delete(file);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.StackTrace + "  " + ex.Message + "\n");
                    Console.ReadLine();
                }
            }

        }


        private void checkUpdate()
        {
            addTextConsole("W'll restart for updates...\n");

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
