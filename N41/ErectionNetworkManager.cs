using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;

using HHI.Security;
using HHI.ServiceModel;
using HHI.Windows.Forms;
using HHI.Windows.Forms.Extensions;
using HHI.SHP.PP008.Client;
using HHI.SHP.PP008.COMMON;
using HHI.SHP.PP008.ENT.Enums;
using HHI.SHP.PP008.ENT.ErectControl;
using HHI.SHP.PP008.ENT.Interface;
using HHI.SHP.PP008.ENT.Utility;

namespace HHI.SHP.PP008.ENT.Models
{
    public class ErectionNetworkManager
    {
        //private int _dayMinvalue = -900;
        private int _dayMinvalue = Int16.MinValue;

        #region Property

        private HMDErecNetCalendar _ProdCalendar;

        public HMDErecNetCalendar ProdCalendar
        {
            get { return _ProdCalendar; }
            set { _ProdCalendar = value; }
        }
        public MergeBufferList MergeBuffers { get; set; }

        //기본계획 선표분석용 칼렌더
        private HMDErecNetCalendar managerCalendar;

        public HMDErecNetCalendar ManagerCalendar
        {
            get { return managerCalendar; }
            set { managerCalendar = value; }
        }

        public ErectionNetworkLoadInfoList loadinfoList;
        public ShipInfo shipinfo { get; set; }
        public HMDErecNetCalendar BasicCalendar { get; set; }
        public int FTNetDay { get; set; }
        public int KLNetDay { get; set; }
        public int LCNetDay { get; set; }
        public int ProjectTerm { get; set; }
        public int GapTerm { get; set; }
        public HistoryInfoModel HistoryInfo { get; set; }
        public List<RelationItemUnit> RelationBuffers { get; set; }

              public bool IsCalendar { get; set; } //어떤 Calendar가 적용되는지 false탑재 true생산

        public NodeItemUnitCollection NodeUnitCollection { set; get; }
        public RelationItemUnitCollection RelationUnitCollection { get; set; }

        #endregion

        #region member

        private string UpdateUser;

        private string IP;
        private string pid;
        //public AmiErectionNetworkControl ErectionNetworkControl;
        public ErectNetworkControl nwControl;

        public ErectWorkInfo nwUnDoWorkInfo = null;

        private DataSet dsErecNode;
        private DataSet dsErecNodeLink;

        //private IHHIMasterDataService masterDataService;

        public NodeItemUnit AfterNodeChk;
        public NodeItemUnit AfterNodeLast;
        public NodeItemUnit BeforeNodeLast;
              #endregion

        public ErectionNetworkManager(ShipInfo selectShip, ErectNetworkControl i_ErectNeworkControl, string UpdateUser, string ip, string pid)
        {
            // TODO: Complete member initialization
            this.shipinfo = selectShip;
            //this.ErectionNetworkControl = ErectionNetworkControl_2;
            this.nwControl = i_ErectNeworkControl;
            this.UpdateUser = UpdateUser;
            this.IP = ip;
            this.pid = pid;
            //this.masterDataService = masterDataService;
            MergeBuffers = new MergeBufferList();
            RelationBuffers = new List<RelationItemUnit>();
        }

        #region Calendar

        public void SetCalendar(HMDErecNetCalendar calendar)
        {
            BasicCalendar = calendar;
        }
              public int GetNetDayFromNetDay(int netDay)
        {
            if (BasicCalendar != null)
                return BasicCalendar.GetNetDayFromNetDay(netDay);

            return -1;
        }

        public string GetCalDayFromNetDay(int netDay)
        {
            if (BasicCalendar != null)
                return BasicCalendar.GetCalDayFromNetDay(netDay);

            return string.Empty;
        }


        /// <summary>
        /// 달력 생성.
        /// </summary>
        /// <param name="calendar"></param>
        /// <param name="IsCalendar"></param>
        /// <returns></returns>
        public HMDErecNetCalendar CreatCalendar(DataTable calendar, bool IsCalendar)
        {
            if (calendar == null || calendar.Rows.Count < 1)
                return null;

            if (IsCalendar)
            {
                _ProdCalendar = new HMDErecNetCalendar();
                foreach (DataRow calUnit in calendar.Rows)
                {
                    if (calUnit == calendar.Rows[0])
                    {
                        _ProdCalendar.AddFirstItem(calUnit);
                    }
                    else
                    {
                        _ProdCalendar.AddItem(calUnit);
                    }
                }
                return _ProdCalendar;

            }
            else
            {
                managerCalendar = new HMDErecNetCalendar();
                foreach (DataRow calUnit in calendar.Rows)
                {
                    if (calUnit == calendar.Rows[0])
                    {
                        managerCalendar.AddFirstItem(calUnit);
                    }
                    else
                    {
                        managerCalendar.AddItem(calUnit);
                    }
                }
                return managerCalendar;
            }

            return null;
        }

        public void ChangeCalendar(int ProjectTerm) //true : 생산칼렌더  false : 선표분석용 칼렌더
        {
            if (IsCalendar)
                SetCalendar(ProdCalendar);
            else
                SetCalendar(managerCalendar);


            InitDays(ProjectTerm);

        }

        internal void SyncEntLnt(int oldNetDay)
        {
            foreach (NodeItemUnit nodeItemUnit in this.NodeUnitCollection)
            {
                int nEnt = nodeItemUnit.EntNet;
                int nLnt = nodeItemUnit.LntNet;
                nEnt = GetNetDayFromNetDay((oldNetDay + (nodeItemUnit.EntNet - 1))) - KLNetDay + 1;
                nLnt = GetNetDayFromNetDay((oldNetDay + (nodeItemUnit.LntNet - 1))) - KLNetDay + 1;
                if (nodeItemUnit.EntNet != nEnt)
                {

                }
                if (nodeItemUnit.LntNet != nLnt)
                {

                }
                nodeItemUnit.EntNet = nEnt;
                nodeItemUnit.LntNet = nLnt;
            }
        }
        internal void UpdatePitch()
        {
            foreach (RelationItemUnit relationItemUnit in this.RelationUnitCollection)
            {
                NodeItemUnit PreNode = GetNodeItemUnit(relationItemUnit.PreccedeNodeKey);
                NodeItemUnit AftNode = GetNodeItemUnit(relationItemUnit.AfterNodeKey);
                int nPitch = 0;
                if (PreNode != null && AftNode != null)
                {
                    int nEntPitch = AftNode.EntNet - PreNode.EntNet;
                    int nLntPitch = AftNode.LntNet - PreNode.LntNet;

                    if (nLntPitch > nEntPitch)
                        nPitch = nEntPitch;
                    else nPitch = nLntPitch;

                    if (PreNode.BlockList == "F/T")
                        nPitch = nEntPitch;

                    relationItemUnit.Pitch = nPitch;
                }
            }
        }
        internal void SetNodeCalType()
        {
            foreach (NodeItemUnit nodeItemUnit in this.NodeUnitCollection)
            {
                nodeItemUnit.Caltype = 1;
            }
        }

        public void InitDays(int projectTerm)
        {
            if (shipinfo.KL == null || shipinfo.LC == null)
                return;

            if (shipinfo.FT != null)
                FTNetDay = BasicCalendar.GetNetDayFromCalDay(shipinfo.FT);
            if (BasicCalendar != null)
            {
                KLNetDay = BasicCalendar.GetNetDayFromCalDay(shipinfo.KL);
                LCNetDay = BasicCalendar.GetNetDayFromCalDay(shipinfo.LC);
                this.ProjectTerm = projectTerm;
            }
        }
        /// <summary>
        /// 현재프로젝트의 Calendar를 만들어냄
        /// </summary>
        /// <param name="startDate"></param>
        /// <returns></returns>
        internal HMDErecNetCalendar MakeCalendarinProject(DateTime startDate)
        {
            HMDErecNetCalendar calendar = new HMDErecNetCalendar();
            TimeSpan span = new TimeSpan(0, 0, 0);
            DateTime kLDate = DateTime.ParseExact(shipinfo.KL, "yyyyMMdd", CultureInfo.InvariantCulture);
            DateTime LCDate = DateTime.ParseExact(shipinfo.LC, "yyyyMMdd", CultureInfo.InvariantCulture);
            DateTime AfterNodeDate;

            int num = 0;

            if (AfterNodeLast != null)
            {
                AfterNodeDate = DateTime.ParseExact(CalculateStrEntDate(AfterNodeLast), "yyyyMMdd", CultureInfo.InvariantCulture);
                if (AfterNodeDate < startDate)
                {
                    num = AfterNodeLast.EntNet;
                    startDate = AfterNodeDate;
                }
            }

            if (startDate < kLDate)
            {
                num = BasicCalendar[startDate.ToString("yyyyMMdd")].NetDay - BasicCalendar[kLDate.ToString("yyyyMMdd")].NetDay;
            }
            else
            {
                num = 1;
                startDate = kLDate;
            }

            while (startDate <= LCDate)
            {
                if (num == 0)
                    num = num + 1;

                string currentDate = startDate.ToString("yyyyMMdd");
                HMDErecNetDayUnit NetDayUnit = BasicCalendar[currentDate];
                calendar.Add(currentDate, NetDayUnit);
                calendar[currentDate].Number = num;
                if (calendar[currentDate].IsHolyday)
                {

                }
                else
                {
                    num = num + 1;
                }

                startDate = startDate.AddDays(1);
            }

            return calendar;
        }
        
        internal HMDErecNetCalendar MakeCalendarInLoad()
        {
            HMDErecNetCalendar calendar = new HMDErecNetCalendar();

            DateTime kLDate = DateTime.ParseExact(shipinfo.KL, "yyyyMMdd", CultureInfo.InvariantCulture);
            DateTime LCDate = DateTime.ParseExact(shipinfo.LC, "yyyyMMdd", CultureInfo.InvariantCulture);

            int count = 1;
            while (kLDate <= LCDate)
            {
                string currentDate = kLDate.ToString("yyyyMMdd");
                HMDErecNetDayUnit NetDayUnit = BasicCalendar[currentDate];
                if (NetDayUnit.IsHolyday == false)
                {
                    calendar.Add(count.ToString(), NetDayUnit);
                    calendar[count.ToString()].NetDay = (calendar[count.ToString()].NetDay - KLNetDay + 1);
                    count++;
                }
                kLDate = kLDate.AddDays(1);
            }

            return calendar;
        }
        public int GetNetDayFromCalDay(string p)
        {
            return this.BasicCalendar.GetNetDayFromCalDay(p);
        }

        #endregion

        #region Make

        public HistoryInfoModel MakeHistory(DataSet ds, DataTable dtHistory)
        {
            return HistoryInfo = new HistoryInfoModel(ds, dtHistory, shipinfo);
        }

        public void MakeNodeUnit(DataTable dtNodeList, DataSet dsNodeList, DataSet dsAct, DataTable dtAct, DataSet dsSunBlock, DataTable dtSunBlock)
        {
            //노드 정보 담을 컬렉션
            NodeUnitCollection = new NodeItemUnitCollection(dsNodeList, dsAct);
            if (dtNodeList != null)
            {
                foreach (DataRow item in dtNodeList.Rows)
                {
                    DataRow actRow = GetFindActRow(item, dtAct);
                    DataRow SunBlockRow = GetFindRow(item, dtSunBlock);
                    var a = NodeUnitCollection.AddNode(item, actRow, SunBlockRow);
                }
            }

            var tmp = NodeUnitCollection.Where(t => t.ItemCode == "E11A3");
        }

        private DataRow GetFindActRow(DataRow oriRow, DataTable dt)
        {
            foreach (DataRow row in dt.Rows)
            {
                if (oriRow[ATSEB001.MIS_NOD].ToString() == row[ATSEA002.MIS_COD].ToString() && row[ATSEA002.ACT_COD].ToString().Contains("N41000") == true)
                {
                    return row;
                }
            }
            return null;
        }

