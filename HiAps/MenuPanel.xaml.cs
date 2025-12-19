using Amisys.Framework.Infrastructure.DataModels;
using Amisys.Framework.Infrastructure.Utility;
using Amisys.Infrastructure.HHIInfrastructure.Defintions;
using Amisys.Infrastructure.HHIInfrastructure.Utility;
using Amisys.Infrastructure.HHIInfrastructure.DataModels;
using FirstFloor.ModernUI.Windows.Controls;
using NewMainShell.DataAccess;
using NewMainShell.DataModel;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
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

namespace NewMainShell.Views
{
    /// <summary>
    /// Interaction logic for MenuPanel.xaml
    /// </summary>
    public partial class MenuPanel : UserControl
    {
        public Dictionary<int, AmiMenuTreeViewItem> _TreeViewItemDic;
        private Dictionary<int, DataRow> _LeafNodeDic;
        public MenuPanel()
        {
            InitializeComponent();
            IsLoadedMenu = false;
            this.MouseWheel += new MouseWheelEventHandler(MenuPanel_MouseWheel);

        }

        private void MenuPanel_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            Decorator border = VisualTreeHelper.GetChild(MenuTreeView, 0) as Decorator;
            if (border != null)
            {
                // Get scrollviewer
                ScrollViewer scrollViewer = border.Child as ScrollViewer;
                if (scrollViewer != null)
                {
                    double step = 0.0;

                    if (e.Delta < 0)
                    {
                        if (scrollViewer.VerticalOffset + 5 <= scrollViewer.ScrollableHeight + 5)
                            step = scrollViewer.VerticalOffset + 10;
                        else
                            step = scrollViewer.VerticalOffset;
                    }
                    else
                    {
                        if (scrollViewer.VerticalOffset - 5 > 0)
                            step = scrollViewer.VerticalOffset - 10;
                        else
                            step = scrollViewer.VerticalOffset;
                    }

                    scrollViewer.ScrollToVerticalOffset(step);
                }
            }
        }

        #region members

        public DataTable _MenuTable;

        #endregion

        #region properties

        public bool IsLoadedMenu { get; set; }

        #endregion

        #region event definitions

        public delegate void RunworkspaceHandler(object sender, EventArgs arg);
        public event RunworkspaceHandler Runworkspace;

        public event EventHandler HideMenu;

        #endregion

        #region events

        private void OnMouseDoubleClick(object sender, EventArgs e)
        {
            AmiMenuTreeViewItem item = MenuTreeView.SelectedItem as AmiMenuTreeViewItem;
            if (item == null || item.ViewList == null || item.ViewList.Count < 1) return;

            // 임시
            // 메뉴중에 실행파일이 있으면 바로 호출
            RunExecutable(item.ViewList);

            if (Runworkspace != null) Runworkspace(item, new EventArgs());
            if (HideMenu != null) HideMenu(this, new EventArgs());
        }

        private void ExecuteAmiMainShell(string workspaceName)
        {
            if (string.IsNullOrEmpty(workspaceName) == false)
            {
                if (workspaceName == "선표 편집")
                    AmiFileUtility.Execute("AmiMainShell", "SungDong AMISYS AMISYS Master Master W Y 1");
                else if (workspaceName == "년간 계획")
                    AmiFileUtility.Execute("AmiMainShell", "SungDong Amisys Amisys Master Master W Y 2");
            }
        }

        private void RunExecutable(AmiViewList amiViewList)
        {
            if (amiViewList != null)
            {
                foreach (var item in amiViewList)
                {
                    string assemblyName = item["AssemblyName"].ToString();
                    if (AmiFileUtility.IsExecutable(assemblyName) == true)
                    {
                        string arguments = item["Tag"].ToString();
                        AmiFileUtility.Execute(assemblyName, arguments);
                    }
                }
            }
        }

