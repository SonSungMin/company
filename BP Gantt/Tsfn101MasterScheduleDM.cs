using Amisys.Infrastructure.Infrastructure.DataModels;
using Amisys.Infrastructure.Infrastructure.Interfaces;
using Amisys.Infrastructure.Infrastructure.Utility;
using Amisys.Presentation.AftMidPlan.AMPActivityManager.Utility;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Collections;
using Amisys.Presentation.AftMidPlan.AMPActivityManager.Definitions;
using Amisys.Component.Presentation.AmiSmartGantt;
using System.Data;

namespace Amisys.Presentation.AftMidPlan.AMPActivityManager.Models
{
    [Export(typeof(Tsfn101MasterScheduleDM))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    public class Tsfn101MasterScheduleDM
    {
        #region ctor
        [ImportingConstructor]
        public Tsfn101MasterScheduleDM()
        {

        }
        #endregion

        #region member variables

        #endregion

        #region property

        // 스케줄 옵션

        public AmpScheduleOption ScheduleOption
        {
            get { return _ScheduleOption; }
            set { _ScheduleOption = value; }
        }
        private AmpScheduleOption _ScheduleOption = new AmpScheduleOption();


        public IHHICalendar Calendar
        {
            get { return _Calendar; }
            set { _Calendar = value; }
        }
        private IHHICalendar _Calendar;


        public DataTSXA002List DptList
        {
            get { return _DptList; }
            set { _DptList = value; }
        }
        private DataTSXA002List _DptList;



        public DataTSFN101List ActList
        {
            get { return _ActList; }
            set { _ActList = value; }
        }
        private DataTSFN101List _ActList;

        /// <summary>
        /// Key = DIV_COD:ACT_COD
        /// </summary>
        public Dictionary<string, DataTSFN101> ActDict
        {
            get { return _ActDict; }
            set { _ActDict = value; }
        }
        private Dictionary<string, DataTSFN101> _ActDict;



        public DataTSFN102List LinkList
        {
            get { return _LinkList; }
            set { _LinkList = value; }
        }
        private DataTSFN102List _LinkList;


        // 절점 목록
        public DataTSDC002List WrkPntList
        {
            get { return _WrkPntList; }
            set { _WrkPntList = value; }
        }
        private DataTSDC002List _WrkPntList;

        // 절점 제약
        public DataTSFN105List WrkPntConstraints
        {
            get { return _WrkPntConstraints; }
            set { _WrkPntConstraints = value; }
        }
        private DataTSFN105List _WrkPntConstraints;


        // 절점 제약 간트 링크 리스트
        public WrkPntCnstLinkList WrkPntCnstLinks
        {
            get { return _WrkPntCnstLinks; }
            set { _WrkPntCnstLinks = value; }
        }
        private WrkPntCnstLinkList _WrkPntCnstLinks;



        /// <summary>
        /// 간트 유틸리티
        /// </summary>
        public GanttUtility GanttUtil
        {
            get { return _GanttUtil; }
            set { _GanttUtil = value; }
        }
        private GanttUtility _GanttUtil;

        #endregion

        #region 달력

        public void InitGanttUtil(DataTSFN009List patternList)
        {
            GanttUtil = new Utility.GanttUtility(patternList);
        }

        #endregion

        #region Act/Link

        public void InitDataSource(DataTSFN101List actList, DataTSFN102List linkList)
        {
            this.ActList = actList;
            this.LinkList = linkList;

            if (actList != null && linkList != null)
            {
                // ACT에 달력 설정하고 공기 계산, 필수
                foreach (var act in actList)
                {
                    act.Calendar = this.Calendar;
                    act.UpdatePlanDuration();
                }


                // 사전
                this.ActDict = actList.ToDictionary(e => MakeDictKey(e.DIV_COD, e.ACT_COD), e => e);

                // 관계 생성
                var tmpLinkList = linkList.ToList();
                foreach (var link in tmpLinkList)
                {
                    if (string.IsNullOrEmpty(link.PRE_ACT) == true || link.PRE_ACT.Length < 12
                        || string.IsNullOrEmpty(link.AFT_ACT) == true || link.AFT_ACT.Length < 12)
                        continue;

                    var isHullPre = Defs.IsHullAct(link.PRE_ACT.Substring(5, 1));
                    var isHullAft = Defs.IsHullAct(link.AFT_ACT.Substring(5, 1));

                    if (isHullPre == true && isHullAft == true)
                    {
                        // 선후행이 모두 선각일 경우
                        // ACT_COD 가 같은 모든 구획에 대해서 관계 생성
                        // 원본에 없으면 추가
                        var preActList = GetActList(link.PRE_ACT);
                        var aftActList = GetActList(link.AFT_ACT);
                        if (preActList != null && aftActList != null)
                        {
                            foreach (var preAct in preActList)
                            {
                                foreach (var aftAct in aftActList)
                                {
                                    // 구획이 같으면 연결
                                    if (preAct.DIV_COD == aftAct.DIV_COD)
                                    {
                                        // link의 구획이 다르면 신규로 생성해서 추가
                                        if (link.PRE_DIV != preAct.DIV_COD)
                                        {
                                            var tmpLink = CreateTempNStageLink(link, link.PRE_DIV);
                                            SetActLink(preAct, aftAct, tmpLink);
                                        }
                                        else
                                        {
                                            SetActLink(preAct, aftAct, link);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else if (isHullPre == true && isHullAft == false)
                    {
                        // 선행이 선각이고 후행이 의장이면
                        // 다른 구획의 Link만 생성
                        var preAct = GetAct(link.AFT_DIV, link.PRE_ACT);
                        var aftAct = GetAct(link.AFT_DIV, link.AFT_ACT);
                        if (preAct != null && aftAct != null)
                        {
                            if (link.PRE_DIV == link.AFT_DIV)
                            {
                                // 그냥 추가
                                SetActLink(preAct, aftAct, link);
                            }
                            else
                            {
                                // 생성하고 추가
                                var tmpLink = CreateTempNStageLink(link, link.AFT_DIV);
                                SetActLink(preAct, aftAct, tmpLink);
                            }
                        }
                    }
                    else if (isHullPre == false && isHullAft == true)
                    {
                        // 선행이 의장이고 후행이 선각이면
                        // 다른 구획의 Link만 생성
                        var preAct = GetAct(link.PRE_DIV, link.PRE_ACT);
                        var aftAct = GetAct(link.PRE_DIV, link.AFT_ACT);
                        if (preAct != null && aftAct != null)
                        {
                            if (link.PRE_DIV == link.AFT_DIV)
                            {
                                // 그냥 추가
                                SetActLink(preAct, aftAct, link);
                            }
                            else
                            {
                                // 생성하고 추가
                                var tmpLink = CreateTempNStageLink(link, link.PRE_DIV);
                                SetActLink(preAct, aftAct, tmpLink);
                            }
                        }
                    }
                    else
                    {
                        var preAct = GetAct(link.PRE_DIV, link.PRE_ACT);
                        var aftAct = GetAct(link.AFT_DIV, link.AFT_ACT);

                        if (preAct != null && aftAct != null)
                        {
                            SetActLink(preAct, aftAct, link);
                        }
                    }

                    // slack
                    if (link != null)
                        link.Slack = CalcSlack(link);
                }


                // act에 관계 연결
                // 선각 act는 모든 구획에 대해서 연결
                //foreach (var link in linkList)
                //{
                //    if (link.HO_GUBUN1 == Defs.HO_GUBUN_BEF_ACT && link.HO_GUBUN2 == Defs.HO_GUBUN_BEF_ACT)
                //    {
                //        // 선후모두 선각ACT면
                //        // ACT_COD가 같은 ACT 모두 연결
                //        var preActList = GetActList(link.PRE_ACT);
                //        var aftActList = GetActList(link.AFT_ACT);
                //        if (preActList != null && aftActList != null)
                //        {
                //            foreach (var preAct in preActList)
                //            {
                //                foreach (var aftAct in aftActList)
                //                {
                //                    // 구획이 같으면 연결
                //                    if (preAct.DIV_COD == aftAct.DIV_COD)
                //                    {
                //                        SetActLink(preAct, aftAct, link);
                //                    }
                //                }
                //            }
                //        }
                //    }
                //    else
                //    {
                //        var preAct = GetAct(link.PRE_DIV, link.PRE_ACT);
                //        var aftAct = GetAct(link.AFT_DIV, link.AFT_ACT);

                //        if (preAct != null && aftAct != null)
                //        {
                //            SetActLink(preAct, aftAct, link);
                //        }
                //    }
                //}
            }
        }

        private DataTSFN102 CreateTempNStageLink(DataTSFN102 link, string divCod)
        {
            var tmpLink = LinkList.AddItem(null);
            link.CopyTo(tmpLink);
            tmpLink.PRE_DIV = divCod;
            tmpLink.AFT_DIV = divCod;
            // 임시로 생성한 관계는 DB에 추가하지 않음
            tmpLink.Row.AcceptChanges();
            return tmpLink;
        }

        private void SetActLink(DataTSFN101 preAct, DataTSFN101 aftAct, DataTSFN102 link)
        {
            if (link != null)
            {
                link.PreAct = preAct;
                link.AftAct = aftAct;
            }

            if (preAct != null)
                preAct.AddAftLink(link);
            if (aftAct != null)
                aftAct.AddPreLink(link);
        }

        public void InitWrkPntList(DataTSDC002List wrkPntList)
        {
            this.WrkPntList = wrkPntList;

            // 일자가 갑쳐면 position을 늘려둠
            if (wrkPntList != null)
            {
                var posList = new SortedList<string, int>();
                foreach (var wrkPnt in wrkPntList.OrderBy(e => e.PNT_SEQ))
                {
                    if (posList.ContainsKey(wrkPnt.PLN_ST) == false)
                    {
                        posList.Add(wrkPnt.PLN_ST, 1);
                    }
                    else
                    {
                        wrkPnt.PositionInGroup = posList[wrkPnt.PLN_ST];
                        posList[wrkPnt.PLN_ST]++;
                    }
                }
            }
        }

        public void InitWrkPntConstraints(DataTSFN105List cnsts)
        {
            this._WrkPntConstraints = cnsts;
            if (ActList != null)
            {
                List<DataTSFN101> tmpActList = new List<DataTSFN101>();

                foreach (var cnst in cnsts)
                {
                    var actList = ActList.Where(e => e.ACT_COD == cnst.ACT_COD);
                    if (actList != null && actList.Count() > 0)
                    {
                        foreach (var act in actList)
                        {
                            act.AddWrkPntConstraint(cnst);
                            cnst.Act = act;
                        }
                        tmpActList.AddRange(actList);
                    }
                }

                // 절점 제약 링크
                this.WrkPntCnstLinks = new WrkPntCnstLinkList(_WrkPntConstraints.ToList());
            }
            else
            {
                WrkPntCnstLinks = new WrkPntCnstLinkList(null);
            }
        }


        /// <summary>
        /// ACT_COD가 같은 모든 ACT 리턴(선각)
        /// </summary>
        public List<DataTSFN101> GetActList(string actCod)
        {
            if (ActList != null)
            {
                return ActList.Where(e => e.ACT_COD == actCod).ToList();
            }
            return null;
        }


        public DataTSFN101 GetAct(string divCod, string actCod)
        {
            DataTSFN101 act = null;

            if (ActDict != null)
                ActDict.TryGetValue(MakeDictKey(divCod, actCod), out act);

            return act;
        }

        private string MakeDictKey(string divCod, string actCod)
        {
            return divCod + ":" + actCod;
        }

        public DataTSFN101 GetAct(string divCod, string wrkStg, string wrkTyp, string wrkTyp2)
        {
            if (ActList != null)
            {
                return ActList.FirstOrDefault(e => e.DIV_COD == divCod && e.WRK_STG == wrkStg && e.WRK_TYP == wrkTyp && e.WRK_TYP2 == wrkTyp2);
            }
            return null;
        }

        #endregion

        #region Validation

        public string Validate()
        {
            return string.Empty;
        }

        #endregion


        #region 간트 Act/Link

        public List<HHIDay> GetGanttCalendar(DataTSFN011 selCase, List<DataTSFN101> actList)
        {
            if (selCase != null && Calendar != null && actList != null && actList.Count > 0)
            {
                var minSt = actList.Where(e => string.IsNullOrEmpty(e.PLN_ST) == false).Min(e => e.PLN_ST);
                var maxFi = actList.Where(e => string.IsNullOrEmpty(e.PLN_FI) == false).Max(e => e.PLN_FI);

                // 완료일은 최소 호선의 DL
                if (string.IsNullOrEmpty(selCase.DL) == false && maxFi.CompareTo(selCase.DL) < 0)
                    maxFi = selCase.DL;

                // margin: 1 month
                minSt = Calendar.AddCalDays(minSt, -30);
                maxFi = Calendar.AddCalDays(maxFi, 30);

                return GetGanttCalendar(minSt, maxFi);
            }
            return new List<HHIDay>();
        }

        public List<HHIDay> GetGanttCalendar(string st, string fi)
        {
            List<HHIDay> days = new List<HHIDay>();

            if (Calendar != null && string.IsNullOrEmpty(st) == false
                && string.IsNullOrEmpty(fi) == false && st.CompareTo(fi) <= 0)
            {
                var tmpDays = (Calendar as HHICalendar).Where(e => e.Key.CompareTo(st) >= 0 && e.Key.CompareTo(fi) <= 0);
                if (tmpDays != null && tmpDays.Count() > 0)
                    days = tmpDays.Select(e => e.Value).ToList();

                if(this.WrkPntList != null)
                    GanttUtil.SetDayStyle(this.WrkPntList.ToList(), days);
            }

            return days;
        }

        public List<DataTSFN101> GetGanttActList(List<string> divCodList
            , List<DataTSFN101> filteredActList
            , List<string> exceptBlockList
            , bool showAllErecBlock = true
            , bool showProNetPlan = false
            , bool showProNetResult = false
            , GanttViewMode ganttViewMode = GanttViewMode.Default)
        {
            List<DataTSFN101> list = new List<DataTSFN101>();
            if (ActList != null)
            {
                // 구획간 연결이 포함된 구획 목록
                var divLinkDivList = new List<string>();
                if (ganttViewMode == GanttViewMode.DivLinkWithDivAct)
                    divLinkDivList = GetDivLinkedDivList();

                // 구획별 완료일이 가장 늦은 탑재 블록
                var divDaeActDict = new Dictionary<string, string>();
                if (showAllErecBlock == false)
                    divDaeActDict = FindDaeItm(exceptBlockList,divCodList);

                foreach (var act in ActList)
                {
                    // 간트 보기 모드: 구획 연결을 가진 ACT 만 표시
                    if (ganttViewMode == GanttViewMode.DivLinkOnly && HasDivLink(act) == false)
                        continue;

                    // 구획 연결을 가진 ACT가 포함된 구획 모두
                    if (ganttViewMode == GanttViewMode.DivLinkWithDivAct && (divLinkDivList == null || divLinkDivList.Contains(act.DIV_COD) == false))
                        continue;

                    // 표시할 구획에 포함 안됨
                    if (divCodList != null && divCodList.Contains(act.DIV_COD) == false)
                        continue;

                    // 필터할 ACT가 정해져 있으면
                    if (filteredActList != null && filteredActList.Count > 0
                        && filteredActList.Contains(act) == false)
                        continue;

                    // 대표 ITM이 아님
                    if (showAllErecBlock == false && Defs.IsHullAct(act.WRK_STG) == true
                        && (divDaeActDict.ContainsKey(act.DIV_COD) == false || divDaeActDict[act.DIV_COD] != act.ITM_COD))
                        continue;

                    list.Add(act);
                }

                // 정렬
                list = list.OrderBy(e => e.COMMON_DIV).ThenBy(e => e.DIV_COD).ThenBy(e => e.DIV_COD2).ThenBy(e => e.ITM_COD).ThenBy(e => e.VIEW_SEQ)
                           .ThenBy(e => e.ACT_COD).ToList();

                if (showProNetPlan == true || showProNetResult == true)
                    list.ForEach(e => e.SetProNetPosition());

                // 스타일 설정
                if (GanttUtil != null)
                    GanttUtil.SetActStyle(list, false, showProNetPlan, showProNetResult);
            }
            return list;
        }

        private bool HasDivLink(DataTSFN101 act)
        {
            if (act != null)
            {
                if (act.PreLinks != null)
                {
                    foreach (var link in act.PreLinks)
                        if (link.IsDivLink == true)
                            return true;
                }
                if (act.AftLinks != null)
                {
                    foreach (var link in act.AftLinks)
                        if (link.IsDivLink == true)
                            return true;
                }
            }
            return false;
        }

        private List<string> GetDivLinkedDivList()
        {
            var list = new List<string>();

            if (LinkList != null)
            {
                var divLinks = LinkList.Where(e => e.IsDivLink == true).ToList();
                foreach (var link in divLinks)
                {
                    if (list.Contains(link.PRE_DIV) == false)
                        list.Add(link.PRE_DIV);
                    if (list.Contains(link.AFT_DIV) == false)
                        list.Add(link.AFT_DIV);
                }
            }

            return list;
        }

        public List<DataTSFN105> GetGanttWrkPntCnstList(List<DataTSFN101> actList)
        {
            var list = new List<DataTSFN105>();
            if (actList != null && WrkPntConstraints != null && WrkPntConstraints.Count > 0)
            {
                list.AddRange(WrkPntConstraints.Where(e => e.Act != null && actList.Contains(e.Act)));

                GanttUtil.SetWrkPntCnstStyle(list);
            }
            return list;
        }

        public List<WrkPntCnstLink> GetGanttWrkPntCnstLinkList(List<DataTSFN105> wrkPntCnstNodeList)
        {
            List<WrkPntCnstLink> list = new List<WrkPntCnstLink>();
            if (WrkPntCnstLinks != null && WrkPntCnstLinks.Count > 0)
            {
                list.AddRange(WrkPntCnstLinks.Where(e => wrkPntCnstNodeList.Contains(e.WrkPntCnst) == true));
            }
            return list;
        }

        public WrkPntCnstLink GetGanttWrkPntCnstLink(DataTSFN105 wrkPntCnst)
        {
            if (WrkPntCnstLinks != null)
            {
                return WrkPntCnstLinks.FirstOrDefault(e => e.WrkPntCnst == wrkPntCnst);
            }
            return null;
        }

        private Dictionary<string, string> FindDaeItm(List<string> exceptList ,List<string> divCodList)
        {
            var itmDict = new Dictionary<string, string>();
            var fiDict = new Dictionary<string, string>();

            if (ActList != null)
            {
                foreach (var act in ActList)
                {
                    if (act.HO_GUBUN != Defs.HO_GUBUN_BEF_ACT)
                        continue;
                    if (string.IsNullOrEmpty(act.PLN_FI) == true)
                        continue;
                    if (divCodList != null && divCodList.Contains(act.DIV_COD) == false)
                        continue;
                    if (exceptList.Contains(act.SHP_KND + "|" + act.ITM_COD.Substring(0, 1)))
                        continue;


                    if (itmDict.ContainsKey(act.DIV_COD) == false)
                    {
                        itmDict.Add(act.DIV_COD, act.ITM_COD);
                        fiDict.Add(act.DIV_COD, act.PLN_FI);
                    }
                    else if (fiDict[act.DIV_COD].CompareTo(act.PLN_FI) < 0)
                    {
                        itmDict[act.DIV_COD] = act.ITM_COD;
                        fiDict[act.DIV_COD] = act.PLN_FI;
                    }
                }
            }

            return itmDict;
        }

        // 간트 Display 하기위한 link 목록
        public List<ILink> GetGanttLinkList(List<DataTSFN101> actList)
        {
            List<ILink> list = new List<ILink>();
            if (LinkList != null && actList != null && actList.Count > 0)
            {
                var actCodList = actList.Select(e => e.ACT_COD);
                list = LinkList.Where(e => actCodList.Contains(e.PRE_ACT) || actCodList.Contains(e.AFT_ACT)).Cast<ILink>().ToList();
            }
            return list;
        }

        public List<DataTSDC002> GetWrkPntList()
        {
            if (WrkPntList != null)
                return WrkPntList.ToList();
            return null;

            //var list = new List<DataTSDC002>();
            //if (WrkPntList != null)
            //{
            //    // 일자가 겹치면 position을 변경해둠

            //}            

            //return list;
        }



        #endregion

        #region save as 관련

        public void ChangeActLinkCaseNo(string newCaseNo)
        {
            if (ActList != null)
            {
                foreach (var act in ActList)
                    act.CASE_NO = newCaseNo;
            }
            if (LinkList != null)
            {
                foreach (var link in LinkList)
                    link.CASE_NO = newCaseNo;
            }
        }

        public void MakeSaveAsData(string newCaseNo, out DataTable dtAct, out DataTable dtLink)
        {
            dtAct = null;
            dtLink = null;

            if (ActList != null && ActList.dtDataSource != null)
            {
                List<DataRow> delList = new List<DataRow>();
                dtAct = ActList.dtDataSource.Copy();
                foreach (var dr in dtAct.AsEnumerable())
                {
                    if(dr.RowState != DataRowState.Deleted)
                    {
                        dr["CASE_NO"] = newCaseNo;
                    
                        // 선각 ACT는 삭제 목록에 추가하고 제외시킴
                        if (Defs.IsHullAct(dr["WRK_STG"].ToString()))
                            delList.Add(dr);
                    }
                }
                // 선각 ACT는 제외
                foreach (var delRow in delList)
                    dtAct.Rows.Remove(delRow);
            }
            if (LinkList != null && LinkList.dtDataSource != null)
            {
                List<DataRow> delList = new List<DataRow>();
                HashSet<string> keySet = new HashSet<string>();
                dtLink = LinkList.dtDataSource.Copy();
                foreach (var dr in dtLink.AsEnumerable())
                {
                    if (dr.RowState != DataRowState.Deleted)
                    {
                        dr["CASE_NO"] = newCaseNo;

                        // 선각 ACT 간의 관계는 하나만, 생성
                        var key = dr["PRE_ACT"].ToString() + dr["AFT_ACT"].ToString();
                        if (keySet.Contains(key) == true)
                            delList.Add(dr);
                        else
                            keySet.Add(key);
                    }
                }
                // 삭제 처리
                foreach (var delRow in delList)
                    dtLink.Rows.Remove(delRow);
            }
        }

        #endregion

        #region ACT/LInk 추가/삭제


        public DataTSFN101 CreateTempAct(DataTSFN101 selAct)
        {
            if (ActList != null)
            {
                return ActList.AddTempItem(selAct);
            }
            return null;
        }

        internal bool AddActBefore(DataTSFN101 selAct, DataTSFN101 newAct)
        {
            if (ActList != null && selAct != null && ActDict.ContainsKey(newAct.ACT_COD) == false)
            {
                ActList.AddItemBefore(selAct, newAct);
                ActDict.Add(MakeDictKey(newAct.DIV_COD, newAct.ACT_COD), newAct);
                newAct.Calendar = this.Calendar;
                return true;
            }
            return false;
        }

        internal bool AddActAfter(DataTSFN101 selAct, DataTSFN101 newAct)
        {
            if (ActList != null && selAct != null && ActDict.ContainsKey(newAct.ACT_COD) == false)
            {
                ActList.AddItemAfter(selAct, newAct);
                ActDict.Add(MakeDictKey(newAct.DIV_COD, newAct.ACT_COD), newAct);
                newAct.Calendar = this.Calendar;
                return true;
            }
            return false;
        }


        /// <summary>
        /// 기존 ACT의 ACT_COD(키필드) 가 변경되면 삭제하고 같은 자리에 신규추가
        /// </summary>
        internal DataTSFN101 DelInsAct(DataTSFN101 act)
        {
            if (act != null && ActList != null)
            {
                // 복사
                var newAct = ActList.CloneData(act);
                // 삽입
                AddActBefore(act, newAct);
                // 관계정보 복사
                if (act.PreLinks != null)
                {
                    foreach (var link in act.PreLinks)
                        AddLink(link.PreAct, newAct, link);
                }

                if (act.AftLinks != null)
                {
                    foreach (var link in act.AftLinks)
                        AddLink(link.AftAct, newAct, link);
                }

                // 기존 act 삭제
                DelAct(act);

                return newAct;
            }
            return null;
        }

        internal void SyncLinkActCod(DataTSFN101 act)
        {
            if (act != null)
            {
                if (act.PreLinks != null)
                {
                    foreach (var link in act.PreLinks)
                        link.AftId = act.ACT_COD;
                }
                if (act.PreLinks != null)
                {
                    foreach (var link in act.AftLinks)
                        link.PreId = act.ACT_COD;
                }
            }
        }


        public void DelAct(DataTSFN101 act)
        {
            if (act != null)
            {
                // 절점제약 삭제
                DelWrkPntConstraint(act);

                // 관계 삭제
                DelLink(act);

                // ACT 삭제
                ActDict.Remove(MakeDictKey(act.DIV_COD, act.ACT_COD));
                ActList.DelItem(act);
            }
        }

        private void DelWrkPntConstraint(DataTSFN101 act)
        {
            if (act != null)
            {
                if (act != null && act.WrkPntConstraints != null && act.WrkPntConstraints.Count > 0)
                {
                    var delList = act.WrkPntConstraints.ToList();
                    foreach (var cnst in delList)
                    {
                        DelWrkPntConstraint(cnst);
                    }
                }
            }
        }

        public void DelLink(DataTSFN101 act)
        {
            if (act != null)
            {
                List<DataTSFN102> delLinks = new List<DataTSFN102>();
                if (act.PreLinks != null)
                    delLinks.AddRange(act.PreLinks);
                if (act.AftLinks != null)
                    delLinks.AddRange(act.AftLinks);

                foreach (var link in delLinks)
                {
                    DelLink(link);
                }
            }
        }

        public void DelLink(DataTSFN102 link)
        {
            if (link != null)
            {
                if (link.PreAct != null)
                    link.PreAct.DelAftLink(link);
                if (link.AftAct != null)
                    link.AftAct.DelPreLink(link);

                LinkList.DelItem(link);
            }
        }


        // actList 에 걸쳐진 링크 목록
        public List<DataTSFN102> GetRelatedLinkList(DataTSFN101 act)
        {
            return GetRelatedLinkList(new List<DataTSFN101>() { act });
        }

        // actList 에 걸쳐진 링크 목록
        public List<DataTSFN102> GetRelatedLinkList(List<DataTSFN101> actList)
        {
            List<DataTSFN102> linkList = new List<DataTSFN102>();

            foreach (var act in actList)
            {
                if (act.PreLinks != null && act.PreLinks.Count > 0)
                    linkList.AddRange(act.PreLinks);
                if (act.AftLinks != null && act.AftLinks.Count > 0)
                    linkList.AddRange(act.AftLinks);
            }

            return linkList;
        }

        public DataTSFN102 GetLink(DataTSFN101 preAct, DataTSFN101 aftAct)
        {
            if (preAct != null && aftAct != null && preAct.AftLinks != null)
            {
                return preAct.AftLinks.FirstOrDefault(e => e.AftAct == aftAct);
            }
            return null;
        }

        public DataTSFN102 GetLink(string preActCod, string aftActCod)
        {
            if (string.IsNullOrEmpty(preActCod) == false && string.IsNullOrEmpty(aftActCod) == false
                && LinkList != null)
            {
                return LinkList.FirstOrDefault(e => e.PRE_ACT == preActCod && e.AFT_ACT == aftActCod);
            }
            return null;
        }

        public DataTSFN102 AddLink(DataTSFN101 preAct, DataTSFN101 aftAct, DataTSFN102 linkInfo)
        {
            var newLink = AddLink(preAct, aftAct, linkInfo.LinkType);
            if (newLink != null)
            {
                newLink.STD_OST = linkInfo.STD_OST;
                newLink.OFF_SET = linkInfo.OFF_SET;
                newLink.LINE_SNAP_LOC = linkInfo.LINE_SNAP_LOC;
                return newLink;
            }
            return null;
        }

        public DataTSFN102 AddLink(DataTSFN101 preAct, DataTSFN101 aftAct, LinkTypeDef linkType)
        {
            if (preAct != null && aftAct != null && LinkList != null)
            {
                var link = LinkList.AddItem(null);
                link.FIG_SHP = preAct.FIG_SHP;
                link.CASE_NO = preAct.CASE_NO;
                link.SHP_COD = preAct.SHP_COD;
                link.PRE_ACT_ID = "";
                link.PRE_ACT = preAct.ACT_COD;
                link.PRE_DIV = preAct.DIV_COD;
                link.AFT_DIV = aftAct.DIV_COD;
                link.PRE_NWK = preAct.NWK_ID;
                link.HO_GUBUN1 = preAct.HO_GUBUN;
                link.AFT_ACT_ID = "";
                link.AFT_ACT = aftAct.ACT_COD;
                link.AFT_NWK = aftAct.NWK_ID;
                link.HO_GUBUN2 = aftAct.HO_GUBUN;
                link.REL_TYP = linkType.ToString();

                link.LINE_SNAP_LOC = "FinishNoMargin";

                // ACT 간 관계 설정
                SetActLink(preAct, aftAct, link);

                return link;
            }
            return null;
        }

        //19.08.13 신성훈 선택한 소구획만 삭제하도록 수정.
        public void DelDiv(string divCod, string divCod2)
        {
            if (ActList != null)
            {
                //var divActList = GetDivActList(divCod);
                //19.08.13 신성훈 선택한 소구획만 삭제하도록 수정.
                var divActList = GetDivActList(divCod, divCod2);
                if (divActList != null && divActList.Count > 0)
                {
                    divActList.ForEach(e => DelAct(e));
                }
            }
        }
        public void DelDiv(string divCod)
        {
            if (ActList != null)
            {
                var divActList = GetDivActList(divCod);
                if (divActList != null && divActList.Count > 0)
                {
                    divActList.ForEach(e => DelAct(e));
                }
            }
        }

        //19.08.13 신성훈 선택한 소구획만 삭제하도록 수정.
        public List<DataTSFN101> GetDivActList(string divCod, string divCod2)
        {
            List<DataTSFN101> list = new List<DataTSFN101>();
            if (string.IsNullOrEmpty(divCod) == false && ActList != null)
            {
                //19.08.13 신성훈 선택한 소구획만 삭제하도록 수정.
                list.AddRange(ActList.Where(e => e.DIV_COD == divCod && e.DIV_COD2 == divCod2));
            }
            return list;
        }

        public List<DataTSFN101> GetDivActList(string divCod)
        {
            List<DataTSFN101> list = new List<DataTSFN101>();
            if (string.IsNullOrEmpty(divCod) == false && ActList != null)
            {
                //19.08.13 신성훈 선택한 소구획만 삭제하도록 수정.
                list.AddRange(ActList.Where(e => e.DIV_COD == divCod));
            }
            return list;
        }

        public void CalcDuration(DataTSFN101 act)
        {
            if (act != null)
            {
                act.UpdatePlanDuration();
            }
        }

        public void UpdatePlanFinish(DataTSFN101 act)
        {
            if (act != null)
            {
                act.UpdatePlanFinish();
            }
        }

        public void CalcOffset(DataTSFN101 act)
        {
            // 선후행 관계의 계획 옵셋 재계산
            if (act != null)
            {
                act.UpdatePlanOffset();
            }
        }

        public int? CalcPlanOffset(DataTSFN102 link)
        {
            if (link != null && Calendar != null)
            {
                var preAct = link.PreAct;
                var aftAct = link.AftAct;
                switch (link.LinkType)
                {
                    case Component.Presentation.AmiSmartGantt.LinkTypeDef.SS:
                        return this.Calendar.GetOffsetAbsolute(preAct.PlanStart, aftAct.PlanStart);
                    case Component.Presentation.AmiSmartGantt.LinkTypeDef.SF:
                        return this.Calendar.GetOffsetAbsolute(preAct.PlanStart, aftAct.PlanFinish) + 1;
                    case Component.Presentation.AmiSmartGantt.LinkTypeDef.FS:
                        return this.Calendar.GetOffsetAbsolute(preAct.PlanFinish, aftAct.PlanStart) - 1;
                    case Component.Presentation.AmiSmartGantt.LinkTypeDef.FF:
                        return this.Calendar.GetOffsetAbsolute(preAct.PlanFinish, aftAct.PlanFinish);
                    case Component.Presentation.AmiSmartGantt.LinkTypeDef.NA:
                        break;
                }
            }
            return 0;
        }

        public void CalcSlack(List<DataTSFN101> actList)
        {
            if (actList != null)
            {
                actList.ForEach(e => CalcSlack(e));
            }
        }

        public void CalcSlack(DataTSFN101 act)
        {
            // 선후행 관계의 계획 옵셋 재계산
            if (act != null)
            {
                if (act.PreLinks != null)
                {
                    foreach (var link in act.PreLinks)
                        link.Slack = CalcSlack(link);
                }
                if (act.AftLinks != null)
                {
                    foreach (var link in act.AftLinks)
                        link.Slack = CalcSlack(link);
                }
            }
        }

        public int? CalcSlack(DataTSFN102 link)
        {
            if (link != null && Calendar != null && link.PreAct != null && link.AftAct != null)
            {
                var preAct = link.PreAct;
                var aftAct = link.AftAct;
                switch (link.LinkType)
                {
                    case Component.Presentation.AmiSmartGantt.LinkTypeDef.SS:
                        return this.Calendar.GetOffsetAbsolute(preAct.PlanStart, aftAct.PlanStart);
                    case Component.Presentation.AmiSmartGantt.LinkTypeDef.SF:
                        return this.Calendar.GetOffsetAbsolute(preAct.PlanStart, aftAct.PlanFinish) + 1;
                    case Component.Presentation.AmiSmartGantt.LinkTypeDef.FS:
                        return this.Calendar.GetOffsetAbsolute(preAct.PlanFinish, aftAct.PlanStart) - 1;
                    case Component.Presentation.AmiSmartGantt.LinkTypeDef.FF:
                        return this.Calendar.GetOffsetAbsolute(preAct.PlanFinish, aftAct.PlanFinish);
                    case Component.Presentation.AmiSmartGantt.LinkTypeDef.NA:
                        break;
                }
            }
            return 0;
        }

        #endregion

        #region 절점제약 관리

        public void ImportWrkPntConstraint(DataTSFN105List cnstList)
        {
            if (cnstList != null && cnstList.Count > 0)
            {
                foreach (var cnst in cnstList)
                {
                    var item = WrkPntConstraints.GetItem(cnst.FIG_SHP, cnst.CASE_NO, cnst.ACT_COD, cnst.WRK_PNT);
                    if (item == null)
                    {
                        item = WrkPntConstraints.AddItem(null);
                        item.CASE_NO = cnst.CASE_NO;
                        item.FIG_SHP = cnst.FIG_SHP;
                        item.SHP_COD = cnst.SHP_COD;
                        item.ACT_COD = cnst.ACT_COD;
                        item.ACT_DES = cnst.ACT_DES;
                        item.WRK_PNT = cnst.WRK_PNT;
                        item.SIGN = cnst.SIGN;
                        item.WRK_PNT_SHT = cnst.WRK_PNT_SHT;    
                        item.PLN_ST = cnst.PLN_ST;
                        item.PLN_FI = cnst.PLN_FI;
                        item.STD_OST = cnst.STD_OST;
                        item.PNT_OST = cnst.PNT_OST;
                        item.ST_FI_GBN = cnst.ST_FI_GBN;
                        item.RMK = cnst.RMK;
                        item.USE_YN = cnst.USE_YN;

                        var actList = GetActList(cnst.ACT_COD);
                        if (actList != null && actList.Count > 0)
                        {
                            foreach (var act in actList)
                            {
                                act.AddWrkPntConstraint(item);
                                item.Act = act;

                                AddWrkPntCnstLink(act, item);
                            }
                        }
                    }
                }
            }
        }

        private void AddWrkPntCnstLink(DataTSFN101 act, DataTSFN105 WrkPntCnst)
        {
            if (act != null && WrkPntCnst != null && WrkPntCnstLinks != null)
            {
                var wrkPntCnstLink = new WrkPntCnstLink(act, WrkPntCnst);
                WrkPntCnstLinks.Add(wrkPntCnstLink);
            }
        }

        private void DelWrkPntConstLink(DataTSFN101 act, DataTSFN105 wrkPntCnst)
        {
            if (act != null && wrkPntCnst != null && WrkPntCnstLinks != null)
            {
                WrkPntCnstLinks.RemoveAll(e => e.Act == act && e.WrkPntCnst == wrkPntCnst);
            }
        }

        public WrkPntCnstLink GetWrkPntCnstLink(DataTSFN101 act, DataTSFN105 wrkPntCnst)
        {
            if (act != null && wrkPntCnst != null && WrkPntCnstLinks != null)
            {
                return WrkPntCnstLinks.FirstOrDefault(e => e.Act == act && e.WrkPntCnst == wrkPntCnst);
            }
            return null;
        }

        public List<WrkPntCnstLink> GetWrkPntCnstLink(DataTSFN101 act)
        {
            if (act != null && WrkPntCnstLinks != null)
            {
                return WrkPntCnstLinks.Where(e => e.Act == act).ToList();
            }
            return null;
        }

        public DataTSFN105 AddWrkPntConstraint(DataTSFN101 act, DataRow wrkPnt, string stFiGbn)
        {
            if (act != null && wrkPnt != null && WrkPntConstraints != null)
            {
                var item = WrkPntConstraints.GetItem(act.FIG_SHP, act.CASE_NO, act.ACT_COD, wrkPnt["WRK_PNT"].ToString());
                if (item == null)
                {
                    item = WrkPntConstraints.AddItem(null);
                    item.CASE_NO = act.CASE_NO;
                    item.FIG_SHP = act.FIG_SHP;
                    item.SHP_COD = act.SHP_COD;
                    item.ACT_COD = act.ACT_COD;
                    item.ACT_DES = act.ACT_DES;
                    item.WRK_PNT = wrkPnt["WRK_PNT"].ToString();
                    item.WRK_PNT_SHT = wrkPnt["WRK_PNT_SHT"].ToString();
                    item.PLN_ST = wrkPnt["PLN_ST"].ToString();
                    item.PLN_FI = wrkPnt["PLN_FI"].ToString();
                    item.STD_OST = 0;
                    item.PNT_OST = 0;
                    item.ST_FI_GBN = stFiGbn;
                    item.RMK = "";
                    item.USE_YN = "Y";

                    act.AddWrkPntConstraint(item);
                    item.Act = act;

                    AddWrkPntCnstLink(act, item);
                }
                return item;
            }
            return null;
        }

        public void DelWrkPntConstraint(DataTSFN105 cnst)
        {
            if (cnst != null && WrkPntConstraints != null)
            {
                // ACT가 가진 절점제약은 제거
                foreach (var act in this.ActList)
                {
                    if (act.WrkPntConstraints.Contains(cnst) == true)
                    {
                        act.DelWrkPntConstraint(cnst);

                        DelWrkPntConstLink(act, cnst);
                    }
                }

                WrkPntConstraints.DelItem(cnst);
            }
        }

        #endregion

        #region F/W 스케줄

        public void ForwardSchedule(bool useStdTerm = true, AmpScheduleOffset offsetOption = AmpScheduleOffset.Standard, bool useStartWrkPntConstraint = true, bool useFinishWrkPntConstraint = true)
        {
            SetScheduleOption(AmpScheduleMode.Forward, useStdTerm, offsetOption, useStartWrkPntConstraint, useFinishWrkPntConstraint);

            var tgtActs = new List<DataTSFN101>();
            var schActs = new List<DataTSFN101>();
            var errActs = new List<DataTSFN101>();


            var roots = this.ActList.Where(e => e.HasPreLinks() == false).ToList();

            // debug
            //roots = roots.Where(e => e.DIV_COD == "110").ToList();

            foreach (var root in roots)
            {
                var tmpTgtActs = new List<DataTSFN101>();
                var tmpSchActs = new List<DataTSFN101>();
                var tmpErrActs = new List<DataTSFN101>();

                ForwardSchedule(root, tmpTgtActs, tmpSchActs, tmpErrActs);

                tgtActs.AddRange(tmpTgtActs);
                schActs.AddRange(tmpSchActs);
                errActs.AddRange(tmpErrActs);
            }
        }

        public void ForwardScheduleDiv(string divCod, bool useStdTerm = true, AmpScheduleOffset offsetOption = AmpScheduleOffset.Standard, bool useStartWrkPntConstraint = true, bool useFinishWrkPntConstraint = true)
        {
            SetScheduleOption(AmpScheduleMode.Forward, useStdTerm, offsetOption, useStartWrkPntConstraint, useFinishWrkPntConstraint);

            var tgtActs = new List<DataTSFN101>();
            var schActs = new List<DataTSFN101>();
            var errActs = new List<DataTSFN101>();

            var roots = this.ActList.Where(e => e.DIV_COD == divCod && e.HasPreLinks() == false).ToList();

            foreach (var root in roots)
            {
                var tmpTgtActs = new List<DataTSFN101>();
                var tmpSchActs = new List<DataTSFN101>();
                var tmpErrActs = new List<DataTSFN101>();

                ForwardSchedule(root, tmpTgtActs, tmpSchActs, tmpErrActs);

                tgtActs.AddRange(tmpTgtActs);
                schActs.AddRange(tmpSchActs);
                errActs.AddRange(tmpErrActs);
            }
        }


        public void ForwardSchedule(DataTSFN101 act, List<DataTSFN101> tgtActs, List<DataTSFN101> schActs, List<DataTSFN101> errActs)
        {
            List<DataTSFN101> tmpTgtList = new List<DataTSFN101>();
            List<DataTSFN101> tmpSchList = new List<DataTSFN101>();
            List<DataTSFN101> tmpErrList = new List<DataTSFN101>();

            // 스케줄 데이터 초기화
            ClearSchData();

            // F/W 대상 목록 설정
            InitFwdSchObj(act, tmpTgtList);

            // F/W 스케줄 수행
            ForwardScheduleRecursive(act, tmpSchList, tmpErrList, true, true);

            CommitSch(tmpTgtList);

            // Slack 계산
            CalcSlack(tmpSchList);

            if (tgtActs != null) tgtActs.AddRange(tmpTgtList);
            if (schActs != null) schActs.AddRange(tmpSchList);
            if (errActs != null) errActs.AddRange(tmpErrList);
        }

        private void InitFwdSchObj(DataTSFN101 act, List<DataTSFN101> tgtActs)
        {
            if (act != null)
            {
                //if (act.HO_GUBUN != Defs.HO_GUBUN_AFT_ACT)
                //{
                act.IsSelected = true;
                AddToTargetList(act, tgtActs);
                //}
                InitFwdSchObjRecursive(act, tgtActs);
            }
        }

        private void InitFwdSchObjRecursive(DataTSFN101 act, List<DataTSFN101> tgtActs)
        {
            if (act.HasAftLinks() == false)
                return;

            foreach (var link in act.AftLinks)
            {
                var aftAct = link.AftAct;

                if (aftAct != null && aftAct.IsSelected == false)
                {
                    aftAct.IsSelected = true;
                    AddToTargetList(aftAct, tgtActs);
                    InitFwdSchObjRecursive(aftAct, tgtActs);
                }
            }
        }


        private void ForwardScheduleRecursive(DataTSFN101 act, List<DataTSFN101> schActs, List<DataTSFN101> errActs, bool initAll, bool skipFixed)
        {
            if (act.Count != 0 && act.Count < act.PreLinks.Count)
            {
                if (ForwardScheduleBacklog(act, schActs, errActs, skipFixed) == false)
                    return;
            }

            foreach (var link in act.AftLinks)
            {
                // 필터처리..

                var aftAct = link.AftAct;
                aftAct.CountUp();

                // cycle
                if (aftAct.Count >= Defs.CYCLE_DETECT_THRESHOLD)
                {
                    AddToErrorList(aftAct, SchError.Cycle, errActs);
                    return;
                }

                if (CalculateFwdSchDate(link, schActs, errActs, skipFixed) == false)
                    return;

                //if (act.Count != 0 || aftAct.Count <= aftAct.AftLinks.Count)
                if (act.Count != 0 || aftAct.Count <= aftAct.PreLinks.Count)
                {
                    ForwardScheduleRecursive(aftAct, schActs, errActs, initAll, skipFixed);
                }
            }
        }

        private bool ForwardScheduleBacklog(DataTSFN101 act, List<DataTSFN101> schActs, List<DataTSFN101> errActs, bool skipFixed)
        {
            foreach (var link in act.PreLinks)
            {
                var preAct = link.PreAct;
                if (preAct == null || preAct.IsSelected == false)
                {
                    act.CountUp();
                    if (CalculateFwdSchDate(link, schActs, errActs, skipFixed) == false)
                        return false;
                }
            }
            return true;
        }

        private bool CalculateFwdSchDate(DataTSFN102 link, List<DataTSFN101> schActs, List<DataTSFN101> errActs, bool skipFixed)
        {
            // 선행 일정 체크
            if (link.PreAct.IsEmptySchDate() == true)
            {
                AddToErrorList(link.PreAct, SchError.NoDate, errActs);
            }

            // 고정 ACT면 Pass
            if (skipFixed && link.AftAct.FIXED_YN == "Y")
                return true;

            // 일정 계산
            ModifyFwdSchDate(link, schActs);

            return true;
        }

        private void ModifyFwdSchDate(DataTSFN102 link, List<DataTSFN101> schActs)
        {
            var preAct = link.PreAct;
            var aftAct = link.AftAct;

            //var offset = this.ScheduleOption.OffsetOption ? (link.STD_OST ??  0) : (link.OFF_SET ?? 0);
            var offset = this.ScheduleOption.GetScheduleOffset(link);
            var term = this.ScheduleOption.UseStdTerm ? (aftAct.STD_TRM ?? 1) : aftAct.PlanDuration;
            if (aftAct.Count == 1)
            {
                // 최초 계산되는 일정이면
                switch (link.LinkType)
                {
                    case LinkTypeDef.FF:
                        aftAct.SetSchFiWithDur(preAct.SchFi + offset, term);
                        break;
                    case LinkTypeDef.FS:
                        aftAct.SetSchStWithDur(preAct.SchFi + offset + 1, term);
                        break;
                    case LinkTypeDef.SF:
                        aftAct.SetSchFiWithDur(preAct.SchSt + offset - 1, term);
                        break;
                    case LinkTypeDef.SS:
                        aftAct.SetSchStWithDur(preAct.SchSt + offset, term);
                        break;
                    default:
                        break;
                }

                // 절점일정제약처리
                CheckFwdWrkPntConstraint(aftAct, term);
            }
            else
            {
                // 첫번째 가 아니면 
                // 선행 Act에 의해 일정이 밀리는 경우만 재설정
                int schDate = 0;

                switch (link.LinkType)
                {
                    case LinkTypeDef.FF:
                        schDate = preAct.SchFi + offset;
                        if (schDate > aftAct.SchFi)
                            aftAct.SetSchFiWithDur(schDate, term);
                        break;
                    case LinkTypeDef.FS:
                        schDate = preAct.SchFi + offset + 1;
                        if (schDate > aftAct.SchSt)
                            aftAct.SetSchStWithDur(schDate, term);
                        break;
                    case LinkTypeDef.SF:
                        schDate = preAct.SchSt + offset - 1;
                        if (schDate > aftAct.SchFi)
                            aftAct.SetSchFiWithDur(schDate, term);
                        break;
                    case LinkTypeDef.SS:
                        schDate = preAct.SchSt + offset;
                        if (schDate > aftAct.SchSt)
                            aftAct.SetSchStWithDur(schDate, term);
                        break;
                    default:
                        break;
                }
            }

            AddToScheduleList(aftAct, schActs);
        }


        private void CheckFwdWrkPntConstraint(DataTSFN101 act, int term)
        {
            if (act != null && this.ScheduleOption.UseStartWrkPntConstraint == true && act.HasStartWrkPntConstraints
               && string.IsNullOrEmpty(act.StartWrkPntDate) == false)
            {
                // 착수 절점 제약이 있으면 적용
                var schSt = Calendar.GetCalDay(act.SchSt);
                if (schSt.CompareTo(act.StartWrkPntDate) <= 0)
                {
                    // 절점 제약일자보다 앞이면 뒤로 밈
                    var newSchSt = Calendar.GetNetDay(act.StartWrkPntDate) + 1;
                    act.SetSchStWithDur(newSchSt, term);
                }
            }
        }


        #endregion

        #region 스케줄 공통

        public void SetScheduleOption(AmpScheduleMode schMode, bool useStdTerm, AmpScheduleOffset offsetOption, bool useStartWrkPntConstraint, bool useFinishWrkPntConstraint)
        {
            this.ScheduleOption.ScheduleMode = schMode;
            this.ScheduleOption.UseStdTerm = useStdTerm;
            this.ScheduleOption.OffsetOption = offsetOption;
            this.ScheduleOption.UseStartWrkPntConstraint = useStartWrkPntConstraint;
            this.ScheduleOption.UseFinishWrkPntConstraint = useFinishWrkPntConstraint;
        }



        private void CommitSch(List<DataTSFN101> schActs)
        {
            if (schActs != null)
            {
                switch (ScheduleOption.ScheduleMode)
                {
                    case AmpScheduleMode.Forward:
                        schActs.ForEach(e => e.CommitSch());
                        break;
                    case AmpScheduleMode.Backward:
                        schActs.ForEach(e => e.CommitSch());
                        break;
                    case AmpScheduleMode.ForwardCP:
                        schActs.ForEach(e => e.CommitSchEst());
                        break;
                    case AmpScheduleMode.BackwardCP:
                        schActs.ForEach(e => e.CommitSchLst());
                        break;
                    default:
                        break;
                }
            }
        }

        private void ClearSchData()
        {
            if (ActList != null)
            {
                foreach (var act in ActList)
                {
                    act.ClearSchData();
                }
            }
        }

        private void AddToErrorList(DataTSFN101 act, SchError error, List<DataTSFN101> list)
        {
            if (act != null && list != null)
            {
                act.SchStatus = error;
                list.Add(act);
            }
        }
        private void AddToScheduleList(DataTSFN101 act, List<DataTSFN101> list)
        {
            if (act != null && list != null)
            {
                if (list.Contains(act) == false)
                    list.Add(act);
            }
        }

        private void AddToTargetList(DataTSFN101 act, List<DataTSFN101> list)
        {
            if (act != null && list != null)
            {
                if (list.Contains(act) == false)
                    list.Add(act);
            }
        }

        #endregion


        #region B/W 스케줄

        public void BackwardSchedule(bool useStdTerm = false, AmpScheduleOffset scheduleOffset = AmpScheduleOffset.Plan, bool useStartWrkPntConstraint = false, bool useFinishWrkPntConstraint = true)
        {
            SetScheduleOption(AmpScheduleMode.Backward, useStdTerm, scheduleOffset, useStartWrkPntConstraint, useFinishWrkPntConstraint);

            var tgtActs = new List<DataTSFN101>();
            var schActs = new List<DataTSFN101>();
            var errActs = new List<DataTSFN101>();


            var startActs = this.ActList.Where(e => e.HasAftLinks() == false).ToList();

            // debug
            //roots = roots.Where(e => e.DIV_COD == "110").ToList();

            foreach (var act in startActs)
            {
                var tmpTgtActs = new List<DataTSFN101>();
                var tmpSchActs = new List<DataTSFN101>();
                var tmpErrActs = new List<DataTSFN101>();

                BackwardSchedule(act, tmpTgtActs, tmpSchActs, tmpErrActs);

                tgtActs.AddRange(tmpTgtActs);
                schActs.AddRange(tmpSchActs);
                errActs.AddRange(tmpErrActs);
            }
        }

        public void BackwardScheduleDiv(string divCod, bool useStdTerm = true, AmpScheduleOffset scheduleOffset = AmpScheduleOffset.Standard, bool useStartWrkPntConstraint = true, bool useFinishWrkPntConstraint = true)
        {
            SetScheduleOption(AmpScheduleMode.Backward, useStdTerm, scheduleOffset, useStartWrkPntConstraint, useFinishWrkPntConstraint);

            var tgtActs = new List<DataTSFN101>();
            var schActs = new List<DataTSFN101>();
            var errActs = new List<DataTSFN101>();

            var startActs = this.ActList.Where(e => e.DIV_COD == divCod && e.HasAftLinks() == false).ToList();

            foreach (var act in startActs)
            {
                var tmpTgtActs = new List<DataTSFN101>();
                var tmpSchActs = new List<DataTSFN101>();
                var tmpErrActs = new List<DataTSFN101>();

                BackwardSchedule(act, tmpTgtActs, tmpSchActs, tmpErrActs);

                tgtActs.AddRange(tmpTgtActs);
                schActs.AddRange(tmpSchActs);
                errActs.AddRange(tmpErrActs);
            }
        }


        public void BackwardSchedule(DataTSFN101 act, List<DataTSFN101> tgtActList, List<DataTSFN101> schActs, List<DataTSFN101> errActs)
        {
            var tmpTgtList = new List<DataTSFN101>();
            var tmpSchList = new List<DataTSFN101>();
            var tmpErrList = new List<DataTSFN101>();

            // 스케줄 데이터 초기화
            ClearSchData();

            // B/W 대상 목록 설정
            InitBwdSchObj(act, tmpTgtList);

            // CP 계산시에는 시작 ACT의 일정도 변경처리
            if (ScheduleOption.ScheduleMode == AmpScheduleMode.BackwardCP)
                SetStartActBwdSchDate(act);

            // B/W 스케줄 수행
            BackwardScheduleRecursive(act, tmpSchList, tmpErrList, true, true);

            CommitSch(tmpTgtList);

            // Slack 계산
            CalcSlack(tmpTgtList);

            if (tgtActList != null) tgtActList.AddRange(tmpTgtList);
            if (schActs != null) schActs.AddRange(tmpSchList);
            if (errActs != null) errActs.AddRange(tmpErrList);
        }

        private void InitBwdSchObj(DataTSFN101 act, List<DataTSFN101> tgtActs)
        {
            if (act != null)
            {
                act.IsSelected = true;
                AddToTargetList(act, tgtActs);
                InitBwdSchObjRecursive(act, tgtActs);
            }
        }

        private void InitBwdSchObjRecursive(DataTSFN101 act, List<DataTSFN101> tgtActs)
        {
            if (act.HasPreLinks() == false)
                return;

            foreach (var link in act.PreLinks)
            {
                var preAct = link.PreAct;

                if (preAct != null && preAct.IsSelected == false)
                {
                    // 의장 ACT만 대상임
                    //if (preAct.HO_GUBUN == Defs.HO_GUBUN_AFT_ACT)
                    //{
                    preAct.IsSelected = true;
                    AddToTargetList(preAct, tgtActs);
                    //}
                    InitBwdSchObjRecursive(preAct, tgtActs);
                }
            }
        }


        // CP 계산시에는 시작 ACT의 일정도 변경처리
        private void SetStartActBwdSchDate(DataTSFN101 act)
        {
            if (act != null)
            {

            }
        }


        private void BackwardScheduleRecursive(DataTSFN101 act, List<DataTSFN101> schActs, List<DataTSFN101> errActs, bool initAll, bool skipFixed)
        {
            if (act.Count != 0 && act.Count < act.AftLinks.Count)
            {
                if (BackwardScheduleBacklog(act, schActs, errActs, skipFixed) == false)
                    return;
            }

            foreach (var link in act.PreLinks)
            {
                // 필터처리..

                var preAct = link.PreAct;
                preAct.CountUp();

                // cycle
                if (preAct.Count >= Defs.CYCLE_DETECT_THRESHOLD)
                {
                    AddToErrorList(preAct, SchError.Cycle, errActs);
                    return;
                }

                if (CalculateBwdSchDate(link, schActs, errActs, skipFixed) == false)
                    return;

                if (act.Count != 0 || preAct.Count <= preAct.PreLinks.Count)
                {
                    BackwardScheduleRecursive(preAct, schActs, errActs, initAll, skipFixed);
                }
            }
        }

        private bool BackwardScheduleBacklog(DataTSFN101 act, List<DataTSFN101> schActs, List<DataTSFN101> errActs, bool skipFixed)
        {
            foreach (var link in act.AftLinks)
            {
                var aftAct = link.AftAct;
                if (aftAct == null || aftAct.IsSelected == false)
                {
                    act.CountUp();
                    if (CalculateBwdSchDate(link, schActs, errActs, skipFixed) == false)
                        return false;
                }
            }
            return true;
        }

        private bool CalculateBwdSchDate(DataTSFN102 link, List<DataTSFN101> schActs, List<DataTSFN101> errActs, bool skipFixed)
        {
            // 선행 일정 체크
            if (link.AftAct.IsEmptySchDate() == true)
            {
                AddToErrorList(link.AftAct, SchError.NoDate, errActs);
            }

            // 고정 ACT면 Pass
            //if (skipFixed && link.AftAct.Fixed == true)
            //    return true;

            // 일정 계산
            ModifyBwdSchDate(link, schActs);

            return true;
        }

        private void ModifyBwdSchDate(DataTSFN102 link, List<DataTSFN101> schActs)
        {
            var preAct = link.PreAct;
            var aftAct = link.AftAct;

            var offset = this.ScheduleOption.GetScheduleOffset(link);
            var term = this.ScheduleOption.UseStdTerm ? (preAct.STD_TRM ?? 1) : preAct.PlanDuration;
            if (preAct.Count == 1)
            {
                // 최초 계산되는 일정이면
                switch (link.LinkType)
                {
                    case LinkTypeDef.FF:
                        preAct.SetSchFiWithDur(aftAct.SchFi - offset, term);
                        break;
                    case LinkTypeDef.FS:
                        preAct.SetSchFiWithDur(aftAct.SchSt - offset - 1, term);
                        break;
                    case LinkTypeDef.SF:
                        preAct.SetSchStWithDur(aftAct.SchFi - offset + 1, term);
                        break;
                    case LinkTypeDef.SS:
                        preAct.SetSchStWithDur(aftAct.SchSt - offset, term);
                        break;
                    default:
                        break;
                }

                // 착수 절점 제약이 있으면 적용
                //if (this.ScheduleOption.UseStartWrkPntConstraint == true && aftAct.HasStartWrkPntConstraints
                //    && string.IsNullOrEmpty(aftAct.StartWrkPntDate) == false)
                //{
                //    var schSt = Calendar.GetCalDay(aftAct.SchSt);
                //    if (schSt.CompareTo(aftAct.StartWrkPntDate) < 0)
                //    {
                //        // 절점 제약일자보다 앞이면 뒤로 밈
                //        var newSchSt = Calendar.GetNetDay(aftAct.StartWrkPntDate);
                //        aftAct.SetSchStWithDur(newSchSt, term);
                //    }
                //}
            }
            else
            {
                // 첫번째 가 아니면 
                // 선행 Act에 의해 일정이 밀리는 경우만 재설정
                int schDate = 0;

                switch (link.LinkType)
                {
                    case LinkTypeDef.FF:
                        schDate = aftAct.SchFi + offset;
                        if (schDate < preAct.SchFi)
                            preAct.SetSchFiWithDur(schDate, term);
                        break;
                    case LinkTypeDef.FS:
                        schDate = aftAct.SchSt - offset - 1;
                        if (schDate < preAct.SchFi)
                            preAct.SetSchFiWithDur(schDate, term);
                        break;
                    case LinkTypeDef.SF:
                        schDate = aftAct.SchFi - offset + 1;
                        if (schDate < preAct.SchSt)
                            preAct.SetSchStWithDur(schDate, term);
                        break;
                    case LinkTypeDef.SS:
                        schDate = aftAct.SchSt - offset;
                        if (schDate < preAct.SchSt)
                            preAct.SetSchStWithDur(schDate, term);
                        break;
                    default:
                        break;
                }
            }
            AddToScheduleList(preAct, schActs);
        }

        private void CheckBwdWrkPntConstraint(DataTSFN101 act, int term)
        {
            if (act != null && this.ScheduleOption.UseFinishWrkPntConstraint == true && act.HasFinishWrkPntConstraints
               && string.IsNullOrEmpty(act.FinishWrkPntDate) == false)
            {
                // 착수 절점 제약이 있으면 적용
                var schFi = Calendar.GetCalDay(act.SchFi);
                if (schFi.CompareTo(act.FinishWrkPntDate) < 0)
                {
                    // 절점 제약일자보다 앞이면 뒤로 밈
                    var newSchFi = Calendar.GetNetDay(act.FinishWrkPntDate);
                    act.SetSchFiWithDur(newSchFi, term);
                }
            }
        }

        #endregion



        #region CP

        public void FindCP()
        {
            if (ActList != null)
            {
                // 초기화
                foreach (var act in ActList)
                    act.ClearCpData();

                // F/W 수행
                SetScheduleOption(AmpScheduleMode.ForwardCP, false, AmpScheduleOffset.Plan, true, true);
                var fwdStartActs = this.ActList.Where(e => e.HasPreLinks() == false).ToList();
                foreach (var act in fwdStartActs)
                    ForwardSchedule(act, null, null, null);

                // B/W 수행
                SetScheduleOption(AmpScheduleMode.BackwardCP, false, AmpScheduleOffset.Plan, true, true);
                var bwdStartActs = this.ActList.Where(e => e.HasAftLinks() == false).ToList();
                foreach (var act in bwdStartActs)
                    BackwardSchedule(act, null, null, null);

                // CP 설정
                foreach (var act in ActList)
                {
                    if (act.EstSt != 0 && act.EstSt == act.LstSt)
                        act.IsCp = true;
                }
            }
        }

        #endregion

        #region SAVE 용

        /// <summary>
        /// ACT 저장 데이터, 족장을 제외한 선각 (TSEA002)
        /// N 공정, N91 제외
        /// </summary>
        /// <returns></returns>
        public DataTable GetSaveDataTableNStgHullAct()
        {
            if (ActList != null)
            {
                var ds = new DataSet();
                var dt = ActList.dtDataSource.Clone();
                ds.Tables.Add(dt);

                HashSet<string> actList = new HashSet<string>();

                foreach (var row in ActList.dtDataSource.AsEnumerable())
                {
                    if (row.RowState == DataRowState.Deleted)   // N 공정 ACT는 삭제 불가
                        continue;

                    var wrkStg = row["WRK_STG"].ToString();
                    var wrkTyp = row["WRK_TYP"].ToString();

                    if (wrkStg == Defs.WRK_STG_N && wrkTyp != "91")
                    {
                        var actCod = row["ACT_COD"].ToString(); // 중복 추가 방지
                        if (actList.Contains(actCod) == false)
                        {
                            dt.ImportRow(row);
                            actList.Add(actCod);
                        }
                    }
                }
                return dt;
            }
            return null;
        }

        /// <summary>
        /// ACT 저장 데이터, 족장 (TSFA001)
        /// N91 만
        /// </summary>
        /// <returns></returns>
        public DataTable GetSaveDataTableN91Act()
        {
            if (ActList != null)
            {
                var ds = new DataSet();
                var dt = ActList.dtDataSource.Clone();
                ds.Tables.Add(dt);

                HashSet<string> actList = new HashSet<string>();

                foreach (var row in ActList.dtDataSource.AsEnumerable())
                {
                    if (row.RowState == DataRowState.Deleted)   // N 공정 ACT는 삭제 불가
                        continue;

                    var wrkStg = row["WRK_STG"].ToString();
                    var wrkTyp = row["WRK_TYP"].ToString();

                    if (wrkStg == Defs.WRK_STG_N && wrkTyp == "91")
                    {
                        var actCod = row["ACT_COD"].ToString();
                        if (actList.Contains(actCod) == false)  // 중복 추가 방지
                        {
                            dt.ImportRow(row);
                            actList.Add(actCod);
                        }
                    }
                }
                return dt;
            }
            return null;
        }

        /// <summary>
        /// ACT 저장 데이터, 후행 (TSFN101)
        /// N 공정이 아닌 ACT
        /// </summary>
        public DataTable GetSaveDataTableAftAct()
        {
            if (ActList != null)
            {
                var ds = new DataSet();
                var dt = ActList.dtDataSource.Clone();
                ds.Tables.Add(dt);
                foreach (var row in ActList.dtDataSource.AsEnumerable())
                {
                    var wrkStg = string.Empty;
                    if (row.RowState == DataRowState.Deleted)
                        wrkStg = row["WRK_STG", DataRowVersion.Original].ToString();
                    else
                        wrkStg = row["WRK_STG"].ToString();

                    if (wrkStg != Defs.WRK_STG_N)
                        dt.ImportRow(row);
                }
                return dt;
            }
            return null;
        }

        /// <summary>
        /// 선각 ACT 저장 테이블
        /// 기존 테이블 복사하고 HO_GUBUN 이 1인 것만 추가
        /// </summary>
        /// <returns></returns>
        public DataTable GetUpdateTableHullAct()
        {
            return MakeSaveDataTableByHO_GUBUN("1");
        }


        /// <summary>
        /// 후행 ACT 저장 테이블
        /// 기존 테이블 복사하고 HO_GUBUN 이 2인 것만 추가
        /// </summary>
        /// <returns></returns>
        public DataTable GetUpdateTableAftAct()
        {
            return MakeSaveDataTableByHO_GUBUN("2");
        }

        private DataTable MakeSaveDataTableByHO_GUBUN(string HO_GUBUN)
        {
            if (ActList != null)
            {
                var ds = new DataSet();
                var dt = ActList.dtDataSource.Clone();
                ds.Tables.Add(dt);
                foreach (var row in ActList.dtDataSource.AsEnumerable())
                {
                    // 삭제된 행 IMport 확인 필요
                    //if (row["HO_GUBUN", DataRowVersion.Original].ToString() == HO_GUBUN)
                    var hoGubun = string.Empty;
                    if (row.RowState == DataRowState.Deleted)
                        hoGubun = row["HO_GUBUN", DataRowVersion.Original].ToString();
                    else
                        hoGubun = row["HO_GUBUN"].ToString();

                    if (hoGubun == HO_GUBUN)
                        dt.ImportRow(row);
                }
                return dt;
            }
            return null;
        }


        #endregion

        #region ACT/LINK 가져오기
        /* *************************************************************************************************
            Modify Log 

              Date          Requester           Description 
            _______________________________________________________________________________________________
            2019.06.25     유현우D         현상 : 마스터 스케줄에서 가져오기 (호선 : 3062 , 구획 : 48U00 ) 를 할때 시스템 다운 
                                           인덱스 범위 관련 오류로 인해 시스템 다운 현상 발생하여 수정 
            2019.07.05     유현우D         현상 : 마스터 스케줄에서 가져오기 (호선 : 2987 , 여러 개 구획 : 010/020/030/040/060/070/090 ) 를 할때 시스템 다운
                                           6.25 처리 관련 인덱스 0일경우 -1로 바뀜으로써 시스템 다운 현상 발생하여 수정
         *****************************************************************************************************/

        public void Import(string caseNo, string divCod, DataTSFN101List impActList, DataTSFN102List impLinkList)
        {
            if (ActList != null && string.IsNullOrEmpty(divCod) == false && impActList != null && impActList.Count > 0)
            {
                // 추가할 위치
                var idx = ActList.IndexOfOrder(divCod);
                //2019.06.25 
                if(idx != 0)
                idx = idx - 1; 

                // act 추가
                foreach (var act in impActList)
                {
                    var newAct = ActList.AddTempItem(null);
                    
                    act.CopyTo(newAct);

                    newAct.Calendar = this.Calendar;

                    var idxAct = ActList[idx];
                    ActList.AddItemNext(idxAct, newAct);
                    ActDict.Add(MakeDictKey(newAct.DIV_COD, newAct.ACT_COD), newAct);

                    idx++;
                }

                // 관계 추가
                if (impLinkList != null)
                {
                    foreach (var link in impLinkList)
                    {
                        //var preAct = GetAct(divCod, link.PRE_ACT);
                        //관계 추가 시 구획 간 연결을 위해 전 후행액트의 구획을 이용해 연결. 19.07.09 신성훈
                        var preAct = GetAct(link.PRE_DIV, link.PRE_ACT);
                        var aftAct = GetAct(link.AFT_DIV, link.AFT_ACT);
                        var newLink = AddLink(preAct, aftAct, link);
                    }
                }
            }
        }

        //public void Import(string divCod, DataTSFN101List impActList, DataTSFN102List impLinkList)
        //{
        //    if (ActList != null && string.IsNullOrEmpty(divCod) == false && impActList != null && impActList.Count > 0)
        //    {
        //        // 추가할 위치
        //        var idx = ActList.IndexOfOrder(divCod);

        //        // act 추가
        //        foreach (var act in impActList)
        //        {
        //            var newAct = ActList.AddTempItem(null);
        //            act.CopyTo(newAct);

        //            var idxAct = ActList[idx];
        //            ActList.AddItemNext(idxAct, newAct);

        //            idx++;
        //        }

        //        // 관계 추가
        //        if (impLinkList != null)
        //        {
        //            foreach (var link in impLinkList)
        //            {
        //                var preAct = GetAct(divCod, link.PRE_ACT);
        //                var aftAct = GetAct(divCod, link.AFT_ACT);
        //                var newLink = AddLink(preAct, aftAct, link);
        //            }
        //        }
        //    }
        //}

        #endregion

        #region 표준부서지정


        public void AssignStdDpt(DataTSFN101List actList)
        {
            if (actList != null)
            {
                foreach (var act in actList)
                {

                }
            }
        }


        #endregion

        #region  N 공정 탑재일자 맞추기

        // N41의 완료일을 나머지 N Stage Act의 최대 완료일로 맞춤
        public List<DataTSFN101> MakeN41StageFinishDates(string itmCod, string divCod)
        {
            var list = GetNStageActList(itmCod, divCod);
            if (list != null && list.Count > 0)
            {
                var n41 = list.FirstOrDefault(e => e.WRK_TYP == "41");
                if (n41 != null)
                {
                    var tmpList = new List<DataTSFN101>(list);
                    tmpList.Remove(n41);
                    if (tmpList.Count > 0)
                    {
                        n41.PlanFinish = tmpList.Max(e => e.PlanFinish);
                    }
                }
            }
            return list;
        }

        public List<DataTSFN101> GetNStageActList(string itmCod, string divCod)
        {
            if (ActList != null && string.IsNullOrEmpty(itmCod) == false && string.IsNullOrEmpty(divCod) == false)
            {
                return ActList.Where(e => e.ITM_COD == itmCod && e.DIV_COD == divCod && e.WRK_STG == Defs.WRK_STG_N).ToList();
            }
            return null;
        }


        /// <summary>
        /// 지정한 구획의 N 공정 ACT가 다른 구획에 포함된 경우 일정을 DIV_COD의 구획 일정으로 맞춤
        /// </summary>
        /// <param name="dIV_COD"></param>
        public List<string> SyncNStageActDates(string itmCod, string divCod)
        {
            // 공일한 ACT가 포함된 구획 목록
            var divList = new List<string>();
            divList.Add(divCod);

            var divNStageActs = GetNStageActList(itmCod, divCod);
            if (divNStageActs != null && divNStageActs.Count > 0)
            {
                foreach (var act in divNStageActs)
                {
                    var sameActCodActList = GetActList(act.ACT_COD);
                    if (sameActCodActList != null && sameActCodActList.Count > 1)
                    {
                        foreach (var tmpAct in sameActCodActList)
                        {
                            // 자신은 제외
                            if (act == tmpAct)
                                continue;

                            // 일정이 같으면 pass
                            if (tmpAct.PlanStart == act.PlanStart && tmpAct.PlanFinish == act.PlanFinish)
                                continue;

                            tmpAct.SetPlanDate(act.PlanStart, act.PlanFinish);
                            CalcDuration(tmpAct);

                            if (divList.Contains(tmpAct.DIV_COD) == false)
                                divList.Add(tmpAct.DIV_COD);
                        }
                    }
                }
            }
            return divList;
        }


        #endregion

        #region 순서 정렬

        public void ResetViewSeq(List<DataTSFN101> actList)
        {
            if (actList != null)
            {
                var prevDiv = string.Empty;
                var viewSeq = 0;

                foreach (var act in actList)
                {
                    if (act.DIV_COD != prevDiv)
                        viewSeq = 1;

                    act.VIEW_SEQ = viewSeq++;
                    prevDiv = act.DIV_COD;
                }
            }
        }

        #endregion
    }
}