        private DataRow GetFindRow(DataRow oriRow, DataTable dt)
        {
            foreach (DataRow row in dt.Rows)
            {
                if (oriRow[ATSEB001.MIS_NOD].ToString() == row[ATSEA002.MIS_COD].ToString())
                {
                    return row;
                }
            }
            return null;
        }
        public void MakeRelationUnit(DataTable linkList, DataSet dslinkList)
        {
            MakeRelationUnitCollection(dslinkList);
            if (linkList != null && linkList.Rows.Count > 0)
            {
                foreach (DataRow item in linkList.Rows)
                {
                    var relation = RelationUnitCollection.AddRelation(item);
                }
            }

            CheckAssigned();
        }

        private void CheckAssigned()
        {
            foreach (NodeItemUnit node in this.NodeUnitCollection)
            {
                if (node.PosY < 0 && (GetChildRlation(node.MISCode).Count == 0 && GetParentRlation(node.MISCode).Count == 0))
                {
                    node.IsAssigned = false;
                }
            }
        }
              internal void MakeRelationUnitCollection(DataSet dslinkList)
        {
            RelationUnitCollection = new RelationItemUnitCollection(dslinkList);
        }

        internal List<RelationItemUnit> MakeRelationUnit(List<ErectNodeControl> i_nwNodeList)
        {
            NodeItemUnit LCNode = GetLCNode();
            List<RelationItemUnit> newRelationList = new List<RelationItemUnit>();

            if (LCNode != null)
            {
                // 종료선 연결 정보
                foreach (var eachNodeItem in i_nwNodeList)
                {
                    try
                    {
                        // 선후행 관계에 L/C 가 존재하는지 확인
                        if (this.RelationUnitCollection.Contains(eachNodeItem.NodeKey, LCNode.MISCode) == true) continue;
                        // 이전 노드에서 L/C 존재 확인 : L/C 근접이 아닌 경우 오류가 발생하여 로직 추가 (2021-02-26)
                        if (this.RelationUnitCollection.isPreNode_LC_Exist(eachNodeItem.NodeKey, LCNode.MISCode) == true) continue;

                        RelationItemUnit newRelation = this.RelationUnitCollection.AddEndLienRelation(eachNodeItem.NodeKey, LCNode.MISCode, this.UpdateUser, this.shipinfo);
                        if (newRelation != null)
                        {
                            newRelationList.Add(newRelation);
                        }
                    }
                    catch { }
                }
            }

            return newRelationList;
        }
        #endregion

        #region item
        public List<NodeItemUnit> GetLeafNode()
        {
            if (this.NodeUnitCollection != null)
            {
                List<NodeItemUnit> leafNodes = new List<NodeItemUnit>();
                foreach (var node in this.NodeUnitCollection)
                {
                    if (GetChildRelationItemUnits(node.MISCode).Count == 0)
                        leafNodes.Add(node);
                }

                return leafNodes;
            }

            return null;
        }
        public NodeItemUnit GetMaxLeafNode()
        {
            int count = 0;
            NodeItemUnit max = null;

            foreach (NodeItemUnit item in GetLeafNode())
            {
                if (count < item.EntNet)
                {
                    count = item.EntNet;
                    max = item;
                }
            }
            return max;
        }

        internal List<NodeItemUnit> GetFTNodes()
        {
            return this.NodeUnitCollection.Where(p => p.BlockList == "F/T" && p.BlockList == "F/T2" && p.BlockList == "F/T3").ToList();
        }

        internal NodeItemUnit GetFTNode()
        {
            return this.NodeUnitCollection.Where(p => p.BlockList == "F/T").FirstOrDefault();
        }
        internal NodeItemUnit GetFT2Node()
        {
            return this.NodeUnitCollection.Where(p => p.BlockList == "F/T2").FirstOrDefault();
        }

        internal NodeItemUnit GetFT3Node()
        {
            return this.NodeUnitCollection.Where(p => p.BlockList == "F/T3").FirstOrDefault();
        }

        internal List<NodeItemUnit> GetFTNodes2()
        {
            return this.NodeUnitCollection.Where(p => p.BlockList == "F/T").ToList();
        }

        internal List<NodeItemUnit> GetFT2Nodes()
        {
            return this.NodeUnitCollection.Where(p => p.BlockList == "F/T2").ToList();
        }

        internal List<NodeItemUnit> GetFT3Nodes()
        {
            return this.NodeUnitCollection.Where(p => p.BlockList == "F/T3").ToList();
        }
        internal List<RelationItemUnit> GetCriticalPath()
        {
            return this.RelationUnitCollection.Where(p => p.IsCriticalPath).ToList();
        }

        internal List<RelationItemUnit> GetChildRelationItemUnits(string Nodekey)
        {
            List<RelationItemUnit> list = new List<RelationItemUnit>();

            list = this.RelationUnitCollection.Where(p => p.PreccedeNodeKey == Nodekey).ToList();

            return list;
        }

        internal List<RelationItemUnit> GetParentRelationItemUnits(string Nodekey)
        {
            List<RelationItemUnit> list = new List<RelationItemUnit>();

            list = this.RelationUnitCollection.Where(p => p.PreccedeNodeKey == Nodekey).ToList();

            return list;
        }
        internal List<NodeItemUnit> GetChildNodeItemUnits(string key)
        {
            List<NodeItemUnit> list = new List<NodeItemUnit>();

            List<RelationItemUnit> childRelations = GetChildRelationItemUnits(key);

            foreach (RelationItemUnit relation in childRelations)
            {
                NodeItemUnit node = GetNodeItemUnit(relation.AfterNodeKey);
                if (node != null)
                {
                    list.Add(node);
                }
            }

            return list;
        }

        internal List<NodeItemUnit> GetParentNodeItemUnits(string RelationKey)
        {
            List<NodeItemUnit> list = new List<NodeItemUnit>();

            List<RelationItemUnit> parentRelations = GetParentRelationItemUnits(RelationKey);
            foreach (RelationItemUnit relation in parentRelations)
            {
                NodeItemUnit node = GetNodeItemUnit(relation.PreccedeNodeKey);
                if (node != null)
                {
                    list.Add(node);
                }
            }

            return list;
        }

        internal void AddNode(NodeItemUnit nodeItemUnit)
        {
            if (this.NodeUnitCollection.ContainsNode(nodeItemUnit.MISCode))
            {

            }
            else
            {
                this.NodeUnitCollection.Add(nodeItemUnit);
            }
        }
        internal void AddNewNode(NodeItemUnit nodeItemUnit)
        {
            if (nodeItemUnit != null && NodeUnitCollection != null)
            {
                nodeItemUnit.ShipCode = shipinfo.SHP_COD;
                nodeItemUnit.FigShip = shipinfo.FIG_SHP;
                NodeUnitCollection.AddNewNode(nodeItemUnit);
            }
        }

        /// <summary>
        /// 새로운 MISCODE를 만들어냄.
        /// </summary>
        /// <returns></returns>
        internal string FindNewMisCode()
        {
            List<NodeItemUnit> list = new List<NodeItemUnit>();

            foreach (NodeItemUnit unit in NodeUnitCollection)
            {
                if (CheckChageInt(unit.MISCode))
                    list.Add(unit);
            }
            string maxCode = list.Max(p => p.MISCode);
            //19.3.20 신성훈 MIS 채번 끝자리가 0이면 건너뛰도록 변경
            //한번에 여러번 절점노드 추가할때 탑재네트웍 신규/수정 시 액트의 MIS노드와 겹쳐 오류발생하기 때문.
            int temp;

            temp = (int.Parse(maxCode) + 1);

            if (temp % 10 == 0)
            {
                temp++;
                maxCode = temp.ToString();
            }

            return (temp).ToString();

        }

        internal bool CheckChageInt(string key)
        {
            try
            {
                int.Parse(key);
            }
            catch
            {
                return false;
            }
            return true;
        }
        internal List<string> GetFindBlockKeyLIst(string i_FindText, bool isFindType)
        {
            i_FindText = i_FindText.ToLower();
            List<string> FindKeyList = new List<string>();
            foreach (NodeItemUnit node in NodeUnitCollection)
            {
                if (isFindType)
                {
                    if (node.MISCode.ToLower().IndexOf(i_FindText) != -1)
                    {
                        FindKeyList.Add(node.MISCode);
                    }
                }
                else
                {
                    if (node.BlockList != null)
                    {
                        if (node.BlockList.ToLower().IndexOf(i_FindText) != -1)
                        {
                            FindKeyList.Add(node.MISCode);
                        }
                    }

                }
            }
            return FindKeyList;
        }
        internal List<NodeItemUnit> GetNodeItemUnits(List<string> keyList)
        {
            List<NodeItemUnit> nodeList = new List<NodeItemUnit>();

            foreach (string key in keyList)
            {
                NodeItemUnit node = GetNodeItemUnit(key);
                if (node != null)
                    nodeList.Add(node);
            }

            return nodeList;
        }

        internal NodeItemUnit GetNodeItemUnit(string key)
        {
            foreach (NodeItemUnit node in NodeUnitCollection)
            {
                if (node.MISCode == key)
                    return node;
            }

            return null;
        }
        internal RelationItemUnit GetRelationItemUnit(string key)
        {
            foreach (RelationItemUnit relation in RelationUnitCollection)
            {
                if (relation.RelationKey == key)
                    return relation;
            }

            return null;
        }

        internal NodeItemUnit GetLCNode()
        {
            foreach (NodeItemUnit node in this.NodeUnitCollection)
            {
                if (node.BlockList == "L/C")
                    return node;
            }

            return null;
        }
        internal NodeItemUnit GetKLNode()
        {
            foreach (NodeItemUnit node in this.NodeUnitCollection)
            {
                if (node.IsKL)
                    return node;
            }

            return null;
        }

        internal NodeItemUnit GetKL2Node()
        {
            foreach (NodeItemUnit node in this.NodeUnitCollection)
            {
                if (node.IsKL2)
                    return node;
            }

            return null;
        }
        private List<NodeItemUnit> GetKLItemNodes()
        {
            List<NodeItemUnit> list = new List<NodeItemUnit>();

            foreach (NodeItemUnit node in this.NodeUnitCollection)
            {
                if (node.BlockList == "K/L")
                    list.Add(node);
            }
            return list;
        }

        internal List<RelationItemUnit> GetChildRlation(string nodeKey)
        {
            return this.RelationUnitCollection.Where(p => p.PreccedeNodeKey == nodeKey).ToList();
        }

        internal List<RelationItemUnit> GetParentRlation(string nodeKey)
        {
            return this.RelationUnitCollection.Where(p => p.AfterNodeKey == nodeKey).ToList();
        }
              internal List<RelationItemUnit> GetParentRlation_pre(string nodeKey)
        {
            return this.RelationUnitCollection.Where(p => p.PreccedeNodeKey == nodeKey).ToList();
        }

        internal NodeItemUnit GetAfterNode(RelationItemUnit relation)
        {
            return GetNodeItemUnit(relation.AfterNodeKey);
        }

        internal NodeItemUnit GetPreccedeNode(RelationItemUnit relation)
        {
            return GetNodeItemUnit(relation.PreccedeNodeKey);
        }

        private List<NodeItemUnit> GetAfterNodes(NodeItemUnit node)
        {
            List<NodeItemUnit> list = new List<NodeItemUnit>();
            List<RelationItemUnit> relationList = GetChildRlation(node.MISCode);

            foreach (RelationItemUnit relation in relationList)
            {
                NodeItemUnit afterNode = GetAfterNode(relation);
                if (afterNode != null)
                {
                    if (node.BlockList != "L/C")
                        list.Add(node);
                    list.AddRange(GetAfterNodes(afterNode));
                }
            }

            return list;
        }

