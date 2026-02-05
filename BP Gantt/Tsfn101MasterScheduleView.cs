using Amisys.Framework.Infrastructure;
using Amisys.Framework.Infrastructure.DataModels;
using Amisys.Framework.Infrastructure.Interfaces;
using Amisys.Framework.Infrastructure.PrismSupps;
using Amisys.Framework.Infrastructure.RegionAdapters;
using Amisys.Framework.Infrastructure.Utility;
using Amisys.Infrastructure.Infrastructure.DataModels;
using Amisys.Infrastructure.Infrastructure.Defintions;
using Amisys.Infrastructure.Infrastructure.Extensions;
using Amisys.Infrastructure.Infrastructure.Interfaces;
using Amisys.Infrastructure.Infrastructure.TableInfos;
using Amisys.Infrastructure.Infrastructure.Utility;
using DevExpress.Xpf.Bars;
using DevExpress.Xpf.Grid;
using DevExpress.Xpf.Docking.Base;
using Microsoft.Practices.Composite.Events;
using Microsoft.Practices.EnterpriseLibrary.Data;
using Microsoft.Practices.Prism.Regions;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Amisys.Presentation.AftMidPlan.AMPActivityManager.Models;
using Amisys.Presentation.AftMidPlan.AMPActivityManager.Dialogs;
using Amisys.Component.Presentation.AmiSmartGantt;
using Amisys.Presentation.AftMidPlan.AMPActivityManager.Definitions;
using Amisys.Presentation.AftMidPlan.AMPActivityManager.Utility;
using Amisys.Infrastructure.Infrastructure.UserControls;
using System.Reflection;

namespace Amisys.Presentation.AftMidPlan.AMPActivityManager.Views
{