        Point _LastMouseDown;
        //NCTreeViewItem _DraggedItem, _Target;
        private void treeView_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                _LastMouseDown = e.GetPosition(MenuTreeView);
            }

        }

        private void treeView_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Point currentPosition = e.GetPosition(MenuTreeView);


                if ((Math.Abs(currentPosition.X - _LastMouseDown.X) > 10.0) ||
                    (Math.Abs(currentPosition.Y - _LastMouseDown.Y) > 10.0))
                {
                    AmiMenuTreeViewItem item = MenuTreeView.SelectedItem as AmiMenuTreeViewItem;
                    if (item == null || item.ViewList == null || item.ViewList.Count < 1) return;

                    DataObject data = new DataObject();
                    data.SetData("Menu", MenuTreeView.SelectedItem);
                    DragDropEffects finalDropEffect = DragDrop.DoDragDrop(this, data, DragDropEffects.Move);
                }
            }
        }

        private void HideMenuButton_Click(object sender, RoutedEventArgs e)
        {
            if (HideMenu != null) HideMenu(this, new EventArgs());
        }

        private void SearchWorkspaceButton_Click(object sender, RoutedEventArgs e)
        {
            //if (string.IsNullOrEmpty(SearchTextBox.Text)) return;
            ApplyFilter(MenuTreeView, SearchTextBox.Text);
        }

        #endregion

        #region private methods

        private void ApplyFilter(TreeView tree, string searchString)
        {
            if (tree != null)
            {
                foreach (AmiMenuTreeViewItem item in tree.Items)
                {
                    if (ApplyTreeItemFilter(item, searchString))
                    {
                        // 검색 결과가 있으면 Expand
                        item.IsExpanded = true;
                    }
                }
            }
        }

        private bool ApplyTreeItemFilter(AmiMenuTreeViewItem item, string searchString)
        {
            if (item != null)
            {
                // bool hasString = string.IsNullOrEmpty(searchString) ? true : item.NodeName.Contains(searchString);
                //juwang 임시수정 2014.10.01
                bool hasString = string.IsNullOrEmpty(searchString) ? true : item.MenuDataRow["NodeName"].ToString().Contains(searchString);
                if (item.Items != null)
                {
                    foreach (var child in item.Items)
                    {
                        hasString |= ApplyTreeItemFilter(child as AmiMenuTreeViewItem, searchString);
                    }
                }

                item.Visibility = hasString ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

                return hasString;
            }
            return false;
        }

        private void InitTreeViewItems()
        {
            if (_TreeViewItemDic == null)
                _TreeViewItemDic = new Dictionary<int, AmiMenuTreeViewItem>();
            else
                _TreeViewItemDic.Clear();

            if (_LeafNodeDic == null)
                _LeafNodeDic = new Dictionary<int, DataRow>();
            else
                _LeafNodeDic.Clear();

            MenuTreeView.Items.Clear();
        }

        private void MakeMenuTree(DataTable dt)
        {
            if (dt != null && dt.Rows.Count > 0)
            {
                InitTreeViewItems();

                AmiMenuTreeViewItem parentTreeViewItem = null;

                int prevParentId = -1;

                DataRow rootRow = dt.Rows[0] as DataRow;
                int rootNodeId = int.Parse(rootRow["NodeId"].ToString());

                for (int i = 1; i < dt.Rows.Count; i++)
                {
                    var row = dt.Rows[i];

                    if (row is DataRow)
                    {
                        DataRow dr = row as DataRow;

                        bool isActive = dr["ActiveFlag"].ToString() == "Y" ? true : false;


                        if (isActive)
                        {
                            var node = dr["NodeId"].ToString();
                            int nodeId = String.IsNullOrEmpty(node) == false ? int.Parse(node.ToString()) : 0;

                            var parentNode = dr["ParentNodeId"].ToString();


                            var nodeName = dr["NodeName"].ToString();

                            int parentId = String.IsNullOrEmpty(parentNode) == false ? int.Parse(parentNode.ToString()) : 0;
                            {
                                if (parentId != prevParentId)
                                {
                                    if (_TreeViewItemDic.ContainsKey(parentId))
                                        parentTreeViewItem = _TreeViewItemDic[parentId];
                                    else
                                        parentTreeViewItem = null;
                                }

                                bool isLeafNode = dr["IsLeafNode"].ToString() == "Y" ? true : false;

                                if (isLeafNode)
                                {
                                    if (parentTreeViewItem != null)
                                    {
                                        if (parentId != prevParentId && isActive == true)
                                        {
                                            parentTreeViewItem.MouseDoubleClick += this.OnMouseDoubleClick;
                                            //parentTreeViewItem.SetEnabled();
                                        }

                                        parentTreeViewItem.AddViewList(dr);

                                        _LeafNodeDic.Add(nodeId, dr);
                                    }
                                    else
                                    {
                                        string msg = string.Format("Name:{0} IsLeafNode:{1} NodeId:{2}"
                                            , dr["NodeName"].ToString()
                                            , dr["IsLeafNode"].ToString()
                                            , dr["NodeId"].ToString());
                                        ModernDialog.ShowMessage(msg, "Error", MessageBoxButton.OK);
                                    }

                                }
                                else
                                {
                                    string nodeHeader = string.Format("{0}", dr["NodeName"]);
                                    var tvi = CreateTreeViewItem(dr);
                                    tvi.Tag = dr;

                                    _TreeViewItemDic.Add(nodeId, tvi);
                                    if (isActive == true)
                                    {
                                        if (parentTreeViewItem == null && MenuTreeView != null && parentId == rootNodeId)
                                            MenuTreeView.Items.Add(tvi);
                                        else
                                        {
                                            if (parentTreeViewItem != null)
                                            {
                                                parentTreeViewItem.Items.Add(tvi);
                                            }
                                        }
                                    }

                                }
                            }

                            prevParentId = parentId;
                        }
                        //else
                        //{
                        //    var node = dr["NodeId"].ToString();
                        //    int nodeId = String.IsNullOrEmpty(node) == false ? int.Parse(node.ToString()) : 0;
                        //    var tvi = CreateTreeViewItem(dr);
                        //    tvi.Tag = dr;
                        //    _TreeViewItemDic.Add(nodeId, tvi);
                        //}
                    }
                }
            }
        }

        private AmiMenuTreeViewItem CreateTreeViewItem(DataRow dr)
        {
            try
            {
                AmiMenuTreeViewItem tvi = new AmiMenuTreeViewItem(dr);

                tvi.Header = dr["NodeName"].ToString();

                tvi.Style = (Style)FindResource("TreeItemStyle");
                return tvi;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private AmiMenuTreeViewItem recv_FindMenu(AmiMenuTreeViewItem menu, int id)
        {
            foreach (AmiMenuTreeViewItem child in menu.Items)
            {
                if (child.GetWorkspaceId() == id) return child;
                recv_FindMenu(child, id);
            }
            return null;
        }

        #endregion

        #region public methods

        public bool InterceptData
        {
            get
            {
                string val = ConfigurationManager.AppSettings["AppInterceptData"];
                return string.IsNullOrEmpty(val) == false && val.ToUpper() == "TRUE";
            }
        }

        public bool LoadDataFromFile
        {
            get
            {
                string val = ConfigurationManager.AppSettings["AppLoadDataFromFile"];
                return string.IsNullOrEmpty(val) == false && val.ToUpper() == "TRUE";
            }
        }

        private string dataFilepath = AmiFileUtility.GetAppBasedAbsolutePath(@"XmlData\Menu.xml");

        public void LoadMenu()
        {
            if (IsLoadedMenu == true) return;


            string systemCode = App.mArgs.GetValue(ArgKind.SystemCode);

            bool OprDatabase = false;
            if (systemCode.Length > 4)
            {
                bool oprUse = systemCode.Substring(3, 1) == "1" ? true : false;
                if (oprUse)
                    OprDatabase = true;
            }

            SimpleDataAccessHelper access = new SimpleDataAccessHelper(OprDatabase);

            string DomainCategory = App.mArgs.GetValue(ArgKind.DomainCategroy);
            // HySPICS에서 넘어온 Parameter 중 사번을 이용해서 권한을 가져온다. (V_USER_AUTH_INFO)
            string userType = SetUserGroup(App.mArgs.GetValue(ArgKind.UserId), OprDatabase);

            if (string.IsNullOrEmpty(userType) == false)
                App.mArgs.AddItem(ArgKind.UserGroupId, userType);

            //string query = AmiDataAccessHelper.GetQuery("TCM_MENU_LOAD");
            //query = query.Replace(@":DomainCategory", DomainCategory);
            //query = query.Replace(@":UseType", userType);
            // 사번을 이용해서 가져온 권한(UseType)으로 사용가능한 메뉴를 구성함.
            string query = string.Format("Select \"DomainCategory\", \"UseType\", \"ViewSequence\", \"NodeId\", \"NodeName\", "
            + " \"ParentNodeId\", \"KorDesc\", \"EngDesc\", \"ChnDesc\", \"IsLeafNode\",\"ActiveFlag\",\"AssemblyName\", "
            + " \"AssemblyType\",\"Fixed\", \"ViewName\", \"AuthFlag\",\"ReLoading\",\"IconPath\",\"Tag\", \"HelpFilePath\" "
            + " From \"TCM_AmiShip_Menu\" where \"DomainCategory\"='{0}' and \"UseType\" = '{1}' order by \"ViewSequence\"", DomainCategory, userType);

            Console.WriteLine("query:" + query);
            DataSet ds = new DataSet();
            if (LoadDataFromFile)
            {
                DataSetUtil.ReadXml(ds, dataFilepath);
                if (ds != null && ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
                {
                    _MenuTable = ds.Tables[0];
                }
            }
            else
            {
                ds = access.ExecuteDataSet(query);
            }
            if (ds != null && ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
            {
                _MenuTable = ds.Tables[0];

                if (InterceptData)
                {
                    DataSetUtil.WriteXml(ds, dataFilepath);
                }
            }

            //_MenuTable = ds.Tables[0];

            IsLoadedMenu = true;
        }

        private string SetUserGroup(string userId, bool OprDatabase)
        {
            // 사번을 이용하여 HiAPS의 권한을 가져오는 함수
            string UserGroupId = string.Empty;
            if (string.IsNullOrEmpty(userId) == false)
            {
                try
                {
                    SimpleDataAccessHelper dataAccess = new SimpleDataAccessHelper(OprDatabase);

                    string query = string.Format("SELECT * FROM V_USER_AUTH_INFO WHERE EMPLNO = '{0}' ", userId);

                    var ds = dataAccess.ExecuteDataSet(query);
                    if (ds != null && ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
                    {
                        string userGroup = ds.Tables[0].Rows[0]["USER_GROUP"].ToString();
                        UserGroupId = userGroup;
                    }
                }
                catch (System.Exception)
                {
                    // 임시
                    return UserGroupId;
                }
            }

            return UserGroupId;
        }

        public AmiMenuTreeViewItem GetWorkspace(string code)
        {
            int id;
            if (int.TryParse(code, out id) == false) return null;

            AmiMenuTreeViewItem selMenu = null;
            foreach (AmiMenuTreeViewItem menu in _TreeViewItemDic.Values)
            {
                if (menu.GetWorkspaceId() == id)
                {
                    selMenu = menu;
                    break;
                }
                selMenu = recv_FindMenu(menu, id);
                if (selMenu != null) break;
            }

            if (selMenu != null && selMenu.Items.Count == 0) return selMenu;
            else return null;
        }

        public AmiMenuTreeViewItem GetMenuItem(int workId)
        {
            if (_TreeViewItemDic == null) return null;
            if (_TreeViewItemDic.ContainsKey(workId)) return _TreeViewItemDic[workId];
            else return null;
        }

        public DataRow GetView(int viewid)
        {
            if (_LeafNodeDic == null) return null;
            if (_LeafNodeDic.ContainsKey(viewid)) return _LeafNodeDic[viewid];
            else return null;
        }


        public void MakeMenuTree()
        {
            if (_MenuTable == null) return;
            MakeMenuTree(_MenuTable);
        }

        public List<BindableViewInfo> GetAvailableViews(AmiMenuTreeViewItem menuItem)
        {
            string rootId = string.Empty;
            BindableViewInfo info;
            List<BindableViewInfo> retList = new List<BindableViewInfo>();
            Dictionary<string, BindableViewInfo> rowDic = new Dictionary<string, BindableViewInfo>();
            foreach (DataRow row in _MenuTable.Rows)
            {
                if (row["ActiveFlag"].ToString() != "Y") continue;

                rowDic[row["NodeId"].ToString()] = info = new BindableViewInfo(row);
                if (string.IsNullOrEmpty(info.ParentNodeID)) { rootId = info.NodeID; continue; }
                retList.Add(info);

            }

            for (int index = retList.Count - 1; index >= 0; index--)
            {
                info = retList[index];
                if (info.ParentNodeID == rootId) continue;

                if (rowDic.ContainsKey(info.NodeID) && rowDic.ContainsKey(info.ParentNodeID))
                {
                    rowDic[info.ParentNodeID].Childs.Add(rowDic[info.NodeID]);
                }
                retList.RemoveAt(index);
            }

            return retList;
        }

        #endregion

    }
}
