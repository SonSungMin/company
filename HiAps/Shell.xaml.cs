using System;
using System.IO;
using System.Xml;
using System.Collections.Generic; 
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input; 
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using DevExpress.Xpf.Core;
using DevExpress.Xpf.Bars;
using DevExpress.Xpf.Layout.Core;
using DevExpress.Xpf.Docking;
using DevExpress.Xpf.NavBar;
using System.ComponentModel.Composition;
using Microsoft.Practices.Prism.Regions;
using Microsoft.Practices.Prism.Modularity;
using Microsoft.Practices.Composite.Events;
using Amisys.Framework.Infrastructure.PrismSupps;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel;
using Amisys.Framework.Infrastructure.Utility;
using Amisys.Framework.Infrastructure.DataModels;
using Amisys.Framework.Infrastructure.Interfaces;
using Amisys.Framework.Infrastructure.RegionAdapters;
using Amisys.Framework.Infrastructure;
using Amisys.Framework.Presentation.Views;
using Microsoft.Practices.ServiceLocation;
using System.Diagnostics;
using DevExpress.Xpf.Ribbon;
using System.Globalization;
using Microsoft.Practices.Composite.Presentation.Events;
using DevExpress.Xpf.Docking.Base;
using Amisys.Infrastructure.HHIInfrastructure.Interfaces;
using System.Data;
using Amisys.Infrastructure.HHIInfrastructure.Defintions;
using DevExpress.Xpf.LayoutControl;
using System.Collections.Specialized;
using System.Reflection;
using Amisys.Framework.Presentation.AmiMainShell.Utility;
using Amisys.Framework.Infrastructure.Definitions;
using Amisys.Framework.Presentation.DataAccess;
using Amisys.Framework.Presentation.Utility;
using DevExpress.Xpf.Core.Serialization;

namespace Amisys.Framework.Presentation.AmiMainShell.Views
{
    /// <summary>
    /// Shell.xaml에 대한 상호 작용 논리
    /// </summary>
    [Export(typeof(Shell))]
    public partial class Shell : UserControl, INotifyPropertyChanged
    {
        Dictionary<string, string> ModuleOriginInfo;

        private IRegionManager regionManager;
        private IModuleManager moduleManager;
        private IModuleCatalog moduleCatalog;
        private IEventAggregator eventAggregator;
        private ILayoutService layoutService;
        private IHHIAppLoginInfoService loginInfoService;

        protected IAmiDataService amiDataService
        {
            get
            {
                return ServiceLocator.Current.GetInstance<IAmiDataService>();
            }
        }
        protected IHHIMasterDataService masterDataService
        {
            get
            {

                return ServiceLocator.Current.GetInstance<IHHIMasterDataService>();
            }
        }
        protected ILogService logger
        {
            get
            {
                return ServiceLocator.Current.GetInstance<ILogService>();
            }
        }
        protected IAppLoginInfoService logonInfoService
        {
            get
            {
                return ServiceLocator.Current.GetInstance<IAppLoginInfoService>();
            }
        }

        private AmiMenuTreeViewItem currentWorkspace;

        private const string DefaultWorkspaceName = "MainShell";
        private string currentWorkspaceName = DefaultWorkspaceName;
        public string CurrentWorkspaceName
        {
            get
            {
                return currentWorkspaceName;
            }
            set
            {
                currentWorkspaceName = value;
                NotifyPropertyChanged("CurrentWorkspaceName");
            }
        }

        public int CurrentWorkspaceId
        {
            get { return _CurrentWorkspaceId; }
            set { _CurrentWorkspaceId = value; }
        }
        private int _CurrentWorkspaceId = -1;


        private UpdateFileHelper helper;

        public UpdateFileHelper Helper
        {
            get
            {
                if (helper == null)
                    helper = new UpdateFileHelper();

                return helper;
            }
        }


        public WorkspaceUsingProfileHelper WorkspaceUsingProfile
        {
            get 
            {
                if (_WorkspaceUsingProfile == null)
                {
                    // workspace profiler helper 
                    _WorkspaceUsingProfile = new WorkspaceUsingProfileHelper(amiDataService);
                }
                return _WorkspaceUsingProfile;
            }
        }
		private WorkspaceUsingProfileHelper _WorkspaceUsingProfile;
        
        /// <summary>
        /// ModuleName.ViewName
        /// </summary>
        public string CurrentViewName
        {
            get
            {
                return _CurrentViewName;
            }
            set
            {
                _CurrentViewName = value;
                NotifyPropertyChanged("CurrentViewName");
            }
        }
        private string _CurrentViewName;

        private string _ThemeName = "VS2010";
        public string ThemeName
        {
            get
            {
                return _ThemeName;
            }
            set
            {
                _ThemeName = value;
                NotifyPropertyChanged("ThemeName"); 
            }
        }


        public AmiViewList CurrentWorkspace
        {
            get
            {
                return _CurrentWorkspace;
            }
            set
            {
                _CurrentWorkspace = value;
                NotifyPropertyChanged("CurrentWorkspace");
                NotifyPropertyChanged("CurrentWorkspaceName");
            }
        }
        private AmiViewList _CurrentWorkspace;

        protected ICatalogService catalogService
        {
            get
            {
                return ServiceLocator.Current.GetInstance<ICatalogService>();
            }
        }


        protected IMessageService messageService
        {
            get
            {
                return ServiceLocator.Current.GetInstance<IMessageService>();
            }
        }

        #region properties


        private bool _IsBusy;

        public bool IsBusy
        {
            get
            {
                return _IsBusy;
            }
            set
            {
                _IsBusy = value;
                NotifyPropertyChanged("IsBusy");
            }
        }


        private string _BusyContent;

        public string BusyContent
        {
            get
            {
                return _BusyContent;
            }
            set
            {
                _BusyContent = value;
                NotifyPropertyChanged("BusyContent");
            }
        }

        private string _SelFigNo = HullMidPlanType.HullOperMidPlan;
        public string SelFigNo
        {
            get
            {
                return _SelFigNo;
            }
            set
            {
                _SelFigNo = value;
                this.NotifyPropertyChanged("SelFigNo");
            }
        }       

        #endregion

