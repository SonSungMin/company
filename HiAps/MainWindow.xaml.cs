using Amisys.Framework.Presentation.AmiMainShell;
using Amisys.Framework.Presentation.Definitions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Amisys.Infrastructure.HHIInfrastructure.Defintions;
using FirstFloor.ModernUI.Windows.Controls;
using System.ComponentModel;
using NewMainShell.Views;
using NewMainShell.DataModel;
using Amisys.Framework.Presentation.DataAccess;
using FirstFloor.ModernUI.Presentation;
using System.Threading;
using Amisys.Framework.Infrastructure.DataModels;
using NewMainShell.Controls; 
using System.Windows.Threading;
using Amisys.Framework.Infrastructure.Utility;
using System.Data;
using NewMainShell.Dialogs;
using NewMainShell.Definitions;
using System.Configuration;
using System.IO;
using Amisys.Infrastructure.HHIInfrastructure.Utility;
using Amisys.Framework.Presentation.AmiMainShell.Views;

namespace NewMainShell
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : APSSModernWindow, INotifyPropertyChanged
    {
        #region Private Members, Service / Manager

        private MainFrame _Frame;
        private Shell _MainControl;
        private string moduleName = "NewMainShell";
        private NaviManager _NaviManager = new NaviManager();
        private UpdateFileHelper fileHelper;
        protected UpdateFileHelper FileHelper
        {
            get
            {
                if (fileHelper == null)
                    fileHelper = new UpdateFileHelper();
                return fileHelper;
            }
        }

        private readonly BackgroundWorker worker = new BackgroundWorker();

        #endregion



        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = this;
            SetWindowSize();

            this.Loaded += OnLoaded;

            AppearanceManager.Current.AccentColor = Color.FromRgb(0x6a, 0x00, 0xff);
            SetVisibilityOption();
        }

        #region properties

        private Visibility _VisibilityOption = Visibility.Visible;

        public Visibility VisibilityOption
        {
            get
            {
                return _VisibilityOption;
            }
            set
            {
                _VisibilityOption = value;
                NotifyPropertyChanged("VisibilityOption");
            }

        }

        public bool IsPrevious
        {
            get
            {
                return _NaviManager.IsPrevious;
            }
        }

        public bool IsNext
        {
            get
            {
                return _NaviManager.IsNext;
            }
        }

        private bool _IsShowMenu = true;
        public bool IsShowMenu
        {
            get { return _IsShowMenu; }
            set
            {
                if (_IsShowMenu == value) return;
                _IsShowMenu = value;
                NotifyPropertyChanged("IsShowMenu");
            }
        }


        private int _HeaderFontSize = 15;
        public int HeaderFontSize
        {
            get { return _HeaderFontSize; }
            set
            {
                if (_HeaderFontSize == value) return;
                _HeaderFontSize = value;
                NotifyPropertyChanged("HeaderFontSize");
            }
        }

        #endregion

        private void OnLoaded(object sender, EventArgs e)
        {
            _NaviManager.AddPrevious(NaviType.Home, null);

            AppearanceManager.Current.ThemeSource = new Uri("/Themes/AmisysLogo.xaml", UriKind.Relative);

            worker.DoWork += worker_DoWork;
            worker.RunWorkerCompleted += worker_RunWorkerCompleted;
            worker.RunWorkerAsync();
        }

        [Conditional("RELEASE")]
        private void SetVisibilityOption()
        {
            VisibilityOption = Visibility.Collapsed;
        }

        private void worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (_Frame != null && _Frame.MenuControl != null && _Frame.MenuControl._TreeViewItemDic != null)
            {
                LoadUserViewOption();
            }
        }

        private void worker_DoWork(object sender, DoWorkEventArgs e)
        {
            while (_Frame == null || _Frame.MenuControl == null || _Frame.MenuControl._TreeViewItemDic == null)
            {
                Thread.Sleep(100);
            }
        }

        private void SetWindowSize()
        {
            int widthSum = 0, minHeight = 99999;
            System.Windows.Forms.Screen[] screenList = System.Windows.Forms.Screen.AllScreens;
            foreach (System.Windows.Forms.Screen sc in screenList)
            {
                widthSum += sc.WorkingArea.Width;
                if (minHeight > sc.WorkingArea.Height) minHeight = sc.WorkingArea.Height;
            }

            this.Width = widthSum;
            this.Height = minHeight;
            this.Left = 0;
            this.Top = 0;
        }

        BusyWindow _BusyWin = null;
        private void OnRunWorkspace(object sender, EventArgs e)
        {
            AmiMenuTreeViewItem item = null;
            if (sender is FavoriteItem) item = ((FavoriteItem)sender).MenuItem;
            else if (sender is AmiMenuTreeViewItem) item = (AmiMenuTreeViewItem)sender;

            RunWorkspace(item);

            _NaviManager.AddPrevious(NaviType.Workspace, item);
            UpdateHistory();
        }

        private void UpdateHistory()
        {
            NotifyPropertyChanged("IsPrevious");
            NotifyPropertyChanged("IsNext");
        }

        private void ThreadStartingPoint()
        {
            _BusyWin = new BusyWindow();
            _BusyWin.Show();
            System.Windows.Threading.Dispatcher.Run();
        }

        public void RunWorkspace(int nodeId)
        {
            if (_Frame == null || _Frame.MenuControl == null) return;
            AmiMenuTreeViewItem menu = _Frame.MenuControl.GetMenuItem(nodeId);
            if (menu != null) RunWorkspace(menu);
        }

        public void RunWorkspace(AmiMenuTreeViewItem menu)
        {
            this.IsEnabled = false;

            Thread newWindowThread = new Thread(new ThreadStart(ThreadStartingPoint));
            newWindowThread.SetApartmentState(ApartmentState.STA);
            newWindowThread.IsBackground = true;
            newWindowThread.Start();

#if RUN_WORKSPACE_WINDOW
            CreateMainControl(menu);
#else
            _Frame.IsHome = false;
            if (_MainControl == null)
            {
                CreateMainControl(menu);
            }
#endif
            if (this.ContentSource.ToString() == "/Themes/Settings.xaml")
            {
                this.ContentObject = null;
                this.ContentObject = _Frame;
            }

            NewMainShell.Utilities.NextStepDispatcher next = new NewMainShell.Utilities.NextStepDispatcher();
            next.NextStep += OnOpenWorkspaceNextStep;
            next.Tag = menu;
            next.Start();
        }

        private void CreateMainControl(AmiMenuTreeViewItem menu = null)
        {
            ShellConstants.ArgList = App.mArgs;
            if (menu != null)
                ShellConstants.CurrentMenuItem = menu;

            AmiMefBootStrapper boot = new AmiMefBootStrapper();
            boot.MainWindow = this;
            boot.Run();

            ShellConstants.Catalog = boot.Catalog;

            _MainControl = (Shell)boot.GetShell();
#if RUN_WORKSPACE_WINDOW
            _MainControl.ControlLoaded += OnMainControlLoaded;
#else
            _Frame.ChangeContent(_MainControl);
#endif
        }

        #region events

        private void OnOpenWorkspaceNextStep(object sender, EventArgs e)
        {
            NewMainShell.Utilities.NextStepDispatcher cur = (NewMainShell.Utilities.NextStepDispatcher)sender;
            AmiMenuTreeViewItem menu = (AmiMenuTreeViewItem)cur.Tag;
#if RUN_WORKSPACE_WINDOW
#else
            if (_MainControl != null)
            {
                _MainControl.OpenWorkspace(menu);
            }

            this.IsEnabled = true;
            if (_BusyWin != null)
            {
                _BusyWin.Dispatcher.Invoke(DispatcherPriority.Normal
                , new Action(delegate { _BusyWin.Close(); }));
            }
#endif
#if RUN_WORKSPACE_WINDOW
            WorkspaceWindow win = new WorkspaceWindow(_MainControl, menu);
            win.Tag = this;
            _MainControl.Tag = menu;
            win.Show();
#endif
        }
