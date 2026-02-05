using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using HHI.Windows.Forms;
using HHI.ServiceModel;
using HHI.SHP.PP008.COMMON;
using HHI.Windows.Forms.DevExtension;
using DevExpress.XtraGrid.Views.Grid;
using HHI.Windows.Forms.Extensions;
using HHI.SHP.PP008.Client;
using DevExpress.Data.Filtering;
using System.Collections;
using System.Reflection;
using HHI.Security;
using System.Threading;
using System.Diagnostics;
using HHI.SHP.PP008.MTP.StdWrk;

using HHI.SHP.PP008.MTP.Gantt.Interface;
using HHI.SHP.PP008.MTP.Gantt.Definitions;
using Amisys.Infrastructure.HHIInfrastructure.DataModels.HMPStdStgRule;
using Amisys.Infrastructure.HHIInfrastructure.DataModels;
using Amisys.Presentation.OutfitMidPlan.OMPSmartMasterSchedule.Models;
using Amisys.Service.HHIOutfitMidPlanDataService;
using Amisys.Service.HHIMasterDataService;
using Amisys.Service.HHIHullMidPlanDataService;

namespace HHI.SHP.PP008.LTS
{
    public partial class LTS004N : UserControlBase
    {
        #region ##################################### 전역변수 Start
        private DataTable _dtAuth = new DataTable();
        string _prjgbn = string.Empty;

        WSMProject _WSMProject;
        IMPStageAssignRuleManager stageAssignRuleManager = new MPStageAssignRuleManager();
        UniStgList _UniStgList2 = null;

        OutfitMSManager _Manager;
        HHIOutfitMidPlanDataService outfitDataService;

        StdDptMain _StdDptMain;

        System.Drawing.Point mouseDownLocation; // 마우스 이벤트 위치값 변수

        #endregion ################################## 전역변수 End



        #region ##################################### 생성자 Start
        public LTS004N()
        {
            InitializeComponent();

            _StdDptMain = new StdDptMain(UserID, UserInfoContext.Current["IP_ADDRESS"]);
        }
        #endregion ################################## 생성자 End



        #region ##################################### 초기화 Start

        public override void InitControl(object args)
        {
            base.InitControl(args);

            // 초기화
            SetInit();
        }

        /// <summary>
        /// 초기화
        /// </summary>
        private void SetInit()
        {
            QueryParameterCollection param = new QueryParameterCollection();
            param.Add("USER_ID", this.UserID);
            DataTable dt = this.ExecuteSelect("PKG_PP008_COMMON.USER_AUTH_INFO", param);

            if (dt != null && dt.Rows.Count > 0)
            {
                _dtAuth = dt;
            }

            outfitDataService = new HHIOutfitMidPlanDataService();

            // 콤보 초기화
            SetCombo();

            // 그리드 내부 콤보 초기화
            SetGridCombo();

            // 컨트롤러 초기화
            SetControl();

            // 버튼관련 초기화
            SetButton();

            // 그리드관련 초기화
            SetGrid();
        }

        /// <summary>
        /// 그리드 외부 콤보 초기화
        /// </summary>
        private void SetCombo()
        {
            string prjgbn = string.Empty;
            DataTable dt = null;

            if (_dtAuth != null && _dtAuth.Rows.Count > 0)
            {
                prjgbn = _dtAuth.Rows[0]["PRJ_GBN"].ToString();

                _prjgbn = prjgbn;
            }

            //선표 번호
            QueryParameterCollection param = new QueryParameterCollection();
            param.Add("PRJ_GBN", prjgbn);
            dt = this.ExecuteSelect("PKG_PP009_LTS004.TSAC003_FIG_NO_LIST", param);

            if (dt != null && dt.Rows.Count > 0)
            {
                cboFIGNO.WdxBindItem("FIG_NO", "FIG_NO", "{value}", "", "", WdxComboDisplayTextOption.none, dt);
                cboFIGNO.SelectedIndex = 0;
            }

            //도크배치 선표
            var dt1 = this.ExecuteSelect("PKG_PP009_LTS004.TSAC003_NEWEST_FIG_NO_LIST", param);

            if (dt1 != null && dt1.Rows.Count > 0)
            {
                cboDockFigno.WdxBindItem("FIG_NO", "FIG_NO", "{value}", "", "", WdxComboDisplayTextOption.none, dt);
                cboDockFigno.SelectedIndex = 0;
            }

            DataTable dt_gbn = new DataTable();
            dt_gbn.Columns.Add("COD");
            dt_gbn.Columns.Add("NME");
            dt_gbn.Rows.Add(new object[] { "S", "호선" });
            dt_gbn.Rows.Add(new object[] { "M", "모델선" });

            cboGBN.WdxBindItem("NME", "COD", "{text}", "", "", WdxComboDisplayTextOption.none, dt_gbn);
            cboGBN.SelectedIndex = 0;

            param = new QueryParameterCollection();
            param.Add("PRJ_GBN", prjgbn);

            dt = this.ExecuteSelect("PKG_PP009_LTS004.TSAC003_FIG_NO_LIST", param);

            if (dt != null && dt.Rows.Count > 0)
            {
                cboSRCFIGNO.WdxBindItem("FIG_NO", "FIG_NO", "{value}", "MODEL", "MODEL", WdxComboDisplayTextOption.top, dt);
                cboSRCFIGNO.SelectedIndex = 0;
            }
        }

        /// <summary>
        /// 그리드 내부 콤보 초기화
        /// </summary>
        private void SetGridCombo()
        {
        }

        /// <summary>
        /// 컨트롤러 초기화
        /// </summary>
        private void SetControl()
        {
            spnPlanST.EditValue = DateTime.Now.Year;
            spnPlanFI.EditValue = DateTime.Now.AddYears(1).Year;

            chkShipType.Checked = true;
            chkShipKind.Checked = true;
            chkDockCode.Checked = true;
            chkBLDCode.Checked = true;

            #region 그리드에 ToolStripMenuItem 추가

            ToolStripMenuItem ToolStripMenuItem_Select = new ToolStripMenuItem();
            ToolStripMenuItem_Select.Name = "ToolStripMenuItem_Select";
            ToolStripMenuItem_Select.Size = new System.Drawing.Size(182, 22);
            ToolStripMenuItem_Select.Text = "선택";
            ToolStripMenuItem_Select.Click += new EventHandler(ToolStripMenuItem_Select_ClickEvent);
            grdMain.WdxAddCustomContextMenu(ToolStripMenuItem_Select);

            ToolStripMenuItem ToolStripMenuItem_UnSelect = new ToolStripMenuItem();
            ToolStripMenuItem_UnSelect.Name = "ToolStripMenuItem_UnSelect";
            ToolStripMenuItem_UnSelect.Size = new System.Drawing.Size(182, 22);
            ToolStripMenuItem_UnSelect.Text = "선택 해제";
            ToolStripMenuItem_UnSelect.Click += new EventHandler(ToolStripMenuItem_UnSelect_ClickEvent);
            grdMain.WdxAddCustomContextMenu(ToolStripMenuItem_UnSelect);

            ToolStripMenuItem ToolStripMenuItem_Delete = new ToolStripMenuItem();
            ToolStripMenuItem_Delete.Name = "ToolStripMenuItem_Delete";
            ToolStripMenuItem_Delete.Size = new System.Drawing.Size(182, 22);
            ToolStripMenuItem_Delete.Text = "삭제";
            ToolStripMenuItem_Delete.Click += new EventHandler(ToolStripMenuItem_Delete_ClickEvent);
            grdMain.WdxAddCustomContextMenu(ToolStripMenuItem_Delete);

            ToolStripMenuItem ToolStripMenuItem_UnDelete = new ToolStripMenuItem();
            ToolStripMenuItem_UnDelete.Name = "ToolStripMenuItem_UnDelete";
            ToolStripMenuItem_UnDelete.Size = new System.Drawing.Size(182, 22);
            ToolStripMenuItem_UnDelete.Text = "삭제 취소";
            ToolStripMenuItem_UnDelete.Click += new EventHandler(ToolStripMenuItem_UnDelete_ClickEvent);
            grdMain.WdxAddCustomContextMenu(ToolStripMenuItem_UnDelete);

            ToolStripMenuItem ToolStripMenuItem_ErecShift = new ToolStripMenuItem();
            ToolStripMenuItem_ErecShift.Name = "ToolStripMenuItem_ErecShift";
            ToolStripMenuItem_ErecShift.Size = new System.Drawing.Size(182, 22);
            ToolStripMenuItem_ErecShift.Text = "도크기간 보정 선택";
            ToolStripMenuItem_ErecShift.Click += new EventHandler(ToolStripMenuItem_ErecShift_ClickEvent);
            grdMain.WdxAddCustomContextMenu(ToolStripMenuItem_ErecShift);

            ToolStripMenuItem ToolStripMenuItem_UnErecShift = new ToolStripMenuItem();
            ToolStripMenuItem_UnErecShift.Name = "ToolStripMenuItem_UnErecShift";
            ToolStripMenuItem_UnErecShift.Size = new System.Drawing.Size(182, 22);
            ToolStripMenuItem_UnErecShift.Text = "도크기간 보정 해제";
            ToolStripMenuItem_UnErecShift.Click += new EventHandler(ToolStripMenuItem_UnErecShift_ClickEvent);
            grdMain.WdxAddCustomContextMenu(ToolStripMenuItem_UnErecShift);

            #endregion
        }