        internal RelationItemUnit AddNewRelation(string afterNodekey, string preccedeNodekey)
        {
            NodeItemUnit afterNode = GetNodeItemUnit(afterNodekey);

            NodeItemUnit preccedeNode = GetNodeItemUnit(preccedeNodekey);

            if (afterNode != null && preccedeNode != null)
            {
                return this.RelationUnitCollection.AddRelation(afterNode, preccedeNode, this.shipinfo, this.UpdateUser);
            }

            return null;
        }

        internal RelationItemUnit AddRelation(RelationItemUnit relation)
        {
            return RelationUnitCollection.AddRelation(relation);
        }

        internal void RemoveRelation(RelationItemUnit unit)
        {
            if (unit != null)
            {
                this.RelationUnitCollection.RemoveRelation(unit);
            }
        }
        internal void RemoveRelation(string RelationKey)
        {
            RelationItemUnit unit = GetRelationItemUnit(RelationKey);
            if (unit != null)
            {
                this.RemoveRelation(unit);
            }
        }
        internal void RemoveNode(ErectNodeControl node)
        {
            NodeItemUnit unit = GetNodeItemUnit(node.NodeKey);

            if (unit != null)
            {
                this.RemoveNode(unit);
            }
        }

        internal void RemoveNode(NodeItemUnit node)
        {
            this.NodeUnitCollection.RemoveNode(node);
        }
        #endregion

        #region UpdateEntLnt
        public bool UpdateEntLnt(NodeUnit cyclicNode = null)
        {
            ErecNetworkSchLogic schLogic = new ErecNetworkSchLogic();
            NodeItemUnit LCNode = GetLCNode();
            NodeItemUnit KLNode = GetKLNode();

            schLogic.MaxEnt = this.LCNetDay - this.KLNetDay + 1;
            List<NodeItemUnit> mergeList = InsertNode(schLogic);
            InsertRelation(schLogic);

            var tmp = NodeUnitCollection.Where(t => t.ItemCode == "E11A3");

            bool retEnt = UpdateEnt(schLogic);
            if (retEnt == false)
            {
                return false;
            }
            bool retLnt = UpdateLnt(schLogic);
            var tmp2 = NodeUnitCollection.Where(t => t.ItemCode == "E11A3");
            if (retLnt == false)
            {
                return false;
            }

            UpdateCriticalPath(schLogic);
            UpdateEntLntDifferent();
            UpdateEntLntBORINGNodes();  //Boring Node 중간(KL 이후 LC 이전)에 붙는 경우 있다...따로 빼냄

            // L/C 이후 노드 계산
            UpdateEntLntAfterLCNodes(LCNode, 0, 0);

            // 2017.07.17 한승훈SW 수정
            // KL노드 혹은 KL절점 이전에 속한 노드에 -Pitch 개념을 도입
            if (KLNode != null)
            {
                //UpdateEntLntAfterKLNodes(KLNode, int.MinValue, int.MinValue);
                UpdateAfterKLNodes_TEST();
            }

            //   UpdateErrorPath(afterLCNodes);
            // 머지 노드 자식
            UpdateMergeNodeEntLnt(mergeList);

            return retEnt & retLnt;
        }
        private void UpdateEntLntDifferent()
        {
            List<NodeItemUnit> DifferntNodes = GetDifferentNode();

            foreach (NodeItemUnit node in DifferntNodes)
            {
                if (node.BlockList == "K/L")
                    break;

                var relations = GetChildRlation(node.MISCode);

                if (relations != null && relations.Count > 0)
                {
                    NodeItemUnit afterNode = GetAfterNode(relations[0]);

                    if (afterNode != null)
                    {
                        if (Math.Abs(relations[0].Pitch) > afterNode.EntNet)
                        {
                            node.EntNet = afterNode.EntNet + relations[0].Pitch;
                            node.LntNet = afterNode.LntNet + relations[0].Pitch;
                        }
                        else
                        {
                            node.EntNet = afterNode.EntNet - relations[0].Pitch;
                            node.LntNet = afterNode.LntNet - relations[0].Pitch;
                        }
                    }
                }
            }
        }

        private List<NodeItemUnit> GetDifferentNode()
        {
            List<NodeItemUnit> list = new List<NodeItemUnit>();

            foreach (NodeItemUnit node in this.NodeUnitCollection)
            {
                if (node.IsKL == false && node.IsKL2 == false && node.MISCode == node.RepresentNode)
                {
                    var relations = GetParentRlation(node.MISCode);
                    if (relations == null || relations.Count == 0)
                        list.Add(node);
                }
            }

            return list;
        }
        private void UpdateEntLntBORINGNodes()
        {
            if (NodeUnitCollection != null)
            {
                List<NodeItemUnit> boringNodes = GetBORINGNodes();

                if (boringNodes != null && boringNodes.Count > 0)
                {
                    foreach (NodeItemUnit unit in boringNodes)
                    {
                        var relations = GetChildRlation(unit.MISCode);

                        if (relations != null && relations.Count > 0)
                        {
                            NodeItemUnit afterNode = GetAfterNode(relations[0]);
                            unit.EntNet = afterNode.EntNet + relations[0].Pitch;
                            unit.LntNet = afterNode.LntNet + relations[0].Pitch;
                            if (unit.EntNet == 0)
                                unit.EntNet = unit.EntNet + 1;

                            if (unit.LntNet == 0)
                                unit.LntNet = unit.LntNet + 1;

                            if (unit.EntNet < 0)
                                unit.EntNet = unit.EntNet - 1;
                            if (unit.LntNet < 0)
                                unit.LntNet = unit.LntNet - 1;
                        }

                        relations = GetParentRlation(unit.MISCode);
                        if (relations != null && relations.Count > 0)
                        {
                            foreach (RelationItemUnit relation in relations)
                            {
                                NodeItemUnit preNode = GetPreccedeNode(relation);
                                int nextEnt = relation.Pitch + unit.EntNet;
                                int nextLnt = relation.Pitch + unit.LntNet;

                                UpdateEntLntAfterKLNodes(preNode, nextEnt, nextLnt);
                            }
                        }
                    }
                }
            }
        }

        private List<NodeItemUnit> GetBORINGNodes()
        {
            List<NodeItemUnit> list = new List<NodeItemUnit>();
            NodeItemUnit preNode = GetKLNode();
            while (preNode != null)
            {
                var relations = GetParentRlation(preNode.MISCode);
                preNode = null;
                foreach (RelationItemUnit relation in relations)
                {
                    preNode = GetPreccedeNode(relation);
                    if (preNode != null)
                    {
                        if (preNode.BlockList == "BORING")
                        {
                            list.Add(preNode);
                        }
                    }
                }
            }
            return list;
        }
      
        private void UpdateEntLntAfterKLNodes(NodeItemUnit node, int ent, int lnt)
        {
            if (node != null)
            {
                List<RelationItemUnit> list = GetParentRlation(node.MISCode);

                //if (node.IsKL && ent == int.MinValue && lnt == int.MinValue)
                if (node.IsKL && ent == this._dayMinvalue && lnt == this._dayMinvalue)
                {
                    foreach (RelationItemUnit relation in list)
                    {
                        int minusPitch = relation.Pitch;
                        NodeItemUnit aftNode = GetAfterNode(relation);
                        NodeItemUnit preNode = GetPreccedeNode(relation);

                        // 2013.11.22 도성민 
                        int prevEnt = aftNode.EntNet + minusPitch;
                        int prevLnt = aftNode.LntNet + minusPitch;
                        // 2017.07.17 K/L 노드의 PRE노드가 K/L 절점이 아니면, -Pitch 정의에 의해 Pitch값 -1 
                        if (preNode.ItemCode != "K/L")
                        {
                            prevEnt--;
                            prevLnt--;
                        }

                        UpdateEntLntAfterKLNodes(preNode, prevEnt, prevLnt);
                    }
                }
                else
                {
                    if (!node.IsKL)
                    {
                        if (ent == 0)
                            node.EntNet = ent + 1;
                        else
                            node.EntNet = ent;

                        if (lnt == 0)
                            node.LntNet = lnt + 1;
                        else
                            node.LntNet = lnt;
                    }
                    foreach (RelationItemUnit relation in list)
                    {
                        NodeItemUnit preNode = GetPreccedeNode(relation);
                        // 2013.11.22 도성민 
                        int minusPitch = relation.Pitch;
                        int prevEnt = ent + minusPitch;
                        int prevLnt = lnt + minusPitch;

                        // 2017.07.17 K/L 노드의 PRE노드가 K/L 절점일 경우, -Pitch 정의에 의해 Pitch값 -1 
                        if (minusPitch * ent < 0 && minusPitch + ent < 0)
                            prevEnt--;

                        if (minusPitch * lnt < 0 && minusPitch + lnt < 0)
                            prevLnt--;
                        UpdateEntLntAfterKLNodes(preNode, prevEnt, prevLnt);
                    }

                    if (list.Count == 0)
                    {
                        node.EntNet = ent;
                        node.LntNet = lnt;
                        AfterNodeLast = node;
                        ClearEntLntAfterKLNodes_Foward(node);
                        UpdateEntLntAfterKLNodes_Foward(node, ent, lnt);
                    }
                }
            }
        }
        private void ClearEntLntAfterKLNodes_Foward(NodeItemUnit node)
        {
            if (node != null)
            {
                List<RelationItemUnit> list = GetParentRlation_pre(node.MISCode);

                if (list.Count > 0)
                {
                    foreach (RelationItemUnit relation in list)
                    {
                        NodeItemUnit aftNode = GetAfterNode(relation);

                        if (node.BlockList != "L/C" && node.BlockList != "K/L" && !(node.IsKL))
                        {
                            if (aftNode.BlockList != "L/C" && aftNode.BlockList != "K/L" && !(aftNode.IsKL))
                            {
                                //aftNode.EntNet = int.MinValue;
                                //aftNode.LntNet = int.MinValue;
                                aftNode.EntNet = this._dayMinvalue;
                                aftNode.LntNet = this._dayMinvalue;
                            }
                        }
                        if (node.BlockList == "L/C" || node.BlockList == "K/L" || node.IsKL)
                        {
                            return;
                        }

                        ClearEntLntAfterKLNodes_Foward(aftNode);
                    }
                }
            }
        }

        private void UpdateEntLntAfterKLNodes_Foward(NodeItemUnit node, int ent, int lnt)
        {
            if (node != null)
            {
                List<RelationItemUnit> list = GetParentRlation_pre(node.MISCode);

                if (list.Count > 0)
                {
                    foreach (RelationItemUnit relation in list)
                    {
                        int minusPitch = relation.Pitch;
                        int nextEnt = 0;
                        int nextLnt = 0;
                        NodeItemUnit aftNode = GetAfterNode(relation);

                        if (node.BlockList != "L/C" && node.BlockList != "K/L" && !(node.IsKL))
                        {
                            if (aftNode.BlockList != "L/C" && aftNode.BlockList != "K/L" && !(aftNode.IsKL))
                            {
                                if (minusPitch < 0)
                                {
                                    if (ent < 0 && aftNode.EntNet < ent - minusPitch)
                                        aftNode.EntNet = ent - minusPitch;
                                    else if (ent >= 0 && aftNode.EntNet < ent + minusPitch)
                                        aftNode.EntNet = ent + minusPitch;


                                    if (lnt < 0 && aftNode.LntNet < lnt - minusPitch)
                                        aftNode.LntNet = lnt - minusPitch;
                                    else if (lnt >= 0 && aftNode.LntNet < lnt + minusPitch)
                                        aftNode.LntNet = lnt + minusPitch;
                                }
                                else
                                {
                                    aftNode.EntNet = ent + minusPitch;
                                    aftNode.LntNet = lnt + minusPitch;
                                }

                                nextEnt = aftNode.EntNet;
                                nextLnt = aftNode.LntNet;

                                if (minusPitch * ent < 0 && minusPitch + ent >= 0)
                                    aftNode.EntNet = nextEnt + 1;


                                if (minusPitch * lnt < 0 && minusPitch + lnt >= 0)
                                    aftNode.LntNet = nextLnt + 1;
                            }
                        }
                        if (node.BlockList == "L/C" || node.BlockList == "K/L" || node.IsKL)
                        {
                            return;
                        }

                        UpdateEntLntAfterKLNodes_Foward(aftNode, nextEnt, nextLnt);
                    }
                }
            }
        }