#if RUN_WORKSPACE_WINDOW
        private void OnMainControlLoaded(object sender, EventArgs e)
        {
            this.IsEnabled = true;
            if (_BusyWin != null)
            {
                _BusyWin.Dispatcher.Invoke(DispatcherPriority.Normal
                , new Action(delegate { _BusyWin.Close(); }));
            }

            AmiMainControl con = (AmiMainControl)sender;
            con.ControlLoaded -= OnMainControlLoaded;
            con.OpenWorkspace((AmiMenuTreeViewItem)con.Tag);
        }
#endif

        private void APSSModernWindow_Navigated(object sender, FirstFloor.ModernUI.Windows.Navigation.NavigationEventArgs e)
        {
            if (_Frame == null)
            {
                if (e.Content is MainFrame)
                {
                    _Frame = (MainFrame)e.Content;
                    _Frame.LoadMenu();
                    _Frame.FavoriteControl.Runworkspace += OnRunWorkspace;
                    _Frame.MenuControl.Runworkspace += OnRunWorkspace;
                }
            }
        }
        #region 사용자 설정 화면 옵션

        public class InitWorkspaceInfo
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        public void LoadUserViewOption()
        {
            InitWorkspaceInfo InitWorkspace = (InitWorkspaceInfo)XmlSerializeUtility.ImportFromXml(GetUserViewOptionFilename(), typeof(InitWorkspaceInfo));

            if (InitWorkspace != null)
            {
                AmiMenuTreeViewItem item = _Frame.GetWorkspace(InitWorkspace.Id.ToString());

                if (item != null)
                    RunWorkspace(item);
            }
        }

        public string GetUserViewOptionFilename()
        {
            string filename = "Options\\Option_" + this.moduleName + ".xml";
            return AmiFileUtility.GetAppBasedAbsolutePath(filename);
        }

        #endregion

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);

            if (_Frame != null)
            {             
                _Frame.SaveFavoriteListToXML();
            }

            if (_MainControl != null)
                _MainControl.CloseWorkspace();
        }
        #endregion

        #region left command event

        private void PrevButton_Click(object sender, RoutedEventArgs e)
        {
            NaviItem navi = _NaviManager.GetPrevious();
            UpdateHistory();

            if (navi == null) return;

            if (navi.Type == NaviType.Home)
            {
                _Frame.IsHome = true;
                IsShowMenu = true;
                this.ContentObject = null;
                this.ContentObject = _Frame;
            }
            else if (navi.Type == NaviType.ThemeSetting)
            {
                IsShowMenu = false;
                this.ContentSource = null;
                this.ContentSource = new Uri("/Themes/Settings.xaml", UriKind.Relative);
            }
            else
            {
                IsShowMenu = true;
                RunWorkspace(navi.MenuItem);
            }
        }

        private void HomeButton_Click(object sender, RoutedEventArgs e)
        {
            if (_Frame.IsHome == true && object.ReferenceEquals(_Frame, this.GetContent())) return;

            if (_MainControl != null)
                _MainControl.CloseWorkspace();

            IsShowMenu = true;
            _Frame.IsHome = true;
            this.ContentObject = null;
            this.ContentObject = _Frame;
            _NaviManager.AddPrevious(NaviType.Home, null);
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            NaviItem navi = _NaviManager.GetNext();
            UpdateHistory();

            if (navi == null) return;

            if (navi.Type == NaviType.Home)
            {
                _Frame.IsHome = true;
                IsShowMenu = true;
                this.ContentObject = null;
                this.ContentObject = _Frame;
            }
            else if (navi.Type == NaviType.ThemeSetting)
            {
                IsShowMenu = false;
                this.ContentSource = null;
                this.ContentSource = new Uri("/Themes/Settings.xaml", UriKind.Relative);
            }
            else
            {
                IsShowMenu = true;
                RunWorkspace(navi.MenuItem);
            }
        }

        private void ShowAmisysButton_Click(object sender, RoutedEventArgs e)
        {
            AmisysInfoWindow afWindow = new AmisysInfoWindow();
            afWindow.ShowDialog();
        }

        /// <summary>
        /// 초기화면 지정 버튼
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SetInitWorkspace_ItemClick(object sender, RoutedEventArgs e)
        {            
        }

        /// <summary>
        /// 도움말 보기 버튼
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Help_Click(object sender, RoutedEventArgs e)
        {
            ShowHelpFile();
        }

        #endregion

        #region right command events

        private void MenuButton_Click(object sender, RoutedEventArgs e)
        {
            if (_Frame == null) return;
            _Frame.ShowMenu = !_Frame.ShowMenu;
        }

        private void ThemeButton_Click(object sender, RoutedEventArgs e)
        {
            this.ContentSource = null;
            this.ContentSource = new Uri("/Themes/Settings.xaml", UriKind.Relative);

            _NaviManager.AddPrevious(NaviType.ThemeSetting, null);
            UpdateHistory();
        }

        private void CodeTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
        }

        private void YardButton_Click(object sender, RoutedEventArgs e)
        {
            AmiMenuTreeViewItem menu = _Frame.GetWorkspace(WorkspaceCode.YardManagerCode);
            if (menu != null) OnRunWorkspace(menu, null);
        }

        private void MenuReloadButton_Click(object sender, RoutedEventArgs e)
        {
            _Frame.MenuControl.IsLoadedMenu = false;
            _Frame.LoadMenu();
        }

        private void MenuEditButton_Click(object sender, RoutedEventArgs e)
        {
            AmiMenuTreeViewItem menu = _Frame.GetWorkspace(WorkspaceCode.MenuEditorCode);
            if (menu != null) OnRunWorkspace(menu, null);
        }

        private void HelpDeskButton_Click(object sender, RoutedEventArgs e)
        {
            AmiMenuTreeViewItem menu = _Frame.GetWorkspace(WorkspaceCode.HelpDeskCode);
            if (menu != null) OnRunWorkspace(menu, null);
        }

        private void SaveLayoutButton_Click(object sender, RoutedEventArgs e)
        {
            if (_MainControl == null) return;
            _MainControl.PubSaveLayout();
        }

        private void ReadLayoutButton_Click(object sender, RoutedEventArgs e)
        {
        }

        private void DeleteLayoutButton_Click(object sender, RoutedEventArgs e)
        {
            if(_MainControl != null)
                _MainControl.DeleteLayout();
        }

        private void bLargeGlyph_ItemClick(object sender, RoutedEventArgs e)
        {
        }

        private void bSmallGlyph_ItemClick(object sender, RoutedEventArgs e)
        {
        }

        private void AddViewButton_Click(object sender, RoutedEventArgs e)
        {
            if (_Frame == null)
            {
                ModernDialog.ShowMessage("시스템 로딩 중입니다. 잠시후 시도하세요.", "HIAPS", MessageBoxButton.OK);
                return;
            }
            if (this._MainControl == null || _Frame.IsHome == true)
            {
                ModernDialog.ShowMessage("메인화면에서는 기능이 작동하지 않습니다.", "HIAPS", MessageBoxButton.OK);
                return;
            }

            List<BindableViewInfo> list = _Frame.MenuControl.GetAvailableViews(null);
            ShellDialogBase db = new ShellDialogBase("뷰 추가");
            AddViewPanel panel = new AddViewPanel();
            panel.ViewList = list;
            panel.Owner = db;
            db.Content = panel;

            if (db.ShowDialog() == true)
            {
                List<DataRow> selViews = GetSeletedViews(list);
                if (selViews.Count < 1) return;

                foreach (DataRow row in selViews) _MainControl.LoadModule(row);
            }
        }

        private List<DataRow> GetSeletedViews(List<BindableViewInfo> list)
        {

            List<DataRow> ret = new List<DataRow>();
            foreach (BindableViewInfo info in list)
            {
                if (info.IsChecked == true)
                {
                    ret.Add(info.Source);
                }

                recv_GetSeletedViews(ret, info);
            }
            return ret;
        }

        private void recv_GetSeletedViews(List<DataRow> list, BindableViewInfo parent)
        {

            foreach (BindableViewInfo info in parent.Childs)
            {
                if (info.IsChecked == true)
                {
                    list.Add(info.Source);
                }

                recv_GetSeletedViews(list, info);
            }
        }

        #endregion

        #region INotifyPropertyChanged Members
        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged(string info)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(info));
            }
        }
        #endregion


        #region 도움말 help

        private void APSSModernWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.F1)
            {
                ShowHelpFile();
            }
        }

        private string GetAFIXHelpFilePath()
        {
            string helpfilePath = "\\Help\\AFIX_Introduction.xls";
            if (_Frame != null && _Frame.MenuControl != null)
            {
                DataRow[] rows = _Frame.MenuControl._MenuTable.Select("[NodeId]=0");
                if (rows != null && rows.Count() > 0)
                {
                    string temphelpfilePath = rows[0]["HelpFilePath"].ToString();
                    if (string.IsNullOrEmpty(temphelpfilePath) == false)
                        helpfilePath = temphelpfilePath;
                }
            }
            return helpfilePath;
        }

        private void ShowHelpFile()
        {
            if (this._MainControl == null || _Frame.IsHome == true)
            {
                string helpfilePath = GetAFIXHelpFilePath();//"\\Help\\AFIX_Introduction.xls";
                DownloadAFIXIntroHelpFile(helpfilePath);
            }
            else if (this._MainControl != null)
            {
                ShowHelpFile(this._MainControl.CurrentWorkspace, _MainControl.CurrentViewName);
            }
        }

        public void ShowHelpFile(Shell main)
        {
            ShowHelpFile(main.CurrentWorkspace, main.CurrentViewName);
        }

        private void ShowHelpFile(AmiViewList amiViewList, string currentViewName, bool IsActvityView = true)
        {
            //화면에따라 헬프파일보여줌
            List<string> helpFielPathList = new List<string>();
            List<string> viewNameList = new List<string>();
            List<DataRow> assemblyList = new List<DataRow>();
            if (amiViewList != null)
            {
                foreach (DataRow row in amiViewList)
                {
                    string filePath = row["HelpFilePath"].ToString();
                    string nodeName = row["NodeName"].ToString();
                    string AssemblyName = row["AssemblyName"].ToString();
                    string viewName = row["ViewName"].ToString();

                    if (string.IsNullOrEmpty(viewName) == true)
                        viewName = "MainContentView";

                    if (string.IsNullOrEmpty(filePath) == false)
                    {
                        if (IsActvityView == true)
                        {
                            if (currentViewName.StartsWith(AssemblyName) == true
                                && currentViewName.EndsWith(viewName) == true)
                            {
                                helpFielPathList.Add(filePath);
                                viewNameList.Add(nodeName);
                                assemblyList.Add(row);
                            }
                        }
                        else
                        {
                            helpFielPathList.Add(filePath);
                            viewNameList.Add(nodeName);
                        }
                    }

                    if (currentViewName.StartsWith(AssemblyName) == true
                                && currentViewName.EndsWith(viewName) == true)
                    {
                        assemblyList.Add(row);
                    }
                }
            }
            if (helpFielPathList != null && helpFielPathList.Count > 0)
                DownloadHelpFile(helpFielPathList, viewNameList);
            else
            {
                ModernDialog.ShowMessage("등록된 도움말이 없습니다.", "", MessageBoxButton.OK);
                bool? yesNo = ModernDialog.ShowMessage("로컬 PC의 도움말 파일을 업로드하시겠습니까?", "", MessageBoxButton.YesNo);
                if (yesNo == true)
                {
                    RegisterHelpFileFromLocalPC(assemblyList);
                }
            }
        }

        private void RegisterHelpFileFromLocalPC(List<DataRow> assemblyList)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.FileName = "*.xls"; // |*.xlsxDefault file name             
            dlg.DefaultExt = ".xls"; //|*.xlsx
            dlg.Filter = "Excel97-2003 |*.xls";//|통합 문서 Excel 통합 문서| *.xlsx";
            Nullable<bool> result = dlg.ShowDialog();
            if (result == true)
            {
                string helpfilePath = dlg.FileName;

                string ftpHost = ConfigurationManager.AppSettings["UpdateFTPServer"].ToString();
                string userID = ConfigurationManager.AppSettings["UpdateFTPUserID"].ToString();
                string password = ConfigurationManager.AppSettings["UpdateFTPPassword"].ToString();
                int port = int.Parse(ConfigurationManager.AppSettings["UpdateFTPPort"].ToString());

                if (File.Exists(helpfilePath) == true)
                {
                    if (FileHelper != null)
                    {
                        string remoteFilePath = helpfilePath.Replace("\\", "/");
                        string[] tempString = remoteFilePath.Split('/');
                        int index = tempString.Count() - 1;
                        if (index >= 0)
                        {
                            string fileName = tempString[index];
                            string remoteDirectory = "Help";
                            remoteFilePath = "\\" + remoteDirectory + "\\" + fileName;
                            bool succeed = fileHelper.FileUpload(ftpHost, userID, password, port, remoteDirectory, remoteFilePath, helpfilePath);
                            if (succeed)
                            {
                                System.Windows.MessageBox.Show(string.Format("{0} 파일을 \n\n서버에 업로드하였습니다.", helpfilePath));
                                int upCnt = UpdateHelpFilePathIntoMenuTable(remoteFilePath, assemblyList[0]);
                            }
                            else
                                System.Windows.MessageBox.Show(string.Format("{0} 파일을 \n\n서버에 업로드할 수 없습니다. 운영자에게 연락 바랍니다.", helpfilePath));
                        }
                    }
                }
            }

        }

        private int UpdateHelpFilePathIntoMenuTable(string remoteFilePath, DataRow assemblyInfo)
        {
            string systemCode = App.mArgs.GetValue(ArgKind.SystemCode);

            bool OprDatabase = false;
            if (systemCode.Length > 4)
            {
                bool oprUse = systemCode.Substring(3, 1) == "1" ? true : false;
                if (oprUse)
                    OprDatabase = true;
            }

            try
            {
                SimpleDataAccessHelper access = new SimpleDataAccessHelper(OprDatabase);
                string DomainCategory = App.mArgs.GetValue(ArgKind.DomainCategroy);
                string userType = App.mArgs.GetValue(ArgKind.UserGroupId);
                string AssemblyName = assemblyInfo["AssemblyName"].ToString();
                string viewName = assemblyInfo["ViewName"].ToString();

                string query = string.Empty;

                if (viewName != "MainContentView")
                {
                    query = string.Format("Update \"TCM_AmiShip_Menu\" set  "
                    + " \"HelpFilePath\"  = '{3}' "
                    + " where \"DomainCategory\"='{0}' and \"UseType\" = '{1}' and \"AssemblyName\" = '{2}' and (\"ViewName\" is null or \"ViewName\" = 'MainContentView') "
                    , DomainCategory, userType, AssemblyName, remoteFilePath);
                }
                else
                {
                    query = string.Format("Update \"TCM_AmiShip_Menu\" set  "
                 + " \"HelpFilePath\"  = '{4}' "
                 + " where \"DomainCategory\"='{0}' and \"UseType\" = '{1}' and \"AssemblyName\" = '{2}' and \"ViewName\" = '{3}' "
                 , DomainCategory, userType, AssemblyName, viewName, remoteFilePath);
                }

                int upCnt = access.ExecuteNonQuery(query);
                return upCnt;

            }
            catch (Exception)
            {
                return 0;
            }
        }

        private void DownloadAFIXIntroHelpFile(string helpfilePath)
        {
            UpdateFileHelper fileHelper = new UpdateFileHelper();

            string ftpHost = ConfigurationManager.AppSettings["UpdateFTPServer"].ToString();
            string userID = ConfigurationManager.AppSettings["UpdateFTPUserID"].ToString();
            string password = ConfigurationManager.AppSettings["UpdateFTPPassword"].ToString();
            int port = int.Parse(ConfigurationManager.AppSettings["UpdateFTPPort"].ToString());

            string remoteFilePath = helpfilePath.Replace("\\", "/");
            string localFilePath = AppDomain.CurrentDomain.BaseDirectory + helpfilePath;

            if (fileHelper.DownloadFile(ftpHost, userID, password, port, remoteFilePath, localFilePath) == false)
            {
                ModernDialog.ShowMessage("최신 Help 파일을 다운로드할 수 없습니다. 로컬 PC에서 Help 파일을 찾습니다.", "", MessageBoxButton.OK);
            }

            if (File.Exists(localFilePath) == true)
            {
                HelpFileWindow helpWindow = new HelpFileWindow(fileHelper, ftpHost, userID, password, port, remoteFilePath, localFilePath, "AFIX Framework");
                helpWindow.Owner = System.Windows.Application.Current.MainWindow;
                helpWindow.Show();
            }
        }

        private void DownloadHelpFile(List<string> helpFielPathList, List<string> viewNameList)
        {
            UpdateFileHelper fileHelper = new UpdateFileHelper();

            string ftpHost = ConfigurationManager.AppSettings["UpdateFTPServer"].ToString();
            string userID = ConfigurationManager.AppSettings["UpdateFTPUserID"].ToString();
            string password = ConfigurationManager.AppSettings["UpdateFTPPassword"].ToString();
            int port = int.Parse(ConfigurationManager.AppSettings["UpdateFTPPort"].ToString());

            for (int i = 0; i < helpFielPathList.Count; i++)
            {
                string helpfilePath = helpFielPathList[i];
                string viewName = " 도움말";
                if (viewNameList.Count > i)
                    viewName = viewNameList[i] + viewName;

                string remoteFilePath = helpfilePath.Replace("\\", "/");
                string localFilePath = AppDomain.CurrentDomain.BaseDirectory + helpfilePath;

                if (fileHelper.DownloadFile(ftpHost, userID, password, port, remoteFilePath, localFilePath) == false)
                {
                    ModernDialog.ShowMessage("최신 Help 파일을 다운로드할 수 없습니다. 로컬 PC에서 Help 파일을 찾습니다.", "", MessageBoxButton.OK);
                }

                if (File.Exists(localFilePath) == true)
                {
                    HelpFileWindow helpWindow = new HelpFileWindow(fileHelper, ftpHost, userID, password, port, remoteFilePath, localFilePath, viewName);
                    helpWindow.Owner = System.Windows.Application.Current.MainWindow;
                    helpWindow.Show();
                }
            }
        }

        #endregion
    }
}