        /// <summary>
        /// 버튼 초기화 (권한 설정 등)
        /// </summary>
        private void SetButton()
        {
        }

        /// <summary>
        /// 그리드 초기화
        /// </summary>
        private void SetGrid()
        {
            grdMain.WdxAddRequiredColumns("CPY_FIG_NO", "CPY_SHP", "CPY_FIG_SHP_OFT", "CPY_FIG_NO_OFT");
            // 그리드 초기화
            Utility.InitGrid(grdMain, true, false, "CHK");
            Utility.InitGrid(grdSub, true, false, "");
        }

        #endregion ################################## 초기화 End

        #region ##################################### 버튼 이벤트 Start

        /// <summary>
        /// 조회
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnSearch_Click(object sender, EventArgs e)
        {
            if(!this.layParameter.WdxNullOrWhiteSpaceValidation()) return;

            string startDate = spnPlanST.EditValue + "0101";
            string finishDate = (Int32.Parse(spnPlanFI.EditValue.ToString()) + 1) + "0201";

            string SelFigno = cboFIGNO.EditValue.ToString();
            string SelDock_Figno = cboDockFigno.EditValue.ToString();
            DataTable dt = null;

            string key = SelFigno + startDate + finishDate;

            QueryParameterCollection param = new QueryParameterCollection();
            param.Add("FIG_NO", cboFIGNO.EditValue);
            param.Add("START_DATE", startDate);
            param.Add("FINISH_DATE", finishDate);
            //param.Add("PRJ_GBN", _prjgbn);
            param.Add("DOCK_FIG_NO", cboDockFigno.EditValue);

            //await Task.Run(() =>
            //{
            //    UserInfo.SetCallContext();

            //    Invoke(new Action(() => ShowWaitForm("조회중입니다...")));

            //    if (_prjgbn == "E000")
            //        Invoke(new Action(() => dt = this.ExecuteSelect("PKG_PP009_LTS004.TSAD001_KL_SELECT_E000", param)));
            //    else
            //        Invoke(new Action(() => dt = this.ExecuteSelect("PKG_PP009_LTS004.TSAD001_KL_SELECT", param)));
            //});

            ShowWaitForm("조회중입니다...");

            if (_prjgbn == "E000")
                dt = this.ExecuteSelect("PKG_PP009_LTS004.TSAD001_KL_SELECT_E000", param);
            else
                dt = this.ExecuteSelect("PKG_PP009_LTS004.TSAD001_KL_SELECT", param);

            if (dt != null)
            {
                dt.Columns.Add("WORKING_STATE");
                dt.Columns.Add("ERECSHIFT");
                dt.Columns.Add("RUNDATA");
                dt.Columns.Add("HASCOPYSHIP");

                dt.Columns.Add("STD_DEPT_YN"); // 표준부서적용
                dt.Columns.Add("ACT_PLN_YN"); // Act.일정변경
            }
            
            foreach (DataRow dr in dt.Rows)
            {
                dr["WORKING_STATE"] = "N";
                dr["ERECSHIFT"] = "N";

                dr["STD_DEPT_YN"] = "N"; // 표준부서적용
                dr["ACT_PLN_YN"] = "N"; // Act.일정변경

                string rundata = "N";

                if (dr["KIND"] != null && (dr["KIND"].ToString() == "P" || dr["KIND"].ToString() == "E"))
                {
                    rundata = "Y";
                }

                dr["RUNDATA"] = rundata;

                //UpdateHasCopyShip();
                if (!string.IsNullOrEmpty(dr["CPY_FIG_NO"].ToString()))
                    dr["HASCOPYSHIP"] = "Y";
                else
                    dr["HASCOPYSHIP"] = "N";
            }

            grdMain.DataSource = dt;

            UpdateHasRunSelect(chkRundata.Checked);

            CloseWaitForm();
        }

        private void UpdateHasRunSelect(bool value)
        {
            if (!grvMain.WdxIsDataExists) return;

            DataTable dt = grdMain.DataSource as DataTable;

            foreach (DataRow dr in dt.Rows)
            {
                if (dr["RUNDATA"].ToString() == "Y")
                {
                    dr["CHK"] = value ? "Y" : "N";
                    dr["ERECSHIFT"] = value ? "Y" : "N";
                    
                    //dr["CPY_FIG_NO"] = "999999999"; // 모델선표
                    //dr["CPY_SHP"] = dr["FIG_SHP_DES"]; // 모델 호선

                    //dr["CPY_FIG_NO_OFT"] = "999999999"; // 의장-모델선표
                    //dr["CPY_FIG_SHP_OFT"] = dr["FIG_SHP_DES"]; // 의장 -모델호선
                }
            }

            dt.AcceptChanges();
        }