        private void UpdateAfterKLNodes_TEST()
        {
            List<NodeItemUnit> nList = GetKLItemNodes();

            foreach (NodeItemUnit nodeItem in nList)
            {
                ClearAfterKLBackward_TEST(nodeItem);
            }

            foreach (NodeItemUnit nodeItem in nList)
            {
                UpdateAfterKLBackward_TEST(nodeItem);
            }

            UpdateAfterKLForward_TEST(BeforeNodeLast);
            UpdateAfterKLCP_TEST(BeforeNodeLast);
        }

        private void ClearAfterKLBackward_TEST(NodeItemUnit node)
        {
            if (node != null)
            {
                node.EntNet = int.MaxValue;
                node.LntNet = int.MaxValue;

                List<RelationItemUnit> list = GetParentRlation(node.MISCode);

                foreach (RelationItemUnit relation in list)
                {
                    NodeItemUnit preNode = GetPreccedeNode(relation);
                    ClearAfterKLBackward_TEST(preNode);
                }
            }
        }

        List<string> DUAL_CHK = new List<string>();
        private void ClearForward_TEST(NodeItemUnit node)
        {
            if (node != null)
            {
                // 신규 코드에는 초기화 값을 _dayMinvalue(Int16.MinValue)로 설정
                node.EntNet = this._dayMinvalue;
                node.LntNet = this._dayMinvalue;

                List<RelationItemUnit> list = GetParentRlation_pre(node.MISCode);

                foreach (RelationItemUnit relation in list)
                {
                    NodeItemUnit aftNode = GetAfterNode(relation);
                    if (aftNode != null)
                    {
                        ClearForward_TEST(aftNode);
                    }
                }
            }
        }

        private void ClearBackward_TEST(NodeItemUnit node)
        {
            if (node != null)
            {
                node.LntNet = int.MaxValue;

                List<RelationItemUnit> list = GetParentRlation(node.MISCode);

                foreach (RelationItemUnit relation in list)
                {
                    NodeItemUnit preNode = GetPreccedeNode(relation);
                    ClearBackward_TEST(preNode);
                }
            }
        }

        private void UpdateAfterKLBackward_TEST(NodeItemUnit node)
        {
            if (node != null)
            {
                if (node.BlockList == "K/L")
                {
                    node.EntNet = int.MaxValue;
                    node.LntNet = 1;
                }

                List<RelationItemUnit> list = GetParentRlation(node.MISCode);

                if (list.Count == 0)
                {
                    BeforeNodeLast = node;
                }
                foreach (RelationItemUnit relation in list)
                {
                    int minusPitch = -relation.Pitch;
                    int prevLnt = 0;
                    NodeItemUnit preNode = GetPreccedeNode(relation);

                    if (preNode != null)
                    {
                        prevLnt = node.LntNet + minusPitch;

                        if (node.LntNet * minusPitch < 0 && node.LntNet + minusPitch < 0)
                        {
                            prevLnt--;
                        }
                        else if (node.LntNet * minusPitch < 0 && node.LntNet + minusPitch > 0)
                        {
                            prevLnt++;
                        }
                        else if (node.LntNet * minusPitch < 0 && node.LntNet + minusPitch == 0)
                        {
                            if (node.LntNet > 0)
                            {
                                prevLnt--;
                            }
                            else if (node.LntNet < 0)
                            {
                                prevLnt++;
                            }
                        }

                        if (preNode.LntNet > prevLnt)
                        {
                            preNode.LntNet = prevLnt;
                            //preNode.EntNet = int.MinValue;
                            preNode.EntNet = this._dayMinvalue;
                        }

                        UpdateAfterKLBackward_TEST(preNode);
                    }
                }
            }
        }

        private void UpdateAfterKLForward_TEST(NodeItemUnit node)
        {
            if (node != null)
            {
                List<RelationItemUnit> list = GetParentRlation_pre(node.MISCode);

                if (node.MISCode == BeforeNodeLast.MISCode)
                {
                    //node.EntNet = 1;
                    node.EntNet = node.LntNet;
                }

                if (node.BlockList == "K/L")
                {
                    node.EntNet = 1;
                    return;
                }

                if (node.BlockList == "L/C")
                {
                    node.EntNet = node.LntNet;
                    return;
                }
                foreach (RelationItemUnit relation in list)
                {
                    int minusPitch = relation.Pitch;
                    int nextEnt = 0;
                    NodeItemUnit aftNode = GetAfterNode(relation);

                    nextEnt = node.EntNet + minusPitch;

                    if (aftNode != null)
                    {
                        if (aftNode.EntNet < nextEnt)
                            aftNode.EntNet = nextEnt;
                        else if (aftNode.EntNet >= nextEnt && aftNode.LntNet > 0)
                        {
                            if (node.EntNet < 0 && node.EntNet * aftNode.EntNet <= 0)
                            {
                                aftNode.EntNet = nextEnt + 1;
                            }
                            else
                            {
                                aftNode.EntNet = nextEnt;
                            }
                        }
                        UpdateAfterKLForward_TEST(aftNode);
                    }
                }
            }
        }

        private void UpdateAfterKLCP_TEST(NodeItemUnit node)
        {
            if (node != null)
            {
                List<RelationItemUnit> list = GetParentRlation_pre(node.MISCode);

                if (node.MISCode == BeforeNodeLast.MISCode)
                {
                    node.IsCP = true;
                }

                foreach (RelationItemUnit relation in list)
                {
                    NodeItemUnit aftNode = GetAfterNode(relation);

                    if (aftNode != null)
                    {
                        if (node.EntNet == node.LntNet && aftNode.EntNet == aftNode.LntNet)
                        {
                            if (aftNode.BlockList != "K/L")
                            {
                                if (aftNode.EntNet - node.EntNet == relation.Pitch && node.IsCP)
                                {
                                    node.IsCP = true;
                                    aftNode.IsCP = true;
                                    relation.IsCriticalPath = true;
                                }
                            }
                            else
                            {
                                if (aftNode.EntNet - node.EntNet == relation.Pitch + 1 && node.IsCP)
                                {
                                    node.IsCP = true;
                                    aftNode.IsCP = true;
                                    relation.IsCriticalPath = true;
                                }
                            }
                        }
                        else
                        {
                            aftNode.IsCP = false;
                            relation.IsCriticalPath = false;
                        }

                        if (node.BlockList == "K/L")
                        {
                            return;
                        }

                        UpdateAfterKLCP_TEST(aftNode);
                    }
                }
            }
        }

        private void UpdateEntLntAfterLCNodes(NodeItemUnit node, int ent, int lnt)
        {
            if (node != null)
            {
                List<RelationItemUnit> list = GetChildRlation(node.MISCode);


                if (node.BlockList != "L/C")
                {
                    node.EntNet = ent;
                    node.LntNet = lnt;
                }
                foreach (RelationItemUnit relation in list)
                {
                    NodeItemUnit afterNode = GetAfterNode(relation);

                    int nextEnt = relation.Pitch + node.EntNet;
                    int nextLnt = relation.Pitch + node.LntNet;

                    UpdateEntLntAfterLCNodes(afterNode, nextEnt, nextLnt);
                }
            }
        }

        private List<NodeItemUnit> InsertNode(ErecNetworkSchLogic schLogic)
        {
            //List<NodeItemUnit> nodes = new List<NodeItemUnit>();

            List<NodeItemUnit> unitList = new List<NodeItemUnit>();
            int count = 0;

            foreach (NodeItemUnit node in this.NodeUnitCollection)
            {
                if (node.RepresentNode == node.MISCode)
                {
                    if (node.OUT_FLG == false)
                    {
                        node.IsCP = false;
                        schLogic.AddNode(count, node.MISCode);
                        count++;
                    }
                }
                else
                {
                    unitList.Add(node);
                }
            }

            return unitList;
        }

        private void InsertRelation(ErecNetworkSchLogic schLogic)
        {
            foreach (RelationItemUnit connection in this.RelationUnitCollection)
            {
                NodeUnit preccedNode = schLogic.GetNode(connection.PreccedeNodeKey);
                NodeUnit afterNode = schLogic.GetNode(connection.AfterNodeKey);

                if (preccedNode != null && afterNode != null)
                {
                    schLogic.AddArc(preccedNode.nodeId, afterNode.nodeId, connection.Pitch);
                    connection.IsCriticalPath = false;
                }

            }
        }

        private void UpdateMergeNodeEntLnt(List<NodeItemUnit> mergeList)
        {
            foreach (NodeItemUnit node in mergeList)
            {
                NodeItemUnit nodeUnit = this.GetNodeItemUnit(node.RepresentNode);
                if (nodeUnit != null)
                {
                    node.EntNet = nodeUnit.EntNet;
                    node.LntNet = nodeUnit.LntNet;
                }
            }
        }
        private void UpdateCriticalPath(ErecNetworkSchLogic schLogic)
        {
            //연결선의 색 굵기 변경, 노드의 CP여부 변경
            List<ArcUnit> unitList = schLogic.FindCriticalPath();

            if (unitList != null && unitList.Count > 0)
            {
                this.NodeUnitCollection.InitCP();

                foreach (RelationItemUnit relation in this.RelationUnitCollection)
                {
                    ArcUnit arcUnit = unitList.Where(p => p.PrevNode.NodeId == relation.PreccedeNodeKey && p.NextNode.NodeId == relation.AfterNodeKey).FirstOrDefault();
                    if (arcUnit != null)
                    {
                        relation.IsCriticalPath = true;

                        NodeItemUnit afterNode = GetAfterNode(relation);
                        NodeItemUnit preNode = GetPreccedeNode(relation);
                        if (afterNode != null && preNode != null)
                        {
                            afterNode.IsCP = true;
                            preNode.IsCP = true;
                        }
                    }
                }
            }
        }

        private bool UpdateLnt(ErecNetworkSchLogic schLogic, NodeUnit cyclicNode = null)
        {
            this.FindLnt();

            foreach (NodeItemUnit node in this.NodeUnitCollection)
            {
                if(node.ItemCode == "E11A3")
                {

                }
                NodeUnit unit = schLogic.GetNode(node.MISCode);
                if (unit != null)
                    unit.LntDay = node.LntNet;
            }

            return true;
        }

        private bool UpdateEnt(ErecNetworkSchLogic schLogic, NodeUnit cyclicNode = null)
        {
            this.FindEnt();

            foreach (NodeItemUnit node in this.NodeUnitCollection)
            {
                if(node.ItemCode == "E11A3")
                {

                }

                NodeUnit unit = schLogic.GetNode(node.MISCode);
                if (unit != null)
                    unit.EntDay = node.EntNet;
            }

            return true;
        }

        private int InitDay = 1;

        public List<NodeItemUnit> FindEnt()
        {
            List<NodeItemUnit> rootNodes = this.GetRootNode();

            if (rootNodes != null && rootNodes.Count > 0)
            {
                foreach (NodeItemUnit nodeItem in rootNodes)
                {
                    ClearForward_TEST(nodeItem);
                }

                foreach (NodeItemUnit nodeItem in rootNodes)
                {
                    nodeItem.EntNet = InitDay;
                    ForwardRecursive(nodeItem, nodeItem.EntNet);
                }
            }

            return null;
        }

        public NodeItemUnit FindLnt()
        {
            NodeItemUnit leafNodes = GetLCNode();

            if (leafNodes != null)
            {

                ClearBackward_TEST(leafNodes);

                leafNodes.LntNet = GetNetDayFromCalDay(shipinfo.LC) - GetNetDayFromCalDay(shipinfo.KL) + 1;
                BackwardRecursive(leafNodes, leafNodes.LntNet);

                //return this._Nodes.Values.ToList();
            }

            return null;
        }