    /// <summary>
    /// Tsfn101View
    /// </summary>
    [Export]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    public partial class Tsfn101MasterScheduleView : AmiViewBaseMef
    {
        #region enum
        private enum WorkType
        {
            Load,
            Save,
            /// <summary>
            /// 다른 리비전 번호로 저장
            /// </summary>
            SaveAs,
            SavePronet, 
            /// <summary>
            /// 표준 ACT로 호선 ACT 생성, 개선판 (선각 ACT는 101에 추가하지 않고 관계만 생성
            /// </summary>
            CreateFromStd,
            /// <summary>
            /// 표준절점제약으로 호선절점제약 생성
            /// </summary>
            CreatePntConstFromStd,
        }
        #endregion

        #region field

        bool IsModelShip = false;

        private readonly bool ERP_DEL_ACT = false;
        private readonly bool ERP_QA_SYS = true;

        private readonly IMasterDataService MasterDataService;
        private readonly IAftMidPlanDataService AftMidPlanDataService;
        private readonly IERPConnectService ErpConnectService;

        [Import]
        private Tsfn101MasterScheduleDM dataModel;

        private DataTSFN011List shipCaseList;

        private WorkType workType;

        private List<DataTSFN101> delActList;

        private DataTSFN101List cntractedActs;

        //19.12.02 신성훈 추가
        private DataTSFN006List tsfn006DtSrc;

        #endregion      

        #region ctor
        [ImportingConstructor]
        public Tsfn101MasterScheduleView(IMasterDataService MasterDataService, IAftMidPlanDataService AftMidPlanDataService, IERPConnectService erpConnectService)
            : base()
        {
            InitializeViewInfo();

            this.DataContext = this;
            this.MasterDataService = MasterDataService;
            this.AftMidPlanDataService = AftMidPlanDataService;
            this.ErpConnectService = erpConnectService;

            InitializeComponent();
            
            EventSubscribe(SubSaveLayout);
            EventSubscribe(SubChangeToolbarGlyphSize);
            EventSubscribe(SubShowHideContent);

            //Docking
            //EventSubscribe(SubDockItemActivated);
            //EventSubscribe(SubDockItemDragging);
            //EventSubscribe(SubDockItemEndDocking);

            NextStepDispatcher next = new NextStepDispatcher();
            next.NextStep += OnConstructNext;
            next.Start();

            DevExpress.Xpf.Core.DXGridDataController.DisableThreadingProblemsDetection = true;
        }

        private bool smartGanttLoaded = false;
        private void SmartGantt_Loaded(object sender, RoutedEventArgs e)
        {
            smartGantt.Loaded -= SmartGantt_Loaded;
            smartGanttLoaded = true;

            if (dataModel != null && dataModel.ActList != null)
                ShowGantt();
        }

        #endregion

        #region init
        private void OnConstructNext(object sender, EventArgs args)
        {
            #region 선각/모델선 구별

            IsModelShip = Tag != null && Tag.Equals("OUT");

            // 모델선인 경우
            if (IsModelShip)
            {
                eSelFigNo.IsVisible = true;          // 모델선 선택 콤보를 보여준다.
                eSelCaseNo.IsVisible = false;        // 리비전을 숨긴다.

                bDelete.IsVisible = false;           // 삭제
                bDeleteLink.IsVisible = false;       // 삭제 (선택한 관계 삭제하기)
                bDeleteDiv.IsVisible = false;        // 삭제 (선택한 구획의 ACT및 관계를 삭제)
                bCreatePronetSave.IsVisible = false; // ProNet일정 가져오기
            }
            // 선각인 경우
            else
            {
                eSelFigNo.IsVisible = false;         // 모델선 선택 콤보를 숨긴다.
                eSelCaseNo.IsVisible = true;         // 리비전을 보여준다.

                bDelete.IsVisible = true;            // 삭제
                bDeleteLink.IsVisible = true;        // 삭제 (선택한 관계 삭제하기)
                bDeleteDiv.IsVisible = true;         // 삭제 (선택한 구획의 ACT및 관계를 삭제)
                bCreatePronetSave.IsVisible = true;  // ProNet일정 가져오기
            }

            #endregion


            LoadGanttViewOption();

            LoadLayout();
            //호선리스트
            InitFigShpList();
            //착수완료 기준
            InitStFiGbn();
            //부서리스트
            InitDptCodCombo();
            //smartGantt.Loaded += SmartGantt_Loaded;
            InitGanttViewMode();
            //구획연결
            InitDivLinkGrid();
            //LastBlock 제외블록 리스트
            InitExceptBlockList();
        }

        private void InitExceptBlockList()
        {
            ExceptBlockList = MasterDataService.GetTSXA002_STD_COD_SELECTList("MS001");
        }

        private void InitDivLinkGrid()
        {
            // 구획연결 탭 
            // 연결 목록에는 선/후행 구획이 다른 관계만 표시
            DivLinkGridControl.FilterString = "[PreAct.DIV_COD] != [AftAct.DIV_COD]";
        }

        private void InitGanttViewMode()
        {
            // 간트보기옵션
            Dictionary<GanttViewMode, string> kvp = new Dictionary<Definitions.GanttViewMode, string>();
            kvp.Add(GanttViewMode.Default, EnumUtil.GetEnumDescription(GanttViewMode.Default));
            kvp.Add(GanttViewMode.DivLinkOnly, EnumUtil.GetEnumDescription(GanttViewMode.DivLinkOnly));
            kvp.Add(GanttViewMode.DivLinkWithDivAct, EnumUtil.GetEnumDescription(GanttViewMode.DivLinkWithDivAct));
            cmbGanttViewMode.ItemsSource = kvp;
        }

        private void InitWrkPntContextMenu()
        {
            // 노드 팝업 메뉴
            if (dataModel.WrkPntList != null && dataModel.WrkPntList.Count > 0)
            {
                bAddStWrkPntCnst.Items.Clear();
                bAddFiWrkPntCnst.Items.Clear();

                foreach (var wrkPnt in dataModel.WrkPntList)
                {
                    // 착수절점
                    var menu = new System.Windows.Controls.MenuItem();
                    menu.Header = wrkPnt.WRK_PNT + " " + wrkPnt.WRK_PNT_SHT;
                    menu.Click += bAddStWrkPntCnst_Click;
                    menu.Tag = wrkPnt.Row;
                    bAddStWrkPntCnst.Items.Add(menu);
                    // 완료절점
                    menu = new System.Windows.Controls.MenuItem();
                    menu.Header = wrkPnt.WRK_PNT + " " + wrkPnt.WRK_PNT_SHT;
                    menu.Click += bAddFiWrkPntCnst_Click;
                    menu.Tag = wrkPnt.Row;
                    bAddFiWrkPntCnst.Items.Add(menu);
                }
            }
        }

        private void InitDptCodCombo()
        {
            this.DptList = MasterDataService.GetTSXA002_STD_COD_SELECTList(StdCodMaster.SPS02);
            dataModel.DptList = this.DptList;
            cmbDptCodDes.ItemsSource = dataModel.DptList;
            cmbDptCodOrgDes.ItemsSource = dataModel.DptList;
        }

        private void InitStFiGbn()
        {
            // 절점제약 기준 (착수/완료)
            var list = new Dictionary<string, string>();
            list.Add("S", "착수");
            list.Add("F", "완료");
            cmbStFiGbn.ItemsSource = list;
        }

        /// <summary>
        /// 호선 콤보 초기화
        /// </summary>
        private void InitFigShpList()
        {
            // 모델선인 경우
            if (IsModelShip)
            {
                if (cmbFigNo != null && AftMidPlanDataService != null)
                {
                    var ds = AftMidPlanDataService.GetDataSetMODEL_SELECT();
                    if (ds != null && ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
                        cmbFigNo.ItemsSource = ds.Tables[0];
                }
            }
            // 선각인 경우
            else
            {
                if (cmbFigShp != null && AftMidPlanDataService != null)
                {
                    var ds = AftMidPlanDataService.GetDataSetTSAA002_KL_SELECT("19000101", "29991231");
                    if (ds != null && ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
                        cmbFigShp.ItemsSource = ds.Tables[0];
                }
            }
        }

        /// <summary>
        /// 모델선 변경 이벤트
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void eSelFigNo_EditValueChanged(object sender, RoutedEventArgs e)
        {
            // 모델선 호선 콤보 설정
            if (cmbFigShp != null && AftMidPlanDataService != null)
            {
                cmbFigShp.ItemsSource = null;

                var ds = AftMidPlanDataService.GetDataSetMODEL_SHIP_SELECT(SelFigNo);
                
                if (ds != null && ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
                    cmbFigShp.ItemsSource = ds.Tables[0];
            }
        }

        private void eSelFigShp_EditValueChanged(object sender, RoutedEventArgs e)
        {
            InitCaseList();
        }

        private void eSelFigShp_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.Enter) == true)
            {
                LoadData();
            }
        }

        /// <summary>
        /// 모델선 리비전 생성
        /// (모델선인 경우 리비전 정보를 관리하지 않기 때문에 임의로 생성해준다.)
        /// </summary>
        void InitCaseModelList()
        {
            shipCaseList = new DataTSFN011List();

            DataTable dt = new DataTable();
            dt.Columns.Add("SHP_COD");
            dt.Columns.Add("SHP_DES");
            dt.Columns.Add("SHP_TYP_NM");
            dt.Columns.Add("CASE_NO");
            dt.Columns.Add("WRK_PNT_REV_NO"); // 절점일정 계획 리비전
            dt.Columns.Add("SHP_KND");
            dt.Columns.Add("SHP_TYP");
            dt.Columns.Add("DCK_COD");
            dt.Columns.Add("REF_MODEL_NO");
            dt.Columns.Add("WC");
            dt.Columns.Add("KL");
            dt.Columns.Add("FT");
            dt.Columns.Add("LC");
            dt.Columns.Add("DL");
            dt.Columns.Add("FIG_SHP");
            dt.Columns.Add("REV_DES");
            dt.Columns.Add("CNF_YN");  // 확정 여부
            dt.Columns.Add("CNF_DAT"); // CNF_DAT 확정 일시
            dt.Columns.Add("CNF_USR"); // 확정한 사용자
            dt.Columns.Add("UP_DAT");
            dt.Columns.Add("UP_USR");
            dt.Columns.Add("IN_DAT");
            dt.Columns.Add("IN_USR");
            dt.Columns.Add("OWNRP_NM");
            dt.Columns.Add("YD_GBN");

            DataRow row = dt.NewRow();
            DataRow shipRow = (SelFigShpInfo as DataRowView).Row;
            row["SHP_COD"] = shipRow["SHP_COD"];
            row["SHP_DES"] = shipRow["SHP_DES"];
            row["SHP_TYP_NM"] = shipRow["SHP_TYP_NM"];
            row["CASE_NO"] = SelCaseNo;
            row["WRK_PNT_REV_NO"] = SelCaseNo;
            row["SHP_KND"] = shipRow["SHP_KND"];
            row["SHP_TYP"] = shipRow["SHP_TYP"];
            row["DCK_COD"] = shipRow["DCK_COD"];
            row["REF_MODEL_NO"] = SelCaseNo;
            row["WC"] = shipRow["WC"];
            row["KL"] = shipRow["KL"];
            //row["FT"] = shipRow["FT"];
            row["LC"] = shipRow["LC"];
            row["DL"] = shipRow["DL"];
            row["FIG_SHP"] = shipRow["FIG_SHP"];
            row["REV_DES"] = "모델선";
            row["CNF_YN"] = "Y";
            row["CNF_DAT"] = DateTime.Now.ToString("yyyyMMddHHmmdd");
            row["CNF_USR"] = appLogonInfo.GetUserId();
            row["UP_DAT"] = DateTime.Now.ToString("yyyyMMddHHmmdd");
            row["UP_USR"] = appLogonInfo.GetUserId();
            row["IN_DAT"] = DateTime.Now.ToString("yyyyMMddHHmmdd");
            row["IN_USR"] = appLogonInfo.GetUserId();
            row["OWNRP_NM"] = ""; // 선표선주명, 엑셀 출력에서 필요
            row["YD_GBN"] = ""; 
            dt.Rows.Add(row.ItemArray);

            shipCaseList.InitData(dt);
        }

        private void InitCaseList()
        {
            // 모델선인 경우 
            if (IsModelShip)
                InitCaseModelList();
            // 선각인 경우
            else
            {
                if (cmbFigShpRev != null && AftMidPlanDataService != null)
                {
                    // 리비전 목록
                    shipCaseList = AftMidPlanDataService.GetTSFN011_FIG_SHP_MANAGE_NO_OP_REV_SELECTList(SelFigShp);
                    cmbFigShpRev.ItemsSource = shipCaseList;

                    SelCaseNo = string.Empty;

                    if (string.IsNullOrEmpty(saveAsCaseNo) == false)
                    {
                        // 다른이름으로 저장했으면 해당 리비전 선택
                        SelCaseNo = saveAsCaseNo;
                    }
                    else
                    {
                        // 가장 최근 확정된 리비전, 없으면 가장 큰 리비전
                        if (shipCaseList != null && shipCaseList.Count > 0)
                        {
                            shipCaseList.SetLatestConfirmCase();
                            var lastCnfCase = shipCaseList.FirstOrDefault(e => e.LastConfirmYn == true);
                            if (lastCnfCase != null)
                                SelCaseNo = lastCnfCase.CASE_NO;
                            else
                                SelCaseNo = shipCaseList.OrderByDescending(e => e.CASE_NO).First().CASE_NO;
                        }
                    }
                }
            }
        }

        private DataTSFN011 GetSelectedCase(string caseNo)
        {
            if (shipCaseList != null)
            {
                return shipCaseList.FirstOrDefault(e => e.CASE_NO == caseNo);
            }
            return null;
        }

        #endregion


        #region 옵션 관리

        private void SaveGanttViewOption()
        {
            var filename = GetGanttViewOptionFilename();
            AmiXmlHelper.ExportToXml(filename, Options);
        }

        private void LoadGanttViewOption()
        {
            var filename = GetGanttViewOptionFilename();
            var option = (MasterScheduleUserOption)AmiXmlHelper.ImportFromXml(filename, typeof(MasterScheduleUserOption));
            if (option != null)
                Options = option;
        }

        private string GetGanttViewOptionFilename()
        {
            return AmiFileUtility.GetAppBasedAbsolutePath("ViewConfig\\" + moduleName + "_" + viewName + "_option.xml");
        }

        #endregion

        #region 이벤트

        #region 공통이벤트
        /// <summary>
        /// 현재 선택된 화면 여부
        /// </summary>
        public bool ActivatedDockItem
        {
            get { return _ActivatedDockItem; }
            set { _ActivatedDockItem = value; }
        }
        private bool _ActivatedDockItem = false;

        /// <summary>
        /// DockItemActivated Event
        /// </summary>
        /// <param name="param"></param>
        private void SubDockItemActivated(DynamicEventParam param)
        {
            if (param.IsContains("PubDockItemActivated", "", "CurrentViewName", "DockItemActivatedEventArgs"))
            {
                var CurrentViewName = param.GetParams("CurrentViewName");
                if (this.moduleName + "_" + this.viewName == CurrentViewName)
                    _ActivatedDockItem = true;
                else
                    _ActivatedDockItem = false;
            }
        }

        /// <summary>
        /// DockItemDragging Event
        /// </summary>
        /// <param name="param"></param>
        private void SubDockItemDragging(DynamicEventParam param)
        {
            if (_ActivatedDockItem && param.IsContains("PubDockItemDragging", "", "DockItemDraggingEventArgs"))
            {
                DockItemDraggingEventArgs e = param.GetParams("DockItemDraggingEventArgs");
            }
        }

        /// <summary>
        /// DockItemEndDocking Event
        /// </summary>
        /// <param name="param"></param>
        private void SubDockItemEndDocking(DynamicEventParam param)
        {
            if (_ActivatedDockItem && param.IsContains("PubDockItemEndDocking", "", "DockItemDockingEventArgs"))
            {
                DockItemDockingEventArgs e = param.GetParams("DockItemDockingEventArgs");
            }
        }

        /// <summary>
        /// 레이아웃 조회
        /// </summary>
        private void LoadLayout()
        {
            this.layoutManager.LoadControlLayout(this.ActGridControl, panelCaption, appLogonInfo.GetUserName());
            this.layoutManager.LoadControlLayout(this.LinkGridControl, panelCaption, appLogonInfo.GetUserName());
        }

        /// <summary>
        /// 레아아웃 저장
        /// </summary>
        private void SaveLayout()
        {
            this.layoutManager.SaveControlLayout(this.ActGridControl, panelCaption, appLogonInfo.GetUserName());
            this.layoutManager.SaveControlLayout(this.LinkGridControl, panelCaption, appLogonInfo.GetUserName());
        }

        /// <summary>
        /// 레이아웃 저장 Subscribve
        /// </summary>
        /// <param name="param"></param>
        private void SubSaveLayout(DynamicEventParam param)
        {
            if (param.IsContains("PubSaveLayout", ""))
            {
                SaveLayout();
            }
        }

        /// <summary>
        /// 저장할 데이터 유무 Request 처리
        /// </summary>
        /// <param name="param"></param>
        protected override void SubReqHavingUnsavedData(DynamicEventParam param)
        {
            if (param.IsContains("PubReqHavingUnsavedData", ""))
            {
                if (CheckHasUnsavedData() == true)
                {
                    PubResHavingUnsavedData(true);
                }
            }
        }

        /// <summary>
        /// 저장할 데이터가 있는지 체크
        /// </summary>
        /// <param name="param"></param>
        private bool CheckHasUnsavedData()
        {
            //데이터 저장 여부 체크 루틴 구현
            return false;
        }

        /// <summary>
        /// 저장할 데이터 유무 Response 처리
        /// </summary>
        /// <param name="haveUnsavedData"></param>
        protected override void PubResHavingUnsavedData(bool haveUnsavedData)
        {
            base.PubResHavingUnsavedData(haveUnsavedData);
        }

        /// <summary>
        /// 데이터 저장 처리
        /// </summary>
        /// <param name="param"></param>
        protected override void SubSaveData(DynamicEventParam param)
        {
            if (param.IsContains("PubSaveData", ""))
            {
                //SaveData();
            }
        }

        /// <summary>
        /// 툴바 사이즈 변경 subscribe
        /// </summary>
        /// <param name="param"></param>
        private void SubChangeToolbarGlyphSize(DynamicEventParam param)
        {
            if (param.IsParamContains("PubChangeToolbarGlyphSize", "ToolbarGlyphSize"))
            {
                GlyphSize size = param.GetParams("ToolbarGlyphSize");
                Tsfn101ViewBarManager.ToolbarGlyphSize = size;
            }
        }

        /// <summary>
        /// 바 아이템 디스플레이미 모드
        /// </summary>
        public BarItemDisplayMode BarItemDisplayMode
        {
            get
            {
                return _BarItemDisplayMode;
            }
            set
            {
                _BarItemDisplayMode = value;
                this.OnPropertyChanged("BarItemDisplayMode");
            }
        }
        private BarItemDisplayMode _BarItemDisplayMode = BarItemDisplayMode.Default;

        /// <summary>
        /// 바 아이템 디스플레이미 모드 subscribe
        /// </summary>
        /// <param name="param"></param>
        private void SubShowHideContent(DynamicEventParam param)
        {
            if (param.IsParamContains("PubShowHideContent", "BarItemDisplayMode"))
            {
                BarItemDisplayMode = param.GetParams("BarItemDisplayMode");
            }
        }
        #endregion

        #endregion

        #region  property


        public MasterScheduleUserOption Options
        {
            get { return _Options; }
            set { _Options = value; OnPropertyChanged(); }
        }
        private MasterScheduleUserOption _Options = new MasterScheduleUserOption();


        public DataTSFN101 SelActItem
        {
            get { return _SelActItem; }
            set { _SelActItem = value; OnPropertyChanged(); }
        }
        private DataTSFN101 _SelActItem;

        public DataTSFN102 SelLinkItem
        {
            get { return _SelLinkItem; }
            set { _SelLinkItem = value; OnPropertyChanged(); }
        }
        private DataTSFN102 _SelLinkItem;

        /// <summary>
        /// Revision 번호
        /// </summary>
        public string SelCaseNo
        {
            get
            {
                // 모델선인 경우 (Revision을 관리하지 않는다.)
                if (IsModelShip)
                    return "000000000000";
                // 선각인 경우
                else
                    return selCASE_NO;
            }
            set { selCASE_NO = value; OnPropertyChanged(); }
        }
        private string selCASE_NO;
        public string SelFigShp
        {
            get
            {
                if(SelFigShpInfo != null)
                {
                    var selshipinfo = SelFigShpInfo as DataRowView;
                    return selshipinfo["FIG_SHP"].ToString();
                }
                return string.Empty;
            }
        }

        public string SelFigNo
        {
            get
            {
                if (SelFigNoInfo != null)
                {
                    var selFiginfo = SelFigNoInfo as DataRowView;
                    return selFiginfo["FIG_NO"].ToString();
                }
                return string.Empty;
            }
        }

        /// <summary>
        /// 모델선 콤보
        /// </summary>
        public object SelFigNoInfo
        {
            get
            {
                return _SelFigNoInfo;
            }
            set
            {
                _SelFigNoInfo = value;
                OnPropertyChanged("SelFigNoInfo");
            }
        }
        private object _SelFigNoInfo;

        /// <summary>
        /// 호선 콤보
        /// </summary>
        public object SelFigShpInfo
        {
            get
            {
                return _SelFigShpInfo;
            }
            set
            {
                _SelFigShpInfo = value;
                OnPropertyChanged("SelFigShpInfo");
            }
        }
        private object _SelFigShpInfo;


        public double ZoomFactor
        {
            get
            {
                return _ZoomFactor;
            }
            set
            {
                _ZoomFactor = value;
                OnPropertyChanged("ZoomFactor");

                ZoomFactorPercent = value * 100;
            }
        }
        private double _ZoomFactor = 1.0;

        public double ZoomFactorPercent
        {
            get
            {
                return _ZoomFactorPercent;
            }
            set
            {
                _ZoomFactorPercent = value;
                OnPropertyChanged("ZoomFactorPercent");
            }
        }
        private double _ZoomFactorPercent = 100;

        /// <summary>
        /// 관계생성모드
        /// </summary>
        public bool LinkEditMode
        {
            get
            {
                return _LinkEditMode;
            }
            set
            {
                _LinkEditMode = value;
                OnPropertyChanged("LinkEditMode");
            }
        }
        private bool _LinkEditMode = false;

        public DataTSXA002List DptList
        {
            get
            {
                return _DptList;
            }
            set

            {
                _DptList = value;
                OnPropertyChanged("DptList");
            }
        }
        private DataTSXA002List _DptList;


        public DataTSXA002List ExceptBlockList
        {   
            get
            {
                return _ExceptBlockList;
            }
            set
            {
                _ExceptBlockList = value;
                OnPropertyChanged("ExceptBlockList");
            }
        }
        private DataTSXA002List _ExceptBlockList;


        /// <summary>
        /// 절점제약기준, 착수/완료
        /// </summary>
        public bool WrkPntCnstStart
        {
            get
            {
                return _WrkPntCnstStart;
            }
            set
            {
                _WrkPntCnstStart = value;
                OnPropertyChanged("WrkPntCnstStart");
            }
        }
        private bool _WrkPntCnstStart;


        /// <summary>
        /// 필터적용여부, ACT 상세에 필터링된 내용만 표시할지 여부
        /// </summary>
        public bool ApplyFilter
        {
            get
            {
                return _ApplyFilter;
            }
            set
            {
                _ApplyFilter = value;
                OnPropertyChanged("ApplyFilter");
            }
        }
        private bool _ApplyFilter;

        public GanttViewMode SelGanttViewMode
        {
            get
            {
                return _SelGanttViewMode;
            }
            set
            {
                _SelGanttViewMode = value;
                OnPropertyChanged("SelGanttViewMode");
            }
        }
        private GanttViewMode _SelGanttViewMode;

        #endregion


        #region worker
        protected override void worker_DoWork(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            this.entLibService.Process(() =>
            {
                string res = string.Empty;
                switch (workType)
                {
                    case WorkType.Load:

                        Load();
                        break;
                    case WorkType.Save:
                        res = Save();
                        if (confirmAfterSave == true && string.IsNullOrEmpty(res) == true)
                            res = Confirm(SelCaseNo, SelFigShp);
                        e.Result = res;
                        break;
                    case WorkType.SaveAs:
                        res = SaveAs();
                        if (confirmAfterSave == true && string.IsNullOrEmpty(res) == true)
                            res = Confirm(saveAsCaseNo, SelFigShp);
                        e.Result = res;
                        break;
                    case WorkType.SavePronet:
                        e.Result = SavePronet();
                        break;
                    case WorkType.CreateFromStd:
                        e.Result = CreateFromStd();
                        break;
                    case WorkType.CreatePntConstFromStd:
                        e.Result = CreatePntConstFromStd();
                        break;
                }
            });
        }

        protected override void worker_RunWorkerCompleted(object sender, System.ComponentModel.RunWorkerCompletedEventArgs e)
        {
            base.worker_RunWorkerCompleted(sender, e);

            var strResult = string.Empty;

            switch (workType) 
            {
                case WorkType.Load:
                    // 데이터 로드 완료 처리
                    ActGridControl.ItemsSource = dataModel.ActList;
                    LinkGridControl.ItemsSource = dataModel.LinkList;

                    // 구획 관계
                    PreActGridControl.ItemsSource = dataModel.ActList;
                    AftActGridControl.ItemsSource = dataModel.ActList;
                    DivLinkGridControl.ItemsSource = dataModel.LinkList;

                    // 절점제약
                    PntActGridControl.ItemsSource = dataModel.ActList;
                    PntGridControl.ItemsSource = dataModel.WrkPntList;
                    PntConstGridControl.ItemsSource = dataModel.WrkPntConstraints;
                    //절점 컨텍스트 메뉴 동적으로 생성(호선의 절점 데이터 사용)
                    InitWrkPntContextMenu();
                    //간트 화면 표시
                    ShowGantt();
                    break;
                case WorkType.Save:
                    strResult = (string)e.Result;
                    if (string.IsNullOrEmpty(strResult) == true)
                        messageService.ShowMessage("변경사항을 저장하였습니다.");
                    //LoadData();
                    break;
                    
                case WorkType.SaveAs:
                    // rev 목록 갱신
                    strResult = (string)e.Result;
                    if (string.IsNullOrEmpty(strResult) == true)
                    {
                        messageService.ShowMessage("변경사항을 저장하였습니다.");
                        InitCaseList();
                    }
                    break;
                case WorkType.CreateFromStd:
                    strResult = (string)e.Result;
                    if (string.IsNullOrEmpty(strResult) == true)
                    {
                        messageService.ShowMessage("호선ACT 생성을 완료하였습니다."); // 성공

                        //ActGridControl.ItemsSource = dataModel.ActList;
                        //LinkGridControl.ItemsSource = dataModel.LinkList;
                        //ShowGantt();

                        //표준부서 셋팅
                        SetAssignStdDpt();

                        // 생성시 발생한 에러 표시
                        ShowErrorActs(createErrorActs);

                        // 다시 조회
                        workType = WorkType.Load;
                        base.RunBackgroundWorker();
                    }
                    else
                        messageService.ShowMessage(strResult);    // 싱패
                    break;
                case WorkType.CreatePntConstFromStd:
                    strResult = (string)e.Result;
                    if (string.IsNullOrEmpty(strResult) == true)
                        messageService.ShowMessage("호선절점제약 생성을 완료하였습니다."); // 성공
                    else
                        messageService.ShowMessage(strResult);    // 싱패
                    break;
            }
        }

        private void SetAssignStdDpt()
        {
            List<DataTSFN101> actList = null;
            if (DockLayoutManager.ActiveDockItem == GanttPanel)
                actList = smartGantt.SelectNodeList.Select(i => i.Source).Cast<DataTSFN101>().ToList();
            else if (DockLayoutManager.ActiveDockItem == ActPanel)
                actList = ActGridControl.SelectedItems.Cast<DataTSFN101>().ToList();

            AssignStdDpt(actList);
        }

        #endregion

        #region 데이터 로드

        private void bLoad_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
        {
            if(IsModelShip && string.IsNullOrEmpty(SelFigNo))
            {
                messageService.ShowWarning("선택된 모델선이 없습니다.");
                return;
            }
            if(string.IsNullOrEmpty(SelFigShp))
            {
                messageService.ShowWarning("선택된 호선이 없습니다.");
                return;
            }
            if(string.IsNullOrEmpty(SelCaseNo))
            {
                messageService.ShowWarning("선택된 리비전이 없습니다.");
                return;
            }

            LoadData();
        }

        private void LoadData()
        {
            // REV 호선정보체크 AND UPDATE 
            // REV에 호선정보가 REV를 생성하는시점데이터로 고정이 되어있어서 운영의 호선정보가 업데이트됐을시 문제가 되는부분이 발견되어 수정
            // 20190110 이창근
            var selCase = GetSelectedCase(SelCaseNo);

            if (IsChangedShipInfo(selCase))
            {
                // 호선정보 업데이트

#if !DEBUG
                AftMidPlanDataService.UpdateShipInfo(selCase.SHP_COD);
#endif
                var tempCaseNo = selCase.CASE_NO;
                InitCaseList();
                SelCaseNo = tempCaseNo;
            }
            
            if (shipCaseList == null || shipCaseList.Count == 0)
            {
                messageService.ShowWarning("생성된 리비전이 없습니다. 신규 리비전을 생성하세요.");
                return;
            }

            if (string.IsNullOrEmpty(SelFigShp) == true || string.IsNullOrEmpty(SelCaseNo) == true)
            {
                messageService.ShowWarning("호선 및 리비전을 선택하세요.");
                return;
            }

            workType = WorkType.Load;
            base.RunBackgroundWorker();
        }

        
        string[] CheckValArr = new string[] {
            "SHP_DES",
            "YD_GBN",
            "DCK_COD",
            "SHP_KND",
            "SHP_TYP",
            "SHP_TYP_QTY",
            "SHP_TYP_NM",
            "WC",
            "KL",
            "FT",
            "LC",
            "DL",
            "BLD_COD",
            "ORI_OWN_NM",
            "OWNRP_NM",
            "PRJ_GBN",
            "CPY_SHP"
        };

        private bool IsChangedShipInfo(DataTSFN011 selCase)
        {
            var newShipInfo = MasterDataService.GetSelectProjectInfo(selCase.SHP_COD);
            if (newShipInfo != null)
            {
                foreach (var colName in CheckValArr)
                {
                    if (newShipInfo.Table.Columns.Contains(colName))
                    {
                        var curVal = GetPropertyValue(selCase, colName);
                        var newVal = newShipInfo[colName].ToString();

                        try
                        {
                            if (curVal.Equals(newVal) == false) return true;
                        }
                        catch(Exception ex)
                        {
                            return false;
                        }
                    }
                }
            }
            return false;
        }

        public static object GetPropertyValue(object obj, string propertyName)
        {
            if (obj != null && string.IsNullOrEmpty(propertyName) == false)
            {
                PropertyInfo pi = obj.GetType().GetProperty(propertyName);
                try
                {
                    if (pi != null)
                        return pi.GetValue(obj, null);
                }
                catch(Exception ex)
                {
                    return null;
                }
            }
            return null;
        }


        private void Load()
        {
            // 케이스 마스터
            var selCase = GetSelectedCase(SelCaseNo);
            delActList = new List<DataTSFN101>();

            // 간트 패턴 정보
            var ganttPattern2 = AftMidPlanDataService.GetTSFN009_CATEGORY_SELECTList("*");
            dataModel.InitGanttUtil(ganttPattern2);
            dataModel.Calendar = MasterDataService.GetHHICalendar();

            // ACT 및 관계
            // 건조는 101에서 제외
            var actList = AftMidPlanDataService.GetTSFN101_CASE_NO_FIG_SHP_MS_SELECTList2(SelCaseNo, SelFigShp, SelFigNo);
            var linkList = AftMidPlanDataService.GetTSFN102_CASE_NO_FIG_SHP_MS_SELECTList2(SelCaseNo, SelFigShp, SelFigNo);
            dataModel.InitDataSource(actList, linkList);
            
            // 호선절점
            // 절점은 케이스에 써진 리비전을 가져오도록 수정 필요
            // 화면 열면서 조회
            DataTSDC002List wrkPnts = null;
            if (string.IsNullOrEmpty(selCase.WRK_PNT_REV_NO) || IsModelShip)
            {
                wrkPnts = AftMidPlanDataService.GetTSDC002_FIG_SHP_SELECTList(SelFigShp, SelFigNo);   // 별도 리비전이 없으면 운영
            }
            else
            {
                // 선각인 경우만
                if (IsModelShip == false)
                    wrkPnts = AftMidPlanDataService.GetTSDC002_REV_FIG_SHP_REV_NO_SELECTList(SelFigShp, selCase.WRK_PNT_REV_NO);
                else
                    wrkPnts = new DataTSDC002List();
            }
            dataModel.InitWrkPntList(wrkPnts);

            // 절점제약
            var cnts = AftMidPlanDataService.GetTSFN105_CASE_NO_FIG_SHP_SELECTList(SelCaseNo, SelFigShp, SelFigNo);
            dataModel.InitWrkPntConstraints(cnts);

            // 표준건조공기
            // 19.12.02 신성훈
            //tsfn006DtSrc = AftMidPlanDataService.GetTSFN006_SELECTList(selCase.SHP_TYP, selCase.DCK_COD);
        }
         

        #endregion

        #region 데이터 저장
        

        private string saveAsCaseNo;
        private string saveAsRevDes;
        private bool confirmAfterSave;
        //private void bSave_ItemClick(object sender, ItemClickEventArgs e)
        //{
        //    if (dataModel != null && dataModel.ActList != null)
        //    {
        //        ActGridControl.ValueEndEdit();
        //        LinkGridControl.ValueEndEdit();

        //        var res = dataModel.Validate();
        //        if (string.IsNullOrEmpty(res) == true)
        //        {
        //            var dlg = new MasterScheduleSaveDialog(this.messageService, this.shipCaseList, GetSelectedCase(SelCaseNo));
        //            if (dlg.ShowDialog() == true && dlg.SelCase != null)
        //            {
        //                if (dlg.SelCase.CASE_NO == this.SelCaseNo)
        //                {
        //                    // 조회한 리비전에 저장
        //                    workType = WorkType.Save;
        //                    base.RunBackgroundWorker();
        //                }
        //                else if (dlg.NewCase == null && dlg.SelCase.CASE_NO != this.SelCaseNo)
        //                {
        //                    // 다른 리비전에 저장
        //                    saveAsCaseNo = dlg.SelCase.CASE_NO;
        //                    saveAsRevDes = dlg.SelCase.REV_DES;
        //                    workType = WorkType.SaveAs;
        //                    base.RunBackgroundWorker();
        //                }
        //                else if (dlg.NewCase != null)
        //                {
        //                    // 신규 리비전 생성
        //                    saveAsCaseNo = AftMidPlanDataService.ExecuteFC_AMP_GET_NEXT_CASE_NO(SelFigShp);
        //                    if (string.IsNullOrEmpty(saveAsCaseNo) == true)
        //                        messageService.ShowWarning("신규 리비전 번호를 생성할 수 없습니다.");
        //                    saveAsRevDes = dlg.SelCase.REV_DES;
        //                    workType = WorkType.SaveAs;
        //                    base.RunBackgroundWorker();
        //                }
        //            }
        //        }
        //        else
        //        {
        //            messageService.ShowWarning(res);
        //        }
        //    }
        //}

        private int SavePronet()
        {
            if (dataModel != null && dataModel.ActList != null && dataModel.LinkList != null)
            {
                int res = 0;
                //선각 --> TSEA002 
                var nStgActDt = dataModel.GetSaveDataTableNStgHullAct();
                //if (nStgActDt != null)
                //    AftMidPlanDataService.UpdateTSEA002_PRONET_DT(nStgActDt);

                // 족장(N41) -> TSFA001
                var n91ActDt = dataModel.GetSaveDataTableN91Act();
                if (nStgActDt != null) 
                    AftMidPlanDataService.UpdateTSFA001_PRONET_DT(nStgActDt);

                // 후행
                var aftActDt = dataModel.GetSaveDataTableAftAct();
                if (aftActDt != null) { }
                    AftMidPlanDataService.UpdateTSFN101_PRONET_DT(aftActDt);

                //Load();
                //저장 후에 재조회가 안된다고 하여 Load에서 LoadData로 변경(20200624 주원)
                LoadData();


            }
            return 0;
        }
        private void bSave_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (dataModel != null && dataModel.ActList != null)
            {
                ActGridControl.ValueEndEdit();
                LinkGridControl.ValueEndEdit();

                var res = dataModel.Validate();
                if (string.IsNullOrEmpty(res) == true)
                {
                    // 모델선인 경우 리비전 팝업을 띄우지 않는다.
                    if(IsModelShip)
                    {
                        workType = WorkType.Save;
                        base.RunBackgroundWorker();
                        return;
                    }

                    var dlg = new CaseSaveDialog(this.messageService, this.shipCaseList, GetSelectedCase(SelCaseNo));
                    if (dlg.ShowDialog() == true)
                    {
                        confirmAfterSave = dlg.DoConfirm;
                        if (dlg.SelOld == true && dlg.SelCase.CASE_NO == this.SelCaseNo)
                        {
                            // 조회한 리비전에 저장
                            saveAsRevDes = dlg.Description;
                            workType = WorkType.Save;
                            base.RunBackgroundWorker();

                            //PRONET 일정 저장 기능 추가(20200528 주원)
                            /*if (dataModel != null && dataModel.ActList != null && dataModel.LinkList != null)
                            {
                                //선각 --> TSEA002 
                                var nStgActDt = dataModel.GetSaveDataTableNStgHullAct();

                                // 족장(N41) -> TSFA001
                                var n91ActDt = dataModel.GetSaveDataTableN91Act();
                                if (nStgActDt != null)
                                    AftMidPlanDataService.UpdateTSFA001_PRONET_DT(nStgActDt);

                                // 후행
                                var aftActDt = dataModel.GetSaveDataTableAftAct();
                                if (aftActDt != null) { }
                                AftMidPlanDataService.UpdateTSFN101_PRONET_DT(aftActDt);

                                Load();
                            }*/

                        }
                        else if (dlg.SelOld == true && dlg.SelCase.CASE_NO != this.SelCaseNo)
                        {
                            // 다른 리비전에 저장
                            saveAsCaseNo = dlg.SelCase.CASE_NO;
                            saveAsRevDes = dlg.Description;
                            workType = WorkType.SaveAs;
                            base.RunBackgroundWorker();

                            //PRONET 일정 저장 기능 추가(20200528 주원)
                            /*if (dataModel != null && dataModel.ActList != null && dataModel.LinkList != null)
                            {
                                //선각 --> TSEA002 
                                var nStgActDt = dataModel.GetSaveDataTableNStgHullAct();
                             
                                // 족장(N41) -> TSFA001
                                var n91ActDt = dataModel.GetSaveDataTableN91Act();
                                if (nStgActDt != null)
                                    AftMidPlanDataService.UpdateTSFA001_PRONET_DT(nStgActDt);

                                // 후행
                                var aftActDt = dataModel.GetSaveDataTableAftAct();
                                if (aftActDt != null) { }

                                //변경된 리비전을 데이터로 가져온다.
                                for(int i = 0; i<aftActDt.Rows.Count; i++)
                                {
                                    aftActDt.Rows[0]["CASE_NO"] = saveAsCaseNo;
                                }
                                AftMidPlanDataService.UpdateTSFN101_PRONET_DT(aftActDt);

                                Load();
                            }*/

                        }
                        else if (dlg.SelOld == false)
                        {
                            // 신규 리비전 생성
                            saveAsCaseNo = AftMidPlanDataService.ExecuteFC_AMP_GET_NEXT_CASE_NO(SelFigShp);
                                        //ExecuteFC_PRONET_NEW(string FIG_SHP, string CASE_NO)
                            if (string.IsNullOrEmpty(saveAsCaseNo) == true)
                                messageService.ShowWarning("신규 리비전 번호를 생성할 수 없습니다.");
                            saveAsRevDes = dlg.Description;
                            workType = WorkType.SaveAs;
                            base.RunBackgroundWorker();

                            //기존 리비전 번호 데이터로 신규 리비전 만들고
                           /* string oldCaseNo = this.SelCaseNo;
                            var newCaseNo = AftMidPlanDataService.ExecuteFC_PRONET_NEW(SelFigShp, oldCaseNo);

                            //PRONET 일정 저장 기능 추가(20200528 주원)
                            if (dataModel != null && dataModel.ActList != null && dataModel.LinkList != null)
                            {
                                // 후행
                                var aftActDt = dataModel.GetSaveDataTableAftAct();
                                if (aftActDt != null) { }

                                //변경된 리비전을 데이터로 가져온다.
                                for (int i = 0; i < aftActDt.Rows.Count; i++)
                                {
                                    aftActDt.Rows[0]["CASE_NO"] = newCaseNo;
                                }
                                AftMidPlanDataService.UpdateTSFN101_PRONET_DT(aftActDt);

                                Load();
                            }*/
                        }
                    }
                }
                else
                {
                    messageService.ShowWarning(res);
                }
            }
        }