        /// <summary>
        /// 선표/호선 선택 팝업
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnSrcSearch_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(spnPlanST.Text) || string.IsNullOrEmpty(spnPlanFI.Text))
            {
                MessageBox.Show("조회기간을 입력하세요.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            DataRow[] checkedRows = grdMain.WdxGetCheckedRows();

            if (grvMain.FocusedRowHandle < 0 && (checkedRows == null || checkedRows.Length == 0))
            {
                MessageBox.Show("선표/호선을 적용할 Row를 선택하세요.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string SHP_KND;     // 선종
            string SHP_TYP_QTY; // 선형
            string DCK_COD;     // 도크 
            string DCK_BLD_COD; // 건조방식
            string RUNDATA;     // 진행

            RUNDATA = chkRundata.Checked ? "Y" : "N";

            // 다중 선택한 경우 
            if (checkedRows != null && checkedRows.Length > 1)
            {
                bool checkedShipKind = chkShipKind.Checked; // 선종
                bool checkedShipType = chkShipType.Checked; // 선형
                bool checkedDock = chkDockCode.Checked;     // 도크
                bool checkedBLD = chkBLDCode.Checked;       // 건조방식
                
                SHP_KND = checkedRows[0]["SHP_KND"].ToString();
                SHP_TYP_QTY = checkedRows[0]["SHP_TYP_QTY"].ToString();
                DCK_COD = checkedRows[0]["DCK_COD"].ToString();
                DCK_BLD_COD = checkedRows[0]["DCK_BLD_COD"].ToString();

                var diffInfo = checkedRows.AsEnumerable().Where(t => (checkedShipKind && t["SHP_KND"].ToString() != SHP_KND) ||
                                                                     (checkedShipType && t["SHP_TYP_QTY"].ToString() != SHP_TYP_QTY) ||
                                                                     (checkedDock && t["DCK_COD"].ToString() != DCK_COD) ||
                                                                     (checkedBLD && t["DCK_BLD_COD"].ToString() != DCK_BLD_COD));
                
                // 체크된 선종, 선형, 도크, 건조방식 항목 중 다중으로 선택된 항목중에 다른 것이 있다면
                if (diffInfo != null && diffInfo.Count() > 0)
                {
                    string msg = "선택한 조건인 ";
                    msg += checkedShipKind ? "선종," : "";
                    msg += checkedShipType ? "선형," : "";
                    msg += checkedDock     ? "도크," : "";
                    msg += checkedBLD      ? "건조방식," : "";
                    msg = msg.Substring(0, msg.Length - 1);
                    msg += " (은)는 모두 동일해야합니다.";

                    MessageBox.Show(msg, "알림", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
            }
            else
            {
                checkedRows = new DataRow[1];
                checkedRows[0] = grvMain.GetFocusedDataRow();

                SHP_KND = checkedRows[0]["SHP_KND"].ToString();
                SHP_TYP_QTY = checkedRows[0]["SHP_TYP_QTY"].ToString();
                DCK_COD = checkedRows[0]["DCK_COD"].ToString();
                DCK_BLD_COD = checkedRows[0]["DCK_BLD_COD"].ToString();
            }

            SHP_KND = chkShipKind.Checked ? SHP_KND : "";
            SHP_TYP_QTY = chkShipType.Checked ? SHP_TYP_QTY : "";
            DCK_COD = chkDockCode.Checked ? DCK_COD : "";
            DCK_BLD_COD = chkBLDCode.Checked ? DCK_BLD_COD : "";

            LTS004P1 pop = new LTS004P1();
            pop.RequireAuthentication = false;
            pop.RequireAuthorization = false;
            pop.WdxSendDataEvent += Pop_WdxSendDataEvent;
            
            string startDate = spnPlanST.EditValue + "0101";
            string finishDate = (Int32.Parse(spnPlanFI.EditValue.ToString()) + 1) + "0201";

            Hashtable param = new Hashtable();
            param.Add("A", chkShipKind.EditValue); // 선종
            param.Add("B", chkShipType.EditValue); // 선형
            param.Add("C", chkDockCode.EditValue); // 도크
            param.Add("D", chkBLDCode.EditValue);  // 건조방식
            param.Add("E", chkRundata.EditValue);  // 진행선택

            param.Add("A1", SHP_KND);     // 선종
            param.Add("B1", SHP_TYP_QTY); // 선형
            param.Add("C1", DCK_COD);     // 도크
            param.Add("D1", DCK_BLD_COD); // 건조방식
            param.Add("E1", RUNDATA);     // 진행선택

            param.Add("PLAN_ST", startDate);
            param.Add("PLAN_FI", finishDate);
            
            ShowPopUp(pop, false, true, param);
        }

        /// <summary>
        /// 저장
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnSave_Click(object sender, EventArgs e)
        {
            if (grdMain.WdxIsChecked == false)
            {
                MessageBox.Show("선택된 항목이 없습니다.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            grvMain.WdxClearErrors();
            // 선각/의장 모델선표, 모델호선 필수 입력
            bool required = grdMain.WdxValidateCheckedRows();
            if (!required)
            {
                MessageBox.Show("선각의 모델선표, 모델호선\n의장의 모델선표, 모델호선\n은 필수 입력항목입니다.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (MessageBox.Show("선택된 항목을 저장하시겠습니까?", "알림", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No)
                return;

            ShowWaitForm("사업계획 데이터 복사/삭제 목록 생성 중입니다...");

            // 사업계획 데이터 파티션 추가
            var paramMainAdd = new QueryParameterCollection();
            paramMainAdd.Add("FIG_NO", cboFIGNO.EditValue.ToString());
            this.ExecuteNonQuery("MAKE_FIG_NO_PARTITION_NEW", paramMainAdd, QueryServiceTransactions.TxLocal, "SHP.PP008.QueryAgent");

            QueryParameterCollection param = grdMain.WdxGetParameterbyCheckedRows();            
            param.AddArrayValues("USER_ID", UserID);

            // 사업계획 데이터 복사대상 생성(삭제 후 생성 TSMG035)
            QueryResponse response = this.ExecuteNonQuery("PKG_PP009_LTS004.COPY_DEL_CREATE_LIST", param, QueryServiceTransactions.TxLocal, "SHP.PP008.QueryAgent");

            foreach(string value in response.Parameters["O_APP_MSG"] as string[])
            {
                if(!value.Equals("OK"))
                {
                    MessageBox.Show($"사업계획 데이터 복사/삭제 목록 생성 중 오류가 발생하였습니다.\n\n[오류]\n{value}", "알림", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    CloseWaitForm();
                    return;
                }
            }
            
            ShowWaitForm("사업계획 데이터 복사/삭제 대상 적용 중입니다...");

            param = new QueryParameterCollection();
            param.ArrayBindCount = 1;
            string[] fig_no = new string[] { cboFIGNO.EditValue.ToString() };
            param.Add("FIG_NO", fig_no);

            response = this.ExecuteNonQuery("PKG_PP009_LTS004.COPY_DEL_UPDATE_LIST", param, QueryServiceTransactions.TxLocal, "SHP.PP008.QueryAgent");

            foreach (string value in response.Parameters["O_APP_MSG"] as string[])
            {
                if (!value.Equals("OK"))
                {
                    MessageBox.Show($"사업계획 데이터 복사/삭제 대상 적용 중 오류가 발생하였습니다.\n\n[오류]\n{value}", "알림", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    CloseWaitForm();
                    return;
                }
            }
            
            ShowWaitForm("사업계획 데이터 복사/삭제를 진행중입니다...");

            int errCnt = 1;
            string shp_cod = "";
            string fig_shp = "";
            string model_no = "";

            TOTAL_CNT = 0;
            TOTAL_ERR = 0;
            PRECESSING_GBN = "";

            Task task1 = Run1();
            Task task2 = Run2();
            Task task3 = Run3();
            Task task4 = Run4();
            Task task5 = Run5();
            Task task6 = Run6();
            Task task7 = Run7();
            Task task8 = Run8();
            Task task9 = Run9();
            Task task10 = Run10();

            while (TOTAL_CNT < TOTAL_GBN)
            {
                ShowWaitForm($"사업계획 데이터 복사/삭제를 진행중입니다...({TOTAL_CNT}/{TOTAL_GBN})\n( 진행중인 항목 : {PRECESSING_GBN} )");
                Thread.Sleep(100);
            }

            // 오류가 없으면 표준부서적용 로직 적용
            if (TOTAL_ERR == 0)
            {
                foreach (DataRow row in grdMain.WdxGetCheckedRows())
                {
                    ShowWaitForm("표준 부서를 적용중입니다...");

                    // 표준부서적용이 체크된 경우만
                    if (row["STD_DEPT_YN"].ToString().Equals("N"))
                        continue;

                    shp_cod = row["SHP_COD"].ToString(); // row["FIG_SHP_DES"].ToString();
                    fig_shp = row["FIG_SHP_DES"].ToString();// row["CPY_SHP"].ToString();
                    model_no = cboFIGNO.EditValue.ToString();// row["CPY_FIG_NO"].ToString();

                    // 선각 표준부서 적용 로직 추가
                    string msg = ApplyStdDpt(shp_cod, fig_shp, row);
                    if (!msg.Equals("OK"))
                    {
                        MessageBox.Show(msg, "알림", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        goto StepFinish;
                    }
                }

                MessageBox.Show("사업계획 데이터를 복사하였습니다.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show($"사업계획 데이터를 모두 복사하지 못했습니다.\n\n( 복사하지 못한 항목 : {PRECESSING_GBN} )", "알림", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
StepFinish:
            CloseWaitForm();
        }

        
        /// <summary>
        /// 표준 부서 적용
        /// </summary>
        /// <param name="shp_cod"></param>
        /// <param name="fig_shp"></param>
        /// <param name="row"></param>
        /// <returns></returns>
        string ApplyStdDpt(string shp_cod, string fig_shp, DataRow row)
        {
            string msg = "OK";
            string model_no = this.ToString(cboFIGNO.EditValue);

            // 선각 표준부서 적용 ----------------------------------------------------------------------------------
            try
            {
                ShowWaitForm("선각 표준부서를 적용 중입니다...");

                _WSMProject = _StdDptMain.MakeShipData(fig_shp, "", row["CPY_FIG_NO"].ToString().StartsWith("99999") ? "" : model_no);

                if (_WSMProject == null)
                {
                    return msg = $"선각 표준부서 지정 규칙 적용 오류\n{fig_shp} 호선의 기본 정보가 없습니다.";
                }

                List<WSMAct> selItems = _StdDptMain.SelectActs(_WSMProject, "C", _StdDptMain.GetWrkTyps, true);
                // 표준부서 지정규칙 적용
                _StdDptMain.ReloadStdRuleData();
                _StdDptMain.ApplyStdStageRuleAll(selItems, stageAssignRuleManager, null);

                int upCnt = 0;
                // 저장
                DBSaveResult rst = _StdDptMain.SaveData(_WSMProject, ref upCnt, fig_shp, model_no);

                if (rst == DBSaveResult.Fail)
                {
                    return msg = "선각 표준부서 지정 규칙 적용 오류입니다.";
                }
            }
            catch (Exception ex)
            {
                return msg = $"선각 표준부서 지정 규칙 적용 오류\n{ex.Message}";
            }


            // 의장 표준부서 적용 ----------------------------------------------------------------------------------
            if (_Manager == null)
            {
                _Manager = new OutfitMSManager(null, new HHIMasterDataService(), new HHIHullMidPlanDataService(), outfitDataService);
            }

            DataRow shipInfo = ShipInfo(shp_cod, fig_shp);

            if (!shipInfo.Table.Columns.Contains("SHP_KND"))
                shipInfo.Table.Columns.Add("SHP_KND");
            if (!shipInfo.Table.Columns.Contains("DCK_COD"))
                shipInfo.Table.Columns.Add("DCK_COD");

            shipInfo["SHP_KND"] = row["SHP_KND"];
            shipInfo["DCK_COD"] = row["DCK_COD"];

            _Manager.SetShipInfo(shipInfo);
            _Manager.LoadWrkStdDptInfo();
                        
            if (_Manager.HasMilestoneInfoInDB() == false)
                return msg = $"{fig_shp} 호선은 절점정보가 생성되지 않은 호선입니다.\n절점생성후 조회하세요.";

            List<OutfitMSActivity> actList = _Manager.GetActivityList();


            if(_Manager.HasActivityData() == false)
            {
                ShowWaitForm("의장 Activity를 조회 중입니다...");
                _Manager.LoadData(this.ToString(cboFIGNO.EditValue));
                actList = _Manager.GetActivityList();
            }

            if (_Manager != null && _Manager.HasActivityData())
            {
                List<string> noStdDptActList = new List<string>(); //190715 신성훈 추가 누락리스트 표기

                double cnt = 0;
                double totCnt = actList.Count;

                //foreach (var act in actList)
                Parallel.ForEach(actList, act =>
                {
                    ShowWaitForm($"의장 표준 부서를 적용중입니다... {Math.Round(cnt++ / totCnt * 100, 0)}%  {cnt}/{totCnt}");

                    if (act.FixedDptYn == false)
                    {
                        string stdDpt = GetStdDpt(act, shp_cod);
                        if (stdDpt != act.DptCod)
                        {
                            if (string.IsNullOrEmpty(stdDpt))
                            {
                                noStdDptActList.Add(act.ActCod); //190715 신성훈 추가 누락리스트 표기
                                                                 //return msg = $"{act.ActCod}의 표준부서를 찾을 수 없습니다.";
                            }
                            else
                            {
                                //19.07.26 신성훈 부서변경 시 계약여부 체크 로직 추가
                                //Y = 외주계약, N = 직영실적, NA = 변경가능
                                string CtrtChk = ContractYnChk(act);

                                if (CtrtChk == "Y")
                                {
                                    throw new Exception($"의장 {act.ActCod}의 외주계약 발생. 현업에 수정계약 요망.");
                                }
                                else if (CtrtChk == "N")
                                {
                                    throw new Exception($"의장 {act.ActCod}의 직영실적발생. 현업 조치 요망.");
                                }
                                else if (CtrtChk == "NA")
                                    ChangeDptCod(act, stdDpt);
                            }
                        }
                    }
                });
                
                // 표준부서를 찾을 수 없는 Act 화면에 출력 190715 신성훈 추가 누락리스트 표기
                if (noStdDptActList.Count > 0)
                {
                    string actCodListStr = ListToFlat(noStdDptActList);
                    if (actCodListStr.Length > 40)
                        actCodListStr = actCodListStr.Substring(0, 40);
                    
                    return msg = $"의장 Act의 표준부서를 찾을 수 없습니다.\n{actCodListStr}...";
                }
            }

            return msg;
        }

        DataRow ShipInfo(string shp_cod, string fig_shp)
        {
            DataTable dt = new DataTable();
            dt.Columns.Add("SHP_COD", typeof(string));
            dt.Columns.Add("FIG_SHP", typeof(string));

            DataRow row = dt.NewRow();
            row.ItemArray = new object[] { shp_cod, fig_shp };

            return row;
        }

        string ListToFlat(IEnumerable<string> arr, string delimiter = ",", string wrapper = "")
        {
            string rtn = "";

            foreach(string str in arr)
                rtn += str + delimiter;

            return rtn;
        }

        /// <summary>
        /// Act 목록에서 부서 변경
        /// </summary>
        /// <param name="act"></param>
        /// <param name="dptCod"></param>
        private void ChangeDptCod(OutfitMSActivity act, string dptCod)
        {
            if (this._Manager != null)
            {
                this._Manager.ChangeDptCod(act, dptCod);
            }
        }

        //19.07.25 신성훈 부서변경 시 계약여부 확인 로직 추가.
        private string ContractYnChk(OutfitMSActivity act)
        {
            if (act != null && this._Manager != null)
            {
                //dptCod = outfitDataService.GetOutfitStdDpt(act.ShipInfo, chgFltAct, act.Row);       
                string ctrtChkYnRslt = outfitDataService.ContractYnChkMtd(act.Row);
                return ctrtChkYnRslt;
            }
            else
                return string.Empty;
        }

        private string GetStdDpt(OutfitMSActivity act, string shp_cod)
        {
            string dptCod = string.Empty;
            if (act != null && this._Manager != null)
            {
                //if (act.IsBlkAct())
                // 구획 Act가 아니면
                if (act.IsDivAct() == false)
                {
                    //전체 선행액트 대상이 아닌 선택 액트의 ITM_COD를 이용해 필터링한 선행액트를 가지고 작업 수행하도록 변경
                    //기존 전체 선행액트 조회로직 주석처리. 19.07.16 신성훈
                    //var hullActs = this.manager.GetHullActivityList().Select(e => e.Row);
                    var fltAct = this._Manager.GetFilteredHullActList(act.ShipInfo["FIG_SHP"], act.ItemCod, cboFIGNO.EditValue.ToString());
                    var chgFltAct = this._Manager.GetFilteredActivityList().Select(e => e.Row);

                    //전체 선행액트 대상이 아닌 선택 액트의 ITM_COD를 이용해 필터링한 선행액트를 가지고 작업 수행하도록 변경
                    //기존 전체 선행액트 조회로직 주석처리. 19.07.16 신성훈
                    //if (hullActs != null)
                    //{                       
                    //    dptCod = outfitDataService.GetOutfitStdDpt(act.ShipInfo, hullActs, act.Row);
                    //dptCod = outfitDataService.GetOutfitStdDpt(act.ShipInfo, hullAct.Row, act.Row);
                    //}
                    if (chgFltAct != null)
                    {
                        dptCod = outfitDataService.GetOutfitStdDpt(act.ShipInfo, chgFltAct, act.Row);
                    }
                    if (string.IsNullOrEmpty(dptCod))
                    {
                        dptCod = outfitDataService.GetOutfitStdDpt(shp_cod, act.ActCod);
                    }
                }
                else
                {
                    dptCod = outfitDataService.GetOutfitStdDpt(shp_cod, act.ActCod);
                }
            }

            return dptCod;
        }

        private void btnExcel_Click(object sender, EventArgs e)
        {
            if (!grdMain.WdxIsDataExists)
            {
                MessageBox.Show("엑셀로 출력할 데이터가 없습니다.");
                return;
            }

            SaveFileDialog dlg = new SaveFileDialog();
            dlg.Filter = "Excel통합문서 (.xls)|*.xls"; //"Excel통합문서(*.xlsx,*.xls)|*.xlsx;*.xls";
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                grdMain.ExportToXls(dlg.FileName);
            }
        }


        #endregion ################################## 버튼 이벤트 End

        #region ##################################### Function Start

        int TOTAL_CNT = 0;
        int TOTAL_ERR = 0;
        int TOTAL_GBN = 10;
        string PRECESSING_GBN = "";
        public async Task<int> Run1()
        {
            var ctx = UserInfoContext.Current;

            await Task.Run((() =>
            {
                ctx.SetCallContext();

                PRECESSING_GBN += "1,";

                QueryParameterCollection param1 = new QueryParameterCollection();
                param1.Add("GUBN", 1);
                param1.Add("FIG_NO", cboFIGNO.EditValue);
                param1.Add("USRID", UserID);
                
                QueryResponse response = this.ExecuteNonQuery("PKG_PP009_LTS004.COPY_DEL_SHIPTICKETDATA", param1, QueryServiceTransactions.TxLocal, "SHP.PP008.QueryAgent");

                string msg = response.Parameters["O_APP_MSG"] as string;
                
                if (!msg.Contains("OK"))
                {
                    MessageBox.Show($"1. 사업계획 데이터 복사 중 오류가 발생하였습니다.\n\n[오류]\n{msg}", "알림", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    TOTAL_CNT = TOTAL_GBN;
                    TOTAL_ERR++;
                }
                else
                {
                    TOTAL_CNT++;
                    PRECESSING_GBN = PRECESSING_GBN.Replace("1,", "");
                }

            }));

            return 1;
        }
        public async Task<int> Run2()
        {
            var ctx = UserInfoContext.Current;

            await Task.Run((() =>
            {
                ctx.SetCallContext();

                PRECESSING_GBN += "2,";

                QueryParameterCollection param1 = new QueryParameterCollection();
                param1.Add("GUBN", 2);
                param1.Add("FIG_NO", cboFIGNO.EditValue);
                param1.Add("USRID", UserID);
                
                QueryResponse response = this.ExecuteNonQuery("PKG_PP009_LTS004.COPY_DEL_SHIPTICKETDATA", param1, QueryServiceTransactions.TxLocal, "SHP.PP008.QueryAgent");

                string msg = response.Parameters["O_APP_MSG"] as string;

                if (!msg.Contains("OK"))
                {
                    MessageBox.Show($"2. 사업계획 데이터 복사 중 오류가 발생하였습니다.\n\n[오류]\n{msg}", "알림", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    TOTAL_CNT = TOTAL_GBN;
                    TOTAL_ERR++;
                }
                else
                {
                    TOTAL_CNT++;
                    PRECESSING_GBN = PRECESSING_GBN.Replace("2,", "");
                }
            }));
            
            return 1;
        }

        public async Task<int> Run3()
        {
            var ctx = UserInfoContext.Current;

            await Task.Run((() =>
            {
                ctx.SetCallContext();

                PRECESSING_GBN += "3,";

                QueryParameterCollection param1 = new QueryParameterCollection();
                param1.Add("GUBN", 3);
                param1.Add("FIG_NO", cboFIGNO.EditValue);
                param1.Add("USRID", UserID);
                
                QueryResponse response = this.ExecuteNonQuery("PKG_PP009_LTS004.COPY_DEL_SHIPTICKETDATA", param1, QueryServiceTransactions.TxLocal, "SHP.PP008.QueryAgent");

                string msg = response.Parameters["O_APP_MSG"] as string;

                if (!msg.Contains("OK"))
                {
                    MessageBox.Show($"3. 사업계획 데이터 복사 중 오류가 발생하였습니다.\n\n[오류]\n{msg}", "알림", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    TOTAL_CNT = TOTAL_GBN;
                    TOTAL_ERR++;
                }
                else
                {
                    TOTAL_CNT++;
                    PRECESSING_GBN = PRECESSING_GBN.Replace("3,", "");
                }
            }));
            
            return 1;
        }

        public async Task<int> Run4()
        {
            var ctx = UserInfoContext.Current;

            await Task.Run((() =>
            {
                ctx.SetCallContext();

                PRECESSING_GBN += "4,";

                QueryParameterCollection param1 = new QueryParameterCollection();
                param1.Add("GUBN", 4);
                param1.Add("FIG_NO", cboFIGNO.EditValue);
                param1.Add("USRID", UserID);
                
                QueryResponse response = this.ExecuteNonQuery("PKG_PP009_LTS004.COPY_DEL_SHIPTICKETDATA", param1, QueryServiceTransactions.TxLocal, "SHP.PP008.QueryAgent");

                string msg = response.Parameters["O_APP_MSG"] as string;

                if (!msg.Contains("OK"))
                {
                    MessageBox.Show($"4. 사업계획 데이터 복사 중 오류가 발생하였습니다.\n\n[오류]\n{msg}", "알림", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    TOTAL_CNT = TOTAL_GBN;
                    TOTAL_ERR++;
                }
                else
                {
                    TOTAL_CNT++;
                    PRECESSING_GBN = PRECESSING_GBN.Replace("4,", "");
                }
            }));
            
            return 1;
        }

        public async Task<int> Run5()
        {
            var ctx = UserInfoContext.Current;

            await Task.Run((() =>
            {
                ctx.SetCallContext();

                PRECESSING_GBN += "5,";

                QueryParameterCollection param1 = new QueryParameterCollection();
                param1.Add("GUBN", 5);
                param1.Add("FIG_NO", cboFIGNO.EditValue);
                param1.Add("USRID", UserID);
                
                QueryResponse response = this.ExecuteNonQuery("PKG_PP009_LTS004.COPY_DEL_SHIPTICKETDATA", param1, QueryServiceTransactions.TxLocal, "SHP.PP008.QueryAgent");

                string msg = response.Parameters["O_APP_MSG"] as string;

                if (!msg.Contains("OK"))
                {
                    MessageBox.Show($"5. 사업계획 데이터 복사 중 오류가 발생하였습니다.\n\n[오류]\n{msg}", "알림", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    TOTAL_CNT = TOTAL_GBN;

                    TOTAL_ERR++;
                }
                else
                {
                    TOTAL_CNT++;
                    PRECESSING_GBN = PRECESSING_GBN.Replace("5,", "");
                }
            }));
            
            return 1;
        }
        public async Task<int> Run6()
        {
            var ctx = UserInfoContext.Current;

            await Task.Run((() =>
            {
                ctx.SetCallContext();

                PRECESSING_GBN += "6,";

                QueryParameterCollection param1 = new QueryParameterCollection();
                param1.Add("GUBN", 6);
                param1.Add("FIG_NO", cboFIGNO.EditValue);
                param1.Add("USRID", UserID);
                
                QueryResponse response = this.ExecuteNonQuery("PKG_PP009_LTS004.COPY_DEL_SHIPTICKETDATA", param1, QueryServiceTransactions.TxLocal, "SHP.PP008.QueryAgent");

                string msg = response.Parameters["O_APP_MSG"] as string;

                if (!msg.Contains("OK"))
                {
                    MessageBox.Show($"6. 사업계획 데이터 복사 중 오류가 발생하였습니다.\n\n[오류]\n{msg}", "알림", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    TOTAL_CNT = TOTAL_GBN;
                    TOTAL_ERR++;
                }
                else
                {
                    TOTAL_CNT++;
                    PRECESSING_GBN = PRECESSING_GBN.Replace("6,", "");
                }
            }));
            
            return 1;
        }
        public async Task<int> Run7()
        {
            var ctx = UserInfoContext.Current;

            await Task.Run((() =>
            {
                ctx.SetCallContext();

                PRECESSING_GBN += "7,";

                QueryParameterCollection param1 = new QueryParameterCollection();
                param1.Add("GUBN", 7);
                param1.Add("FIG_NO", cboFIGNO.EditValue);
                param1.Add("USRID", UserID);
                
                QueryResponse response = this.ExecuteNonQuery("PKG_PP009_LTS004.COPY_DEL_SHIPTICKETDATA", param1, QueryServiceTransactions.TxLocal, "SHP.PP008.QueryAgent");

                string msg = response.Parameters["O_APP_MSG"] as string;

                if (!msg.Contains("OK"))
                {
                    MessageBox.Show($"7. 사업계획 데이터 복사 중 오류가 발생하였습니다.\n\n[오류]\n{msg}", "알림", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    TOTAL_CNT = TOTAL_GBN;
                    TOTAL_ERR++;
                }
                else
                {
                    TOTAL_CNT++;
                    PRECESSING_GBN = PRECESSING_GBN.Replace("7,", "");
                }
            }));
            
            return 1;
        }
        public async Task<int> Run8()
        {
            var ctx = UserInfoContext.Current;

            await Task.Run((() =>
            {
                ctx.SetCallContext();

                PRECESSING_GBN += "8,";

                QueryParameterCollection param1 = new QueryParameterCollection();
                param1.Add("GUBN", 8);
                param1.Add("FIG_NO", cboFIGNO.EditValue);
                param1.Add("USRID", UserID);
                
                QueryResponse response = this.ExecuteNonQuery("PKG_PP009_LTS004.COPY_DEL_SHIPTICKETDATA", param1, QueryServiceTransactions.TxLocal, "SHP.PP008.QueryAgent");

                string msg = response.Parameters["O_APP_MSG"] as string;

                if (!msg.Contains("OK"))
                {
                    MessageBox.Show($"8. 사업계획 데이터 복사 중 오류가 발생하였습니다.\n\n[오류]\n{msg}", "알림", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    TOTAL_CNT = TOTAL_GBN;
                    TOTAL_ERR++;
                }
                else
                {
                    TOTAL_CNT++;
                    PRECESSING_GBN = PRECESSING_GBN.Replace("8,", "");
                }
            }));
            
            return 1;
        }
        public async Task<int> Run9()
        {
            var ctx = UserInfoContext.Current;

            await Task.Run((() =>
            {
                ctx.SetCallContext();

                PRECESSING_GBN += "9,";

                QueryParameterCollection param1 = new QueryParameterCollection();
                param1.Add("GUBN", 9);
                param1.Add("FIG_NO", cboFIGNO.EditValue);
                param1.Add("USRID", UserID);
                
                QueryResponse response = this.ExecuteNonQuery("PKG_PP009_LTS004.COPY_DEL_SHIPTICKETDATA", param1, QueryServiceTransactions.TxLocal, "SHP.PP008.QueryAgent");

                string msg = response.Parameters["O_APP_MSG"] as string;

                if (!msg.Contains("OK"))
                {
                    MessageBox.Show($"9. 사업계획 데이터 복사 중 오류가 발생하였습니다.\n\n[오류]\n{msg}", "알림", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    TOTAL_CNT = TOTAL_GBN;
                    TOTAL_ERR++;
                }
                else
                {
                    TOTAL_CNT++;
                    PRECESSING_GBN = PRECESSING_GBN.Replace("9,", "");
                }
            }));
            
            return 1;
        }
        public async Task<int> Run10()
        {
            var ctx = UserInfoContext.Current;

            await Task.Run((() =>
            {
                ctx.SetCallContext();

                PRECESSING_GBN += "10,";

                QueryParameterCollection param1 = new QueryParameterCollection();
                param1.Add("GUBN", 10);
                param1.Add("FIG_NO", cboFIGNO.EditValue);
                param1.Add("USRID", UserID);
                
                QueryResponse response = this.ExecuteNonQuery("PKG_PP009_LTS004.COPY_DEL_SHIPTICKETDATA", param1, QueryServiceTransactions.TxLocal, "SHP.PP008.QueryAgent");

                string msg = response.Parameters["O_APP_MSG"] as string;

                if (!msg.Contains("OK"))
                {
                    MessageBox.Show($"10. 사업계획 데이터 복사 중 오류가 발생하였습니다.\n\n[오류]\n{msg}", "알림", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    TOTAL_CNT = TOTAL_GBN;
                    TOTAL_ERR++;
                }
                else
                {
                    TOTAL_CNT++;
                    PRECESSING_GBN = PRECESSING_GBN.Replace("10,", "");
                }
            }));
            
            return 1;
        }

        #endregion ################################## Function End

        #region ##################################### Event Start

        private void Pop_WdxSendDataEvent(object sender, WdxSendDataEventArgs e)
        {
            if (e.Parameter == null)
                return;

            DataRow sourceRow = e.Parameter as DataRow;
            string gbn = sourceRow["GBN"].ToString();

            // 체크박스에 체크하지 않은 경우 
            if (!grdMain.WdxIsChecked)
            {
                SetPopReturnParam(gbn, grvMain.GetFocusedDataRow(), sourceRow);
            }
            // 체크박스를 선택한 경우
            else
            {
                foreach (DataRow targetRow in grdMain.WdxGetCheckedRows())
                {
                    SetPopReturnParam(gbn, targetRow, sourceRow);
                }
            }
        }

        void SetPopReturnParam(string gbn, DataRow targetRow, DataRow sourceRow)
        {
            // 모델선 -> 팝업 내용 적용
            // 호선 -> 기존 로직
            // 모델선인 경우 rundata = "N"
            string gubunSM = cboGBN.EditValue.ToString();

            // 호선인 경우
            if(gubunSM.Equals("S"))
            {
                // 팝업에서 선각(S)을 선택한 경우, A : 선각, 의장 모두 선택
                if (gbn == "S" || gbn == "A")
                {
                    targetRow["CHK"] = "Y"; // 행을 선택함.
                    targetRow["ERECSHIFT"] = "Y";

                    if (targetRow["RUNDATA"].ToString() == "Y")
                    {
                        targetRow["CPY_FIG_NO"] = "999999999";
                        targetRow["CPY_SHP"] = sourceRow["FIG_SHP"];
                    }
                    else
                    {
                        targetRow["CPY_FIG_NO"] = sourceRow["FIG_NO"];
                        targetRow["CPY_SHP"] = sourceRow["FIG_SHP"];
                        targetRow["SUM_MUL_WGT2"] = sourceRow["SUM_MUL_WGT"];
                        targetRow["FIG_DES"] = sourceRow["FIG_DES"];
                        targetRow["A_SHP_DES"] = sourceRow["A_SHP_DES"];
                    }
                }

                // 의장(O)을 선택한 경우, A : 선각, 의장 모두 선택
                if (gbn == "O" || gbn == "A")
                {
                    targetRow["ERECSHIFT"] = "Y";

                    if (targetRow["RUNDATA"].ToString() == "Y")
                    {
                        targetRow["CPY_FIG_NO_OFT"] = "999999999";
                        targetRow["CPY_FIG_SHP_OFT"] = sourceRow["FIG_SHP"];
                    }
                    else
                    {
                        targetRow["CPY_FIG_NO_OFT"] = sourceRow["FIG_NO"];
                        targetRow["CPY_FIG_SHP_OFT"] = sourceRow["FIG_SHP"];
                        targetRow["FIG_DES_OFT"] = sourceRow["FIG_DES"];
                        targetRow["A_SHP_DES_OFT"] = sourceRow["A_SHP_DES"];
                    }
                }

            }
            // 모델선인 경우
            else
            {
                // 팝업에서 선각(S)을 선택한 경우, A : 선각, 의장 모두 선택
                if (gbn == "S" || gbn == "A")
                {
                    targetRow["CHK"] = "Y"; // 행을 선택함.
                    targetRow["ERECSHIFT"] = "N";

                    targetRow["CPY_FIG_NO"] = sourceRow["FIG_NO"];
                    targetRow["CPY_SHP"] = sourceRow["FIG_SHP"];
                    targetRow["SUM_MUL_WGT2"] = sourceRow["SUM_MUL_WGT"];
                    targetRow["FIG_DES"] = sourceRow["FIG_DES"];
                    targetRow["A_SHP_DES"] = sourceRow["A_SHP_DES"];
                }

                // 의장(O)을 선택한 경우, A : 선각, 의장 모두 선택
                if (gbn == "O" || gbn == "A")
                {
                    targetRow["CPY_FIG_NO_OFT"] = sourceRow["FIG_NO"];
                    targetRow["CPY_FIG_SHP_OFT"] = sourceRow["FIG_SHP"];
                    targetRow["A_SHP_DES_OFT"] = sourceRow["A_SHP_DES"];
                    targetRow["FIG_DES_OFT"] = sourceRow["FIG_DES"];
                }
            }
        }

        private void grvMain_CellValueChanging(object sender, DevExpress.XtraGrid.Views.Base.CellValueChangedEventArgs e)
        {
            DataRow dr = grvMain.GetFocusedDataRow();

            if(dr == null)
                return;

            if (e.Column.FieldName == "CHK")
            {
                dr["ERECSHIFT"] = e.Value;
            }
            // 진행호선 적용
            else if (e.Column.FieldName == "RUN_SHIP_INDC")
            {
                string value = e.Value.ToString();
                string fig_no = dr["FIG_SHP"].ToString();
                
                if(value.Equals("Y"))
                {
                    // Check시 선각/의장 모델선표에‘999999999’ 적용
                    dr["CPY_FIG_NO"] = "999999999";
                    dr["CPY_FIG_NO_OFT"] = "999999999";

                    // 선각/의장 모델호선에 대상호선 표시
                    dr["CPY_SHP"] = fig_no;
                    dr["CPY_FIG_SHP_OFT"] = fig_no;

                    // Check시 화면 소스 내 RUNDATA = ‘Y’ 처리
                    dr["RUNDATA"] = "Y";
                }
                else
                {
                    // UnCheck시 선각/의장 모델선표 및 모델호선 ‘’처리
                    dr["CPY_FIG_NO"] = "";
                    dr["CPY_FIG_NO_OFT"] = "";

                    dr["CPY_SHP"] = "";
                    dr["CPY_FIG_SHP_OFT"] = "";

                    // UnCheck시 화면 소스 내 RUNDATA = ‘N’ 처리
                    dr["RUNDATA"] = "N";
                }

            }
            else if (e.Column.FieldName == "CPY_FIG_NO" || e.Column.FieldName == "CPY_SHP")
            {
                if (dr != null)
                {
                    dr["FIG_DES"] = string.Empty;
                    dr["A_SHP_DES"] = string.Empty;

                    if (dr["RUNDATA"].ToString() == "Y")
                    {
                        dr["CPY_FIG_NO"] = "999999999"; // 선각 모델 선표
                        dr["CPY_SHP"] = dr["FIG_SHP"];  // 선각 모델 호선
                    }
                }
            }
        }

        private void grvMain_RowStyle(object sender, DevExpress.XtraGrid.Views.Grid.RowStyleEventArgs e)
        {
            GridView View = sender as GridView;

            if (e.RowHandle >= 0)
            {
                string HasCopyShip = View.GetRowCellValue(e.RowHandle, "HASCOPYSHIP").ToString();
                string state = View.GetRowCellValue(e.RowHandle, "WORKING_STATE").ToString();

                if (string.IsNullOrEmpty(HasCopyShip))
                {
                    e.Appearance.BackColor = Color.Yellow;
                }
                else if (state == "D")
                {
                    e.Appearance.BackColor = Color.Red;
                }
                else if (state == "F")
                {
                    e.Appearance.BackColor = Color.Orange;
                }
            }
        }
        
        private void chkRundata_CheckedChanged(object sender, EventArgs e)
        {
            UpdateHasRunSelect(chkRundata.Checked);
        }


        #endregion ################################## Event End

        #region ##################################### ToolStripMenuItem Event Start

        private void ToolStripMenuItem_Select_ClickEvent(object sender, EventArgs e)
        {
            int[] handles = grvMain.GetSelectedRows();

            DataTable dt = grdMain.DataSource as DataTable;

            foreach (int handle in handles)
            {
                var rowIdx = grvMain.GetDataSourceRowIndex(handle);
                dt.Rows[rowIdx]["CHK"] = "Y";
                dt.Rows[rowIdx]["ERECSHIFT"] = "Y";
                //grvMain.SetRowCellValue(handle, "CHK", "Y");
            }

         (grdMain.DataSource as DataTable).AcceptChanges();
        }

        private void ToolStripMenuItem_UnSelect_ClickEvent(object sender, EventArgs e)
        {
            int[] handles = grvMain.GetSelectedRows();
            DataTable dt = grdMain.DataSource as DataTable;

            foreach (int handle in handles)
            {
                var rowIdx = grvMain.GetDataSourceRowIndex(handle);
                dt.Rows[rowIdx]["CHK"] = "N";
                dt.Rows[rowIdx]["ERECSHIFT"] = "N";
                //grvMain.SetRowCellValue(handle, "CHK", "N");
            }

            (grdMain.DataSource as DataTable).AcceptChanges();
        }

        private void ToolStripMenuItem_Delete_ClickEvent(object sender, EventArgs e)
        {
            int[] handles = grvMain.GetSelectedRows();
            DataTable dt = grdMain.DataSource as DataTable;

            foreach (int handle in handles)
            {
                var rowIdx = grvMain.GetDataSourceRowIndex(handle);
                dt.Rows[rowIdx]["CHK"] = "Y";
                dt.Rows[rowIdx]["WORKING_STATE"] = "D";
                //grvMain.SetRowCellValue(handle, "CHK", "Y");
                //grvMain.SetRowCellValue(handle, "WORKING_STATE", "D");
            }

            (grdMain.DataSource as DataTable).AcceptChanges();
        }

        private void ToolStripMenuItem_UnDelete_ClickEvent(object sender, EventArgs e)
        {
            int[] handles = grvMain.GetSelectedRows();
            DataTable dt = grdMain.DataSource as DataTable;

            foreach (int handle in handles)
            {
                var rowIdx = grvMain.GetDataSourceRowIndex(handle);
                dt.Rows[rowIdx]["CHK"] = "Y";
                dt.Rows[rowIdx]["WORKING_STATE"] = "N";
                //grvMain.SetRowCellValue(handle, "CHK", "N");
                //grvMain.SetRowCellValue(handle, "WORKING_STATE", "N");
            }

            (grdMain.DataSource as DataTable).AcceptChanges();
        }

        private void ToolStripMenuItem_ErecShift_ClickEvent(object sender, EventArgs e)
        {
            int[] handles = grvMain.GetSelectedRows();
            DataTable dt = grdMain.DataSource as DataTable;

            foreach (int handle in handles)
            {
                var rowIdx = grvMain.GetDataSourceRowIndex(handle);
                if (dt.Rows[rowIdx]["CHK"].ToString() == "Y")
                    dt.Rows[rowIdx]["ERECSHIFT"] = "Y";

                //if (grvMain.GetRowCellValue(handle, "CHK").ToString() == "Y")
                //    grvMain.SetRowCellValue(handle, colERECSHIFT, "Y");
            }

           (grdMain.DataSource as DataTable).AcceptChanges();
        }

        private void ToolStripMenuItem_UnErecShift_ClickEvent(object sender, EventArgs e)
        {
            int[] handles = grvMain.GetSelectedRows();
            DataTable dt = grdMain.DataSource as DataTable;

            foreach (int handle in handles)
            {
                var rowIdx = grvMain.GetDataSourceRowIndex(handle);
                dt.Rows[rowIdx]["ERECSHIFT"] = "N";
                //grvMain.SetRowCellValue(handle, "CHK", "N");
                //grvMain.SetRowCellValue(handle, colERECSHIFT, "N");
            }

            (grdMain.DataSource as DataTable).AcceptChanges();
        }

        #endregion ################################## ToolStripMenuItem Event End


        // 표준 부서 체크된 항목을 가져온다.
        // 표준부서 지정 규칙 적용
        // 선택 Activity에 대해서 선행 및 후행 Activity에 대한 표준부서 지정 규칙 적용
        private void ApplyStandardDept()
        {
            var rows = grdMain.WdxGetCheckedRows();

            if (rows == null || rows.Length == 0)
            {
                MessageBox.Show(this, "선택된 내역이 없습니다.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            //string workStage = string.Empty; // 공정
            //List<string> workTypes = new List<string>(); // 공종

            //QueryParameterCollection param = new QueryParameterCollection();
            //param.Add("USER_ID", this.UserID);
            //DataTable dt = this.ExecuteSelect("PKG_PP009_LTS004.GETACTLIST", param);
        }

        // 999999999 은 진행으로 표시
        private void grvMain_CustomColumnDisplayText(object sender, DevExpress.XtraGrid.Views.Base.CustomColumnDisplayTextEventArgs e)
        {
            if ("CPY_FIG_NO".Equals(e.Column.FieldName) || "CPY_FIG_NO_OFT".Equals(e.Column.FieldName))
            {
                if ($"{e.Value}" == "999999999")
                {
                    e.DisplayText = "진행";
                }
            }
        }

        private void cboGBN_SelectedIndexChanged(object sender, EventArgs e)
        {
            string prjgbn = "";

            grdMain.DataSource = null;

            if (cboGBN.EditValue.ToString().Equals("M"))
            {
                _prjgbn = prjgbn = "MODEL";

                bandTarget.Caption = "대상모델선";
                bandTarget.Width = 120;
            }
            else
            {
                bandTarget.Caption = "대상호선";
                bandTarget.Width = 80;

                if (_dtAuth != null && _dtAuth.Rows.Count > 0)
                {
                    _prjgbn = prjgbn = _dtAuth.Rows[0]["PRJ_GBN"].ToString();
                }
            }

            QueryParameterCollection param = new QueryParameterCollection();
            param.Add("PRJ_GBN", prjgbn);
            DataTable dt = this.ExecuteSelect("PKG_PP009_LTS004.TSAC003_FIG_NO_LIST", param);

            if (dt != null && dt.Rows.Count > 0)
            {
                cboFIGNO.WdxBindItem("FIG_NO", "FIG_NO", "{value}", "", "", WdxComboDisplayTextOption.none, dt);
                cboFIGNO.SelectedIndex = 0;
            }
        }

        private void grvMain_ShowingEditor(object sender, CancelEventArgs e)
        {
            if (grvMain.FocusedColumn.FieldName.EndsWith("RUN_SHIP_INDC") == false)
                return;

            DataRow dr = grvMain.GetFocusedDataRow();

            string value = dr["RUN_SHIP_INDC"].ToString();
            string fig_no = dr["FIG_SHP"].ToString();

            // 미정선(대상호선이 U로 시작)은 Check 불가
            if (fig_no.StartsWith("U"))
            {
                e.Cancel = true;
                return;
            }
        }

        private void grvMain_RowCellClick(object sender, RowCellClickEventArgs e)
        {
            //btnSrcSearch_Click(null, null);
            //return;

            if (grvMain.FocusedRowHandle < 0)
                return;

            string SHP_KND;     // 선종
            string SHP_TYP_QTY; // 선형
            string DCK_COD;     // 도크 
            string DCK_BLD_COD; // 건조방식

            DataRow[] checkedRows = new DataRow[1];
            checkedRows[0] = grvMain.GetFocusedDataRow();

            SHP_KND = checkedRows[0]["SHP_KND"].ToString();
            SHP_TYP_QTY = checkedRows[0]["SHP_TYP_QTY"].ToString();
            DCK_COD = checkedRows[0]["DCK_COD"].ToString();
            DCK_BLD_COD = checkedRows[0]["DCK_BLD_COD"].ToString();

            SHP_KND = chkShipKind.Checked ? SHP_KND : "";
            SHP_TYP_QTY = chkShipType.Checked ? SHP_TYP_QTY : "";
            DCK_COD = chkDockCode.Checked ? DCK_COD : "";
            DCK_BLD_COD = chkBLDCode.Checked ? DCK_BLD_COD : "";

            QueryParameterCollection param = new QueryParameterCollection();
            param.Add("SRC_FIG_NO", cboSRCFIGNO.EditValue);
            param.Add("PRJ_GBN", _prjgbn);

            param.Add("SHP_KND", SHP_KND);
            param.Add("SHP_TYP_QTY", SHP_TYP_QTY);
            param.Add("DCK_COD", DCK_COD);
            param.Add("BLD_COD", DCK_BLD_COD);

            DataTable dt = null;

            if (cboSRCFIGNO.EditValue.ToString() == "MODEL")
                dt = this.ExecuteSelect(grdSub, "PKG_PP009_LTS004.TSAD001_FIG_NO_MODEL_SELECT2", param);
            else
                dt = this.ExecuteSelect(grdSub, "PKG_PP009_LTS004.TSAD001_FIG_NO_SELECT2", param);
        }

        private void grvSub_DoubleClick(object sender, EventArgs e)
        {
            DataRow row = grvSub.GetFocusedDataRow();

            if (row == null)
                return;

            string chkValue = "";

            if (chkShip.Checked && !chkOutfit.Checked)
                chkValue = "S";
            else if (chkShip.Checked && chkOutfit.Checked)
                chkValue = "A";
            else if (!chkShip.Checked && chkOutfit.Checked)
                chkValue = "O";
            else if (!chkShip.Checked && !chkOutfit.Checked)
            {
                MsgBox.Show("선각, 의장 중 한 개 이상을 선택하세요.", "알림", MessageBoxButtons.OK, ImageKinds.Warnning);
                return;
            }

            if(!row.Table.Columns.Contains("GBN"))
                row.Table.Columns.Add("GBN");
            if (!row.Table.Columns.Contains("CPY_FIG_NO"))
                row.Table.Columns.Add("CPY_FIG_NO");
            if (!row.Table.Columns.Contains("ERECSHIFT"))
                row.Table.Columns.Add("ERECSHIFT");

            row["GBN"] = chkValue;
            row["ERECSHIFT"] = "Y";
            row["CPY_FIG_NO"] = "999999999";

            string gbn = row["GBN"].ToString();

            SetPopReturnParam(gbn, grvMain.GetFocusedDataRow(), row);

            // 멀티 적용을 하지 않는다. 2022-01-24 
            //// 체크박스에 체크하지 않은 경우 
            //if (!grdMain.WdxIsChecked)
            //{
            //    SetPopReturnParam(gbn, grvMain.GetFocusedDataRow(), row);
            //}
            //// 체크박스를 선택한 경우
            //else
            //{
            //    foreach (DataRow targetRow in grdMain.WdxGetCheckedRows())
            //    {
            //        SetPopReturnParam(gbn, targetRow, row);
            //    }
            //}
        }
        #region void grv_Click(object sender, EventArgs e) : grv - Click
        private void grv_Click(object sender, EventArgs e)
        {
            var view = sender as Windows.Forms.DevExtension.Grid.WdxBandedGridView;
            var args = e as DevExpress.Utils.DXMouseEventArgs;
            if (args != null && args.Button == MouseButtons.Left)
            {
                var info = view.CalcHitInfo(mouseDownLocation) as DevExpress.XtraGrid.Views.BandedGrid.ViewInfo.BandedGridHitInfo;
                if (info != null && info.InBandPanel && info.HitTest == DevExpress.XtraGrid.Views.BandedGrid.ViewInfo.BandedGridHitTest.Band)
                {
                    var col = info.Band.Columns.OfType<DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn>().FirstOrDefault();
                    if (col != null && col.FieldName != "CHK")
                    {
                        var sortOrder = col.SortOrder;
                        view.SortInfo.Clear();
                        switch (sortOrder)
                        {
                            case DevExpress.Data.ColumnSortOrder.None:
                                view.SortInfo.AddRange(new[] { new DevExpress.XtraGrid.Columns.GridColumnSortInfo(col, DevExpress.Data.ColumnSortOrder.Ascending) });
                                break;
                            case DevExpress.Data.ColumnSortOrder.Ascending:
                                view.SortInfo.AddRange(new[] { new DevExpress.XtraGrid.Columns.GridColumnSortInfo(col, DevExpress.Data.ColumnSortOrder.Descending) });
                                break;
                            case DevExpress.Data.ColumnSortOrder.Descending:
                                sortOrder = DevExpress.Data.ColumnSortOrder.None;
                                view.SortInfo.Clear();
                                break;
                            default:
                                sortOrder = DevExpress.Data.ColumnSortOrder.None;
                                view.SortInfo.Clear();
                                break;
                        }
                    }

                }
            }
        }
        #endregion
        #region void grv_MouseDown(object sender, MouseEventArgs e) : grv - MouseDown
        private void grv_MouseDown(object sender, MouseEventArgs e)
        {
            var args = e as DevExpress.Utils.DXMouseEventArgs;
            if (args.Button == MouseButtons.Left) mouseDownLocation = args.Location;
        }
        #endregion

        #region void grv_CustomDrawBandHeader(object sender, DevExpress.XtraGrid.Views.BandedGrid.BandHeaderCustomDrawEventArgs e) : grv - CustomDrawBandHeader
        private void grv_CustomDrawBandHeader(object sender, DevExpress.XtraGrid.Views.BandedGrid.BandHeaderCustomDrawEventArgs e)
        {
            var view = sender as Windows.Forms.DevExtension.Grid.WdxBandedGridView;

            e.DefaultDraw();
            // 현재 값
            var col = e.Band?.Columns.OfType<DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn>().FirstOrDefault();
            if (col != null && col.FieldName != "CHK")
            {
                var sortOrder = col.SortOrder;
                // 정렬 이미지 크기
                var bound = new System.Drawing.Rectangle(e.Bounds.Right - 16 - 2, e.Bounds.Y + (e.Bounds.Height - 16) / 2, 16, 16);
                switch (sortOrder)
                {
                    case DevExpress.Data.ColumnSortOrder.None:
                        break;
                    case DevExpress.Data.ColumnSortOrder.Ascending:
                        e.Cache.DrawImage(Properties.Resources.sortasc_16x16, bound);
                        break;
                    case DevExpress.Data.ColumnSortOrder.Descending:
                        e.Cache.DrawImage(Properties.Resources.sortdesc_16x16, bound);
                        break;
                    default:
                        break;
                }
            }

            e.Handled = true;
        }
        #endregion
    }
}