        public List<NodeItemUnit> GetRootNode()
        {
            if (this.NodeUnitCollection != null)
            {
                List<NodeItemUnit> rootNodes = new List<NodeItemUnit>();
                foreach (var node in this.NodeUnitCollection)
                {
                    // 포워드 시작 기준 노드
                    string s = "";

                    if (node.IsKL)
                        rootNodes.Add(node);

                    if (node.BlockList == "K/L")
                        rootNodes.Add(node);

                }

                return rootNodes;
            }

            return null;
        }

        public void ForwardRecursive(NodeItemUnit preNodeUnit, int arcLen)
        {
            if (preNodeUnit != null)
            {
                List<RelationItemUnit> list = GetParentRlation_pre(preNodeUnit.MISCode);

                preNodeUnit.EntNet = Math.Max(arcLen, preNodeUnit.EntNet);

                if (list.Count != null)
                {
                    foreach (var rel in list)
                    {
                        ForwardRecursive(GetAfterNode(rel), preNodeUnit.EntNet + rel.Pitch);
                    }
                }
            }

            return;
        }

        public void BackwardRecursive(NodeItemUnit aftNodeUnit, int arcLen)
        {
            try
            {
                if (aftNodeUnit != null)
                {
                    List<RelationItemUnit> list = GetParentRlation(aftNodeUnit.MISCode);
                    aftNodeUnit.LntNet = Math.Min(arcLen, aftNodeUnit.LntNet);

                    if (list.Count != null)
                    {
                        foreach (var rel in list)
                        {
                            BackwardRecursive(GetPreccedeNode(rel), aftNodeUnit.LntNet - rel.Pitch);
                        }
                    }
                }

                return;
            }
            catch (Exception e)
            {
                throw;
            }
        }

        internal Dictionary<string, string> GetUpdateLeftText(bool IsNetDay)
        {
            Dictionary<string, string> textdic = new Dictionary<string, string>();

            try
            {
                foreach (NodeItemUnit node in NodeUnitCollection)
                {
                    if (IsNetDay)
                    {
                        textdic.Add(node.MISCode, node.EntNet.ToString() + " ");

                    }
                    else
                    {
                        textdic.Add(node.MISCode, CalculateStrEnt(node));
                    }
                }
            }
            catch
            {

            }

            return textdic;
        }

        internal Dictionary<string, string> GetUpdateRightText(bool IsNetDay)
        {
            Dictionary<string, string> textdic = new Dictionary<string, string>();

            foreach (NodeItemUnit node in NodeUnitCollection)
            {
                if(node.ItemCode == "E11A3")
                {

                }

                if (IsNetDay)
                    textdic.Add(node.MISCode, " " + node.LntNet.ToString());
                else
                {
                    textdic.Add(node.MISCode, CalculateStrLnt(node));
                }
            }
            return textdic;
        }

        internal Dictionary<string, string> GetUpdateBottomText(int IsBlockList, List<ErectNodeControl> NodeList)
        {
            Dictionary<string, string> NodeDisplayDictionary = new Dictionary<string, string>();

            foreach (ErectNodeControl node in NodeList)
            {
                NodeItemUnit nodeUnit = this.GetNodeItemUnit(node.NodeKey);
                if (nodeUnit != null)
                {
                    UpdateNodeBottomText(node, IsBlockList);
                    NodeDisplayDictionary.Add(nodeUnit.MISCode, nodeUnit.DisplayBlockList);
                }
            }

            return NodeDisplayDictionary;
        }

        internal Dictionary<string, string> GetUpdateBottomTextDock(List<ErectNodeControl> NodeList)
        {
            Dictionary<string, string> NodeDisplayDictionary = new Dictionary<string, string>();

            foreach (ErectNodeControl node in NodeList)
            {
                NodeItemUnit nodeUnit = this.GetNodeItemUnit(node.NodeKey);
                if (nodeUnit != null)
                {
                    UpdateDockBottomText(node);
                    NodeDisplayDictionary.Add(nodeUnit.MISCode, nodeUnit.DisplayBlockList);
                }

            }

            return NodeDisplayDictionary;
        }

        private void UpdateDockBottomText(ErectNodeControl item)
        {
            if (item != null)
            {
                NodeItemUnit nodeItemUnit = this.GetNodeItemUnit(item.NodeKey);

                nodeItemUnit.DisplayBlockList = nodeItemUnit.Dock;
            }
        }

        private void UpdateNodeBottomText(ErectNodeControl item, int IsListMode)
        {
            NodeItemUnit nodeItemUnit = this.GetNodeItemUnit(item.NodeKey);

            if (nodeItemUnit == null)
                return;

            nodeItemUnit.DisplayBlockList = string.Empty;

            if (IsListMode == 1)
            {
                if (item.CheckMergeNode())
                {
                    List<string> keyList = new List<string>(); ;
                    foreach (ErectNodeControl mergeItem in item.MergeNodes)
                    {
                        keyList.Add(mergeItem.NodeKey);
                    }
                    //합쳐진 노드의 블록리스트를 생성.
                    keyList.Add(item.NodeKey);
                    nodeItemUnit.DisplayBlockList = MakeBlockList(keyList);
                }
                else
                {
                    nodeItemUnit.DisplayBlockList = nodeItemUnit.BlockList;
                }
            }
            else if (IsListMode == 2)
            {
                if (nodeItemUnit.HoGubun == "2")
                    nodeItemUnit.DisplayBlockList = nodeItemUnit.BlockList;
                else
                    nodeItemUnit.DisplayBlockList = nodeItemUnit.ItemCode;
            }
            else
            {
                nodeItemUnit.DisplayBlockList = nodeItemUnit.Dock;
            }
        }

        private string MakeBlockList(List<string> keyList)
        {
            if (keyList.Count == 0)
                return null;

            List<string> blockList = new List<string>();
            List<NodeItemUnit> nodeList = GetNodeItemUnits(keyList);
            foreach (NodeItemUnit node in nodeList)
            {
                blockList.Add(node.BlockList);
            }

            string str = this.GetCombineBlockList(blockList);

            return str;
        }

        public string CalculateStrLnt(NodeItemUnit node)
        {
            if(node.ItemCode == "E11A3")
            {

            }
            string strLnt;
            int nLntDay = KLNetDay + node.LntNet - 1;

            if (node.LntNet < 0)
                nLntDay++;

            if (nLntDay < 1)
                nLntDay = 1;
            nLntDay = int.Parse(BasicCalendar.GetCalDayFromNetDay(nLntDay));
            strLnt = (nLntDay / 100) % 100 + "-" + (nLntDay % 100);

            return strLnt;
        }

        public string CalculateStrEnt(NodeItemUnit node)
        {
            if(node.ItemCode == "E11A3")
            {

            }

            int nEntDay = KLNetDay + node.EntNet - 1;

            if (node.EntNet < 0)
                nEntDay++;

            if (nEntDay < 1)
                nEntDay = 1;
            nEntDay = int.Parse(BasicCalendar.GetCalDayFromNetDay(nEntDay));
            string strEnt;
            strEnt = (nEntDay / 100) % 100 + "-" + (nEntDay % 100);

            return strEnt;
        }

        public string CalculateStrEntDate(NodeItemUnit node)
        {
            int nEntDay = KLNetDay + node.EntNet - 1;

            if (node.EntNet < 0)
                nEntDay++;

            if (nEntDay < 1)
                nEntDay = 1;
            nEntDay = int.Parse(BasicCalendar.GetCalDayFromNetDay(nEntDay));

            string strEnt;
            strEnt = nEntDay.ToString();

            return strEnt;
        }
        #endregion

        #region Load

        internal void SetPreModeViewData(DataTable dtResultData, string startCol, string endCol)
        {
            foreach (DataRow row in dtResultData.Rows)
            {
                string startDate = row[startCol].ExString();
                string endDate = row[endCol].ExString();
                string MIScode = row["MIS_COD"].ExString();

                if (startDate != null && endDate != null && startDate != "" && endDate != ""
                    && MIScode != null && MIScode != "")
                {
                    NodeItemUnit node = GetNodeItemUnit(MIScode);

                    if (node != null)
                    {
                        SetNodeMode(startDate, endDate, node);
                        if (node.PreDataTable == null)
                        {
                            node.InitPreDataTable(dtResultData.Clone());
                        }
                        node.SetPreData(row);
                    }
                }
            }
        }

        private bool SetNodeMode(string startDate, string endDate, NodeItemUnit node)
        {
            DateTime start;
            DateTime end;
            DateTime nodeDay;
            start = DateTime.ParseExact(startDate, "yyyyMMdd", null);
            end = DateTime.ParseExact(startDate, "yyyyMMdd", null);
            nodeDay = DateTime.ParseExact(shipinfo.KL, "yyyyMMdd", null);

            nodeDay = nodeDay.AddDays(node.EntNet);

            if (start > nodeDay)
            {
                node.Mode = NodeMode.None;
            }
            else if (start <= nodeDay && nodeDay <= end)
            {
                node.Mode = NodeMode.Ing;
            }
            else if (end < nodeDay)
            {
                node.Mode = NodeMode.Complate;
            }

            return true;
        }
        internal void InitPreModeViewData(bool IsShow = true)
        {
            if (IsShow)
            {
                foreach (NodeItemUnit node in NodeUnitCollection)
                {
                    node.Mode = NodeMode.None;
                    node.InitPreDataTable();
                }

            }
            else
            {
                foreach (NodeItemUnit node in NodeUnitCollection)
                {
                    node.Mode = NodeMode.NA;
                    node.InitPreDataTable();
                }
            }
        }

        internal ErectionNetworkLoadInfoList MakeLoadinfoData(HMDErecNetCalendar LoadCalendar, ErectionNetworkManager currectmanager)
        {
            loadinfoList = new ErectionNetworkLoadInfoList(LoadCalendar.Count, currectmanager.shipinfo.KL, shipinfo.FIG_SHP);

            for (int i = 1; i < loadinfoList.Count; i++)
            {
                int nEnt = i;
                List<NodeItemUnit> list = NodeUnitCollection.Where(p => p.EntNet == nEnt && p.HoGubun == "1").ToList();
                List<NodeItem> nodes = new List<NodeItem>();
                foreach (NodeItemUnit node in list)
                {
                    if (node.HoGubun == "1")
                    {
                        NodeItem newNode = new NodeItem(node.MISCode, node.ShipCode, shipinfo.FIG_SHP, node.BlockList, node.TotalWeght);

                        nodes.Add(newNode);
                    }
                }

                loadinfoList[i - 1].AddNodes(nodes);
                string stringDate = LoadCalendar[i.ToString()].CalDay;
                DateTime date = DateTime.ParseExact(stringDate, "yyyyMMdd", CultureInfo.InvariantCulture);
                loadinfoList[i - 1].Day = date;
            }
            return loadinfoList;
        }

        #endregion

        #region Save
        internal void DeleteCurrentRevision(UserControlBase i_Form, string I_SHP_COD, int i_SHP_REV)
        {
            var agent = new QueryServiceAgent(QAgent.Default.AddressName, QAgent.Default.ServiceUrl, QAgent.Default.MapperName);
            List<QueryRequest> requestList = new List<QueryRequest>();

            QueryParameterCollection deleteParam = new QueryParameterCollection();
            deleteParam.Add("O_APP_CODE", null);
            deleteParam.Add("O_APP_MSG", null);
            deleteParam.Add("SHP_COD", I_SHP_COD);
            deleteParam.Add("SHP_REV", i_SHP_REV);
            requestList.Add(agent.CreateQueryRequest(PKG_PP008_ENT002.ENT002_DELETE_TSEB016_NODE_D1, deleteParam, QueryServiceTransactions.TxNone));
            requestList.Add(agent.CreateQueryRequest(PKG_PP008_ENT002.ENT002_DELETE_TSEB018_LINK_D1, deleteParam, QueryServiceTransactions.TxNone));

            i_Form.ExecuteMultipleNonQuery(requestList, QueryServiceTransactions.TxLocal, QAgent.DefaultAgentName);
        }