        [ImportingConstructor]
        public Shell(IRegionManager regionManager, IModuleManager moduleManager
                   , IModuleCatalog moduleCatalog, IEventAggregator eventAggregator
                   , ILayoutService layoutService)
        {
            this.regionManager = regionManager;
            this.moduleManager = moduleManager;
            this.moduleCatalog = moduleCatalog;
            this.eventAggregator = eventAggregator;
            this.layoutService = layoutService;

            this.DataContext = this;

            InitializeComponent();
            dockManager.Merge += dockManager_Merge;
            dockManager.UnMerge += dockManager_UnMerge;
            dockManager.ActiveMDIItem = mdiContainer;
            dockManager.DockItemClosed += OnDockItemClosed;
            dockManager.AutoHideGroups.CollectionChanged += OnHideGroupCollectionChanged;

            // Layout 복원 시 Content 재할당을 위한 이벤트 핸들러 등록
            DXSerializer.AddEndDeserializingHandler(dockManager, OnEndDeserializing);

            this.eventAggregator.GetEvent<DynamicEvent>().Subscribe(SubLoadModule);
            this.eventAggregator.GetEvent<DynamicEvent>().Subscribe(SubSaveLayout);
            this.eventAggregator.GetEvent<DynamicEvent>().Subscribe(SubOpenWorkspace, ThreadOption.UIThread);
            this.eventAggregator.GetEvent<DynamicEvent>().Subscribe(SubCloseWorkspace, ThreadOption.UIThread);
            this.eventAggregator.GetEvent<DynamicEvent>().Subscribe(SubResHavingUnsavedData, ThreadOption.PublisherThread);
            this.eventAggregator.GetEvent<DynamicEvent>().Subscribe(SubLoadWithRegionModule);

            this.eventAggregator.GetEvent<DynamicEvent>().Subscribe(SubHideCloseButton);

            parentBarManager.DataContext = this.layoutService;

            InitUIOption();

            ShowMenu();
            layoutService.DevTheme = ThemeName;
            PubThemeChange(ThemeName);

            DevExpress.Xpf.Core.DXGridDataController.DisableThreadingProblemsDetection = true;
        }

        private void OnEndDeserializing(object sender, EndDeserializingEventArgs e)
        {
            if (!(sender is DockLayoutManager))
                return;

            // 현재 Region에 있는 모든 View 가져오기
            var region = regionManager.Regions[MefRegionDefinition.MainRegion];
            if (region == null)
                return;

            var views = region.Views.Cast<object>().ToList();

            // 먼저 모든 Panel에서 Content를 제거하여 View-Panel 연결 끊기
            var allItems = dockManager.GetItems();
            var panelsToRestore = new List<DocumentPanel>();

            foreach (var item in allItems)
            {
                if (item is DocumentPanel panel)
                {
                    if (panel.Content != null)
                    {
                        // 기존 Content 제거
                        var content = panel.Content;
                        panel.Content = null;

                        // IPanelInfo의 부모 참조도 제거
                        if (content is IPanelInfo panelInfo)
                        {
                            panelInfo.SetParentWnd(null);
                        }
                    }

                    if (!string.IsNullOrEmpty(panel.Name))
                    {
                        panelsToRestore.Add(panel);
                    }
                }
            }

            // 이제 Content를 다시 할당
            foreach (var panel in panelsToRestore)
            {
                // Panel의 Name을 기반으로 매칭되는 View 찾기
                var matchingView = FindViewForPanel(panel, views);
                if (matchingView != null)
                {
                    // View가 이미 다른 Panel에 할당되어 있는지 확인
                    RemoveViewFromParent(matchingView);

                    panel.Content = matchingView;

                    // IPanelInfo 인터페이스가 있다면 부모 윈도우 설정
                    if (matchingView is IPanelInfo panelInfo)
                    {
                        panelInfo.SetParentWnd(panel);
                    }

                    // 사용된 View는 목록에서 제거하여 중복 할당 방지
                    views.Remove(matchingView);
                }
            }
        }

        // View를 부모 요소에서 제거
        private void RemoveViewFromParent(object view)
        {
            if (view is FrameworkElement element)
            {
                // LogicalTreeHelper를 사용하여 부모 찾기
                var parent = LogicalTreeHelper.GetParent(element);

                if (parent is ContentControl contentControl)
                {
                    contentControl.Content = null;
                }
                else if (parent is Panel panel)
                {
                    panel.Children.Remove(element);
                }
                else if (parent is Decorator decorator)
                {
                    decorator.Child = null;
                }
            }
        }

        // Panel에 매칭되는 View 찾기
        private object FindViewForPanel(DocumentPanel panel, List<object> views)
        {
            string panelName = panel.Name;

            foreach (var view in views)
            {
                if (view is IPanelInfo panelInfo)
                {
                    // Panel 이름과 매칭
                    string viewPanelName = panelInfo.GetPanelName();

                    // 정확히 일치하는 경우
                    if (panelName == viewPanelName)
                        return view;

                    // "_숫자" suffix가 있는 경우 제거하고 비교
                    string panelNameWithoutSuffix = RemoveNumericSuffix(panelName);
                    if (panelNameWithoutSuffix == viewPanelName)
                        return view;

                    // "ModuleName:ViewName" 형식으로 비교
                    string moduleViewName = $"{panelInfo.GetModuleName()}:{viewPanelName}";
                    if (panelName == moduleViewName || panelNameWithoutSuffix == moduleViewName)
                        return view;
                }
            }

            return null;
        }


        // 숫자 suffix 제거
        private string RemoveNumericSuffix(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;

            int underscoreIndex = name.LastIndexOf('_');
            if (underscoreIndex > 0)
            {
                string suffix = name.Substring(underscoreIndex + 1);
                int numericSuffix;
                if (int.TryParse(suffix, out numericSuffix))
                {
                    return name.Substring(0, underscoreIndex);
                }
            }

            return name;
        }


        #region UI 옵션 초기화

        private void InitUIOption()
        {
            string toolbarMerge = System.Configuration.ConfigurationManager.AppSettings.Get("AppUIMergeToolBarDefault");
            bool merge = (toolbarMerge.ToUpper() == "TRUE");
            SetToolbarMergeStyle(merge);
        }

        #endregion