        private string Save()
        {
            if (dataModel != null && dataModel.ActList != null && dataModel.LinkList != null)
            {
                int res = 0;

                // 3. REV_DES, 원본 리비전 등 업데이트
                var desUpdateRes = AftMidPlanDataService.UpdateTSFN011_REV_DES(this.SelCaseNo, SelFigShp, saveAsRevDes);

                // 선각(N) -> TSEA002
                var nStgActDt = dataModel.GetSaveDataTableNStgHullAct();
                if (nStgActDt != null) 
                    AftMidPlanDataService.UpdateTSEA002_N_STG_PLN_DT(nStgActDt, SelFigNo);

                // 족장(N91) -> TSFA001
                var n91ActDt = dataModel.GetSaveDataTableN91Act();
                if (n91ActDt != null)
                    AftMidPlanDataService.UpdateTSFA001_N91_PLN_DT(n91ActDt, SelFigNo);

                // 후행
                var aftActDt = dataModel.GetSaveDataTableAftAct();
                if (aftActDt != null)
                    res += AftMidPlanDataService.UpdateTSFN101(aftActDt, SelFigNo);
                
                // 원본 Table 변경사항 반영
                dataModel.ActList.dtDataSource.AcceptChanges();

                Dictionary<string, int> groupby = new Dictionary<string, int>();
                Dictionary<string, List<DataTSFN102>> groupbyItm = new Dictionary<string, List<DataTSFN102>>();
                foreach (var item in dataModel.LinkList)
                {
                    if (item.Row.RowState != DataRowState.Unchanged)
                    {
                        if (groupby.ContainsKey(item.CASE_NO + item.FIG_SHP + item.PRE_ACT + item.AFT_ACT) == false)
                        {
                            groupby.Add(item.CASE_NO + item.FIG_SHP + item.PRE_ACT + item.AFT_ACT, 0);
                            groupbyItm.Add(item.CASE_NO + item.FIG_SHP + item.PRE_ACT + item.AFT_ACT, new List<DataTSFN102>());
                        }

                        groupby[item.CASE_NO + item.FIG_SHP + item.PRE_ACT + item.AFT_ACT]++;
                        groupbyItm[item.CASE_NO + item.FIG_SHP + item.PRE_ACT + item.AFT_ACT].Add(item);
                    }
                }

     
                var selItem = dataModel.LinkList.Where(c => c.CASE_NO == "000000000004" && c.FIG_SHP == "2984" && c.PRE_ACT == "F11A3N4A000  " && c.AFT_ACT == "F11A3N4B000  ").ToList();
                
                // 관계
                res += AftMidPlanDataService.UpdateTSFN102(dataModel.LinkList.dtDataSource, SelFigNo);

                // 선각인 경우
                if (IsModelShip == false)
                {
                    // 절점제약
                    res += AftMidPlanDataService.UpdateTSFN105(dataModel.WrkPntConstraints.dtDataSource, SelFigNo);
                }

                // 삭제된 ACT ERP에 반영
                DeleteErpAct();

                //return res;
                return string.Empty;
            }
            return string.Empty;
        }

        private void DeleteErpAct()
        {
            if (this.ERP_DEL_ACT == true && delActList != null && delActList.Count > 0)
            {
                DataTable dtAct = AftMidPlanDataService.GetNewDataTableZPSCS278("ACT_LIST");

                foreach (var act in delActList)
                {
                    DataRow newRow = dtAct.NewRow();
                    newRow["PSPID"] = act.Row["FIG_SHP", DataRowVersion.Original].ToString();
                    newRow["ACT_COD"] = act.Row["ACT_COD", DataRowVersion.Original].ToString();
                    newRow["MOD_HIS"] = "D";
                    dtAct.Rows.Add(newRow);
                }

                DataTable dtError = AftMidPlanDataService.GetNewDataTableZPSCS277("ERR_LIST");
                DataTable dtResult = AftMidPlanDataService.GetNewDataTableZPSCS278("RST_LIST");

                // 선각인 경우만
                if (IsModelShip == false)
                {
                    var dsResult = this.ErpConnectService.ExecZ_PS_C_IF032_NCO(dtAct, dtError, dtResult, true, ERP_QA_SYS);

                    if (dsResult != null && dsResult.Tables.Contains("OUT_LIST"))
                    {
                        DataTable outList = dsResult.Tables["OUT_LIST"];
                        DataRow dr = outList.Rows[0];
                        var _IF_ID = dr["IF_ID"].ToString();
                        string JOBNAME = dr["BATCH_NM"].ToString();
                        string JOBCOUNT = dr["BATCH_ID"].ToString();
                        int actCnt = dtAct.Rows.Count;
                        AftMidPlanDataService.InsertTSFZ003(SelFigShp, _IF_ID, JOBNAME, JOBCOUNT, actCnt);
                        string delQuery = string.Format("DELETE TSFZ001 WHERE IF_ID = '{0}' AND PSPID = '{1}'", _IF_ID, SelFigShp);
                        AftMidPlanDataService.BulkDeleteInsertTSFZ001(dtAct, delQuery, _IF_ID);
                    }
                }
            }
        }

        private string SaveAs()
        {
            if (dataModel != null && dataModel.ActList != null && dataModel.LinkList != null && string.IsNullOrEmpty(saveAsCaseNo) == false)
            {
                // 1. 신규 리비전번호 채번
                //saveAsCaseNo = AftMidPlanDataService.ExecuteFC_AMP_GET_NEXT_CASE_NO(SelFigShp);
                //if (string.IsNullOrEmpty(saveAsCaseNo) == true)
                //    return "신규 리비전 번호 생성할 수 없습니다.";

                // 2. TGT리비전 삭제/복사
                // 신규 리비전 생성 시, 확정정보/인터페이스 정보는 제외
                // 선각인 경우만 수행
                if (IsModelShip == false)
                {
                    var delRev = AftMidPlanDataService.DeleteTSFN011(saveAsCaseNo, SelFigShp);
                    var copyRev = AftMidPlanDataService.CopyTSFN011(SelCaseNo, SelFigShp, saveAsCaseNo, SelFigShp, saveAsRevDes);
                    // 3. REV_DES, 원본 리비전 등 업데이트
                    var desUpdateRes = AftMidPlanDataService.UpdateTSFN011_REV_DES(saveAsCaseNo, SelFigShp, saveAsRevDes);
                    // 4. 리비전 데이터 복사
                    var copyRevData = AftMidPlanDataService.ExecutePROC_AMP_COPY_SHIP_CASE(SelCaseNo, SelFigShp, saveAsCaseNo, SelFigShp);
                }

                ////////////////////////////////
                // 5. 편집중인 마스터 스케줄 로선/리비전 신규로 업데이트 후 저장(delete/insert)

                // 선각(N) -> TSEA002
                var nStgActDt = dataModel.GetSaveDataTableNStgHullAct();
                if (nStgActDt != null)
                    AftMidPlanDataService.UpdateTSEA002_N_STG_PLN_DT(nStgActDt, SelFigNo);

                // 족장(N91) -> TSFA001
                var n91ActDt = dataModel.GetSaveDataTableN91Act();
                if (n91ActDt != null)
                    AftMidPlanDataService.UpdateTSFA001_N91_PLN_DT(n91ActDt, SelFigNo);

                // 외업 ACT, 관계
                DataTable dtAct = null;
                DataTable dtLink = null;
                //dataModel.ChangeActLinkCaseNo(saveAsCaseNo);
                dataModel.MakeSaveAsData(saveAsCaseNo, out dtAct, out dtLink);

                var delQuery = string.Format("DELETE TSFN101 WHERE CASE_NO = '{0}' AND FIG_SHP = '{1}'", saveAsCaseNo, SelFigShp);
                AftMidPlanDataService.BulkDeleteInsertTSFN101(dtAct, delQuery, SelFigNo);
                delQuery = string.Format("DELETE TSFN102 WHERE CASE_NO = '{0}' AND FIG_SHP = '{1}'", saveAsCaseNo, SelFigShp);
                AftMidPlanDataService.BulkDeleteInsertTSFN102(dtLink, delQuery, SelFigNo);

                // 변경사항 반영
                dataModel.ActList.dtDataSource.AcceptChanges();
                dataModel.LinkList.dtDataSource.AcceptChanges();
            }
            return string.Empty;
        }

        public string Confirm(string caseNo, string figShp)
        {
            // 모델선인 경우 아래 로직을 수행하지 않는다.
            if(IsModelShip)
                return string.Empty;

            if (string.IsNullOrEmpty(caseNo) == false && string.IsNullOrEmpty(figShp) == false)
            {
                return AftMidPlanDataService.ExecutePROC_AMP_CONFIRM_MS(caseNo, figShp);
            }

            return string.Empty;
        }