        internal List<QueryRequest> DeleteStandardRevision(string i_PACKAGE, string i_SHP_COD, object i_FIG_NO)
        {
            var agent = new QueryServiceAgent(QAgent.Default.AddressName, QAgent.Default.ServiceUrl, QAgent.Default.MapperName);
            List<QueryRequest> requestList = new List<QueryRequest>();

            QueryParameterCollection deleteParam = new QueryParameterCollection();
            deleteParam.Add("SHP_COD", i_SHP_COD);
            deleteParam.Add("FIG_NO", i_FIG_NO);

            requestList.Add(agent.CreateQueryRequest(i_PACKAGE + ".ENT002_DELETE_INIT_TSEB001_D1", deleteParam, QueryServiceTransactions.TxNone));
            requestList.Add(agent.CreateQueryRequest(i_PACKAGE + ".ENT002_DELETE_INIT_TSEB003_D1", deleteParam, QueryServiceTransactions.TxNone));

            return requestList;
        }

        internal void SetFirm()
        {
            foreach (NodeItemUnit node in this.NodeUnitCollection)
            {
                node.IsFirm = true;
            }
        }

        internal void ClearFirm()
        {
            foreach (NodeItemUnit node in this.NodeUnitCollection)
            {
                node.IsFirm = false;
            }
        }

        internal void SetNodeACT_NW_DES(string p)
        {
            foreach (NodeItemUnit node in this.NodeUnitCollection)
            {
                node.Description = p;
            }
        }

        internal void SetRevision(int rev)
        {
            foreach (NodeItemUnit node in this.NodeUnitCollection)
            {
                node.Revision = rev;
            }

            foreach (RelationItemUnit relation in this.RelationUnitCollection)
            {
                relation.Revision = rev;
            }
        }

        internal void ReadySave(string ACT_NW_DES, int rev, string UserId)
        {
            SetNodeACT_NW_DES(ACT_NW_DES);
            SetRevision(rev);
            this.NodeUnitCollection.ReadySave(UserId, IP, pid);
            this.RelationUnitCollection.ReadySave(UserId);
            if (HistoryInfo != null)
                this.HistoryInfo.ReadySave(UserId);
        }

        internal void ReadySave(string UserId)
        {
            this.NodeUnitCollection.ReadySave(UserId, IP, pid);
            this.RelationUnitCollection.ReadySave(UserId);
            if (HistoryInfo != null)
                this.HistoryInfo.ReadySave(UserId);

            // 탑재노드 이전 Act. 일정 변경(Shift)
            UpdateActData();
            UpdateDock();
        }

        private void UpdateDock()
        {
            DataSet ds = NodeUnitCollection.dsAct;
            if (ds != null)
            {
                DataTable dt = ds.Tables[0];
                if (dt != null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        string misCod = row["MIS_COD"].ToString();

                        NodeItemUnit node = NodeUnitCollection.GetNode(misCod);

                        if (node != null && row["DCK_COD"].ToString() != node.Dock)
                            row["DCK_COD"] = node.Dock;
                    }
                }
            }
        }

        private void UpdateActData()
        {
            foreach (NodeItemUnit node in NodeUnitCollection)
            {
                int nEntDay = (KLNetDay - 1) + node.EntNet;

                if (node.EntNet < 0)
                    nEntDay = nEntDay + 1;

                if (string.IsNullOrEmpty(node.PLN_ST) == false)
                {
                    if (node.HoGubun != "2")
                    {
                        string mis = node.MISCode;
                        int oriNetDay = GetNetDayFromCalDay(node.PLN_ST);
                        int gap = nEntDay - oriNetDay;
                        
                        // 한수현 대리 2021.05.12 계획일이 없을 경우 파라메타 계획일로 적용
                        UpdateActData(mis, gap, node.PLN_ST, node.PLN_FI);
                    }
                }
                else
                {
                    int startNet = nEntDay;
                    int finishNet = nEntDay + (node.STD_TRM == 0 ? node.MOD_STD_TRM : node.STD_TRM) - 1;
                    string planStartDate = GetCalDayFromNetDay(startNet);
                    string planFinishDate = GetCalDayFromNetDay(finishNet);

                    node.PLN_ST = planStartDate;
                    node.PLN_FI = planFinishDate;

                    List<NodeItemUnit> nodeList = NodeUnitCollection.GetMergeNode(node.MISCode);
                    foreach (NodeItemUnit findNode in nodeList)
                    {
                        findNode.PLN_ST = planStartDate;
                        findNode.PLN_FI = planFinishDate;
                    }
                }
            }
        }
        private void UpdateActData(string mis, int gap, string PLN_ST, string PLN_FI)
        {
            DataSet ds = NodeUnitCollection.dsAct;
            if (ds != null)
            {
                DataTable dt = ds.Tables[0];
                if (dt != null)
                {
                    var rows = dt.AsEnumerable().Where(t => t["MIS_COD"].ToString() == mis);
                    foreach (DataRow row in rows) // dt.Rows)
                    {
                        int startNet = GetNetDayFromCalDay(row["PLN_ST"].ToString());
                        int finishNet = GetNetDayFromCalDay(row["PLN_FI"].ToString());

                        // 한수현 대리 2021.05.12 계획일이 없을 경우 파라메타 계획일로 적용
                        {
                            if (row["PLN_ST"].ExString().IsEmpty())
                            {
                                startNet = GetNetDayFromCalDay(PLN_ST);
                            }

                            if (row["PLN_FI"].ExString().IsEmpty())
                            {
                                finishNet = GetNetDayFromCalDay(PLN_FI);
                            }
                        }

                        startNet = startNet + gap;
                        finishNet = finishNet + gap;

                        string planStartDate = GetCalDayFromNetDay(startNet);
                        string planEndDate = GetCalDayFromNetDay(finishNet);

                        row["PLN_ST"] = planStartDate;
                        row["PLN_FI"] = planEndDate;
                    }
                }
            }
        }

        internal void Save(UserControlBase i_Form, string i_User_ID)
        {
            var agent = new QueryServiceAgent(QAgent.Default.AddressName, QAgent.Default.ServiceUrl, QAgent.Default.MapperName);
            List<QueryRequest> requestList = new List<QueryRequest>();

            using (var dtTSEB016 = ErectUtil.GetDataTable(this.NodeUnitCollection.dsNodeList))
            {
                if (dtTSEB016 != null)
                {
                    // 삭제가 아닌 자료
                    var drs = dtTSEB016.AsEnumerable().Where(x => x.RowState != DataRowState.Deleted);
                    // 한수현 대리 2021.05.12 drs.Count() > 0 추가
                    if (drs != null && drs.Count() > 0)
                    {
                        var insTSEB016ParamsList = ConvertHelper.DataTableToQueryParameterCollection(drs.CopyToDataTable());
                        insTSEB016ParamsList.AddArrayValues("USER_ID", i_User_ID);
                        requestList.Add(agent.CreateQueryRequest(PKG_PP008_ENT002.ENT002_INSERT_TSEB016_NODE_I1, insTSEB016ParamsList, QueryServiceTransactions.TxNone));
                    }
                }
            }

            using (var dtTSEB018 = ErectUtil.GetDataTable(this.RelationUnitCollection.dslinkList))
            {
                if (dtTSEB018 != null)
                {
                    // 삭제가 아닌 자료
                    var drs = dtTSEB018.AsEnumerable().Where(x => x.RowState != DataRowState.Deleted);
                    // 한수현 대리 2021.05.12 drs.Count() > 0 추가
                    if (drs != null && drs.Count() > 0)
                    {
var insTSEB018ParamsList = ConvertHelper.DataTableToQueryParameterCollection(drs.CopyToDataTable());
                        insTSEB018ParamsList.AddArrayValues("USER_ID", i_User_ID);
                        requestList.Add(agent.CreateQueryRequest(PKG_PP008_ENT002.ENT002_INSERT_TSEB018_LINK_I1, insTSEB018ParamsList, QueryServiceTransactions.TxNone));
                    }
                }
            }

            // History 추가
            requestList.AddRange(this.SaveHistory(i_User_ID));

            var reponse = i_Form.ExecuteMultipleNonQuery(requestList, QueryServiceTransactions.TxLocal, QAgent.DefaultAgentName);
        }

              internal List<QueryRequest> StandardSave(string i_PACKAGE, string i_SHP_COD, string i_User_ID, object i_FIG_NO)
        {
            var agent = new QueryServiceAgent(QAgent.Default.AddressName, QAgent.Default.ServiceUrl, QAgent.Default.MapperName);
            List<QueryRequest> requestList = new List<QueryRequest>();

            using (var dtTSEB001 = ErectUtil.GetDataTable(this.NodeUnitCollection.dsNodeList))
            {
                if (dtTSEB001 != null)
                {
                    // 한수현 대리 2021.05.12 result 로 count > 0 예외처리
                    //var insTSEB001ParamsList = ConvertHelper.DataTableToQueryParameterCollection(dtTSEB001.AsEnumerable().Where(x => x.RowState != DataRowState.Deleted).CopyToDataTable());
                    var result = dtTSEB001.AsEnumerable().Where(x => x.RowState != DataRowState.Deleted);
                    if (result != null && result.Count() > 0)
                    {
                        // 삭제가 아닌 자료
                        var insTSEB001ParamsList = ConvertHelper.DataTableToQueryParameterCollection(result.CopyToDataTable());
                        insTSEB001ParamsList.AddArrayValues("USER_ID", i_User_ID);

                        if (i_FIG_NO != null)
                        {
                            string[] fig_no = new string[insTSEB001ParamsList.ArrayBindCount];

                            for (int i = 0; i < insTSEB001ParamsList.ArrayBindCount; i++)
                                fig_no[i] = i_FIG_NO.ToString();

                            if (insTSEB001ParamsList.ContainsKey("FIG_NO"))
                                insTSEB001ParamsList["FIG_NO"] = fig_no;
                            else
                                insTSEB001ParamsList.AddArrayValues("FIG_NO", fig_no);
                        }
                        else insTSEB001ParamsList.Add("FIG_NO", DBNull.Value);
                        requestList.Add(agent.CreateQueryRequest(i_PACKAGE + ".ENT002_INSERT_INIT_TSEB001_I1", insTSEB001ParamsList, QueryServiceTransactions.TxNone));
                    }
                }
            }

            using (var dtTSEB003 = ErectUtil.GetDataTable(this.RelationUnitCollection.dslinkList))
            {
                if (dtTSEB003 != null)
                {
                    // 한수현 대리 2021.05.12 result 로 count > 0 예외처리
                    //var insTSEB003ParamsList = ConvertHelper.DataTableToQueryParameterCollection(dtTSEB003.AsEnumerable().Where(x => x.RowState != DataRowState.Deleted).CopyToDataTable());
                    var result = dtTSEB003.AsEnumerable().Where(x => x.RowState != DataRowState.Deleted);
                    if (result != null && result.Count() > 0)
                    {
                        // 삭제가 아닌 자료
                        var insTSEB003ParamsList = ConvertHelper.DataTableToQueryParameterCollection(result.CopyToDataTable());
                        insTSEB003ParamsList.AddArrayValues("USER_ID", i_User_ID);

                        if (i_FIG_NO != null)
                        {
                            string[] fig_no = new string[insTSEB003ParamsList.ArrayBindCount];

                            for (int i = 0; i < insTSEB003ParamsList.ArrayBindCount; i++)
                                fig_no[i] = i_FIG_NO.ToString();

                            if (insTSEB003ParamsList.ContainsKey("FIG_NO"))
                                insTSEB003ParamsList["FIG_NO"] = fig_no;
                            else
                                insTSEB003ParamsList.AddArrayValues("FIG_NO", fig_no);
                        }
                        else insTSEB003ParamsList.Add("FIG_NO", DBNull.Value);
                        requestList.Add(agent.CreateQueryRequest(i_PACKAGE + ".ENT002_INSERT_INIT_TSEB003_I1", insTSEB003ParamsList, QueryServiceTransactions.TxNone));
                    }
                }
            }

            // History 추가
            requestList.AddRange(this.SaveHistory(i_User_ID));

            return requestList;
        }

