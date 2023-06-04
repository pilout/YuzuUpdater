using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terminal.Gui;

namespace YuzuEAUpdater.UI
{
    public class MainUI
    {
        public static Label mainConsole;
        private static ProgressBar progressBar;
        private static ListView listView;
        private  bool checkall = false;
        private Boolean _waitInput = false;
        private string _input = "";
        private static List<int> linesIndex = new List<int>();

        public MainUI(MainWindow window) 
        {
            var MenuBar = new MenuBar();
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
                if (listView.Source == null)
                    return;
                var i = 0;
                foreach (var mod in listView.Source.ToList())
                {
                    listView.Source.SetMark(i, !checkall);
                    i++;
                }

                checkall = !checkall;
                if (checkall)
                    chkAll.Text = "Uncheck All";
                else
                    chkAll.Text = "Check All";
            };
            Task modsTask = null;
            btnDownloadMods.Clicked += () =>
            {
                if (listView.Source.Count == 0 || (modsTask != null && !modsTask.IsCompleted))
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
                            lock (MainWindow.lockObj)
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


            var autoStartItem = new MenuItem("AutoStart Yuzu", null, null, null, null, Key.Null);
            Action autoStartAction = () =>
            {
                autoStartItem.Checked = !autoStartItem.Checked;
                window.autoStartYuzu = autoStartItem.Checked;
                window.setSettings();
            };
            autoStartItem.Action = autoStartAction;
            autoStartItem.Checked = window.autoStartYuzu;
            autoStartItem.CheckType = MenuItemCheckStyle.Checked;


            var confirmDownload = new MenuItem("Ask download", null, null, null, null, Key.Null);
            Action actionConfirmD = () =>
            {
                confirmDownload.Checked = !confirmDownload.Checked;
                window.confirmDownload = confirmDownload.Checked;
                window.setSettings();
            };
            confirmDownload.Action = actionConfirmD;
            confirmDownload.Checked = window.confirmDownload;
            confirmDownload.CheckType = MenuItemCheckStyle.Checked;


            var backupSave = new MenuItem("Backup Save at start", null, null, null, null, Key.Null);
            Action actionBackupSave = () =>
            {
                backupSave.Checked = !backupSave.Checked;
                window.backupSave = backupSave.Checked;
                window.setSettings();
            };
            backupSave.Action = actionBackupSave;
            backupSave.Checked = window.backupSave;
            backupSave.CheckType = MenuItemCheckStyle.Checked;


            var optimizePerfomance = new MenuItem("Optimise Perfomance", null, null, null, null, Key.Null);
            Action actionOptimizePerfomance = () =>
            {
                optimizePerfomance.Checked = !optimizePerfomance.Checked;
                window.optimizePerf = optimizePerfomance.Checked;
                window.setSettings();
            };
            optimizePerfomance.Action = actionOptimizePerfomance;
            optimizePerfomance.Checked = window.optimizePerf;
            optimizePerfomance.CheckType = MenuItemCheckStyle.Checked;


            var killProccessCPU = new MenuItem("Kill Proccess CPU", null, null, null, null, Key.Null);
            Action actionKillProccessCPU = () =>
            {
                killProccessCPU.Checked = !killProccessCPU.Checked;
                window.killCpuProccess = killProccessCPU.Checked;
                window.setSettings();
            };
            killProccessCPU.Action = actionKillProccessCPU;
            killProccessCPU.Checked = window.killCpuProccess;
            killProccessCPU.CheckType = MenuItemCheckStyle.Checked;


            items.Add(new MenuBarItem("[Settings]", new MenuItem[]
            {autoStartItem,
                confirmDownload,
                backupSave,
                optimizePerfomance,
                killProccessCPU
            }));



            window.Add(MenuBar);
            items.Add(new MenuBarItem("[Restore latest backup]", null, window.restoreLatestBackup));
            items.Add(new MenuBarItem("[Get last version]", null, window.checkUpdate));

            MenuBar.Menus = items.ToArray();


            Task t = new Task(() =>
            {
                window.scanTitlesIdAndGetName();
                if (window.games.Count > 0)
                    items.Add(new MenuBarItem("[Mods]", window.games.Select(g => new MenuItem(g.name, null, () =>
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
            window.Add(mainFram);

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
            button.Y = Pos.Bottom(textField) + 1;
            button.Width = Dim.Percent(50);
            button.Height = 1;
            button.Clicked += () =>
            {
                Task t = null;
                if (textField.Text.Length > 0 && (t == null || t.IsCompleted))
                {
                    t = new Task(() =>
                    {
                        window.downloadRelease(new Release(textField.Text.ToString(), true));
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
                if (listView.Source != null)
                {
                    _scrollBar.Size = listView.Source.Count - 1;
                    _scrollBar.Position = listView.TopItem;
                    _scrollBar.OtherScrollBarView.Size = listView.Maxlength - 1;
                    _scrollBar.OtherScrollBarView.Position = listView.LeftItem;
                    _scrollBar.Refresh();
                }
            };


            window.Add(switchFram);
            window.Add(modFram);
            window.Add(progressBar);
            Application.RootKeyEvent += Application_RootKeyEvent;
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


        public IProgress<float> progress
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


        public static void addTextConsole(String text)
        {

            int[] indexs = text.Select((b, i) => b == '\n' ? i + mainConsole.Text.Length : -1).Where(i => i != -1).ToArray();
            linesIndex.AddRange(indexs);

            Application.MainLoop.Invoke(() =>
            {
                mainConsole.Text += (text);

                if (mainConsole.SuperView.Bounds.Height > 0)
                {
                    while (linesIndex.Count > 0 && (mainConsole.Frame.Bottom) > mainConsole.SuperView.Bounds.Height)
                    {
                        var index = linesIndex.First();

                        if (index < mainConsole.Text.Length && index >0)
                            mainConsole.Text = mainConsole.Text.Substring(index);
                        else if(index >0)
                            mainConsole.Text = "";

                        linesIndex.RemoveAt(0);

                        foreach (var l in linesIndex.ToList())
                        {
                            linesIndex[linesIndex.IndexOf(l)] = l - index;
                        }

                    }
                }


                mainConsole.SetNeedsDisplay();
            });
        }


        private bool Application_RootKeyEvent(KeyEvent arg)
        {
            if (_waitInput && MainUI.mainConsole.SuperView.SuperView.Visible)
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
                        MainUI.mainConsole.Text = MainUI.mainConsole.Text.Substring(0, MainUI.mainConsole.Text.Length - 1);
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
    }
            
}