        private void bCreatePronetSave_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
        {
            if (dataModel != null && dataModel.ActList != null)
            {
                ActGridControl.ValueEndEdit();
                LinkGridControl.ValueEndEdit();

                var res = dataModel.Validate();
                if (string.IsNullOrEmpty(res) == true)
                {
                    if (messageService.ShowYesNoQuestion("중일정을 Pronet 계획일로 업데이트합니다.") == true)
                    {
                        if(dataModel != null && dataModel.ActList != null)
                        {
                            ActGridControl.ValueEndEdit();
                            LinkGridControl.ValueEndEdit();

                            if (string.IsNullOrEmpty(res) == true)
                            {
                                var dlg = new CaseSaveDialog(this.messageService, this.shipCaseList, GetSelectedCase(SelCaseNo));
                                if (dlg.ShowDialog() == true)
                                {
                                    confirmAfterSave = dlg.DoConfirm;
                                    if (dlg.SelOld == true && dlg.SelCase.CASE_NO == this.SelCaseNo)
                                    {
                                        // 조회한 리비전에 저장
                                        saveAsRevDes = dlg.Description;
                                        workType = WorkType.Save;
                                        base.RunBackgroundWorker();

                                        //PRONET 일정 저장 기능 추가(20200528 주원)
                                        if (dataModel != null && dataModel.ActList != null && dataModel.LinkList != null)
                                        {
                                            //선각 --> TSEA002 
                                            var nStgActDt = dataModel.GetSaveDataTableNStgHullAct();

                                            // 족장(N41) -> TSFA001
                                            var n91ActDt = dataModel.GetSaveDataTableN91Act();
                                            if (nStgActDt != null)
                                                AftMidPlanDataService.UpdateTSFA001_PRONET_DT(nStgActDt);

                                            // 후행
                                            var aftActDt = dataModel.GetSaveDataTableAftAct();
                                            if (aftActDt != null) { }
                                            AftMidPlanDataService.UpdateTSFN101_PRONET_DT(aftActDt);

                                            Load();
                                            //LoadData();
                                        }

                                    }
                                    else if (dlg.SelOld == true && dlg.SelCase.CASE_NO != this.SelCaseNo)
                                    {
                                        // 다른 리비전에 저장
                                        saveAsCaseNo = dlg.SelCase.CASE_NO;
                                        saveAsRevDes = dlg.Description;
                                        workType = WorkType.SaveAs;
                                        base.RunBackgroundWorker();

                                        //PRONET 일정 저장 기능 추가(20200528 주원)
                                        if (dataModel != null && dataModel.ActList != null && dataModel.LinkList != null)
                                        {
                                            //선각 --> TSEA002 
                                            var nStgActDt = dataModel.GetSaveDataTableNStgHullAct();

                                            // 족장(N41) -> TSFA001
                                            var n91ActDt = dataModel.GetSaveDataTableN91Act();
                                            if (nStgActDt != null)
                                                AftMidPlanDataService.UpdateTSFA001_PRONET_DT(nStgActDt);

                                            // 후행
                                            var aftActDt = dataModel.GetSaveDataTableAftAct();
                                            if (aftActDt != null) { }

                                            //변경된 리비전을 데이터로 가져온다.
                                            for (int i = 0; i < aftActDt.Rows.Count; i++)
                                            {
                                                aftActDt.Rows[0]["CASE_NO"] = saveAsCaseNo;
                                            }
                                            AftMidPlanDataService.UpdateTSFN101_PRONET_DT(aftActDt);

                                            InitCaseList();

                                            LoadData();
                                            Load();
                                        }

                                    }
                                    else if (dlg.SelOld == false)
                                    {
                                        // 신규 리비전 생성
                                        saveAsCaseNo = AftMidPlanDataService.ExecuteFC_AMP_GET_NEXT_CASE_NO(SelFigShp);
                                        //ExecuteFC_PRONET_NEW(string FIG_SHP, string CASE_NO)
                                        if (string.IsNullOrEmpty(saveAsCaseNo) == true)
                                            messageService.ShowWarning("신규 리비전 번호를 생성할 수 없습니다.");
                                        saveAsRevDes = dlg.Description;
                                        workType = WorkType.SaveAs;
                                        base.RunBackgroundWorker();

                                        //기존 리비전 번호 데이터로 신규 리비전 만들고
                                        string oldCaseNo = this.SelCaseNo;
                                        var newCaseNo = AftMidPlanDataService.ExecuteFC_PRONET_NEW(SelFigShp, oldCaseNo);

                                        //PRONET 일정 저장 기능 추가(20200528 주원)
                                        if (dataModel != null && dataModel.ActList != null && dataModel.LinkList != null)
                                        {
                                            //선각 --> TSEA002 
                                            var nStgActDt = dataModel.GetSaveDataTableNStgHullAct();

                                            // 족장(N41) -> TSFA001
                                            var n91ActDt = dataModel.GetSaveDataTableN91Act();

                                            // 후행
                                            var aftActDt = dataModel.GetSaveDataTableAftAct();
                                            if (aftActDt != null) { }

                                            //변경된 리비전을 데이터로 가져온다.
                                            for (int i = 0; i < aftActDt.Rows.Count; i++)
                                            {
                                                aftActDt.Rows[0]["CASE_NO"] = newCaseNo;
                                            }
                                            AftMidPlanDataService.UpdateTSFN101_PRONET_DT(aftActDt);

                                            InitCaseList();

                                            LoadData();
                                            Load();
                                        }

                                    }
                                }
                            }
                            else
                            {
                                messageService.ShowWarning(res);
                            }
                        }

                        //workType = WorkType.SavePronet;
                        //base.RunBackgroundWorker();
                    }
                }
                else
                {
                    messageService.ShowWarning(res);
                }
            }
        }

        #endregion

        #region 엑셀출력/인쇄
        private void bExcel_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
        {
            if (DockLayoutManager.ActiveDockItem == GanttPanel)
                ExportGanttToExcel();
            else if (DockLayoutManager.ActiveDockItem == ActPanel)
                ActGridControl.ExportToExcel();
            else if (DockLayoutManager.ActiveDockItem == LinkPanel)
                LinkGridControl.ExportToExcel();
        }

        private void bPrint_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
        {
            if (DockLayoutManager.ActiveDockItem == ActPanel)
                ActGridControl.ShowPrintPreview(this);
            else if (DockLayoutManager.ActiveDockItem == LinkPanel)
                LinkGridControl.ShowPrintPreview(this);
        }
        #endregion

        #region method

        #endregion

        #region 컨트롤 이벤트 - ZOOM

        private void eZoomFactor_PreviewKeyDown(object sender, RoutedEventArgs e)
        {
            if (dataModel != null)
            {
                if (Keyboard.IsKeyDown(Key.Enter) == true)
                {
                    if (_ZoomFactorPercent >= 40 && _ZoomFactorPercent <= 200)
                    {
                        ZoomFactor = _ZoomFactorPercent / 100.0;
                    }
                }
            }
        }

        #endregion


        #region 간트 표시

        private bool showWrkPntCnstLink = true;

        private void ShowGantt()
        {
            if (dataModel != null)
            {
                BeginWaitCursor();
                var selCase = GetSelectedCase(SelCaseNo);

                //기초 데이터 생성
                //Gantt Node , Link, Calendar 생성
                List<INode> nodeList = new List<INode>();
                List<DataTSFN101> actList = new List<DataTSFN101>();

                //제외 블록
                List<string> exceptList = GetExceptBlockList();

                //필터가 적용되면 대상 act 만 조회
                if (ApplyFilter == false)
                {
                    // 필터적용안함
                    actList = dataModel.GetGanttActList(null, null, exceptList, Options.ShowAllErecBlock, Options.ShowProNetPlan, Options.ShowProNetResult, SelGanttViewMode);
                }
                else
                {
                    // 필터적용
                    var filteredActList = GetFilteredActList();
                    actList = dataModel.GetGanttActList(null, filteredActList, exceptList, Options.ShowAllErecBlock, Options.ShowProNetPlan, Options.ShowProNetResult, SelGanttViewMode);
                }
                nodeList.AddRange(actList);


                var cnstNodeList = dataModel.GetGanttWrkPntCnstList(actList);
                nodeList.AddRange(cnstNodeList);

                List<ILink> linkList = new List<ILink>();
                linkList = dataModel.GetGanttLinkList(actList);
                if (showWrkPntCnstLink == true)
                {
                    var cnstLinkList = dataModel.GetGanttWrkPntCnstLinkList(cnstNodeList);
                    linkList.AddRange(cnstLinkList);
                }

                //간트 데이터 소스 셋팅
                smartGantt.BeginInit();
                //노드 바인딩
                smartGantt.NodeItemSource = nodeList;
                //관계 바인딩
                smartGantt.LinkItemSource = linkList;
                //절점 바인팅
                smartGantt.WorkPointItemSource = dataModel.GetWrkPntList();
                //칼렌더 바인딩
                smartGantt.CalendarItemSource = dataModel.GetGanttCalendar(selCase, actList);

                smartGantt.EndInit();

                //smartGantt.HideAllLink();

                EndWaitCursor();
            }
        }

        private List<string> GetExceptBlockList()
        {
            List<string> blkList = new List<string>();
            foreach (var item in ExceptBlockList)
            {
                blkList.Add(item.STD_DES_SHO + "|" + item.STD_DES_LON);
            }
            return blkList;
        }

        private List<DataTSFN101> GetFilteredActList()
        {
            List<DataTSFN101> list = new List<DataTSFN101>();

            for (int i = 0; i < ActGridControl.VisibleRowCount; ++i)
            {
                var rowHandle = ActGridControl.GetRowHandleByVisibleIndex(i);
                var act = ActGridControl.GetRow(rowHandle);
                if (act != null)
                    list.Add(act as DataTSFN101);
            }

            return list;
        }

        private void BeginWaitCursor()
        {
            Mouse.SetCursor(Cursors.Wait);
        }

        private void EndWaitCursor()
        {
            Mouse.SetCursor(Cursors.Arrow);
        }

        #endregion

        #region 보기 옵션

        private void bViewOptionSubmit_Click(object sender, RoutedEventArgs e)
        {
            SaveGanttViewOption();
            viewOptionPopup.ClosePopup();
            ShowGantt();
        }

        private void bViewOptionCancel_Click(object sender, RoutedEventArgs e)
        {
            LoadGanttViewOption();  // 바뀐 설정 되돌리기
            viewOptionPopup.ClosePopup();
        }

        #endregion


        #region 간트 이벤트, 그리드 이벤트


        private void ActGridControl_SelectionChanged(object sender, GridSelectionChangedEventArgs e)
        {
            //List<AmiNodeBase> list = new List<AmiNodeBase>();
            //if (ActGridControl.SelectedItems != null && ActGridControl.SelectedItems.Count > 0)
            //{
            //    foreach (DataTSFN101 act in ActGridControl.SelectedItems)
            //    {
            //        var node = smartGantt.GetNode(act.ID);
            //        if (node != null)
            //            list.Add(node);
            //    }
            //}
            //smartGantt.SelectNodeList = list;
        }
        
        private void smartGantt_SelectedNodeChanged(object sender, Component.Presentation.AmiSmartGantt.SelNodeArgs arg)
        {
            if (smartGantt.SelectNode != null && smartGantt.SelectNode.Source != null)
            {
                SelActItem = (DataTSFN101)smartGantt.SelectNode.Source;
            }

            //if (smartGantt.SelectNodeList != null && smartGantt.SelectNodeList.Count > 0)
            //{
            //    List<DataTSFN101> list = smartGantt.SelectNodeList.Select(e => e.Source).Cast<DataTSFN101>().ToList();
            //    ActGridControl.SelectedItems = list;
            //}
        }

        private void smartGantt_ChangingNodeProperty(object sender, Component.Presentation.AmiSmartGantt.ChangingNodePropertyArgs args)
        {
            if (dataModel != null && SelActItem != null)
            {
                // 일정 변경 가능 체크 (고정, 자재발주, 실적발생 등)
                if (SelActItem.FIXED_YN == "Y")
                {
                    args.IsCancel = true;
                    return;
                }

                // 선각 ACT
                if (Defs.IsHullAct(SelActItem.WRK_STG) == true)
                {
                    // 건조
                    // 건조 공정은 N41의 완료일을 N4B의 완료일로 맞춤
                    if (SelActItem.WRK_STG == Defs.WRK_STG_N)
                    {
                        if (SelActItem.WRK_TYP == "41")
                        {
                            // N41
                            if (args.UpdateType == NodeUpdateType.StartChanged)
                            {
                                // 탑재 N41 은 착수 변경 불가
                                args.IsCancel = true;
                                return;
                            }
                        }
                    }
                }
            }
        }

        private void smartGantt_NodeComplete(object sender, Component.Presentation.AmiSmartGantt.NodeCompleteArgs arg)
        {
            if (dataModel != null && SelActItem != null)
            {
                var act = SelActItem;
                
                // ACT 공기
                dataModel.CalcDuration(act);
                // ACT 간트 업데이트
                smartGantt.ChangeNodeProperty(act);

                // ACT 전후 관계의 SLACK 업데이트
                dataModel.CalcSlack(act);
                // 링크에 옵셋 표시 옵션이 켜진경우 간트 링크 업데이트
                smartGantt.ChangeLinkListProperty(act.PreLinks, true);
                smartGantt.ChangeLinkListProperty(act.AftLinks, true);

                // 절점제약위배 시 관련 링크 업데이트
                if (act.WrkPntConstraints != null && act.WrkPntConstraints.Count > 0)
                    UpdateWrkPntCnstLink(act);

                // 일정 변경 후처리
                Postprocess(act, arg.UpdateType);
            }
        }

        private void UpdateWrkPntCnstLink(List<DataTSFN101> actList)
        {
            // 절점제약위배 시 관련 링크 업데이트
            if (actList != null && actList.Count > 0)
            {
                foreach (var act in actList)
                    UpdateWrkPntCnstLink(act);
            }
        }

        private void UpdateWrkPntCnstLink(DataTSFN101 act)
        {
            // 절점제약위배 시 관련 링크 업데이트
            if (act.WrkPntConstraints != null && act.WrkPntConstraints.Count > 0)
            {
                var wrkPntCnstLinks = dataModel.GetWrkPntCnstLink(act);
                if (wrkPntCnstLinks != null && wrkPntCnstLinks.Count > 0)
                {
                    foreach (var link in wrkPntCnstLinks)
                    {
                        var smartLink = smartGantt.GetLink(link.ID);
                        if (smartLink != null)
                            smartLink.ForceUpdateLinkUIData();
                    }
                }
            }
        }

        // ACT 일정 조정 후처리
        private void Postprocess(DataTSFN101 act, NodeUpdateType updateType)
        {
            if (act != null)
            {
                if (act.WRK_STG == Defs.WRK_STG_N)
                {
                    // N41의 완료일은 MAX (N4A완료일, N4B 완료일)
                    if (act.WRK_TYP == "41")
                    {
                        // 탑재ACT의 공기(완료일)를 변경하면
                        if (updateType == NodeUpdateType.FinishChanged)
                        {
                            // 각 구획에 흩어진 블록 ACT의 일정 맞춤
                            var changedDivList = dataModel.SyncNStageActDates(act.ITM_COD, act.DIV_COD);


                            // F/W 옵션이 켜진 경우, 변경된 구획 모두 F/W 스케줄
                            // N 공정이 변경된 경우는 해당 구획 모두 F/W 스케줄 수행
                            if (Options.DoForwardSchedule == true && changedDivList != null && changedDivList.Count > 0)
                            {
                                foreach (var divCod in changedDivList)
                                    ForwardScheduleDiv(divCod);
                            }
                        }
                    }
                    else
                    {
                        // N41의 완료일 맞춤
                        var nStageActs = dataModel.MakeN41StageFinishDates(act.ITM_COD, act.DIV_COD);
                        smartGantt.ChangeNodeListProperty(nStageActs);

                        // 각 구획에 흩어진 블록 ACT의 일정 맞춤
                        var changedDivList = dataModel.SyncNStageActDates(act.ITM_COD, act.DIV_COD);

                        // F/W 옵션이 켜진 경우, 변경된 구획 모두 F/W 스케줄
                        // N 공정이 변경된 경우는 해당 구획 모두 F/W 스케줄 수행
                        if (Options.DoForwardSchedule == true && changedDivList != null && changedDivList.Count > 0)
                        {
                            foreach (var divCod in changedDivList)
                                ForwardScheduleDiv(divCod);
                        }
                    }
                }
                else
                {
                    // 그 외 공정, F/W 옵션이 켜진경우 F/W 수행
                    if (Options.DoForwardSchedule == true)
                    {
                        switch (updateType)
                        {
                            case NodeUpdateType.Moved:
                            case NodeUpdateType.StartChanged:
                            case NodeUpdateType.FinishChanged:
                                // 이동, 착수/완료일변경, 해당 Act 기준으로 F/W
                                ForwardScheduleAct(act);
                                break;
                            case NodeUpdateType.GroupChanged:
                            case NodeUpdateType.DateChanged:
                            default:
                                break;
                        }
                    }
                }
            }
        }