        private List<QueryRequest> SaveHistory(string i_User_ID)
        {
            var agent = new QueryServiceAgent(QAgent.Default.AddressName, QAgent.Default.ServiceUrl, QAgent.Default.MapperName);
            List<QueryRequest> requestList = new List<QueryRequest>();

            if (this.HistoryInfo != null)
            {
                QueryParameterCollection delTSEB019Params = new QueryParameterCollection();
                delTSEB019Params.Add("SHP_COD", this.HistoryInfo.ShipCode);
                delTSEB019Params.Add("SHP_REV", this.HistoryInfo.ShipRevision);
                requestList.Add(agent.CreateQueryRequest(PKG_PP008_ENT002.ENT002_DELETE_HISTORY_D1, delTSEB019Params, QueryServiceTransactions.TxNone));

                var dtTSEB019 = ErectUtil.GetDataTable(this.HistoryInfo.dsHistory);
                if (dtTSEB019 != null)
                {
                    var insTSEB019ParamsList = ConvertHelper.DataTableToQueryParameterCollection(dtTSEB019.AsEnumerable().Where(x => x.RowState != DataRowState.Deleted).CopyToDataTable());
                    insTSEB019ParamsList.AddArrayValues("USER_ID", i_User_ID);

                    requestList.Add(agent.CreateQueryRequest(PKG_PP008_ENT002.ENT002_INSERT_HISTORY_I1, insTSEB019ParamsList, QueryServiceTransactions.TxNone));
                }
            }

            return requestList;
        }

              public void ClearFrimNodes(string i_SHP_COD)
        {
            var agent = new QueryServiceAgent(QAgent.Default.AddressName, QAgent.Default.ServiceUrl, QAgent.Default.MapperName);
            List<QueryRequest> requestList = new List<QueryRequest>();

            QueryParameterCollection updParam = new QueryParameterCollection();
            updParam.Add("SHP_COD", i_SHP_COD);

            agent.ExecuteNonQuery(PKG_PP008_ENT002.ENT002_UPDATE_TSEB016_CNF_N_U1, updParam);
        }
        internal List<QueryRequest> Sync_4A_4B(string PACKAGE, string FIG_NO)
        {
            var agent = new QueryServiceAgent(QAgent.Default.AddressName, QAgent.Default.ServiceUrl, QAgent.Default.MapperName);
            List<QueryRequest> requestList = new List<QueryRequest>();


            var dtTSEA002_TSEG005 = ErectUtil.GetDataTable(this.NodeUnitCollection.dsAct);
            if (dtTSEA002_TSEG005 != null && dtTSEA002_TSEG005.Rows.Count > 0)
            {
                var allParamsList = ConvertHelper.DataTableToQueryParameterCollection(dtTSEA002_TSEG005);
                allParamsList.AddArrayValues("FIG_NO", FIG_NO);
                allParamsList.AddArrayValues("USER_ID", this.UpdateUser);

                requestList.Add(agent.CreateQueryRequest(PACKAGE + ".ENT002_UPDATE_SYNC_4A_4B_U1", allParamsList, QueryServiceTransactions.TxNone));
            }

            return requestList;
        }

        internal List<QueryRequest> UpdateTSEA002(string PACKAGE, string FIG_NO, bool i_isErpSend_ActConfirm)
        {
            var agent = new QueryServiceAgent(QAgent.Default.AddressName, QAgent.Default.ServiceUrl, QAgent.Default.MapperName);
            List<QueryRequest> requestList = new List<QueryRequest>();

            var dtTSEA002 = ErectUtil.GetDataTable(this.NodeUnitCollection.dsAct);
            if (dtTSEA002 != null && dtTSEA002.Rows.Count > 0)
            {
                var allParamsList = ConvertHelper.DataTableToQueryParameterCollection(dtTSEA002);
                allParamsList.AddArrayValues("ERP_SEND_YN", i_isErpSend_ActConfirm == true ? "Y" : "N");
                allParamsList.AddArrayValues("USER_ID", this.UpdateUser);
                allParamsList.AddArrayValues("FIG_NO", FIG_NO);
                requestList.Add(agent.CreateQueryRequest(PACKAGE + ".ENT002_UPDATE_TSEA002_U1", allParamsList, QueryServiceTransactions.TxNone));
                requestList.Add(agent.CreateQueryRequest(PACKAGE + ".ENT002_UPDATE_TSEA002_DOCK_U1", allParamsList, QueryServiceTransactions.TxNone));
            }

            return requestList;
        }

        internal List<QueryRequest> UpdateTSEG005(string PACKAGE, string FIG_NO, string SHP_COD)
        {
            var agent = new QueryServiceAgent(QAgent.Default.AddressName, QAgent.Default.ServiceUrl, QAgent.Default.MapperName);
            List<QueryRequest> requestList = new List<QueryRequest>();
            var dtTSEG005 = ErectUtil.GetDataTable(this.NodeUnitCollection.dsAct);
            if (dtTSEG005 != null)
            {
                var updParamsList = ConvertHelper.DataTableToQueryParameterCollection(dtTSEG005.AsEnumerable().Where(x => x.RowState == DataRowState.Modified).CopyToDataTable());
                updParamsList.AddArrayValues("USER_ID", this.UpdateUser);

                if (updParamsList.Keys.Contains("FIG_NO") == false)
                    updParamsList.AddArrayValues("FIG_NO", FIG_NO);
                if (updParamsList.Keys.Contains("SHP_COD") == false)
                    updParamsList.AddArrayValues("SHP_COD", SHP_COD);

                requestList.Add(agent.CreateQueryRequest(PACKAGE + ".ENT002_UPDATE_BIZ_TSEG005_U1", updParamsList, QueryServiceTransactions.TxLocal));
                requestList.Add(agent.CreateQueryRequest(PACKAGE + ".ENT002_UPDATE_BIZ_TSEG005_DOCK", updParamsList, QueryServiceTransactions.TxLocal));
            }
            return requestList;
        }

        internal void BusinessSave(string PACKAGE, string figNo, string userid)
        {
            var agent = new QueryServiceAgent(QAgent.Default.AddressName, QAgent.Default.ServiceUrl, QAgent.Default.MapperName);
            List<QueryRequest> requestList = new List<QueryRequest>();

            DataTable dtTSEG012 = ErectUtil.GetDataTable(this.NodeUnitCollection.dsNodeList);
            if (dtTSEG012 != null)
            {
                QueryParameterCollection delParamsList = null;
                QueryParameterCollection updParamsList = null;

                //var delData = dtTSEG012.AsEnumerable().Where(x => x.RowState == DataRowState.Deleted);
                DataTable dt_del_row = dtTSEG012.GetChanges(DataRowState.Deleted);
                if (dt_del_row != null && dt_del_row.Rows != null && dt_del_row.Rows.Count > 0)
                {
                    // 삭제된 row 정보 복원
                    DataTable param_dt = dt_del_row.Clone();
                    foreach (DataRow row in dt_del_row.Rows)
                    {
                        DataRow nRow = dt_del_row.NewRow();
                        for (int i = 0; i < dt_del_row.Columns.Count; i++)
                        {
                            nRow[i] = row[i, DataRowVersion.Original];
                        }

                        param_dt.Rows.Add(nRow.ItemArray);
                    }

                    delParamsList = ConvertHelper.DataTableToQueryParameterCollection(param_dt);
                    delParamsList.AddArrayValues("USER_ID", userid);

                    requestList.Add(agent.CreateQueryRequest(PACKAGE + ".ENT002_DELETE_BIZ_TSEG012_D1", delParamsList, QueryServiceTransactions.TxLocal));
                }

                var upData = dtTSEG012.AsEnumerable().Where(x => x.RowState == DataRowState.Added || x.RowState == DataRowState.Modified);

                if (upData != null && upData.Count() > 0)
                {
                    updParamsList = ConvertHelper.DataTableToQueryParameterCollection(upData.CopyToDataTable());
                    updParamsList.AddArrayValues("USER_ID", userid);

                    // 신규로 추가된 노드는 fig_no가 없어서 새로 설정한다.
                    var fig_no = dtTSEG012.AsEnumerable().Where(t => t.RowState != DataRowState.Deleted).Max(m => m["FIG_NO"].ToString());
                    updParamsList.Remove("FIG_NO");
                    updParamsList.AddArrayValues("FIG_NO", fig_no);
                    
                    requestList.Add(agent.CreateQueryRequest(PACKAGE + ".ENT002_UPDATE_BIZ_TSEG012_U1", updParamsList, QueryServiceTransactions.TxLocal));
                }
            }
            DataTable dtTSEG013 = ErectUtil.GetDataTable(this.RelationUnitCollection.dslinkList);
            if (dtTSEG013 != null)
            {
                QueryParameterCollection delParamsList = null;
                QueryParameterCollection updParamsList = null;

                //var delData = dtTSEG013.AsEnumerable().Where(x => x.RowState == DataRowState.Deleted);
                DataTable dt_del_row = dtTSEG013.GetChanges(DataRowState.Deleted);

                if (dt_del_row != null && dt_del_row.Rows != null && dt_del_row.Rows.Count > 0)
                {
                    // 삭제된 row 정보 복원하여 parameter로 생성
                    DataTable param_dt = dt_del_row.Clone();
                    foreach (DataRow row in dt_del_row.Rows)
                    {
                        DataRow nRow = dt_del_row.NewRow();
                        for (int i = 0; i < dt_del_row.Columns.Count; i++)
                        {
                            nRow[i] = row[i, DataRowVersion.Original];
                        }

                        param_dt.Rows.Add(nRow.ItemArray);
                    }

                    delParamsList = ConvertHelper.DataTableToQueryParameterCollection(param_dt);
                    delParamsList.AddArrayValues("USER_ID", userid);

                    requestList.Add(agent.CreateQueryRequest(PACKAGE + ".ENT002_DELETE_BIZ_TSEG013_D1", delParamsList, QueryServiceTransactions.TxLocal));
                }

                var upData = dtTSEG013.AsEnumerable().Where(x => x.RowState == DataRowState.Added || x.RowState == DataRowState.Modified);

                if (upData != null && upData.Count() > 0)
                {
                    #region 새로 추가된 노드에 FIG_NO 가 입력되지 않아 강제로 입력한다. UI에서 처리 필요
                    var fig_no = dtTSEG013.AsEnumerable().Where(t => t.RowState != DataRowState.Deleted).Max(m => m["FIG_NO"].ToString());

                    foreach (var row in upData.Where(t => t["FIG_NO"] == null || t["FIG_NO"].ToString() == ""))
                    {
                        row["FIG_NO"] = fig_no;
                    }
                    #endregion

                    updParamsList = ConvertHelper.DataTableToQueryParameterCollection(upData.CopyToDataTable());
                    updParamsList.AddArrayValues("USER_ID", userid);
                    requestList.Add(agent.CreateQueryRequest(PACKAGE + ".ENT002_UPDATE_BIZ_TSEG013_U1", updParamsList, QueryServiceTransactions.TxLocal));
                }
            }

            var reponse = QAgent.Default.ExecuteMultipleNonQuery(requestList, QueryServiceTransactions.TxLocal);

        }
        #endregion

        #region UpdateItem