        private void OnHideGroupCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                dockManager.AutoHideGroups[0].Items.Clear();
                dockManager.AutoHideGroups.Clear();
                ShowMenuColumnDef.Width = new GridLength(22);
            }
        }

        private void OnDockItemClosed(object sender, DockItemClosedEventArgs e)
        {            
            foreach (BaseLayoutItem item in e.AffectedItems)
            {
                if (item is LayoutPanel)
                {
                    foreach (Bar bar in parentBarManager.Bars)
                    {
                        if (object.ReferenceEquals(bar.ToolTip, ((LayoutPanel)item).Control))
                        {
                            _FixedBarParents.Remove(bar.DataContext);
                            bar.UnMerge();
                            parentBarManager.Bars.Remove(bar);
                            break;
                        }
                    }
                }
            }
        }

        private void BeginWaitCursor()
        {
            this.Cursor = Cursors.Wait;
        }

        private void EndWaitCursor()
        {
            this.Cursor = Cursors.Arrow;
        }

        private void HideMenu()
        {
        }

        private void ShowMenu()
        {
        }

        private bool recv_HaveGroup(DevExpress.Xpf.Docking.LayoutGroup parent, DevExpress.Xpf.Docking.LayoutGroup search)
        {
            bool haveGroup;
            foreach (BaseLayoutItem ui in parent.Items)
            {
                if (object.ReferenceEquals(search, ui)) return true;
                if (ui is DevExpress.Xpf.Docking.LayoutGroup)
                {
                    haveGroup = recv_HaveGroup((DevExpress.Xpf.Docking.LayoutGroup)ui, search);
                    if (haveGroup == true) return true;
                }
            }

            return false;
        }

        private int GetRowNumInBar(Bar bar)
        {
            if (bar.Tag != null)
            {
                int retValue = int.Parse(bar.Tag.ToString());
                return retValue;
            }
            return 2;
        }


        List<Bar> _NewBars = new List<Bar>();
        List<object> _FixedBarParents = new List<object>();
        MDIMergeStyle _CurrentMergeStyle = MDIMergeStyle.Always;

        private void RemoveChildBar()
        {
            foreach (Bar bar in _NewBars)
            {
                if (bar.Name.StartsWith("fixedBar")) continue;
                bar.UnMerge();
                parentBarManager.Bars.Remove(bar);
            }
            _NewBars.Clear();
        }

        private void RemoveFixedBar()
        {
            List<Bar> deleteBars = new List<Bar>();
            foreach (var bar in parentBarManager.Bars)
            {
                if (bar.Name.StartsWith("fixedBar"))
                {
                    if (GetRowNumInBar(bar) >= 0)
                    {
                        bar.UnMerge();
                        deleteBars.Add(bar);
                    }
                }
            }
            foreach (var bar in deleteBars)
            {
                parentBarManager.Bars.Remove(bar);
            }
            _FixedBarParents.Clear();
        }

        void dockManager_Merge(object sender, BarMergeEventArgs e)
        {
            RemoveChildBar();
            
            string barName = "ChildBar";
            int cnt = 1;

            foreach (var bar in e.ChildBarManager.Bars)
            {
                if (bar.Visible == false)
                    continue;

                Bar newBar = new Bar();

                if (bar.Name == "fixedBar")
                {
                    if (_FixedBarParents.Contains(bar.DataContext)) continue;
                    newBar.Name = "fixedBar" + cnt.ToString();
                    newBar.ToolTip = bar.DataContext;
                    _FixedBarParents.Add(bar.DataContext);
                }
                else
                {
                    if (_CurrentMergeStyle == MDIMergeStyle.Never) continue;
                    newBar.Name = barName + cnt.ToString();
                }

                newBar.Caption = bar.Caption;
                newBar.DockInfo = new BarDockInfo() { ContainerName = "" };
                int rowNum = GetRowNumInBar(bar);
                newBar.DockInfo.Row = (rowNum < 0) ? 0 : rowNum;
                newBar.Tag = bar.Tag;
                
                e.BarManager.Bars.Add(newBar);
                newBar.Merge(bar);
                cnt++;
                _NewBars.Add(newBar);
            }          
        }

        void dockManager_UnMerge(object sender, BarMergeEventArgs e)
        {
            _NewBars.Clear();
            List<Bar> deleteBars = new List<Bar>();
            foreach (var bar in e.BarManager.Bars)
            {
                if(bar.Name.StartsWith("ChildBar"))
                {
                    if (GetRowNumInBar(bar) >= 0)
                    {
                        bar.UnMerge();
                        deleteBars.Add(bar);
                    }
                }
            }
            foreach (var bar in deleteBars)
            {
                e.BarManager.Bars.Remove(bar);
            }
        }

        private void SubLoadModule(DynamicEventParam param)
        {
            if (param.IsContains("PubLoadModule", "AmiMainShell", "ModuleName"))
            {
                string moduleName = param.GetParams("ModuleName") as string;

                if (param.IsParamContains("PubLoadModule", "ModuleName", "ViewName"))
                {
                    string viewName = param.GetParams("ViewName") as string;
                    LoadModuleWithViewName(moduleName, viewName);
                }
                else
                {
                    if (string.IsNullOrEmpty(moduleName) == false)
                    {
                        LoadModule(moduleName);
                    }
                }
            }
            else if (param.IsContains("PubLoadModule", "AmiMainShell", "Node"))
            {
                // ViewList에서 menu DataRow를 이용하여 로드
                DataRow row = (DataRow)param.GetParams("Node");
                LoadModule(row);
            }
        }

        private void SubLoadWithRegionModule(DynamicEventParam param)
        {
            if (param.IsContains("PubLoadWithRegionModule", "AmiMainShell", "ModuleName", "RegionName"))
            {
                string moduleName = param.GetParams("ModuleName") as string;
                string regionName = param.GetParams("RegionName") as string;

                if (param.IsParamContains("PubLoadWithRegionModule", "ModuleName", "ViewName"))
                {
                    string viewName = param.GetParams("ViewName") as string;
                    LoadModuleWithViewNameAndRegion(moduleName, viewName, regionName);
                }
                else
                {
                    if (string.IsNullOrEmpty(moduleName) == false)
                    {
                        LoadModuleWithRegion(moduleName, regionName);
                    }
                }
            }
        }

        private void SubHideCloseButton(DynamicEventParam param)
        {
            if (param.IsContains("PubHideCloseButton", ""))
            {
                string caption = (string)param.GetParams("LayoutCaption");
                if (string.IsNullOrWhiteSpace(caption)) return;

                BaseLayoutItem item = recv_GetLayoutitem(dockManager.LayoutRoot, caption);

                if (item != null) item.ShowCloseButton = false;
            }
        }

        private void AddCatalog(string moduleName)
        {
            if (catalogService != null)
            {
                try
                {
                    catalogService.AddAssemblyCatalog(moduleName);
                }
                catch (Exception ex)
                {
                    messageService.ShowError(ex.Message);
                    throw;
                }
            }
        }

        private void LoadModule(string moduleName)
        {
            if (this.moduleManager == null) return;

            AddCatalog(moduleName);

            ModuleInfo isLoadedMod = IsContainsModule(moduleName);
            if (isLoadedMod != null)
            {
                if (isLoadedMod.State == ModuleState.NotStarted)
                    this.moduleManager.LoadModule(moduleName);

                PubLoadModule(moduleName);
            }

        }

        public void LoadModule(DataRow row)
        {
            if (this.moduleManager != null && row != null)
            {
                int id = row.Field<int>("NodeId");
                string moduleName = row.Field<string>("AssemblyName");
                string viewName = row.Field<string>("ViewName");

                AddCatalog(moduleName);

                ModuleInfo isLoadedMod = IsContainsModule(moduleName);
                if (isLoadedMod != null)
                {
                    if (isLoadedMod.State == ModuleState.NotStarted)
                        this.moduleManager.LoadModule(moduleName);

                    // set Current View Id
                    amiDataService.CurrentId = id;

                    // Load Module
                    PubLoadModule(moduleName, viewName, id);

                    // clear current view Id
                    amiDataService.CurrentId = -1;
                }
            }
        }

        private void LoadModuleWithRegion(string moduleName, string regionName)
        {
            if (this.moduleManager == null) return;

            AddCatalog(moduleName);

            ModuleInfo isLoadedMod = IsContainsModule(moduleName);
            if (isLoadedMod != null)
            {
                if (isLoadedMod.State == ModuleState.NotStarted)
                    this.moduleManager.LoadModule(moduleName);

                PubLoadModuleWithRegion(moduleName, regionName);
            }
        }

        private void LoadModuleWithViewName(string moduleName, string viewName, int id = 0)
        {
            if (this.moduleManager == null) return;

            AddCatalog(moduleName);

            ModuleInfo isLoadedMod = IsContainsModule(moduleName);
            if (isLoadedMod != null)
            {
                if (isLoadedMod.State == ModuleState.NotStarted)
                    this.moduleManager.LoadModule(moduleName);

                // ViewName에서 클래스명만 추출
                string actualViewName = viewName;
                if (!string.IsNullOrEmpty(viewName) && viewName.Contains(","))
                {
                    string typeName = viewName.Split(',')[0];
                    string[] parts = typeName.Split('.');
                    actualViewName = parts[parts.Length - 1];
                }

                // set Current View Id
                amiDataService.CurrentId = id;

                PubLoadModule(moduleName, actualViewName, id);

                // clear current view Id
                amiDataService.CurrentId = -1;
            }
        }

        private void LoadModuleWithViewNameAndRegion(string moduleName, string viewName, string regionName, int id = 0)
        {
            if (this.moduleManager == null) return;

            AddCatalog(moduleName);

            ModuleInfo isLoadedMod = IsContainsModule(moduleName);
            if (isLoadedMod != null)
            {
                if (isLoadedMod.State == ModuleState.NotStarted)
                    this.moduleManager.LoadModule(moduleName);

                PubLoadModuleWithViewNameAndRegion(moduleName, viewName, regionName, id);
            }
        }

        public void PubLoadModule(string moduleName)
        {
            DynamicEventParam param = new DynamicEventParam("AmiMainShell", "PubLoadModule", moduleName, true);
            param.Publish(this.eventAggregator);
        }

        public void PubLoadModuleWithRegion(string moduleName, string regionName)
        {
            DynamicEventParam param = new DynamicEventParam("AmiMainShell", "PubLoadModule", moduleName, true);
            param.AddParams("RegionName", regionName);
            param.Publish(this.eventAggregator);
        }

        public void PubLoadModuleWithViewNameAndRegion(string moduleName, string viewName, string regionName, int id)
        {
            DynamicEventParam param = new DynamicEventParam("AmiMainShell", "PubLoadModule", moduleName, true);
            param.AddParams("ViewName", viewName);
            param.AddParams("RegionName", regionName);
            param.AddParams("Id", id);
            param.Publish(this.eventAggregator);
        }

        public void PubLoadModule(string moduleName, string viewName, int id)
        {
            DynamicEventParam param = new DynamicEventParam("AmiMainShell", "PubLoadModule", moduleName, true);
            param.AddParams("ViewName", viewName);
            param.AddParams("Id", id);
            param.Publish(this.eventAggregator);
        }
        
        public void PubCloseModule(string moduleName)
        {
            DynamicEventParam param = new DynamicEventParam("AmiMainShell", "PubCloseModule", moduleName, true);
            param.Publish(this.eventAggregator);
        }

        public void PubCloseModule(string moduleName, string viewName, int id)
        {
            DynamicEventParam param = new DynamicEventParam("AmiMainShell", "PubCloseModule", moduleName, true);
            param.AddParams("ViewName", viewName);
            param.AddParams("Id", id);
            param.Publish(this.eventAggregator);
        }


        private void CloseModuleWithViewName(string moduleName, string viewName, int id = 0)
        {
            PubCloseModule(moduleName, viewName, id);
        }


        private ModuleInfo IsContainsModule(string moduleName)
        {
            foreach (var mod in this.moduleCatalog.Modules)
            {
                if (mod.ModuleName == moduleName)
                {
                    return mod;
                }
            }

            return null;
        }

        private string moduleName = "AmiMainShell";
        private void bHome_ItemClick(object sender, ItemClickEventArgs e)
        {
            currentWorkspace = null;
            CurrentWorkspaceName = DefaultWorkspaceName;
            CloseWorkspace();

            LoadModule("HHIHome");
        }

        private void bCloseView_ItemClick(object sender, ItemClickEventArgs e)
        {
            currentWorkspace = null;
            CurrentWorkspaceName = DefaultWorkspaceName;
            CloseWorkspace();
            PubCloseWorkSpace();
            LoadModule("HHIHome");
        }

        private void PubCloseWorkSpace()
        {
            DynamicEventParam param = new DynamicEventParam(this.moduleName, "PubCloseWorkSpace", "HHIStatusBar", true);
            param.AddParams("CurrentWorkspaceName", CurrentWorkspaceName);
            param.Publish(this.eventAggregator);
        }
        //상호 참조를 해결하기 위해서 Shell 이 로드된 후 필요한 모듈을 로드한다.
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            string id = this.logonInfoService.GetUserGroupId();
            this.amiDataService.GetMenuData(id);

            LoadModule("HHIStatusBar");
            LoadModule("AmiNavigation");
            LoadModule("AmiViewList");
            LoadModule("AmiMenu");
        }

        // 프로그램 종료시 WindowClosing 이벤트
        private void AmiMainShell_Closing(object sender, CancelEventArgs e)
        {
            // 데이터 저장 체크
            EnsureDataSave();

            PubApplicationClosing();
        }

        private void PubApplicationClosing()
        {
            if (this.eventAggregator != null)
            {
                DynamicEventParam param = new DynamicEventParam("AmiMainShell", "PubApplicationClosing");
                param.Publish(this.eventAggregator);
            }            
        }

        public void PubShowGridTotalSummary(bool show)
        {
            DynamicEventParam param = new DynamicEventParam(this.moduleName, "PubShowGridTotalSummary");
            param.AddParams("ShowTotalSummary", show);
            param.Publish(this.eventAggregator);
        }

        private void bThemeChange_ItemClick(object sender, ItemClickEventArgs e)
        {
            if(sender is BarButtonItem)
            {
                string themeName = (sender as BarButtonItem).Tag.ToString();
                if(string.IsNullOrEmpty(themeName) == false )
                {
                    layoutService.DevTheme = themeName;
                    ThemeName = themeName;
                    PubThemeChange(themeName);
                }
            }
        }

        public void PubThemeChange(string themeName)
        {
            DynamicEventParam param = new DynamicEventParam("AmiMainShell", "PubThemeChange");
            param.AddParams("ThemeName", themeName);
            param.Publish(this.eventAggregator);
        }
        
        public void PubLoadMenu()
        {
            DynamicEventParam param = new DynamicEventParam("AmiMainShell", "PubLoadMenu", "AmiMenu", true);
            param.Publish(this.eventAggregator);
        }

        private void bLoadMenu_ItemClick(object sender, ItemClickEventArgs e)
        {
            PubLoadMenu();
        }

        #region save/refresh Layout
                
        private void SubOpenWorkspace(DynamicEventParam param)
        {
            if (param.IsContains("PubOpenWorkspace", "AmiMainShell", "ViewList"))
            {
                AmiViewList viewList = param.GetParams("ViewList");
                int workspaceId = viewList.GetWorkspaceNodeID();

                if (CurrentWorkspace == null || workspaceId != CurrentWorkspace.GetWorkspaceNodeID())
                {
                    this.CurrentWorkspace = viewList;

                    CloseWorkspace();

                    OpenWorkspace(this.CurrentWorkspaceName, viewList);
                }
            }
        }

        private void SubCloseWorkspace(DynamicEventParam param)
        {
            if (param.IsContains("PubCloseWorkspace", "AmiMainShell"))
            {
                CloseWorkspace();
                this.CurrentWorkspaceName = string.Empty;
                ShowMenu();
            }
        }

        public void CloseWorkspace()
        { 
            // 데이터 저장 체크
            EnsureDataSave(); // 도성민 확인

            // 뷰리스트
            List<ModuleViewId> moduleViewList;
            GetActiveModuleAndViewList(out moduleViewList);

            // close 뷰
            foreach (var view in moduleViewList)
            {
                CloseModuleWithViewName(view.ModuleName, view.ViewName, view.Id);
            }

            // end current workspace using
            EndWorkspaceProfile();
        }

        private void StartWorkspaceProfile(int id, string name)
        {
            if (WorkspaceUsingProfile != null)
                WorkspaceUsingProfile.Begin(CurrentWorkspaceId, CurrentWorkspaceName);
        }

        private void EndWorkspaceProfile()
        {
            if (WorkspaceUsingProfile != null)
                WorkspaceUsingProfile.End();
        }

        public void OpenWorkspace(AmiMenuTreeViewItem menu)
        {
            if (menu == null) return;
            int workspaceId = menu.GetWorkspaceId();
            if (currentWorkspace == null || workspaceId != this.CurrentWorkspaceId)
            {

                this.CurrentWorkspace = menu.ViewList;
                this.CurrentWorkspaceName = menu.GetWorkSpaceName();
                CloseWorkspace();

                OpenWorkspace(this.CurrentWorkspaceName, menu.ViewList);
            }
        }

        private void OpenWorkspace(string workspaceName, AmiViewList amiViewList = null)
        {
            BeginWaitCursor();
            RemoveFixedBar();

            // 기존 Region의 모든 View를 임시로 저장하고 제거
            var region = regionManager.Regions[MefRegionDefinition.MainRegion];
            var existingViews = region.Views.Cast<object>().ToList();

            // Region에서 모든 View 제거 (Panel-View 연결 끊기)
            foreach (var view in existingViews)
            {
                region.Remove(view);
            }

            // read module list
            List<ModuleViewId> viewList;
            if (LoadViewList(workspaceName, out viewList) == true)
            {
                // ViewIdList로 로드
                foreach (var view in viewList)
                {
                    LoadModuleWithViewName(view.ModuleName, view.ViewName, view.Id);
                }
            }
            else
            {
                // AmiViewList로 로드
                if (amiViewList != null)
                {
                    foreach (var view in amiViewList)
                    {
                        LoadModule(view);
                    }
                }
            }

            // 약간의 지연 후 Layout 복원 (View들이 Region에 추가될 시간을 줌)
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                // restore dock layout
                if (LoadDockLayout(workspaceName) == false)
                {
                    // Layout 파일이 없는 경우
                }
                dockManager.MDIMergeStyle = _CurrentMergeStyle;

                // 첫 번째 아이템 활성화
                if (viewList != null && viewList.Count > 0)
                {
                    BaseLayoutItem item = FindLayoutItemForView(viewList[0]);
                    if (item != null)
                    {
                        SetActivePannel(item);
                    }
                }
                else if (amiViewList != null && amiViewList.Count > 0)
                {
                    DataRow row = amiViewList[0];
                    string caption = row.Field<string>("NodeName");
                    BaseLayoutItem item = recv_GetLayoutitem(dockManager.LayoutRoot, caption);
                    SetActivePannel(item);
                }

                // start workspace 
                StartWorkspaceProfile(CurrentWorkspaceId, CurrentWorkspaceName);

                EndWaitCursor();

            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private BaseLayoutItem FindLayoutItemForView(ModuleViewId viewInfo)
        {
            BaseLayoutItem item = null;

            // 1. Caption(NodeName)으로 찾기
            if (!string.IsNullOrEmpty(viewInfo.NodeName))
            {
                item = recv_GetLayoutitem(dockManager.LayoutRoot, viewInfo.NodeName);
                if (item != null) return item;
            }

            // 2. ViewName 처리
            string viewName = viewInfo.ViewName;

            // AssemblyQualifiedName인 경우 클래스명만 추출
            if (!string.IsNullOrEmpty(viewName) && viewName.Contains(","))
            {
                string typeName = viewName.Split(',')[0];
                string[] parts = typeName.Split('.');
                viewName = parts[parts.Length - 1];
            }

            // 3. "ModuleName:ViewName" 형식으로 찾기
            string namePattern = string.Format("{0}:{1}", viewInfo.ModuleName, viewName);
            item = recv_GetLayoutitemByName(dockManager.LayoutRoot, namePattern);
            if (item != null) return item;

            // 4. ID를 포함한 형식으로 찾기 ("ModuleName:ViewName_ID")
            if (viewInfo.Id > 0)
            {
                string nameWithId = string.Format("{0}:{1}_{2}", viewInfo.ModuleName, viewName, viewInfo.Id);
                item = recv_GetLayoutitemByNameExact(dockManager.LayoutRoot, nameWithId);
                if (item != null) return item;
            }

            return null;
        }

        private BaseLayoutItem recv_GetLayoutitemByNameExact(DevExpress.Xpf.Docking.LayoutGroup group, string name)
        {
            if (group == null) return null;

            foreach (BaseLayoutItem child in group.Items)
            {
                if (child is DevExpress.Xpf.Docking.LayoutGroup)
                {
                    BaseLayoutItem item = recv_GetLayoutitemByNameExact((DevExpress.Xpf.Docking.LayoutGroup)child, name);
                    if (item != null) return item;
                }
                else if (child.Name == name)
                {
                    return child;
                }
            }
            return null;
        }

        private BaseLayoutItem recv_GetLayoutitemByName(DevExpress.Xpf.Docking.LayoutGroup group, string name)
        {
            if (group == null) return null;

            foreach (BaseLayoutItem child in group.Items)
            {
                if (child is DevExpress.Xpf.Docking.LayoutGroup)
                {
                    BaseLayoutItem item = recv_GetLayoutitemByName((DevExpress.Xpf.Docking.LayoutGroup)child, name);
                    if (item != null) return item;
                }
                else if (child.Name != null)
                {
                    // 1. 정확히 일치하는 경우
                    if (child.Name == name)
                    {
                        return child;
                    }

                    // 2. "_숫자" suffix가 있는 경우 제거하고 비교
                    string childNameWithoutSuffix = child.Name;
                    int underscoreIndex = child.Name.LastIndexOf('_');

                    // underscore가 있고, 그 뒤가 숫자인 경우만 제거
                    if (underscoreIndex > 0)
                    {
                        string suffix = child.Name.Substring(underscoreIndex + 1);
                        int numericSuffix;

                        if (int.TryParse(suffix, out numericSuffix))
                        {
                            childNameWithoutSuffix = child.Name.Substring(0, underscoreIndex);


                            if (childNameWithoutSuffix == name)
                            {
                                return child;
                            }
                        }
                    }
                }
            }
            return null;
        }

        private bool LoadViewList(string workspaceName, out List<ModuleViewId> moduleViews)
        {
            moduleViews = new List<ModuleViewId>();

            try
            {
                AmiWorkspaceHelper.Load(workspaceName, moduleViews);
            }
            catch (Exception)
            {
                return false;
            }

            return (moduleViews.Count > 0);
        }
        
        private bool LoadDockLayout(string workspaceName)
        {
            string tmpWorkspaceName = EnsureWorkspaceName(workspaceName);
            string path;
            string filename;
            GetSafeWorkspaceLayoutFilename(tmpWorkspaceName, out path, out filename);

            try
            {
                if (AmiFileUtility.IsExistFile(filename))
                {
                    // restore layout
                    AmiFileUtility.ReleseLockedFile(filename);
                    dockManager.RestoreLayoutFromXml(filename);
                }
                else
                {
                    return false;
                }
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        private void SubSaveLayout(DynamicEventParam param)
        {
            if (param.IsContains("PubSaveLayout", "AmiMainShell"))
            {
                HideMenu();
                SaveDockLayout(currentWorkspaceName);
            }
        }

        private void SaveDockLayout(string workspaceName)
        {
            string tmpWorkspaceName = EnsureWorkspaceName(workspaceName);

            // layout
            SaveLayout(tmpWorkspaceName);

            // view 목록
            SaveViewList(tmpWorkspaceName);
        }

        private string EnsureWorkspaceName(string workspaceName)
        {
            if (string.IsNullOrEmpty(workspaceName))
                return moduleName;
            else
                return workspaceName;
        }

        private void SaveViewList(string workspaceName)
        {
            List<ModuleViewId> viewList;
            GetActiveModuleAndViewList(out viewList);

            try
            {
                AmiWorkspaceHelper.Save(workspaceName, viewList);
            }
            catch (Exception ex)
            {
                string msg = string.Format("[{0}]Layout을 저장할 수 없습니다.", workspaceName);
                logger.Write(msg);
                logger.Write(ex.ToString());
            }
        }

        private void SaveLayout(string workspaceName)
        {
            string path;
            string filename;
            GetSafeWorkspaceLayoutFilename(workspaceName, out path, out filename);

            try
            {
                AmiFileUtility.ForceCreateDirectory(path);
                AmiFileUtility.ForceDeleteFile(filename);

                // Layout 그룹의 Tag에 ViewName을 기록하고
                // Restore 시에 해동 ViewName을 가지는 그룹에 뷰를 붙임
                SetLayoutViewList();
                
                dockManager.SaveLayoutToXml(filename);
            }
            catch (Exception ex)
            {
                string msg = string.Format("[{0}]Layout을 저장할 수 없습니다.", workspaceName);
                logger.Write(msg);
                logger.Write(ex.ToString());
            }
        }

        private void SetLayoutViewList()
        {
            foreach (var item in dockManager.LayoutRoot.Items)
            {
                
            }
        }

        private void GetSafeWorkspaceLayoutFilename(string workspaceName, out string path, out string filename)
        {
            path = string.Format("{0}\\Layout\\", AmiFileUtility.GetApplicationDirectory());
            filename = string.Format("{0}{1}.xml", path, AmiFileUtility.MakeSafeFilename(workspaceName));

            filename = filename.Replace("\\\\", "\\");
        }


        #endregion


        #region Workspace/Module/View 정보

        /// <summary>
        /// 현재 Workspace에 활성화된 Module:View 목록
        /// </summary>
        public void GetActiveModuleAndViewList(out List<ModuleViewId> moduleViews)
        {
            moduleViews = new List<ModuleViewId>();

            foreach (IPanelInfo view in regionManager.Regions[MefRegionDefinition.MainRegion].ActiveViews)
            {
                var moduleView = new ModuleViewId()

                {
                    ModuleName = view.GetModuleName(),
                    ViewName = view.GetViewName(),
                    NodeName = view.GetViewName(),
                    Id = view.GetId()
                };
                moduleViews.Add(moduleView);
            }
        }

        /// <summary>
        /// 현재 Workspace에서 활성화된 모듈 목록
        /// </summary>
        /// <returns></returns>
        public List<string> GetActiveModuleList()
        {
            List<string> modules = new List<string>();

            foreach (IPanelInfo view in regionManager.Regions[MefRegionDefinition.MainRegion].ActiveViews)
            {
                modules.Add(view.GetPanelName());
            }
            
            return modules;
        }

        /// <summary>
        /// 현재 Workspace에서 지정한 Module:View의 개수
        /// </summary>
        /// <param name="moduleName"></param>
        /// <param name="viewName"></param>
        /// <returns></returns>
        public int GetActiveViewCount(string moduleName, string viewName)
        {
            int cnt = 0;

            foreach (IPanelInfo view in regionManager.Regions[MefRegionDefinition.MainRegion].ActiveViews)
            {
                if (view.GetModuleName() == moduleName && view.GetViewName() == viewName)
                {
                    cnt++;
                }
            }

            return cnt;
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

        private void bLoadMenuEditor_ItemClick(object sender, ItemClickEventArgs e)
        {
            LoadModule("AmiMenuEditor");
        }

        private void bLoadHelpDesk_ItemClick(object sender, ItemClickEventArgs e)
        {
            LoadModule("AmiHelpDesk");
        }

        private void bSaveLayout_ItemClick(object sender, ItemClickEventArgs e)
        {
            PubSaveLayout();
        }

        public void PubSaveLayout()
        {
            DynamicEventParam param = new DynamicEventParam("AmiMainShell", "PubSaveLayout");
            param.Publish(this.eventAggregator);
        }

        private void bLoadLayout_ItemClick(object sender, ItemClickEventArgs e)
        {
            LoadDockLayout(CurrentWorkspaceName);
        }

        private void bOption_ItemClick(object sender, ItemClickEventArgs e)
        {
            OptionWindow optionWindow = new OptionWindow();

            if (optionWindow != null)
            {
                optionWindow.ShowDialog();                    
            }
        }

        private void bHelp_ItemClick(object sender, ItemClickEventArgs e)
        {
        }

        private void bExit_ItemClick(object sender, ItemClickEventArgs e)
        {
            Application.Current.Shutdown();
        }
     
        private void PubChangeToolbarGlyphSize(GlyphSize size = GlyphSize.Small)
        {
            DynamicEventParam param = new DynamicEventParam("AmiMainShell", "PubChangeToolbarGlyphSize");
            param.AddParams("ToolbarGlyphSize", size);
            param.Publish(this.eventAggregator);
        }

        private void bLargeGlyph_ItemClick(object sender, ItemClickEventArgs e)
        {
            this.layoutService.ChangeToolbarGlyphSize(false);
            PubChangeToolbarGlyphSize(GlyphSize.Large);
        }

        private void bSmallGlyph_ItemClick(object sender, ItemClickEventArgs e)
        {
            this.layoutService.ChangeToolbarGlyphSize();
            PubChangeToolbarGlyphSize(GlyphSize.Small);
        }

        private void TabbedGroup_SelectedItemChanged(object sender, SelectedItemChangedEventArgs e)
        {
            if (e.Item == null)
                return;

            if (e.Item.Name == "loMenu")
            {
                DynamicEventParam param = new DynamicEventParam("AmiMainShell", "PubSelectedTabChanged");
                param.AddParams("ThemeName", ThemeName);
                param.AddParams("IsSelected", true);
                param.Publish(this.eventAggregator);
            }
            else

            {
                DynamicEventParam param = new DynamicEventParam("AmiMainShell", "PubSelectedTabChanged");
                param.AddParams("ThemeName", ThemeName);
                param.AddParams("IsSelected", false);
                param.Publish(this.eventAggregator);
            }
        }

        private void bShowContent_ItemClick(object sender, ItemClickEventArgs e)
        {
            PubShowHideContent();
        }

        private void bHideContent_ItemClick(object sender, ItemClickEventArgs e)
        {
            PubShowHideContent(false);
        }

        private void PubShowHideContent(bool show=true)
        {
            DynamicEventParam param = new DynamicEventParam("AmiMainShell", "PubShowHideContent");

            if(show)
                param.AddParams("BarItemDisplayMode", BarItemDisplayMode.ContentAndGlyph);
            else
                param.AddParams("BarItemDisplayMode", BarItemDisplayMode.Default);

            param.Publish(this.eventAggregator);
        }

        private void SetToolbarMergeStyle(bool merge)
        {
            if (merge == true)
            {
                _CurrentMergeStyle = MDIMergeStyle.Always;
                dockManager.MDIMergeStyle = MDIMergeStyle.Always;
            }
            else
            {
                _CurrentMergeStyle = MDIMergeStyle.Never;
                dockManager.MDIMergeStyle = MDIMergeStyle.Never;
                RemoveFixedBar();
            }
        }

        #region 전체 모듈/뷰 데이터 저장 체크

        private bool hasUnsavedData = false;

        /// <summary>
        /// Workspace 변경 또는 Application 종료 시에
        /// 각 뷰에 저장할 데이터가 있는지를 체크하고 사용자의 선택에 따라 저장하거나 종료하도록함
        /// 이 과정에서 발생하는 통신은 모두 Sync로 처리함
        /// </summary>
        private void EnsureDataSave()
        {
            // save할 데이터가 있는지 체크
            PubReqHavingUnsavedData();

            if (hasUnsavedData)
            {
                // 저장할 데이터가 있다면, 사용자 확인
                bool save = ConfirmDataSave();
                // 저장

                if (save)
                    PubSaveData();

                // 저장 필요 여부 초기화
                hasUnsavedData = false;
            }
        }

        private void SubResHavingUnsavedData(DynamicEventParam param)
        {
            if (param.IsContains("PubResHavingUnsavedData", this.moduleName, "Result"))
            {
                var result = param.GetParams("Result");

                if (result)
                    hasUnsavedData = true;
            }
        }

        private void PubReqHavingUnsavedData()
        {
            DynamicEventParam param = new DynamicEventParam(this.moduleName, "PubReqHavingUnsavedData");
            param.Publish(this.eventAggregator);
        }

        private bool ConfirmDataSave()
        {
            return messageService.ShowYesNoQuestion("변경된 데이터가 있습니다. 저장하시겠습니까?", "저장");
        }

        private void PubSaveData()
        {
            DynamicEventParam param = new DynamicEventParam(this.moduleName, "PubSaveData");
            param.Publish(this.eventAggregator);
        }

        #endregion

        private void dockManager_LayoutItemRestored(object sender, LayoutItemRestoredEventArgs e)
        {
        }

        private void bShowMenu_ItemClick(object sender, ItemClickEventArgs e)
        {
            ShowMenu();
        }

        private void ShowMenuButton_Click(object sender, RoutedEventArgs e)
        {
            ShowMenu();
        }

        private void dockManager_DockItemActivated(object sender, DockItemActivatedEventArgs ea)
        {
            if (ea != null && ea.Item != null)
            {
                CurrentViewName = ea.Item.Name;
            }
        }

        #region 모듈(어셈블리) 버전 및 폴더 정보

        public void SetModuleOriginInfo(AggregateCatalog aggCatalog)
        {
            ModuleOriginInfo = new Dictionary<string, string>();

            HashSet<string> moduleFolders = new HashSet<string>();
            if (aggCatalog != null)
            {
                foreach (var catalog in aggCatalog.Catalogs)
                {
                    if (catalog is DirectoryCatalog)
                    {
                        var c = catalog as DirectoryCatalog;
                        foreach (var file in c.LoadedFiles)
                        {
                            string moduleName = AmiFileUtility.ExtractFilenameFromPath(file, false);
                            if (ModuleOriginInfo.ContainsKey(moduleName) == false)
                            {
                                ModuleOriginInfo.Add(moduleName, file);
                            }
                        }
                    }
                }
            }
        }

        public string GetAssemblyPath(string assemblyName)
        {
            string path = string.Empty;
            if (ModuleOriginInfo != null && string.IsNullOrEmpty(assemblyName) == false)
            {
                ModuleOriginInfo.TryGetValue(assemblyName.ToUpper(), out path);
            }
            return path;
        }


        #endregion

        #region utility

        public List<IDisposable> GetDisposableContent()
        {
            List<IDisposable> list = new List<IDisposable>();
            rect_GetDisposableContent(list, dockManager.LayoutRoot);
            return list;
        }

        private void rect_GetDisposableContent(List<IDisposable> list, DevExpress.Xpf.Docking.LayoutGroup group)
        {
            if (group == null) return;

            DocumentPanel doc;
            foreach (BaseLayoutItem child in group.Items)
            {
                if (child is DocumentPanel)
                {
                    doc = (DocumentPanel)child;
                    if (doc.Content != null && doc.Content is IDisposable) list.Add((IDisposable)doc.Content);
                }

                if (child is DevExpress.Xpf.Docking.LayoutGroup)
                    rect_GetDisposableContent(list, (DevExpress.Xpf.Docking.LayoutGroup)child);
            }
        }

        private void SetActivePannel(BaseLayoutItem pannel)
        {
            if (pannel != null)
            {
                dockManager.Activate(pannel);
            }
        }
        private BaseLayoutItem recv_GetLayoutitem(DevExpress.Xpf.Docking.LayoutGroup group, string caption)
        {
            if (group == null) return null;

            foreach (BaseLayoutItem child in group.Items)
            {
                if (child is DevExpress.Xpf.Docking.LayoutGroup)
                {
                    BaseLayoutItem item = recv_GetLayoutitem((DevExpress.Xpf.Docking.LayoutGroup)child, caption);
                    if (item != null) return item;
                }
                else if ((child.Caption as string) == caption) return child;
            }
            return null;
        }

        /// <summary>
        /// 지정한 모듈/뷰이름의 활성화된 뷰를 찾아서 리턴
        /// 동일 이름이 여러개일 경우 첫번째 뷰 리턴
        /// </summary>
        /// <param name="moduleName"></param>
        /// <param name="viewName"></param>
        /// <returns></returns>
        public object GetActiveModuleAndView(string moduleName, string viewName)
        {
            return ModuleHelper.GetActiveModuleAndView(moduleName, viewName);
        }

        public string GetCurrentViewName()
        {
            if (CurrentViewName.Length > 50)
                return CurrentViewName.Substring(0, 50);
            else
                return CurrentViewName;
        }

        public void SetSmallGlyphSize()
        {
            this.layoutService.ChangeToolbarGlyphSize();
            PubChangeToolbarGlyphSize(GlyphSize.Small);
        }

        public void SetLargeGlyphSize()
        {
            this.layoutService.ChangeToolbarGlyphSize(false);
            PubChangeToolbarGlyphSize(GlyphSize.Large);
        }

        #endregion


        public void SaveLayout()
        {
            LayoutHelper.SaveDockLayoutAndViewList(CurrentWorkspace);
        }

        public void LoadLayout()
        {
            LayoutHelper.LoadDockLayout(CurrentWorkspace);
        }

        public void DeleteLayout()
        {
            LayoutHelper.DeleteDockLayoutAndViewList(CurrentWorkspace);
        }
    }
}