        private void smartGantt_ASGKeyDown(object sender, KeyEventArgs args)
        {
            if (smartGantt != null && dataModel != null)
            {
                if (args.Key == Key.Delete)
                {
                    // 선택한 링크 삭제
                    if (smartGantt.SelectLink != null && smartGantt.SelectLink.Source != null)
                    {
                        var link = smartGantt.SelectLink.Source as DataTSFN102;
                        smartGantt.RemoveLink(link);
                        dataModel.DelLink(link);
                    }
                    return;
                }

                // Ctrl + 1, 2, 3, 4 관계 생성 또는 타입 변경
                if (Keyboard.Modifiers == ModifierKeys.Alt)
                {
                    if (smartGantt.SelectLink != null)
                    {
                        if (Keyboard.IsKeyDown(Key.D1) == true)
                            ChangeSelectedLinkLineSnapPos(LinkUIType.StartNoMargin);
                        else if (Keyboard.IsKeyDown(Key.D2) == true)
                            ChangeSelectedLinkLineSnapPos(LinkUIType.Normal);
                        else if (Keyboard.IsKeyDown(Key.D3) == true)
                            ChangeSelectedLinkLineSnapPos(LinkUIType.FinishNoMargin);
                    }
                }
                else if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    if (smartGantt.SelectLink != null)
                    {
                        if (Keyboard.IsKeyDown(Key.D1) == true)
                            ChangeSelectedLinkType(LinkTypeDef.FS);
                        else if (Keyboard.IsKeyDown(Key.D2) == true)
                            ChangeSelectedLinkType(LinkTypeDef.SS);
                        else if (Keyboard.IsKeyDown(Key.D3) == true)
                            ChangeSelectedLinkType(LinkTypeDef.FF);
                        else if (Keyboard.IsKeyDown(Key.D4) == true)
                            ChangeSelectedLinkType(LinkTypeDef.SF);
                    }
                    else if (smartGantt.SelectNodeList != null && smartGantt.SelectNodeList.Count == 2)
                    {
                        if (Keyboard.IsKeyDown(Key.D1) == true)
                            CreateLink(LinkTypeDef.FS);
                        else if (Keyboard.IsKeyDown(Key.D2) == true)
                            CreateLink(LinkTypeDef.SS);
                        else if (Keyboard.IsKeyDown(Key.D3) == true)
                            CreateLink(LinkTypeDef.FF);
                        else if (Keyboard.IsKeyDown(Key.D4) == true)
                            CreateLink(LinkTypeDef.SF);
                    }
                }
            }
        }

        private void smartGantt_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (dataModel != null && Keyboard.Modifiers == ModifierKeys.Control)
            {
                var delta = (e.Delta > 0) ? 0.2 : -0.2;
                var scale = _ZoomFactor + delta;

                if (scale < 0.4) scale = 0.4;
                else if (scale > 2) scale = 2;

                ZoomFactor = scale;
            }
        }

        private void smartGantt_CreatingLink(object sender, Component.Presentation.AmiSmartGantt.CreatingLinkEventArgs args)
        {
            // 링크 생성 불가 확인
            if (args.Predecessor != null && args.Successor != null)
            {
                var preAct = args.Predecessor as DataTSFN101;
                var aftAct = args.Successor as DataTSFN101;

                // 자기 자신은 연결 불가
                if (preAct == aftAct)
                {
                    messageService.ShowWarning("선후행 ACT가 동일한 관계는 생성할 수 없습니다.");
                    args.IsCancel = true;
                    return;
                }

                // 기존 관계가 있으면 취소, 반대방향 연결도 불가
                if (dataModel.GetLink(preAct, aftAct) != null || dataModel.GetLink(aftAct, preAct) != null)
                {
                    string msg = string.Format("{0} 과 {1} 는 관계가 이미 있습니다.\n관계 삭제 후 시도하세요."
                        , preAct.ACT_DES, aftAct.ACT_DES);
                    messageService.ShowWarning(msg);
                    args.IsCancel = true;
                    return;
                }
            }
        }

        private void smartGantt_CreatedLink(object sender, Component.Presentation.AmiSmartGantt.CreatedLinkEventArgs args)
        {
            if (dataModel != null && args.Predecessor != null && args.Successor != null)
            {
                var preAct = args.Predecessor as DataTSFN101;
                var aftAct = args.Successor as DataTSFN101;

                var link = dataModel.AddLink(preAct, aftAct, args.LinkType);
                if (link != null)
                {
                    args.Link.Source = link;
                    args.Link.DashType = LineDashType.Solid;
                    //link.OFF_SET = offset
                    smartGantt.ChangeLinkProperty(link);
                }
                else
                {
                    messageService.ShowWarning("링크 생성 불가");
                }
            }
        }

        private void smartGantt_NodeMouseDoubleClick(object sender, Component.Presentation.AmiSmartGantt.MousePositionArgs arg)
        {
            if (SelActItem != null)
            {
                var act = SelActItem;
                var orgTrm = act.STD_TRM;
                var orgPlnTrm = act.PLN_TRM;
                var orgActCod = act.ACT_COD;
                var selCase = GetSelectedCase(SelCaseNo);
                var dlg = new AMPActInfoDialog(this.MasterDataService, this.AftMidPlanDataService, this.messageService
                    , this.dataModel, SelActItem, selCase, false);
                if (dlg.ShowDialog() == true)
                {
                    if (act.Row.RowState != DataRowState.Added && orgActCod != act.ACT_COD)
                    {
                        // 기존항목의 ACT 토드가 변경되면, 재생성 필요
                        DataTSFN101 newAct = dataModel.DelInsAct(act);

                        // 공기가 변경됐다면 완료일 재계산
                        if (orgTrm != act.STD_TRM)
                        {
                            newAct.PlanDuration = act.STD_TRM.Value;
                            newAct.UpdatePlanFinish();
                        }

                        ShowGantt();

                        var ganttBaseNode = smartGantt.GetNode(newAct.ID);
                        smartGantt.SelectNode = ganttBaseNode;

                        arg.Handled = true;
                    }
                    else
                    {
                        // 신규 추가 항목이 아닌경우
                        // ID 변경되면, 전/후 연결의 ID 정보 업데이트
                        if (orgActCod != act.ACT_COD)
                        {
                            dataModel.SyncLinkActCod(act);
                        }

                        // 공기가 변경됐다면 완료일 재계산
                        if (orgPlnTrm != act.PLN_TRM)
                        {
                            act.UpdatePlanFinish();

                            // 일정 변경 후처리
                            Postprocess(act, NodeUpdateType.FinishChanged);
                        }

                        var ganttBaseNode = smartGantt.GetNode(act.ID);
                        smartGantt.SelectNode = ganttBaseNode;
                        smartGantt.ChangeNodeProperty(act);

                        arg.Handled = true;
                    }
                }
            }
        }

        private void smartGantt_LinkMouseDoubleClick(object sender, Component.Presentation.AmiSmartGantt.MousePositionArgs arg)
        {
            ChangeSelectedLinkOffset();
        }


        #endregion


        #region ACT 추가/삭제


        private void mAddActBefore_Click(object sender, RoutedEventArgs e)
        {
            AddActBefore(SelActItem);
        }

        private void mAddActAfter_Click(object sender, RoutedEventArgs e)
        {
            AddActAfter(SelActItem);
        }

        private void bDelAct_Click(object sender, RoutedEventArgs e)
        {
            DeleteSelectedAct();
        }

        private void bAddBefore_ItemClick(object sender, ItemClickEventArgs e)
        {
            AddActBefore(SelActItem);
        }

        private void AddActBefore(DataTSFN101 selActItem)
        {
            if (dataModel != null && selActItem != null)
            {
                var selCase = GetSelectedCase(SelCaseNo);
                var tempItem = dataModel.CreateTempAct(selActItem);
                var dlg = new AMPActInfoDialog(this.MasterDataService, this.AftMidPlanDataService, this.messageService
                    , this.dataModel, tempItem, selCase, true);
                if (dlg.ShowDialog() == true)
                {
                    // 모델에 추가
                    dataModel.AddActBefore(selActItem, tempItem);

                    // 간트에 추가, 변경된 구획(그룹)만 업데이트 하도록 수정 필요
                    ShowGantt();
                }
            }
        }

        private void bAddAfter_ItemClick(object sender, ItemClickEventArgs e)
        {
            AddActAfter(SelActItem);
        }

        private void AddActAfter(DataTSFN101 selActItem)
        {
            if (dataModel != null && selActItem != null)
            {
                var selCase = GetSelectedCase(SelCaseNo);
                var tempItem = dataModel.CreateTempAct(selActItem);
                var dlg = new AMPActInfoDialog(this.MasterDataService, this.AftMidPlanDataService, this.messageService
                    , this.dataModel, tempItem, selCase, true);
                if (dlg.ShowDialog() == true)
                {
                    // 모델에 추가
                    dataModel.AddActAfter(selActItem, tempItem);

                    // 간트에 추가, 변경된 구획(그룹)만 업데이트 하도록 수정 필요
                    ShowGantt();
                }
            }
        }


        private void bDelete_ItemClick(object sender, ItemClickEventArgs e)
        {
            DeleteSelectedAct();
        }

        private void DeleteSelectedAct()
        {
            if (dataModel != null && SelActItem != null)
            {
                // 건조 ACT는 삭제불가
                if (SelActItem.WRK_STG == Defs.WRK_STG_N)
                {
                    messageService.ShowWarning("건조 ACT는 삭제할 수 없습니다.");
                    return;
                }

                if (ERP_DEL_ACT == true && ErpConnectService.DelYnActZ_PS_C_IF038_NCO(SelActItem.FIG_SHP, SelActItem.ACT_COD, ERP_QA_SYS) == false)
                {
                    // ERP에서 삭제 불가
                    messageService.ShowWarning(string.Format("{0} ACT는 ERP에서 삭제할 수 없습니다.", SelActItem.ACT_COD));
                    return;
                }

                var msg = string.Format("ACT {0}({1})를 삭제합니다.", SelActItem.ACT_COD, SelActItem.ACT_DES);
                if (messageService.ShowYesNoQuestion(msg) == true)
                {
                    DeleteAct(SelActItem);

                    ShowGantt();
                }
            }
            else
            {
                messageService.ShowWarning("삭제할 ACT를 선택하세요.");
            }
        }

        private void DeleteAct(DataTSFN101 act)
        {
            if (act != null)
            {
                AddDelActList(act);

                // 간트에서 ACT 및 연결된 링크 삭제
                DeleteActFromGantt(SelActItem);

                // 모델에서 ACT 및 연결된 Link 삭제
                dataModel.DelAct(SelActItem);

                SelActItem = null;
            }
        }

        private void AddDelActList(DataTSFN101 act)
        {
            if (delActList == null)
                delActList = new List<DataTSFN101>();
            if (act != null)
                delActList.Add(act);
        }

        private void AddCntractedActList(DataTSFN101 act)
        {
            if (cntractedActs == null)
                cntractedActs = new DataTSFN101List();
            if (act != null)
                cntractedActs.Add(act);
        }

        private void DeleteActFromGantt(DataTSFN101 act)
        {
            if (act != null)
            {
                // 간트 링크 삭제
                if (act.PreLinks != null)
                {
                    foreach (var link in act.PreLinks)
                        smartGantt.RemoveLink(link);
                }
                if (act.AftLinks != null)
                {
                    foreach (var link in act.AftLinks)
                        smartGantt.RemoveLink(link);
                }

                // 간트 노드 삭제
                smartGantt.RemoveNode(act);
            }
        }

        // 구획삭제
        private void mDeleteDiv_Click(object sender, RoutedEventArgs e)
        {
            var divCod = GetSelectedGanttGroupDivCod();
            //19.08.13 신성훈 소구획 삭제하도록 수정 소구획 값 가져오는 로직 추가.
            var divCod2 = GetSelectedGanttGroupDivCod2();

            if (string.IsNullOrEmpty(divCod) == false)
            {
                //19.08.14 신성훈 중구획클릭삭제하면 구획전체 삭제 소구획클릭삭제하면 소구획삭제 추가
                if (divCod2 == "")
                {
                    if (messageService.ShowYesNoQuestion(string.Format("선택한 구획 {0}를 삭제합니다.", divCod)) == true)
                    {
                        // 구획 내 ACT 삭제 가능한지 체크
                        if (ERP_DEL_ACT == true)
                        {
                            //19.08.13 신성훈 소구획 삭제하도록 수정 소구획 값 가져오는 로직 추가.
                            var divActList = dataModel.GetDivActList(divCod);
                            //var divActList = dataModel.GetDivActList(divCod, divCod2);

                            if (divActList != null && divActList.Count > 0)
                            {
                                foreach (var act in divActList)
                                {
                                    if (ErpConnectService.DelYnActZ_PS_C_IF038_NCO(act.FIG_SHP, act.ACT_COD, ERP_QA_SYS) == false)
                                    {
                                        // ERP에서 삭제 불가
                                        messageService.ShowWarning(string.Format("구획 {0}의 {1} ACT는 ERP에서 삭제할 수 없습니다.", divCod, act.ACT_COD));
                                        return;
                                    }
                                }
                            }
                        }

                        //19.08.13 신성훈 소구획 삭제하도록 수정 소구획 값 가져오는 로직 추가.
                        dataModel.DelDiv(divCod);
                        //dataModel.DelDiv(divCod, divCod2);
                        ShowGantt();
                    }
                }
                else
                {
                    if (messageService.ShowYesNoQuestion(string.Format("선택한 구획 {0}의 소구획 {1}를 삭제합니다.", divCod, divCod2)) == true)
                    {
                        // 구획 내 ACT 삭제 가능한지 체크
                        if (ERP_DEL_ACT == true)
                        {
                            //19.08.13 신성훈 소구획 삭제하도록 수정 소구획 값 가져오는 로직 추가.
                            //var divActList = dataModel.GetDivActList(divCod);
                            var divActList = dataModel.GetDivActList(divCod, divCod2);

                            if (divActList != null && divActList.Count > 0)
                            {
                                foreach (var act in divActList)
                                {
                                    if (ErpConnectService.DelYnActZ_PS_C_IF038_NCO(act.FIG_SHP, act.ACT_COD, ERP_QA_SYS) == false)
                                    {
                                        // ERP에서 삭제 불가
                                        messageService.ShowWarning(string.Format("구획 {0}의 {1} ACT는 ERP에서 삭제할 수 없습니다.", divCod, act.ACT_COD));
                                        return;
                                    }
                                }
                            }
                        }

                        //19.08.13 신성훈 소구획 삭제하도록 수정 소구획 값 가져오는 로직 추가.
                        //dataModel.DelDiv(divCod);
                        dataModel.DelDiv(divCod, divCod2);
                        ShowGantt();
                    }
                }
                
            }
        }


        #endregion

        #region 링크 관리

        private void eLinkEditMode_EditValueChanged(object sender, RoutedEventArgs e)
        {
            if (dataModel != null && smartGantt != null)
            {
                if (LinkEditMode == true)
                    smartGantt.DiagramAction = Component.Presentation.AmiSmartGantt.DiagramActionType.Link;
                else
                    smartGantt.DiagramAction = Component.Presentation.AmiSmartGantt.DiagramActionType.Node;
            }
        }

        private void bCreateLink_ItemClick(object sender, ItemClickEventArgs e)
        {
            CreateLink(LinkTypeDef.FS);
        }

        private DataTSFN102 CreateLink(LinkTypeDef linkType)
        {
            if (dataModel != null && smartGantt != null)
            {
                if (smartGantt.SelectNodeList != null && smartGantt.SelectNodeList.Count == 2)
                {
                    // 선택한 두 노드간 링크 생성
                    var preAct = smartGantt.SelectNodeList[0].Source as DataTSFN101;
                    var aftAct = smartGantt.SelectNodeList[1].Source as DataTSFN101;

                    return CreateLink(preAct, aftAct, linkType);
                }
                else
                {
                    messageService.ShowWarning("링크를 생성할 두 ACT를 선택하세요.");
                }
            }
            return null;
        }

        private DataTSFN102 CreateLink(DataTSFN101 preAct, DataTSFN101 aftAct, LinkTypeDef linkType, int? offset = null)
        {
            if (dataModel != null && preAct != null && aftAct != null)
            {
                var link = dataModel.GetLink(preAct, aftAct);
                if (link != null)
                {
                    // 기존에 만들어진 관계가 있으면 타입만 변경
                    if (link.LinkType != linkType)
                    {
                        link.LinkType = linkType;
                        smartGantt.ChangeLinkProperty(link);
                    }
                }
                else
                {
                    var revLink = dataModel.GetLink(aftAct, preAct);
                    if (revLink != null)
                    {
                        // 반대로 연결된 ACT가 있으면 삭제 후 신규 추가
                        dataModel.DelLink(revLink);
                        smartGantt.RemoveLink(link);
                    }

                    // 모델에 추가
                    link = dataModel.AddLink(preAct, aftAct, linkType);
                    // 간트차트에 추가
                    smartGantt.AddLink(link);

                    if (offset.HasValue)
                    {
                        link.OFF_SET = offset.Value;
                    }

                    // 링크의 옵셋을 0으로 설정하고 F/W 스케줄 수행
                    // 표준과 동일하게 처리할지 확인 필요

                }
            }
            return null;
        }

        private void bCreateAllLink_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (dataModel != null && smartGantt.SelectNodeList != null && smartGantt.SelectNodeList.Count > 1)
            {
                // 선택한 순서대로
                var orderedActList = smartGantt.SelectNodeList.Select(x => x.Source as DataTSFN101).ToList();
                for (int i = 0; i < (orderedActList.Count - 1); ++i)
                {
                    var preAct = orderedActList[i];
                    var aftAct = orderedActList[i + 1];
                    CreateLink(preAct, aftAct, LinkTypeDef.FS);
                }
            }
        }

        private void bDeleteLink_ItemClick(object sender, ItemClickEventArgs e)
        {
            DeleteSelectedLink();
        }

        private void DeleteSelectedLink()
        {
            if (dataModel != null && smartGantt != null
                && smartGantt.SelectLink != null && smartGantt.SelectLink.Source != null)
            {
                if (smartGantt.SelectLink.Source is DataTSFN102)
                {
                    var link = smartGantt.SelectLink.Source as DataTSFN102;
                    DeleteLink(link);
                }
                else if (smartGantt.SelectLink.Source is WrkPntCnstLink)
                {
                    var link = smartGantt.SelectLink.Source as WrkPntCnstLink;
                    DeletePntConst(link.WrkPntCnst);
                }
            }
        }

        private void DeleteLink(DataTSFN102 link)
        {
            if (dataModel != null && link != null && smartGantt != null)
            {
                dataModel.DelLink(link);
                smartGantt.RemoveLink(link);
            }
        }

        private void mSnap_S_Click(object sender, RoutedEventArgs e)
        {
            ChangeSelectedLinkLineSnapPos(LinkUIType.StartNoMargin);
        }

        private void mSnap_M_Click(object sender, RoutedEventArgs e)
        {
            ChangeSelectedLinkLineSnapPos(LinkUIType.Normal);
        }

        private void mSnap_F_Click(object sender, RoutedEventArgs e)
        {
            ChangeSelectedLinkLineSnapPos(LinkUIType.FinishNoMargin);
        }

        public void ChangeSelectedLinkLineSnapPos(LinkUIType type)
        {
            if (smartGantt != null && smartGantt.SelectLink != null && smartGantt.SelectLink.Source != null)
            {
                var link = (DataTSFN102)smartGantt.SelectLink.Source;
                link.LINE_SNAP_LOC = type.ToString();
                smartGantt.SelectLink.UiType = type;
            }
        }

        private void ChangeSelectedLinkType(LinkTypeDef linkType)
        {
            if (smartGantt.SelectLink != null)
            {
                var link = (DataTSFN102)smartGantt.SelectLink.Source;
                if (link != null)
                {
                    link.LinkType = linkType;
                    smartGantt.ChangeLinkProperty(link);
                }
            }
        }


        private void mChangeOffset_Click(object sender, RoutedEventArgs e)
        {
            ChangeSelectedLinkOffset();
        }

        private void ChangeSelectedLinkOffset()
        {
            // 선택한 링크의 계획옵셋 변경 창
            if (smartGantt.SelectLink != null && smartGantt.SelectLink.Source != null && smartGantt.SelectLink.Source is DataTSFN102)
            {
                var link = (DataTSFN102)smartGantt.SelectLink.Source;
                var slack = dataModel.CalcPlanOffset(link);
                var text = string.Format("{0}->{1} ({2})\n표준({3}), 계획({4}), 슬랙({5})", link.PreAct.ACT_DES, link.AftAct.ACT_DES, link.LinkType.ToString()
                    , link.STD_OST ?? 0, link.OFF_SET ?? 0, slack);
                var dlg = new AmiSimpleInputControl("계획 옵셋", text);
                dlg.AllowEnterToOk = true;
                dlg.AllowEscToCancel = true;
                dlg.SetDefaultInput(link.OFF_SET.ToString());
                dlg.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                if (dlg.ShowDialog() == true)
                {
                    var newOffset = AmiParse.IntRParse(dlg.Input);
                    if (newOffset.HasValue && link.OFF_SET != newOffset.Value)
                    {
                        link.OFF_SET = newOffset;
                        smartGantt.ChangeLinkProperty(link);

                        ForwardScheduleAct(link.PreAct);
                    }
                }
            }

        }


        private void mDelLink_Click(object sender, RoutedEventArgs e)
        {
            DeleteSelectedLink();
        }

        private void mRel_FS_Click(object sender, RoutedEventArgs e)
        {
            ChangeSelectedLinkType(LinkTypeDef.FS);
        }

        private void mRel_SS_Click(object sender, RoutedEventArgs e)
        {
            ChangeSelectedLinkType(LinkTypeDef.SS);
        }

        private void mRel_FF_Click(object sender, RoutedEventArgs e)
        {
            ChangeSelectedLinkType(LinkTypeDef.FF);
        }

        private void mRel_SF_Click(object sender, RoutedEventArgs e)
        {
            ChangeSelectedLinkType(LinkTypeDef.SF);
        }

        private void mColorLink_Click(object sender, DevExpress.Xpf.Editors.EditValueChangedEventArgs e)
        {
            if (dataModel != null && smartGantt != null && smartGantt.SelectLink != null)
            {
                var link = (DataTSFN102)smartGantt.SelectLink.Source;
                var color = (Color)e.NewValue;
                link.Stroke = new SolidColorBrush(color);
                smartGantt.ChangeLinkProperty(link);
                smartGantt.SelectLink.ForceUpdateLinkUIData();
            }
        }

        #endregion


        #region 관계표시/숨김


        private void mShowLinkAll_Click(object sender, RoutedEventArgs e)
        {
            if (smartGantt != null)
                smartGantt.ShowAllLink();
        }

        private void mHideLinkAll_Click(object sender, RoutedEventArgs e)
        {
            if (smartGantt != null)
                smartGantt.HideAllLink();
        }

        private void bShowActLink_Click(object sender, RoutedEventArgs e)
        {
            if (SelActItem != null)
            {
                smartGantt.ShowHideLinkOfAct(SelActItem.ID, true);
            }
        }

        private void bHideActLink_Click(object sender, RoutedEventArgs e)
        {
            if (SelActItem != null)
            {
                smartGantt.ShowHideLinkOfAct(SelActItem.ID, false);
            }
        }

        private void bShowDivLink_Click(object sender, RoutedEventArgs e)
        {
            var divCod = GetSelectedGanttGroupDivCod();
            var actList = dataModel.GetDivActList(divCod);
            if (actList != null && actList.Count > 0)
            {
                foreach (var act in actList)
                    smartGantt.ShowHideLinkOfAct(act.ID, true);
            }
        }

        private void bHideDivLink_Click(object sender, RoutedEventArgs e)
        {
            var divCod = GetSelectedGanttGroupDivCod();
            var actList = dataModel.GetDivActList(divCod);
            if (actList != null && actList.Count > 0)
            {
                foreach (var act in actList)
                    smartGantt.ShowHideLinkOfAct(act.ID, false);
            }
        }

        #endregion


        #region 구획 삭제

        private void bDeleteDiv_ItemClick(object sender, ItemClickEventArgs e)
        {
            DeleteDiv();
        }

        private void DeleteDiv()
        {
            var divCod = GetSelectedGanttGroupDivCod();
            //19.08.13 신성훈 소구획 삭제하도록 수정 소구획 값 가져오는 로직 추가.
            var divCod2 = GetSelectedGanttGroupDivCod2();

            if (string.IsNullOrEmpty(divCod) == false)
            {
                //var msg = string.Format("선택한 구획 {0}의 모든 ACT를 삭제합니다.", divCod);
                //19.08.13 신성훈 소구획 삭제하도록 수정.
                var msg = string.Format("선택한 구획 {0}의 {1} ACT를 삭제합니다.", divCod, divCod2);
                if (messageService.ShowYesNoQuestion(msg) == true)
                {
                    //19.08.13 신성훈 선택한 소구획만 삭제하도록 수정.
                    //dataModel.DelDiv(divCod);
                    dataModel.DelDiv(divCod, divCod2);
                    ShowGantt();
                }
            }
            else
            {
                messageService.ShowWarning("삭제할 구획을 선택하세요.");
            }
        }

        private string GetSelectedGanttGroupDivCod()
        {
            if (smartGantt.SelectGroup != null
                && smartGantt.SelectGroup.Count > 0 && smartGantt.SelectGroup[0].Source != null)
            {
                var node = smartGantt.SelectGroup[0];
                return (node.Source as DataTSFN101).DIV_COD;
            }
            if (SelActItem != null)
            {
                return SelActItem.DIV_COD;
            }
            return string.Empty;
        }

        //19.08.13 신성훈 소구획 삭제하도록 수정 소구획 값 가져오는 로직 추가.
        private string GetSelectedGanttGroupDivCod2()
        {
            if (smartGantt.SelectGroup != null
                && smartGantt.SelectGroup.Count > 0 && smartGantt.SelectGroup[0].Source != null)
            {
                var node = smartGantt.SelectGroup[0];
                return (node.Source as DataTSFN101).DIV_COD2;
            }
            if (SelActItem != null)
            {
                return SelActItem.DIV_COD2;
            }
            return string.Empty;
        }

        #endregion

        #region 표준구획 가져오기

        // 타겟 구획 지정한 경우
        //private void bImportDiv_ItemClick2(object sender, ItemClickEventArgs e)
        //{
        //    if (dataModel != null && dataModel.ActList != null)
        //    {
        //        var shipCase = GetSelectedCase(SelCaseNo);
        //        var dataModelStd = new Models.AMPActivityManagerDM();
        //        dataModelStd.GanttUtil = dataModel.GanttUtil;
        //        dataModelStd.StdDivSource = AftMidPlanDataService.GetTSFN001_SHP_KND_SHP_TYP_USE_YN_SELECTList(shipCase.SHP_KND, "*", "Y");
        //        var dlg = new ImportDivStdActDialog(dataModelStd, this.MasterDataService, this.AftMidPlanDataService, this.messageService);
        //        if (dlg.ShowDialog() == true
        //            && dlg.DivStdActMap != null && dlg.DivStdActMap.Count > 0)
        //        {
        //            var divCodList = dlg.DivStdActMap.Keys.Select(i => i["DIV_COD"].ToString()).ToList();
        //            var toDivCodList = dlg.DivStdActMap.Keys.Select(i => i["TO_DIV_COD"].ToString()).ToList();

        //            // 생성

        //            // 표준구획, ACT, 관계
        //            var stdDivList = AftMidPlanDataService.GetTSFN001_SHP_KND_SHP_TYP_USE_YN_SELECTList(shipCase.SHP_KND, "*", "Y");

        //            //var modelNo = Defs.MakeModelNo(shipCase.SHP_TYP, shipCase.DCK_COD);
        //            var modelNo = dlg.SelModelNo;
        //            var stdActList = AftMidPlanDataService.GetTSFN002_MODEL_NO_SELECTList(modelNo);
        //            if (stdActList == null || stdActList.Count == 0)
        //            {
        //                messageService.ShowWarning("표준 ACT[TSFN002]를 조회할 수 없습니다.");
        //                return;
        //            }

        //            var stdLinkList = AftMidPlanDataService.GetTSFN003_MODEL_NO_SELECTList(modelNo);
        //            if (stdLinkList == null || stdLinkList.Count == 0)
        //            {
        //                messageService.ShowWarning("표준 관계[TSFN003]를 조회할 수 없습니다.");
        //                return;
        //            }

        //            // 호선 구획별 블록 매핑
        //            var shipDivItmList = AftMidPlanDataService.GetTSFN021_MAKE_ACT_CASE_NO_FIG_SHP_SELECTList(SelCaseNo, SelFigShp);
        //            if (shipDivItmList == null || shipDivItmList.Count == 0)
        //            {
        //                messageService.ShowWarning("호선 구획블록매핑[TSFN021] 정보를 조회할 수 없습니다.");
        //                return;
        //            }

        //            // 호선 선각 ACT 목록
        //            var shipHullActList = AftMidPlanDataService.GetTSEA002_FIG_SHP_STG_N_ACT_SELECTList(SelFigShp);
        //            if (shipHullActList == null || shipHullActList.Count == 0)
        //            {
        //                messageService.ShowWarning("호선의 건조 ACT 목록을 조죄할 수 없습니다.");
        //                return;
        //            }

        //            // 구획별로
        //            var actCreator = new AftMidPlanCreator(this.MasterDataService, this.AftMidPlanDataService);
        //            for (int i = 0; i < divCodList.Count; ++i)
        //            {
        //                var divCod = divCodList[i];
        //                var toDivCod = toDivCodList[i];
        //                if (string.IsNullOrEmpty(toDivCod) == true)
        //                    toDivCod = divCod;

        //                // ACT/관계 생성
        //                if (actCreator.MakeShipAct_EX_N2(shipCase, stdDivList, stdActList, stdLinkList, shipDivItmList, shipHullActList, dataModel.WrkPntList, divCod, toDivCod, true, true, true) == false
        //                    || actCreator.ActList == null || actCreator.LinkList == null)
        //                {
        //                    messageService.ShowWarning("호선 ACT/관계를 생성할 수 없습니다.");
        //                    return;
        //                }

        //                // 기존 구획 데이터 삭제
        //                dataModel.DelDiv(toDivCod);

        //                // 생성된 ACT를 목록에 추가
        //                dataModel.Import(toDivCod, actCreator.ActList, actCreator.LinkList);
        //            }

        //            ShowGantt();
        //        }
        //    }
        //}

        private void bImportDiv_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (dataModel != null && dataModel.ActList != null)
            {
                var shipCase = GetSelectedCase(SelCaseNo);
                var dataModelStd = new Models.AMPActivityManagerDM();
                dataModelStd.GanttUtil = dataModel.GanttUtil;
                // 19.08.22 신성훈 가져오기 팝업 리스트 로직 변경.
                //dataModelStd.StdDivSource = AftMidPlanDataService.GetTSFN001_SHP_KND_SHP_TYP_USE_YN_SELECTList(shipCase.SHP_KND, "*", "Y");
                dataModelStd.StdDivSource = AftMidPlanDataService.GetTSFN001_TSFN021_SHP_KND_SHP_TYP_USE_YN_SELECTList(shipCase.CASE_NO, shipCase.FIG_SHP, shipCase.SHP_KND, "*", "Y", SelFigNo);
                var dlg = new ImportDivStdActDialog(dataModelStd, this.MasterDataService, this.AftMidPlanDataService, this.messageService);
                if (dlg.ShowDialog() == true
                    && dlg.DivStdActMap != null && dlg.DivStdActMap.Count > 0)
                {
                    var divCodList = dlg.DivStdActMap.Keys.Select(i => i["DIV_COD"].ToString()).ToList();

                    // 생성

                    // 표준구획, ACT, 관계
                    var stdDivList = dataModelStd.StdDivSource;

                    //var modelNo = Defs.MakeModelNo(shipCase.SHP_TYP, shipCase.DCK_COD);
                    var modelNo = dlg.SelModelNo;

                    var stdActList = AftMidPlanDataService.GetTSFN002_MODEL_NO_SELECTList(modelNo);
                    if (stdActList == null || stdActList.Count == 0)
                    {
                        messageService.ShowWarning("표준 ACT[TSFN002]를 조회할 수 없습니다.");
                        return;
                    }

                    var stdLinkList = AftMidPlanDataService.GetTSFN003_MODEL_NO_SELECTList(modelNo);
                    if (stdLinkList == null || stdLinkList.Count == 0)
                    {
                        messageService.ShowWarning("표준 관계[TSFN003]를 조회할 수 없습니다.");
                        return;
                    }

                    // 호선 구획별 블록 매핑
                    var shipDivItmList = AftMidPlanDataService.GetTSFN021_MAKE_ACT_CASE_NO_FIG_SHP_SELECTList(SelCaseNo, SelFigShp, SelFigNo);
                    if (shipDivItmList == null || shipDivItmList.Count == 0)
                    {
                        messageService.ShowWarning("호선 구획블록매핑[TSFN021] 정보를 조회할 수 없습니다.");
                        return;
                    }

                    // 호선 선각 ACT 목록, 족장(N91)은 TSFA001에서 조회
                    var shipHullActList = AftMidPlanDataService.GetTSEA002_FIG_SHP_STG_N_ACT_SELECTList(SelFigShp, SelFigNo);
                    if (shipHullActList == null || shipHullActList.Count == 0)
                    {
                        messageService.ShowWarning("호선의 건조 ACT 목록을 조죄할 수 없습니다.");
                        return;
                    }

                    // 절점
                    DataTSDC002List shipWrkPntList = null;
                    var selCase = GetSelectedCase(SelCaseNo);
                    if (string.IsNullOrEmpty(selCase.WRK_PNT_REV_NO) == true)
                    {
                        shipWrkPntList = AftMidPlanDataService.GetTSDC002_FIG_SHP_SELECTList(SelFigShp, SelFigNo);
                    }
                    else
                    {
                        // 선각인 경우만
                        if (IsModelShip == false)
                            shipWrkPntList = AftMidPlanDataService.GetTSDC002_REV_FIG_SHP_REV_NO_SELECTList(SelFigShp, selCase.WRK_PNT_REV_NO);
                    }

                    // 표준 절점 제약
                    var stdPntConstraints = AftMidPlanDataService.GetTSFN005_MODEL_NO_MAKE_SHIP_SELECTList(modelNo);

                    // 버퍼
                    // 19.08.14 신성훈 테스트
                    var stdBufList = AftMidPlanDataService.GetTSFN007_COMMON_SHP_KND_SHP_TYP_SELECTList(selCase.SHP_KND, "*");
                    //DataTSFN007List stdBufList = null;

                    // 구획별로
                    var actCreator = new AftMidPlanCreator(this.MasterDataService, this.AftMidPlanDataService);
                    // 전 후행 액트 리스트 전역변수화에 따른 작업 전 초기화 처리. 19.07.09 신성훈
                    actCreator.ClearStdToShipActMap();

                    foreach (var divCod in divCodList)
                    {
                        var tmpDivCodList = new List<string>() { divCod };

                        // ACT/관계 생성
                        // 구획 간 관계 생성을 위한 메소드 수정 및 호출 변경 19.07.09 신성훈
                        //if (actCreator.MakeShipAct_EX_N(shipCase, stdDivList, stdActList, stdLinkList, stdBufList, shipDivItmList, shipHullActList, shipWrkPntList, tmpDivCodList, true, true, true) == false 
                        if (actCreator.MakeShipAct_EX_N_DivList(shipCase, stdDivList, stdActList, stdLinkList, stdBufList, shipDivItmList, shipHullActList, shipWrkPntList, tmpDivCodList, divCodList, true, true, true) == false
                            || actCreator.ActList == null || actCreator.LinkList == null)
                        {
                            messageService.ShowWarning(string.Format("구획[{0}]의 ACT/관계를 생성할 수 없습니다.", divCod));
                            return;
                        }

                        // 호선 절점 생성
                        if (actCreator.MakeShipPntConstraints(shipCase, stdDivList, stdPntConstraints, stdBufList, shipWrkPntList, tmpDivCodList) == false)
                        {
                            messageService.ShowWarning(string.Format("구획 [{0}]의 절점제약을 생성할 수 없습니다.", divCod));
                            return;
                        }

                        // 기존 구획 데이터 삭제
                        dataModel.DelDiv(divCod);
                        
                        // 생성된 ACT를 목록에 추가
                        dataModel.Import(SelCaseNo, divCod, actCreator.ActList, actCreator.LinkList);
                        // 생성된 절점제약 목록에 추가
                        dataModel.ImportWrkPntConstraint(actCreator.ShipPntConstraints);
                    }
                   
                    ShowGantt();
                }
            }
        }

        #endregion
        
        #region 표준으로부터 생성 - 선각은 101에 추가하지 않음

        private string createFromStdModelNo;
        private DataTSFN101List createErrorActs;

        private void bCreateFromStd_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (string.IsNullOrEmpty(SelFigShp) == false && string.IsNullOrEmpty(SelCaseNo) == false)
            {
                //var msg = string.Format("{0} {1}의 ACT 및 관계를 표준으로부터 신규 생성합니다.", SelFigShp, SelCaseNo);
                //if (messageService.ShowYesNoQuestion(msg) == true)
                //{
                //    this.workType = WorkType.CreateFromStd;
                //    base.RunBackgroundWorker();
                //}
                var selCase = GetSelectedCase(SelCaseNo);
                var dlg = new SelectModelNoDialog(selCase.SHP_KND, selCase.SHP_TYP, selCase.DCK_COD, this.MasterDataService, this.AftMidPlanDataService, this.messageService, false);
                if (dlg.ShowDialog() == true && string.IsNullOrEmpty(dlg.SelModelNo) == false)
                {
                    this.createFromStdModelNo = dlg.SelModelNo;
                    this.workType = WorkType.CreateFromStd;
                    base.RunBackgroundWorker();
                }
            }
        }

        /// <summary>
        /// 표준에서 생성
        /// </summary>
        /// <returns></returns>
        private string CreateFromStd()
        {
            if (string.IsNullOrEmpty(SelFigShp) == false && string.IsNullOrEmpty(SelCaseNo) == false
                && string.IsNullOrEmpty(createFromStdModelNo) == false && shipCaseList != null)
            {
                /////////////////////////////////////////////////////////////////////////////////
                // ACT 및 관게 생성

                base.SetAppIsBusyStatus("호선 ACT 및 관계 생성 [1/2]");

                // 표준 데이터 및 호선 블록 구획 매핑 조회
                var shipCase = shipCaseList.FirstOrDefault(e => e.CASE_NO == SelCaseNo);
                if (shipCase == null)
                    return "호선의 리비전 정보가 없습니다.";

                // 표준구획, ACT, 관계
                // 19.08.12 신성훈 마스터 스케줄 ACT생성 시 적용구획 쿼리 수정
                //var stdDivList = AftMidPlanDataService.GetTSFN001_SHP_KND_SHP_TYP_USE_YN_SELECTList(shipCase.SHP_KND, "*", "Y");
                var stdDivList = AftMidPlanDataService.GetTSFN001_TSFN021_SHP_KND_SHP_TYP_USE_YN_SELECTList(shipCase.CASE_NO, shipCase.FIG_SHP, shipCase.SHP_KND, "*", "Y", SelFigNo);

                // 사용자가 선택한 모델에서 가져오도록 변경
                //var modelNo = Defs.MakeModelNo(shipCase.SHP_TYP, shipCase.DCK_COD);
                var stdActList = AftMidPlanDataService.GetTSFN002_MODEL_NO_USE_YN_SELECTList(createFromStdModelNo, "Y");
                if (stdActList == null || stdActList.Count == 0)
                    return "표준 ACT[TSFN002]를 조회할 수 없습니다.";

                var stdLinkList = AftMidPlanDataService.GetTSFN003_MODEL_NO_SELECTList(createFromStdModelNo);
                if (stdLinkList == null || stdLinkList.Count == 0)
                    return "표준 관계[TSFN003]를 조회할 수 없습니다.";

                // 호선 구획별 블록 매핑
                var shipDivItmList = AftMidPlanDataService.GetTSFN021_MAKE_ACT_CASE_NO_FIG_SHP_SELECTList(SelCaseNo, SelFigShp, SelFigNo);
                if (shipDivItmList == null || shipDivItmList.Count == 0)
                    return "호선 구획블록매핑[TSFN021] 정보를 조회할 수 없습니다.";

                // 호선 선각 ACT 목록
                var shipHullActList = AftMidPlanDataService.GetTSEA002_FIG_SHP_STG_N_ACT_SELECTList(SelFigShp, SelFigNo);
                if (shipHullActList == null || shipHullActList.Count == 0)
                    return "호선의 건조 ACT 목록을 조회할 수 없습니다.";

                // 호선 절점 목록
                DataTSDC002List shipWrkPntList = null;
                var selCase = GetSelectedCase(SelCaseNo);
                if (string.IsNullOrEmpty(selCase.WRK_PNT_REV_NO) == true)
                {
                    shipWrkPntList = AftMidPlanDataService.GetTSDC002_FIG_SHP_SELECTList(SelFigShp, SelFigNo);   // 별도 리비전이 없으면 운영
                }
                else
                {
                    // 선각인 경우만
                    if (IsModelShip == false)
                        shipWrkPntList = AftMidPlanDataService.GetTSDC002_REV_FIG_SHP_REV_NO_SELECTList(SelFigShp, selCase.WRK_PNT_REV_NO);
                }


                if (shipWrkPntList.Count.Equals(0))
                {
                    return "호선 절점[TSDC002] 정보를 조회할 수 없습니다.";
                }

                // 버퍼 기준정보
                var stdBufList = AftMidPlanDataService.GetTSFN007_COMMON_SHP_KND_SHP_TYP_SELECTList(selCase.SHP_KND, "*");
                //DataTSFN007List stdBufList = null;

                // ACT/관계 생성
                var actCreator = new AftMidPlanCreator(this.MasterDataService, this.AftMidPlanDataService);
                if (actCreator.MakeShipAct_EX_N(shipCase, stdDivList, stdActList, stdLinkList, stdBufList, shipDivItmList, shipHullActList, shipWrkPntList, null, false, false, true) == false
                    || actCreator.ActList == null || actCreator.LinkList == null)
                    return "호선 ACT/관계를 생성할 수 없습니다.";

                /////////////////////////////////////////////////////////////////////////////////
                // 호선 절점 제약 생성

                // 표준절점제약
                base.SetAppIsBusyStatus("호선 절점제약 생성 [2/2]");

                var stdPntConstraints = AftMidPlanDataService.GetTSFN005_MODEL_NO_MAKE_SHIP_SELECTList(createFromStdModelNo);
                if (stdPntConstraints != null && stdPntConstraints.Count > 0)
                {
                    // 호선 절점제약 생성
                    if (actCreator.MakeShipPntConstraints(shipCase, stdDivList, stdPntConstraints, stdBufList, shipWrkPntList) == false)

                        return "호선 절점제약을 생성할 수 없습니다.";
                }    

                // DEBUG
                //actCreator.ActList.dtDataSource.WriteXml(@"c:\act.xml");
                //actCreator.LinkList.dtDataSource.WriteXml(@"c:\link.xml");
                

                /////////////////////////////////////////////////////////////////////////////////
                // 저장
                // ACT/Link
                var delQuery = string.Format("DELETE TSFN101 WHERE FIG_SHP = '{0}' AND CASE_NO = '{1}'", SelFigShp, SelCaseNo);
                AftMidPlanDataService.BulkDeleteInsertTSFN101(actCreator.ActList.dtDataSource, delQuery, SelFigNo);
                delQuery = string.Format("DELETE TSFN102 WHERE FIG_SHP = '{0}' AND CASE_NO = '{1}'", SelFigShp, SelCaseNo);
                AftMidPlanDataService.BulkDeleteInsertTSFN102(actCreator.LinkList.dtDataSource, delQuery, SelFigNo);

                // 절점제약
                delQuery = string.Format("DELETE TSFN105 WHERE FIG_SHP = '{0}' AND CASE_NO = '{1}'", SelFigShp, SelCaseNo);
                if (actCreator.ShipPntConstraints != null && actCreator.ShipPntConstraints.dtDataSource != null)
                    AftMidPlanDataService.BulkDeleteInsertTSFN105(actCreator.ShipPntConstraints.dtDataSource, delQuery, SelFigNo);
                else
                    AftMidPlanDataService.DeleteTSFN105(SelCaseNo, SelFigShp);

                // 리비전 REF_MODEL_NO 업데이트
                selCase.REF_MODEL_NO = createFromStdModelNo;
                // 선각인 경우
                if (IsModelShip == false)
                    AftMidPlanDataService.UpdateTSFN011_REF_MODEL_NO(SelCaseNo, SelFigShp, createFromStdModelNo);

                /////////////////////////////////////////////////////////////////////////////////
                // 에러 표시
                createErrorActs = actCreator.ActList;
                //ShowErrorActs(actCreator.ActList);

                return string.Empty;
            }
            return "호선 및 리비전을 선택하세요.";
        }

        private void ShowErrorActs(DataTSFN101List actList)
        {
            if (actList != null)
            {
                var errActs = actList.Where(e => e.Error != null && e.Error != MakeShipActError.None).ToList();
                if (errActs != null && errActs.Count > 0)
                {
                    var dlg = new ActErrorDialog(this.messageService, actList, dataModel.DptList);
                    dlg.ShowDialog();
                }
            }
        }


        private void ShowCntractedActs(DataTSFN101List cntractedActs)
        {
            if (cntractedActs != null)
            {
                var dlg = new ContractedActDialog(this.messageService, cntractedActs, dataModel.DptList);
                dlg.ShowDialog();
            }
        }

        #endregion

        #region F/W 스케줄


        private void bForwardScheduleAct_Click(object sender, RoutedEventArgs e)
        {
            if (dataModel != null && SelActItem != null)
            {
                ForwardScheduleAct(SelActItem);
            }
        }

        private void bSchOptionSubmit_Click(object sender, RoutedEventArgs e)
        {
            SaveGanttViewOption();
            this.schOptionPopup.ClosePopup();
        }

        private void bForwardSchedule_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (dataModel != null)
            {
                dataModel.ForwardSchedule(Options.UseStdTerm, Options.OffsetOption, Options.UseStartWrkPntConstraint, Options.UseFinishWrkPntConstraint);

                ShowGantt();

                messageService.ShowMessage("F/W 스케줄을 완료하였습니다.");
            }
        }
        private void bDivForwardSchedule_ItemClick(object sender, ItemClickEventArgs e)
        {
            SelectedDivForwardSchedule();
        }


        private void SelectedDivForwardSchedule()
        {
            if (dataModel != null)
            {
                var divCod = GetSelectedGanttGroupDivCod();
                ForwardScheduleDiv(divCod);

                messageService.ShowMessage("F/W 스케줄을 완료하였습니다.");
            }
        }

        private void ForwardScheduleDiv(string divCod)
        {
            if (dataModel != null && string.IsNullOrEmpty(divCod) == false)
            {
                dataModel.ForwardScheduleDiv(divCod, Options.UseStdTerm, Options.OffsetOption, Options.UseStartWrkPntConstraint, Options.UseFinishWrkPntConstraint);
                var acts = dataModel.GetDivActList(divCod);

                smartGantt.BeginUpdateActivity();
                smartGantt.ChangeNodeListProperty(acts);
                smartGantt.ChangeLinkListProperty(dataModel.GetRelatedLinkList(acts), true);

                // 절점제약 위배 act 관계 업데이트
                UpdateWrkPntCnstLink(acts);

                smartGantt.EndUpdateActivity();

                // 스케줄 후 관련 에러 표시
                ShowScheduleError(acts);
            }
        }

        private void ForwardScheduleAct(DataTSFN101 act)
        {
            if (dataModel != null && act != null)
            {
                var tgtActs = new List<DataTSFN101>();
                var schActs = new List<DataTSFN101>();
                var errActs = new List<DataTSFN101>();

                dataModel.SetScheduleOption(AmpScheduleMode.Forward, Options.UseStdTerm, Options.OffsetOption, Options.UseStartWrkPntConstraint, Options.UseFinishWrkPntConstraint);
                dataModel.ForwardSchedule(act, tgtActs, schActs, errActs);

                smartGantt.BeginUpdateActivity();
                smartGantt.ChangeNodeListProperty(schActs);
                smartGantt.ChangeLinkListProperty(dataModel.GetRelatedLinkList(act), true);

                // 절점제약 위배 act 관계 업데이트
                UpdateWrkPntCnstLink(schActs);

                smartGantt.EndUpdateActivity();

                // 스케줄 후 관련 에러 표시
                var tmpActs = new List<DataTSFN101>(tgtActs);
                tgtActs.Add(act);
                ShowScheduleError(tmpActs);
            }
        }

        private void mForwardScheduleDiv_Click(object sender, RoutedEventArgs e)
        {
            SelectedDivForwardSchedule();
        }

        private void ShowScheduleError(List<DataTSFN101> acts)
        {
            if (acts != null && acts.Count > 0)
            {

                // 절점 제약 위반 정보 표시
                if (Options.ShowWrkPntCnstWarning == true)
                {
                    // 표시 방법은 추후 변경, 우선 위반 항목 하나만 표시
                    var errActs = acts.Where(e => e.StartWrkPntViolated == true || e.FinishWrkPntViolated == true).ToList();
                    if (errActs != null && errActs.Count > 0)
                    {
                        var msg = string.Format("Act [{0}]가 절점제약을 위반하였습니다.", errActs[0].ACT_COD);
                        messageService.ShowWarning(msg);
                    }
                }
            }
        }




        #endregion

        #region 표준절점제약으로 호선절점제약 생성

        private void bCreateDivCnstFromStd_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (string.IsNullOrEmpty(SelFigShp) == false && string.IsNullOrEmpty(SelCaseNo) == false)
            {
                var selCase = GetSelectedCase(SelCaseNo);
                var dlg = new SelectModelNoDialog(selCase.SHP_KND, selCase.SHP_TYP, selCase.DCK_COD, this.MasterDataService, this.AftMidPlanDataService, this.messageService, true);
                if (dlg.ShowDialog() == true && string.IsNullOrEmpty(dlg.SelModelNo) == false)
                {
                    this.createFromStdModelNo = dlg.SelModelNo;
                    this.workType = WorkType.CreatePntConstFromStd;
                    base.RunBackgroundWorker();
                }
            }
        }

        private string CreatePntConstFromStd()
        {
            if (string.IsNullOrEmpty(SelFigShp) == false && string.IsNullOrEmpty(SelCaseNo) == false
                && shipCaseList != null)
            {
                var shipCase = shipCaseList.FirstOrDefault(e => e.CASE_NO == SelCaseNo);
                if (shipCase == null)
                    return "로선의 리비전 정보가 없습니다.";

                // 표준구획
                base.SetAppIsBusyStatus("표준 구획 조회 [1/5]");
                var stdDivList = AftMidPlanDataService.GetTSFN001_SHP_KND_SHP_TYP_USE_YN_SELECTList(shipCase.SHP_KND, "*", "Y");
                if (stdDivList == null || stdDivList.Count == 0)
                    return "표준 구획[TSFN001]를 조회할 수 없습니다.";

                // 표준절점제약
                base.SetAppIsBusyStatus("표준 절점제약 조회 [2/5]");
                var stdPntConstraints = AftMidPlanDataService.GetTSFN005_MODEL_NO_MAKE_SHIP_SELECTList(createFromStdModelNo);
                if (stdPntConstraints == null || stdPntConstraints.Count == 0)
                    return "표준 절점제약[TSFN005]를 조회할 수 없습니다.";

                // 호선 절점 목록
                base.SetAppIsBusyStatus("호선 절점 목록 [3/5]");
                DataTSDC002List shipWrkPntList = null;
                var selCase = GetSelectedCase(SelCaseNo);
                if (string.IsNullOrEmpty(selCase.WRK_PNT_REV_NO) == true)
                {
                    shipWrkPntList = AftMidPlanDataService.GetTSDC002_FIG_SHP_SELECTList(SelFigShp, SelFigNo);   // 별도 리비전이 없으면 운영
                }
                else
                {
                    // 선각인 경우만
                    if (IsModelShip == false)
                        shipWrkPntList = AftMidPlanDataService.GetTSDC002_REV_FIG_SHP_REV_NO_SELECTList(SelFigShp, selCase.WRK_PNT_REV_NO);
                }

                // 버퍼 기준정보
                var stdBufList = AftMidPlanDataService.GetTSFN007_COMMON_SHP_KND_SHP_TYP_SELECTList(selCase.SHP_KND, "*");
                //DataTSFN007List stdBufList = null;

                // 호선 절점제약 생성
                base.SetAppIsBusyStatus("호선 절점제약 생성 [4/5]");
                var actCreator = new AftMidPlanCreator(this.MasterDataService, this.AftMidPlanDataService);
                if (actCreator.MakeShipPntConstraints(shipCase, stdDivList, stdPntConstraints, stdBufList, shipWrkPntList) == false)
                    return "호선 절점제약을 생성할 수 없습니다.";

                // 저장
                base.SetAppIsBusyStatus("호선 절점제약 저장 [5/5]");
                var delQuery = string.Format("DELETE TSFN105 WHERE FIG_SHP = '{0}' AND CASE_NO = '{1}'", SelFigShp, SelCaseNo);
                AftMidPlanDataService.BulkDeleteInsertTSFN105(actCreator.ShipPntConstraints.dtDataSource, delQuery, SelFigNo);

                return string.Empty;
            }
            return "호선 및 리비전을 선택하세요.";
        }

        #endregion

        #region 표준부서지정

        private void bStdDptAssign_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (dataModel != null && dataModel.ActList != null)
            {
                if (messageService.ShowYesNoQuestion("모든 ACT의 부서를 표준부서로 설정합니다.") == true)
                {
                    AssignStdDpt(dataModel.ActList.ToList());

                    ShowCntractedActs(cntractedActs);
                    messageService.ShowMessage("표준부서지정을 완료하였습니다.");
                }
            }
        }

        private void bSelActStdDptAssign_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (dataModel != null && dataModel.ActList != null)
            {
                if (messageService.ShowYesNoQuestion("선택한 ACT의 부서를 표준부서로 설정합니다.") == true)
                {
                    SetAssignStdDpt();
                    messageService.ShowMessage("표준부서지정을 완료하였습니다.");
                }
            }
        }

        //19.10.16 신성훈 추가
        private void AssignStdDpt(List<DataTSFN101> actList)
        {
            if (cntractedActs != null)
            {
                cntractedActs.Clear();
            }

            if (actList != null && actList.Count > 0)
            {
                var shipCase = GetSelectedCase(SelCaseNo);

                foreach (var act in actList)
                {
                    // 선각ACT는 제외?
                    //if (act.HO_GUBUN == Defs.HO_GUBUN_BEF_ACT)
                    if (Defs.IsHullAct(act.WRK_STG) == true)
                        continue;

                    // 부서고정된 ACT는 제외
                    if (act.DPT_FIXED_YN == "Y")
                        continue;

                    var stdDptCod = AftMidPlanDataService.FindStdDpt(shipCase.SHP_KND, shipCase.DCK_COD, act.WRK_TYP, act.WRK_TYP2);
                    var ctrtDptDiffChk = AftMidPlanDataService.CtrtDptDiffChk(act.ACT_COD, act.FIG_SHP, act.DPT_COD);
                    
                    //19.10.15 신성훈 계약된 경우 부서 비교를 통해 ERP부서기준으로 맞춰준다.
                    if(String.IsNullOrEmpty(ctrtDptDiffChk) == false)
                    {
                        act.DPT_COD = ctrtDptDiffChk;
                        //계약된 액트 리스트 보관. 19.10.16 신성훈
                        AddCntractedActList(act);
                    }
                    else
                        act.DPT_COD = stdDptCod;
                    //19.10.15 신성훈 계약된 경우 부서 비교를 통해 ERP부서기준으로 맞춰준다음
                    //나머지는 표준부서로직을 타도록 수행하면 얼럿메세지 안띄워도 될것 같음.
                    //19.10.15 신성훈 주석 처리
                    //var ctrtChk = AftMidPlanDataService.ContractYnChk(act.ACT_COD, act.FIG_SHP);

                    //if (string.IsNullOrEmpty(stdDptCod) == false)
                    //{
                        //19.07.26 신성훈 부서변경 시 계약여부 체크 로직 추가
                        //Y = 외주계약, N = 직영실적, NA = 변경가능
                    //    if (ctrtChk == "Y")
                    //    {
                    //        messageService.ShowError(string.Format("{0}의 외주계약 발생. 현업에 수정계약 요망.", act.ACT_COD));
                    //        return;
                    //    }
                    //    else if (ctrtChk == "N")
                    //    {
                    //        messageService.ShowError(string.Format("{0}의 직영실적발생. 현업 조치 요망.", act.ACT_COD));
                    //        return;
                    //    }
                    //    else if (ctrtChk == "NA")
                    //        act.DPT_COD = stdDptCod;
                    //}
                }
            }
        }
        
        #endregion

        #region 간트차트 엑셀 출력

        private void bGanttExcel_ItemClick(object sender, ItemClickEventArgs e)
        {
            ExportGanttToExcel();
        }

        private void ExportGanttToExcel()
        {
            if (dataModel != null && dataModel.ActList != null)
            {
                try
                {
                    var excel = new Utility.GanttExcel();
                    var shipCase = GetSelectedCase(SelCaseNo);

                    excel.ConverterExcel(smartGantt, null, dataModel.Calendar, shipCase, "", this.ZoomFactorPercent, dataModel.WrkPntList);
                }
                catch (Exception ex)
                {
                    messageService.ShowWarning(ex.Message);
                }
            }
        }

        #endregion

        #region ACT 상세 변경 처리


        private void actTableView_ShowingEditor(object sender, ShowingEditorEventArgs e)
        {
            if (SelActItem != null)
            {
                var act = SelActItem;
                var colName = e.Column.Name;
                if (colName == "PLN_ST")
                {
                    // 착수일 변경
                    // N41 착수일 변경 불가
                    if (act.WRK_STG == Defs.WRK_STG_N && act.WRK_TYP == "41")
                    {
                        e.Cancel = true;
                        return;
                    }
                }
                else if (colName == "PLN_FI")
                {
                    // 완료일 변경
                    // N41 착수일 변경 불가 
                    // 20190419 한승훈 기사 탑재 완료계획일 사라지는 로직 관련하여 추가
                    if (act.WRK_STG == Defs.WRK_STG_N && act.WRK_TYP == "41")
                    {
                        e.Cancel = true;
                        return;
                    }
                }
                else if (colName == "PLN_TRM")
                {
                    // 공기 변경
                }
                else if (colName == "")
                {

                }
            }
        }

        private void actTableView_CellValueChanging(object sender, CellValueChangedEventArgs e)
        {

        }

        private void actTableView_CellValueChanged(object sender, CellValueChangedEventArgs e)
        {
            if (SelActItem != null)
            {
                // ACT 일정, 변경 시
                //   - 계획일, 공기 업데이트 
                //   - 후처리(건조 ACT 일정 맞춤, 이후 계획 F/W 수행 등)

                var act = SelActItem;
                var colName = e.Column.Name;

                //19.08.12 신성훈 부서변경 시 계약여부 체크 로직 추가
                //19.10.15 신성훈 주석처리
                //var ctrtChk = AftMidPlanDataService.ContractYnChk(act.ACT_COD, act.FIG_SHP);

                //19.07.26 신성훈 부서변경 시 계약여부 체크 로직 추가
                //Y = 외주계약, N = 직영실적, NA = 변경가능
                //19.10.15 신성훈 주석처리
                //if (ctrtChk == "Y")
                //{
                //    messageService.ShowError(string.Format("{0}의 외주계약 발생. 현업에 수정계약 요망.", act.ACT_COD));
                //    return;
                //}
                //else if (ctrtChk == "N")
                //{
                //    messageService.ShowError(string.Format("{0}의 직영실적발생. 현업 조치 요망.", act.ACT_COD));
                //    return;
                //}
                
                if (colName == "PLN_ST")
                {
                    // 착수일 변경
                    // 착수일, 완료일 -> 공기
                    dataModel.CalcDuration(act);
                    Postprocess(act, NodeUpdateType.StartChanged);
                }
                else if (colName == "PLN_FI")
                {
                    // 완료일 변경
                    // 착수일, 완료일 -> 공기
                    dataModel.CalcDuration(act);
                    Postprocess(act, NodeUpdateType.FinishChanged);
                }
                else if (colName == "PLN_TRM")
                {
                    // 공기 변경
                    // 착수일, 공기 -> 완료일 변경
                    dataModel.UpdatePlanFinish(act);
                    Postprocess(act, NodeUpdateType.FinishChanged);
                }
                else if (colName == "DPT_COD")
                {
                    // 부서 변경
                }
            }
        }
        
        #endregion

        #region B/W 스케줄

        private void bDivBackwardSchedule_ItemClick(object sender, ItemClickEventArgs e)
        {
            SelectedDivBackwardSchedule();
        }

        private void SelectedDivBackwardSchedule()
        {
            if (dataModel != null)
            {
                var divCod = GetSelectedGanttGroupDivCod();
                BackwardScheduleDiv(divCod);

                messageService.ShowMessage("B/W 스케줄을 완료하였습니다.");
            }
        }

        private void BackwardScheduleDiv(string divCod)
        {
            if (dataModel != null && string.IsNullOrEmpty(divCod) == false)
            {
                dataModel.BackwardScheduleDiv(divCod, Options.UseStdTerm, Options.OffsetOption, Options.UseStartWrkPntConstraint, Options.UseFinishWrkPntConstraint);
                var acts = dataModel.GetDivActList(divCod);

                smartGantt.BeginUpdateActivity();
                smartGantt.ChangeNodeListProperty(acts);
                smartGantt.ChangeLinkListProperty(dataModel.GetRelatedLinkList(acts), true);
                smartGantt.EndUpdateActivity();
            }
        }

        #endregion

        #region CP
        private void bFindCp_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (dataModel != null && dataModel.ActList != null)
            {
                dataModel.FindCP();
            }
        }



        #endregion

        #region 절점제약


        private void wrkPntStartFinish_Checked(object sender, RoutedEventArgs e)
        {
            if (WrkPntCnstStart == true)
            {
                // 절점 착수 기준 
                Grid.SetColumn(txtAct, 2);
                Grid.SetColumn(PntActGridControl, 2);
                Grid.SetColumn(txtWrkPnt, 0);
                Grid.SetColumn(PntGridControl, 0);

                PntConstGridControl.FilterString = "[ST_FI_GBN] = 'S'";
            }
            else
            {
                // 절점 완료 기준
                Grid.SetColumn(txtAct, 0);
                Grid.SetColumn(PntActGridControl, 0);
                Grid.SetColumn(txtWrkPnt, 2);
                Grid.SetColumn(PntGridControl, 2);

                PntConstGridControl.FilterString = "[ST_FI_GBN] = 'F'";
            }
        }

        private void bCreatePntConst_Click(object sender, RoutedEventArgs e)
        {
            if (dataModel != null && dataModel.ActList != null)
            {
                PntActGridControl.ValueEndEdit();
                PntGridControl.ValueEndEdit();

                var actList = dataModel.ActList.Where(i => i.Select3 == true).ToList();
                var wrkPnt = (PntGridControl.CurrentItem as DataTSDC002).Row;

                if (actList != null && actList.Count > 0 && wrkPnt != null)
                {
                    var wrkPntCnsts = new List<DataTSFN105>();
                    foreach (var act in actList)
                    {
                        var wrkPntCnst = AddWrkPntCnst(act, wrkPnt, WrkPntCnstStart ? "S" : "F");
                        wrkPntCnsts.Add(wrkPntCnst);
                    }

                    // 선택 초기화
                    dataModel.ActList.ClearSelect();
                }
                else
                {
                    messageService.ShowWarning("절점제약을 추가할 ACT 와 절점을 선택하세요.");
                }
            }
        }

        private void bDeletePntConst_Click(object sender, RoutedEventArgs e)
        {
            if (dataModel != null && dataModel.WrkPntConstraints != null)
            {
                PntConstGridControl.ValueEndEdit();

                var list = dataModel.WrkPntConstraints.Where(i => i.Select == true).ToList();
                if (list != null && list.Count > 0)
                {
                    foreach (var cnst in list)
                    {
                        // 간트 제약노드/링크  제거
                        DeletePntConst(cnst);
                    }

                    // 선택 초기화
                    dataModel.WrkPntConstraints.ClearSelect();
                }
            }
        }

        private void DeletePntConst(DataTSFN105 cnst)
        {
            if (dataModel != null && dataModel != null && cnst != null)
            {
                smartGantt.RemoveNode(cnst);
                var cnstNode = dataModel.GetGanttWrkPntCnstLink(cnst);
                if (cnstNode != null)
                    smartGantt.RemoveLink(cnstNode);

                dataModel.DelWrkPntConstraint(cnst);
            }
        }

        private void bAddStWrkPntCnst_Click(object sender, RoutedEventArgs e)
        {
            if (sender != null)
            {
                var wrkPnt = (DataRow)((System.Windows.Controls.MenuItem)sender).Tag;
                if (wrkPnt != null)
                {
                    AddWrkPntCnst(SelActItem, wrkPnt, "S");
                }
            }
        }

        private DataTSFN105 AddWrkPntCnst(DataTSFN101 act, DataRow wrkPnt, string stFiGbn)
        {
            if (dataModel != null && act != null && wrkPnt != null)
            {
                var wrkPntCnst = dataModel.AddWrkPntConstraint(act, wrkPnt, stFiGbn);
                if (wrkPntCnst != null)
                {
                    // 간트차트에 추가
                    dataModel.GanttUtil.SetWrkPntCnstStyle(wrkPntCnst);
                    smartGantt.AddNode(wrkPntCnst);

                    var wrkPntCnstLink = dataModel.GetGanttWrkPntCnstLink(wrkPntCnst);
                    smartGantt.AddLink(wrkPntCnstLink);
                }
            }
            return null;
        }

        private void bAddFiWrkPntCnst_Click(object sender, RoutedEventArgs e)
        {
            if (sender != null)
            {
                var wrkPnt = (DataRow)((System.Windows.Controls.MenuItem)sender).Tag;
                if (wrkPnt != null)
                {
                    AddWrkPntCnst(SelActItem, wrkPnt, "F");
                }
            }
        }

        #endregion

        #region 필터적용

        private void eApplyFilter_EditValueChanged(object sender, RoutedEventArgs e)
        {
            ShowGantt();
        }



        #endregion

        #region ACT 순서 변경

        private void bChangeOrder_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (dataModel != null && dataModel.ActList != null)
            {
                var dlg = new ChangeActOrderDialog(this.messageService, dataModel.ActList, dataModel.DptList);
                dlg.ShowDialog();
                if (dlg.ChangedOrder == true && dlg.DataSource != null)
                {
                    dataModel.ResetViewSeq(dlg.DataSource.ToList());
                    ShowGantt();
                }
            }
        }

        #endregion

        #region ACT 찾기

        private void eFindAct_PreviewKeyDown(object sender, RoutedEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.Enter) == true)
            {
                var findActStr = eFindAct.EditValue.ToString();
                FindAndFocusAct(findActStr);
            }
        }

        private void FindAndFocusAct(string findActStr)
        {
            if (dataModel != null && dataModel.ActList != null && string.IsNullOrEmpty(findActStr) == false)
            {
                findActStr = findActStr.ToUpper();
                var act = dataModel.ActList.FirstOrDefault(i => i.ACT_COD.StartsWith(findActStr) == true);
                FocusToAct(act);
            }
        }

        private void FocusToAct(DataTSFN101 act)
        {
            if (act != null)
            {
                smartGantt.ScreenIntoView(act.ID);
            }
        }
        private void bFindAct_ItemClick(object sender, ItemClickEventArgs e)
        {
            var findActStr = eFindAct.EditValue.ToString();
            FindAndFocusAct(findActStr);
        }

        #endregion


        #region 간트 보기 모드 변경

        private void eGanttViewMode_EditValueChanged(object sender, RoutedEventArgs e)
        {
            ShowGantt();
        }

        #endregion

        #region 구획간 링크 생성/삭제

        private void bCreateDivLink_Click(object sender, RoutedEventArgs e)
        {
            if (dataModel != null && dataModel.ActList != null)
            {
                PreActGridControl.ValueEndEdit();
                AftActGridControl.ValueEndEdit();

                var preActs = dataModel.ActList.Where(i => i.Select == true).ToList();
                var aftActs = dataModel.ActList.Where(i => i.Select2 == true).ToList();

                if (preActs != null && preActs.Count > 0 && aftActs != null && aftActs.Count > 0)
                {
                    if (messageService.ShowYesNoQuestion("선택한 선/후행 ACT의 관계를 생성합니다.") == true)
                    {
                        var linkType = (LinkTypeDef)cmbDivLink.SelectedItem;
                        var offset = AmiParse.IntParse(eOffset.Text);
                        foreach (var preAct in preActs)
                        {
                            foreach (var aftAct in aftActs)
                            {
                                CreateLink(preAct, aftAct, linkType, offset);
                            }
                        }

                        // 체크 리셋
                        dataModel.ActList.ClearSelect();

                        // 구획간 관계목록 업데이트
                        DivLinkGridControl.RefreshData();
                    }
                }
                else
                {
                    messageService.ShowWarning("연결할 선/후행 ACT를 선택하세요.");
                }
            }
        }

        private void bDeleteDivLink_Click(object sender, RoutedEventArgs e)
        {
            if (dataModel != null && dataModel.LinkList != null)
            {
                DivLinkGridControl.ValueEndEdit();

                var linkList = dataModel.LinkList.Where(i => i.Select == true).ToList();
                if (linkList != null && linkList.Count > 0)
                {
                    var msg = string.Format("선택한 {0}개의 관계를 삭제합니다.", linkList.Count);
                    if (messageService.ShowYesNoQuestion(msg) == true)
                    {
                        foreach (var link in linkList)
                        {
                            DeleteLink(link);
                        }

                        dataModel.LinkList.ClearSelect();

                        // 구획간 관계목록 업데이트
                        DivLinkGridControl.RefreshData();
                    }
                }
            }
        }

        #endregion

        #region 부서고정/일정고정


        private void mFixDptInDiv_Click(object sender, RoutedEventArgs e)
        {
            var selDiv = GetSelectedGanttGroupDivCod();
            if (string.IsNullOrEmpty(selDiv) == false)
            {
                var divActs = dataModel.GetDivActList(selDiv);
                if (divActs != null)
                {
                    divActs.ForEach(i => i.DPT_FIXED_YN = "Y");
                }
            }
        }

        private void mUnFixDptInDiv_Click(object sender, RoutedEventArgs e)
        {
            var selDiv = GetSelectedGanttGroupDivCod();
            if (string.IsNullOrEmpty(selDiv) == false)
            {
                var divActs = dataModel.GetDivActList(selDiv);
                if (divActs != null)
                {
                    divActs.ForEach(i => i.DPT_FIXED_YN = "N");
                }
            }
        }

        private void mUnFivDateInDiv_Click(object sender, RoutedEventArgs e)
        {
            var selDiv = GetSelectedGanttGroupDivCod();
            if (string.IsNullOrEmpty(selDiv) == false)
            {
                var divActs = dataModel.GetDivActList(selDiv);
                if (divActs != null)
                {
                    divActs.ForEach(i => i.FIXED_YN = "N");
                }
            }
        }
        private void mFivDateInDiv_Click(object sender, RoutedEventArgs e)
        {
            var selDiv = GetSelectedGanttGroupDivCod();
            if (string.IsNullOrEmpty(selDiv) == false)
            {
                var divActs = dataModel.GetDivActList(selDiv);
                if (divActs != null)
                {
                    divActs.ForEach(i => i.FIXED_YN = "Y");
                }
            }
        }

        #endregion

        #region ProNet 일정 가져오기
        private void bUpdatePlanFromProNet_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (dataModel.ActList != null)
            {
                var dlg = new UpdateActDateFromProNetDialog(this.messageService, this.dataModel.ActList, this.DptList);
                dlg.ShowDialog();
                if (dlg.ChangedActList != null && dlg.ChangedActList.Count > 0)
                {
                    // 변경된 일정이 있으면 
                    smartGantt.BeginUpdateActivity();
                    smartGantt.ChangeNodeListProperty(dlg.ChangedActList);
                    smartGantt.ChangeLinkListProperty(dataModel.GetRelatedLinkList(dlg.ChangedActList));
                    smartGantt.EndUpdateActivity();
                }
            }
        }

        #endregion

        private void smartGantt_DiagramLeftMouseDown(object sender, MouseButtonEventArgs args)
        {

            NextStepDispatcher clickNext = new NextStepDispatcher();
            clickNext.NextStep += ClickNext_NextStep;
            clickNext.Start();
        }

        private void ClickNext_NextStep(object sender, EventArgs args)
        {
            if (SelActItem != null && smartGantt.SelectNode != null)
            {
                var selitem = SelActItem;
                var line = new Line();
                line.StrokeThickness = 20;
                line.Fill = Brushes.Blue;
                line.Stroke = Brushes.Blue;
                line.X1 = smartGantt.SelectNode.PlanLeft;
                line.X2 = smartGantt.SelectNode.PlanRight;
                line.Y1 = smartGantt.SelectNode.GetRelativeTop();
                line.Y2 = smartGantt.SelectNode.GetRelativeTop();
                
                Canvas.SetZIndex(line, 0);
                Canvas.SetLeft(line, smartGantt.SelectNode.PlanLeft);

                //smartGantt
                    
                    
                //    VtNodeCanvas.Children.Add(line);

                //Canvas zoomCan = null;
                //foreach (var chi in smartGantt.VtNodeCanvas.Children)
                //{
                //    if (chi is Canvas)
                //    {
                //        zoomCan = chi as Canvas;
                //    }
                //}

                //if (zoomCan != null)
                //    zoomCan.Children.Add(line);
            }
        }

        private void smartGantt_DiagramLeftMouseUp(object sender, MouseButtonEventArgs args)
        {

        }
    }
}