        internal void CheckOut()
        {

            NodeItemUnit LCNode = GetLCNode();
            NodeItemUnit KLNode = GetKLNode();

            this.NodeUnitCollection.InitOUT_FLG();

            if (LCNode != null)
            {
                List<RelationItemUnit> list = GetChildRlation(LCNode.MISCode);

                foreach (RelationItemUnit relation in list)
                {
                    NodeItemUnit afterNode = GetNodeItemUnit(relation.AfterNodeKey);
                    if (afterNode.MISCode != null)
                    {
                        SetAfterNodeOutFlag(afterNode, true);
                    }
                }
            }

            if (KLNode != null)
            {
                List<RelationItemUnit> list = GetParentRlation(KLNode.MISCode);

                foreach (RelationItemUnit relation in list)
                {
                    NodeItemUnit preccedeNode = GetNodeItemUnit(relation.PreccedeNodeKey);
                    if (preccedeNode.MISCode != null)
                    {
                        SetPreccedeNodeOutFlag(preccedeNode, true);
                    }
                }
            }

        }

        internal void SetAfterNodeOutFlag(NodeItemUnit node, bool IsOut)
        {
            node.OUT_FLG = IsOut;
            List<RelationItemUnit> list = GetChildRlation(node.MISCode);
            foreach (RelationItemUnit relation in list)
            {
                NodeItemUnit afterNode = GetNodeItemUnit(relation.AfterNodeKey);
                if (afterNode != null)
                {
                    if (afterNode.MISCode != null)
                    {
                        SetAfterNodeOutFlag(afterNode, IsOut);
                    }
                }
            }
        }

        internal void SetPreccedeNodeOutFlag(NodeItemUnit node, bool IsOut)
        {
            node.OUT_FLG = IsOut;
            List<RelationItemUnit> list = GetParentRlation(node.MISCode);
            foreach (RelationItemUnit relation in list)
            {
                NodeItemUnit preccedeNode = GetNodeItemUnit(relation.PreccedeNodeKey);
                if (preccedeNode != null)
                {
                    if (preccedeNode.MISCode != null)
                    {
                        SetPreccedeNodeOutFlag(preccedeNode, IsOut);
                    }
                }
            }
        }

        internal void UpdateNodeColor(Color i_Color, string p)
        {
            NodeItemUnit node = GetNodeItemUnit(p);

            Brush brush = new SolidBrush(i_Color);

            if (node != null)
            {
                node.SetColor(brush);
            }

        }

        internal void UpdateNodeColor(string p)
        {
            NodeItemUnit node = GetNodeItemUnit(p);

            if (node != null)
            {
                node.SetColor();
            }
        }

        internal void SetConnectionStyle(string p, int Thinckness, Brush brush)
        {
            RelationItemUnit relation = GetRelationItemUnit(p);

            if (relation != null)
            {
                relation.SetConnectionStyle(Thinckness, brush);
            }

        }

        internal void UpdateNodePosition(string nodeKey, Point currentPosition)
        {
            NodeItemUnit node = NodeUnitCollection.FirstOrDefault(x => x.MISCode == nodeKey);

            if (node != null)
            {
                node.UpdatePosition(currentPosition);
            }
        }

        internal void UpdateRelationMiddlePostion(string relationKey, string ddr)
        {
            RelationItemUnit relation = RelationUnitCollection.FirstOrDefault(x => x.RelationKey == relationKey);

            if (relation != null)
            {
                relation.UpdateMiddlePosition(ddr);
            }
        }

        internal void UpdateEndLine(string relationKey, bool IsEndLine)
        {
            //RelationItemUnit relation = GetRelationItemUnit(relationKey);
            RelationItemUnit relation = RelationUnitCollection.FirstOrDefault(x => x.RelationKey == relationKey);
            if (relation != null)
            {
                relation.IsEndLine = IsEndLine;
            }
        }



        internal void SetNodeSize(ErectNodeControl i_nwNode, int i_NodeSize)
        {
            //NodeItemUnit nodeItemunit = GetNodeItemUnit(i_nwNode.NodeKey);
            NodeItemUnit nodeItemunit = NodeUnitCollection.FirstOrDefault(x => x.MISCode == i_nwNode.NodeKey);

            nodeItemunit.CustomWidth = i_NodeSize;
            nodeItemunit.CustomHeight = i_NodeSize;
        }

        #endregion

        #region Undo
        public MergeBuffer AddMergeBuffer(NodeItemUnit MergeNode, List<string> keyLisst, BufferType type)
        {
            MergeBuffer buffer = new MergeBuffer(type);
            NodeItemUnit MergeNodeClone = MergeNode.Clone() as NodeItemUnit;
            buffer.AddSymbol(MergeNodeClone);

            foreach (string key in keyLisst)
            {
                if (key != MergeNode.MISCode)
                {
                    NodeItemUnit node = GetNodeItemUnit(key);
                    NodeItemUnit nodeClone = node.Clone() as NodeItemUnit;

                    buffer.AddChildNode(nodeClone);

                    List<RelationItemUnit> childList = GetChildRlation(key);
                    List<RelationItemUnit> parentList = GetParentRlation(key);
                    childList.AddRange(parentList);

                    foreach (RelationItemUnit relation in childList)
                    {
                        RelationItemUnit relationClone = relation.Clone() as RelationItemUnit;
                        if (relationClone != null)
                        {
                            buffer.AddRelationItemUnitsOfChildNode(relationClone);
                        }
                    }

                }
            }

            this.MergeBuffers.Add(buffer);

            return buffer;
        }

        public void AddRelationBuffer(RelationItemUnit Relation)
        {
            RelationItemUnit clone = Relation.Clone() as RelationItemUnit;
            if (clone != null)
                this.RelationBuffers.Add(clone);
        }
        public void RemoveRelationBuffer(RelationItemUnit Relation)
        {
            if (RelationBuffers.Count > 0)
            {
                this.RelationBuffers.Remove(Relation);
            }
        }


        public RelationItemUnit PopRelationBuffer()
        {
            if (RelationBuffers.Count > 0)
            {
                RelationItemUnit relation = RelationBuffers[RelationBuffers.Count - 1];
                this.RelationBuffers.Remove(relation);
                return relation;
            }
            return null;
        }
        #endregion

        internal bool CheckAnassignNode()
        {
            if (NodeUnitCollection.Count > 0)
            {
                foreach (NodeItemUnit node in NodeUnitCollection)
                {
                    if (node.IsAssigned == false && node.HoGubun == "1")
                        return true;
                }
            }
            return false;
        }

        internal void SetKLNode(string nodekey, bool IsKL)
        {
            NodeItemUnit nodeItemUnit = GetNodeItemUnit(nodekey);

            if (nodeItemUnit != null)
            {
                nodeItemUnit.IsKL = IsKL;
                if (IsKL)
                {
                    nodeItemUnit.WorkPoint = "KL01";
                }
                else
                {
                    nodeItemUnit.WorkPoint = null;
                }

                List<RelationItemUnit> list = GetParentRlation(nodeItemUnit.MISCode);
                foreach (RelationItemUnit relation in list)
                {
                    NodeItemUnit preccedNode = GetNodeItemUnit(relation.PreccedeNodeKey);
                    SetPreccedeNodeOutFlag(preccedNode, IsKL);
                }
            }
        }

        internal void SetKL2Node(string nodekey, bool IsKL2)
        {
            NodeItemUnit nodeItemUnit = GetNodeItemUnit(nodekey);

            if (nodeItemUnit != null)
            {
                nodeItemUnit.IsKL2 = IsKL2;
                if (IsKL2)
                {
                    nodeItemUnit.WorkPoint = "KL02";
                }
                else
                {
                    nodeItemUnit.WorkPoint = null;
                }
            }
        }

        public List<NodeItemUnit> GetForwardNodeList(NodeItemUnit node, List<NodeItemUnit> NodeList)
        {
            List<RelationItemUnit> childRelationlist = GetChildRlation(node.MISCode);

            foreach (RelationItemUnit relation in childRelationlist)
            {
                NodeItemUnit childNode = GetAfterNode(relation);

                if (childNode != null)
                {
                    if (GetFTNodes().Contains(childNode))
                    {
                        return NodeList;
                    }
                    NodeList.Add(childNode);

                    GetForwardNodeList(childNode, NodeList);
                }
            }

            return NodeList;
        }
        internal void SetDock(List<NodeItemUnit> nodeList, string dock)
        {
            if (nodeList != null)
            {
                foreach (NodeItemUnit node in nodeList)
                {
                    SetRepregentDock(node, dock);
                }
            }
        }

        internal void SetInitDock(List<NodeItemUnit> nodeList, string dock)
        {
            if (nodeList != null)
            {
                foreach (NodeItemUnit node in nodeList)
                {
                    node.SetInitDock(dock);
                }
            }
        }

        internal void ForwardSetDock(NodeItemUnit node, string dock)
        {

            SetRepregentDock(node, dock);
            List<NodeItemUnit> NodeList = new List<NodeItemUnit>();
            NodeList = GetForwardNodeList(node, NodeList);
            foreach (NodeItemUnit findNode in NodeList)
            {

                SetRepregentDock(findNode, dock);
            }
        }

        private void SetRepregentDock(NodeItemUnit node, string dock)
        {
            if (node != null)
            {
                List<NodeItemUnit> nodeList = NodeUnitCollection.GetMergeNode(node.MISCode);
                foreach (NodeItemUnit findNode in nodeList)
                {
                    // 실행 취소 버퍼에 담기
                    this.nwUnDoWorkInfo.NodeSetDock(findNode.MISCode, findNode.Dock);

                    findNode.SetDock(dock);
                }
            }
        }

        internal void ForwardSetDock(List<NodeItemUnit> nodes, string dock)
        {
            foreach (NodeItemUnit node in nodes)
            {
                ForwardSetDock(node, dock);
            }
        }

        internal List<NodeItemUnit> GetALLNodeList(List<ErectNodeControl> nodeList)
        {
            List<NodeItemUnit> newNodeList = new List<NodeItemUnit>();

            foreach (ErectNodeControl node in nodeList)
            {
                NodeItemUnit unit = GetNodeItemUnit(node.NodeKey);

                if (unit != null)
                {
                    newNodeList.Add(unit);
                }

                if (node.MergeNodes != null)
                {
                    foreach (ErectNodeControl mnode in node.MergeNodes)
                    {
                        unit = GetNodeItemUnit(mnode.NodeKey);

                        if (unit != null)
                        {
                            newNodeList.Add(unit);
                        }
                    }
                }
            }

            return newNodeList;
        }


        internal void ChangeRelationNode(string origionMisNode, string afterMisNode, RelationItemUnit relationUnit)
        {
            if (origionMisNode != null && afterMisNode != null && relationUnit != null && this.RelationUnitCollection != null)
            {
                RelationUnitCollection.ChangeRelationKey(origionMisNode, afterMisNode, relationUnit);
            }
        }
        internal NodeItemUnit GetA11BlockStringNode()
        {
            if (NodeUnitCollection != null)
            {
                foreach (NodeItemUnit node in NodeUnitCollection)
                {
                    //if (node.RepresentativeBlockList != null && node.RepresentativeBlockList.Contains("A11"))
                    //2014.04.24 도성민 수정
                    if (node.WorkPoint == "SB01")
                    {
                        return GetNodeItemUnit(node.RepresentNode);
                    }
                }
            }

            return null;
        }

        internal bool ContainsBoringNode()
        {
            if (NodeUnitCollection == null) return false;
            foreach (NodeItemUnit unit in NodeUnitCollection)
            {
                if (unit.BlockList == "BORING")
                {
                    return true;
                }
            }
            return false;
        }

        public string GetCombineBlockList(List<string> blockStrList)
        {
            ENT.Utility.BlockStrReduct bsr = new Utility.BlockStrReduct();
            return bsr.GetCombineBlockList(blockStrList);
        }

    }
}

      






      


      
