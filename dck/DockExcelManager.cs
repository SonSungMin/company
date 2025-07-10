using DevExpress.Spreadsheet;
using DevExpress.XtraSpreadsheet;
using HHI.SHP.PS002.Client;
using HHI.SHP.PS002.COMMON;
using System;
using System.Collections.Generic;
using System.Data;
//using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;

namespace HHI.SHP.PS002.DCK.Excel
{
    public class DockExcelManager
    {
        MainModel _Model;
        ExcelConvertOpenXML _Excel = null;
        Dictionary<string, ExecDockPosInfo> _ExecPosDic = new Dictionary<string, ExecDockPosInfo>();
        Dictionary<string, BasicDockPosInfo> _BasicPosDic = new Dictionary<string, BasicDockPosInfo>();
        Dictionary<string, DetailDockPosInfo> _DetailPosDic = new Dictionary<string, DetailDockPosInfo>();

        /// <summary>
        /// 1도크의 치수
        ///        ____________________
        ///        |                  |
        ///      - |______      ______|
        /// dim1         |      |
        ///      _       |______|
        ///        |     |      |
        ///         dim2   dim3
        /// </summary>
        double Dock1Dim1, Dock1Dim2, Dock1Dim3;

        ShipManager _ShipManager;
        DockManager _DockManager;

        string _CurrentYear;
        int _CurrentBatch;

        public DockExcelManager(MainModel model)
        {
            if (model.BatchKind == DockBatchType.실행도크배치)
                SetDefaultExecPosData();
            else if (model.BatchKind == DockBatchType.상세도크배치)
                SetDefaultDetailPosData();

            _Model = model;

            DCK004 dck = new DCK004();

            //_ShipManager = new ShipManager(dck);
            //_DockManager = new DockManager();
            _ShipManager = model.ShipManager;
            _DockManager = model.DockManager;

            Init1DockDim();
        }

        /// <summary>
        /// 1도크 3개 치수 초기화
        /// </summary>
        void Init1DockDim()
        {
            DockUnit dock = _Model.DockManager.GetDockInfo(string.Format("{0}{1:D02}", _Model.Year, _Model.BatchNum));
            string[] parts = dock.SHA_PNT.Split(',');

            try
            {
                double[,] points = new double[parts.Length / 2, 2];

                for (int i = 0; i < parts.Length; i += 2)
                {
                    points[i / 2, 0] = double.Parse(parts[i]);
                    points[i / 2, 1] = double.Parse(parts[i + 1]);
                }

                Dock1Dim1 = (points[4, 1] - points[3, 1]) / 10; // 아래로 내려오는 수직 구간
                Dock1Dim2 = (points[6, 0] - points[7, 0]) / 10; // 도크 왼쪽부터 T자의 |부분이 시작되는 구간
                Dock1Dim3 = (points[4, 0] - points[5, 0]) / 10; // T자 바닥 구간
            }
            catch { }
        }

        /// <summary>
        /// NAS에 있는 엑셀 파일 다운로드
        /// </summary>
        /// <returns></returns>
        string GetNASFileDown()
        {
            string copyName = "", fileName = "";
            string typGbn = _Model.BatchKind == DockBatchType.실행도크배치 ? "실행" : "상세";
            string dock = _Model.DockCode;
            string path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string folder = $@"\{dock}DK{typGbn}_{DateTime.Now.ToString("yyyyMMddHHmmdd")}.xlsx";
            copyName = path + folder;

            if (dock.Equals("H"))
                fileName = $"{dock}DK{typGbn}_new.xlsx"; // NAS의 원본양식 화일
            else
                fileName = $"{dock}DK{typGbn}.xlsx"; // NAS의 원본양식 화일

            Net.HttpFileManager httpFileManager = new Net.HttpFileManager();
            httpFileManager.DownloadFile("/ExcelTamplate/DockBatch", fileName, 0, copyName, -1, "SHPPS002" /*SystemCode*/);

            return copyName;
        }

        //#region public methods
        /// <summary>
        /// 도크 배치 엑셀 생성 (시작점)
        /// </summary>
        /// <returns></returns>
        public bool CreateDockExcel()
        {
            try
            {
                //1 - 9, H, G도크가 아니면 리턴
                if (!IsValidDock(_Model.DockCode))
                    return false;
                
                // 엑셀 템플릿 다운로드
                string copyName = GetNASFileDown();

                _Excel = new ExcelConvertOpenXML();
                _Excel.Open(copyName);

                _CurrentYear = _Model.Year;
                _CurrentBatch = _Model.BatchNum;

                // 제목
                SetTitle(_Model.BatchKind);

                if (_Model.BatchKind == DockBatchType.실행도크배치)
                    SetExecBatchInfo(_Model.DockCode);
                else if (_Model.BatchKind == DockBatchType.상세도크배치)
                    SetDetailBatchInfo(_Model.DockCode);
                
                _Excel.Save(copyName);
                //_Excel.ExecuteExcel(copyName);

                SetChangeFormat(copyName);

                return true;
            }
            catch (Exception ex)
            {
                if (_Excel != null) _Excel.Close();

                MessageBox.Show("엑셀변환중 문제가 발생했습니다. 다시 시도해 주세요.\n" + ex);
                return false;
            }
        }

        void SetChangeFormat(string fileName)
        {
            var spreadsheetControl = new SpreadsheetControl();
            spreadsheetControl.Options.DocumentCapabilities.Formulas = DocumentCapability.Enabled;
            spreadsheetControl.LoadDocument(fileName, DevExpress.Spreadsheet.DocumentFormat.Xlsx);

            float adjHeight = 25;
            float adjHeight2 = 45;
            int zOrder = 0;
            int addVal = 5;
            int addFontSize = 12;

            if (_Model.BatchKind == DockBatchType.상세도크배치)
            {
                addVal = 5;
                addFontSize = 11;
            }

            string batchNo = string.Format("{0}{1:D02}", _CurrentYear, _CurrentBatch);

            List<ShipBatchUnit> shipList = _Model.GetShipList(batchNo);

            string next = _Model.GetNextBatchNo(batchNo);
            string year = next.Substring(0, 4);
            int batch = int.Parse(next.Substring(4, 2));

            batchNo = string.Format("{0}{1:D02}", _CurrentYear, batch);
            List<ShipBatchUnit> nxtShipList = _Model.GetShipList(batchNo);

            if(nxtShipList.Any())
            {
                shipList.AddRange(nxtShipList);
            }

            using (var wkBook = spreadsheetControl.Document)
            {
                if (wkBook.Worksheets[0].Name.StartsWith("Sheet"))
                    goto gotoEnd;


                foreach (Worksheet wkSheet in wkBook.Worksheets)
                {
                    wkBook.Unit = DevExpress.Office.DocumentUnit.Point;

                    zOrder = wkSheet.Shapes.Count - 1;

                    foreach (Shape shape in wkSheet.Shapes)
                    {
                        if (shape.Name == "InnerDim")
                        {
                            //shape.ShapeText.AutoSize = ShapeTextAutoSizeType.Normal;
                            //shape.ShapeText.AutoSize = ShapeTextAutoSizeType.Shape;
                            //shape.ShapeText.VerticalOverflow = ShapeTextVerticalOverflowType.Clip;

                            shape.Fill.SetSolidFill(System.Drawing.Color.Yellow);
                            shape.Outline.SetSolidFill(System.Drawing.Color.Red);

                            shape.Top += (shape.Height - adjHeight) / 2;
                            shape.Left -= addVal;

                            shape.Width = shape.ShapeText.Length == 1 ? 20 : shape.ShapeText.Length * addFontSize; // 대략적인 너비 계산
                            shape.Height = adjHeight;

                            //shape.ShapeText.MarginTop = 1;

                            shape.ZOrderPosition = zOrder + 1;
                        }
                        else if (shape.Name == "Title")
                        {
                            shape.ShapeText.AutoSize = ShapeTextAutoSizeType.Shape;
                            //shape.ShapeText.VerticalOverflow = ShapeTextVerticalOverflowType.Clip;

                            shape.Top += (shape.Height / 2) - (adjHeight2 / 2);
                            shape.Height = adjHeight2;
                        }
                        else if (shape.Name.StartsWith("HoldText"))
                        {
                            //shape.ZOrderPosition = zOrder;
                            string shp_cod = shape.Name.Split(':')[1];
                            bool isLeft = false; // 선수 방향 왼쪽

                            var shp_info = shipList.Where(t => t.SHP_COD == shp_cod);
                            if (shp_info.Any())
                                isLeft = shp_info.First().SHP_ANG == 180;

                            float org_width = shape.Width;
                            float org_left = shape.Left;

                            shape.ShapeText.VerticalAnchor = ShapeTextVerticalAnchorType.Center;
                            shape.Width = shape.ShapeText.Length == 1 ? 18 : shape.ShapeText.Length * addFontSize;
                            shape.Height += addVal;
                            shape.Top -= addVal;
                            
                            // 선수 방향 왼쪽
                            if (isLeft)
                                shape.Left -= shape.Width;
                        }
                    }
                }

                wkBook.CalculateFull();
                wkBook.SaveDocument(fileName);
            }

            gotoEnd:

            System.Diagnostics.Process.Start(fileName);
        }

        /// <summary>
        /// 구간 배치 시작 배치
        /// </summary>
        string BatchsStart = "";

        /// <summary>
        /// 구간 배치 종료 배치
        /// </summary>
        string BatchsEnd = "";

        /// <summary>
        /// 구간 배치
        /// </summary>
        /// <param name="startYear"></param>
        /// <param name="startBatch"></param>
        /// <param name="endYear"></param>
        /// <param name="endBatch"></param>
        /// <returns></returns>
        public bool CreateBatchs(string startYear, int startBatch, string endYear, int endBatch)
        {
            if (!IsValidDock(_Model.DockCode))
                return false;

            BatchsStart = $"{startYear}{startBatch.ToString("D2")}";
            BatchsEnd = $"{endYear}{endBatch.ToString("D2")}";

            string copyName = GetNASFileDown();

            _Excel = new ExcelConvertOpenXML();
            _Excel.Open(copyName);

            int sheetCount = GetExcelSheetCount(startYear, startBatch, endYear, endBatch);

            string sheetName = _Excel.GetDefaultSheetName();

            string newName;
            List<string> sheetList = new List<string>();
            for (int index = 0; index < sheetCount; index++)
            {
                newName = GetNewSheetName(startYear, startBatch, index * 2, index + 1);

                if (index == 0)
                {
                    _Excel.ChangeSheetName(sheetName, newName);
                    sheetName = newName;
                }
                else
                    _Excel.CopySheet(sheetName, newName);

                sheetList.Add(newName);
            }

            try
            {
                string batchNo;
                _CurrentYear = startYear;// _Model.Year;
                _CurrentBatch = startBatch;// _Model.BatchNum;
                for (int index = 0; index < sheetCount; index++)
                {
                    _Excel.SetWorkSheet(sheetList[index]);

                    //SetTitle(DockBatchType.상세도크배치);
                    SetTitle(_Model.BatchKind);

                    if (_Model.BatchKind == DockBatchType.실행도크배치)
                    {
                        SetExecBatchInfo(_Model.DockCode);
                    }
                    else if (_Model.BatchKind == DockBatchType.상세도크배치)
                    {
                        SetDetailBatchInfo(_Model.DockCode);
                    }

                    batchNo = GetDockBatchNo(_CurrentYear, _CurrentBatch, 2);

                    if (string.IsNullOrEmpty(batchNo))
                        break;

                    _CurrentYear = batchNo.Substring(0, 4);
                    _CurrentBatch = int.Parse(batchNo.Substring(4, 2));
                }
            }
            catch (Exception e)
            {
                _Excel.Save(copyName);
                _Excel.ExecuteExcel(copyName);
                return false;
            }

            _Excel.Save(copyName);
            //_Excel.ExecuteExcel(copyName);
            SetChangeFormat(copyName);

            BatchsStart = "";
            BatchsEnd = "";

            return true;
        }

        private void SetTitle(string batchKind)
        {
            string batchNo = string.Format("{0}{1:D02}", _CurrentYear, _CurrentBatch);
            string title = _Model.GetTitle(batchNo);
            string title_nxt = _Model.GetNextTitle(batchNo);
            
            if (batchKind == DockBatchType.실행도크배치)
            {
                if(_Model.DockCode == "H")
                    _Excel.SetText(9, 2, title);
                else
                    _Excel.SetText(8, 3, title);
            }
            else if (batchKind == DockBatchType.기본도크배치)
            {
                _Excel.SetText(8, 3, title);
            }
            else
            {
                DetailDockPosInfo info = _DetailPosDic[_Model.DockCode];

                _Excel.SetText(info.BatchTitleRow1, info.BatchTitleColumn1, title);
                _Excel.SetText(info.BatchTitleRow2, info.BatchTitleColumn2, title_nxt);
            }
        }


        private double GetERPosition(DataTable table)
        {
            double pos = 0;
            bool isEr = false;
            foreach (DataRow row in table.Rows)
            {
                //if (row["HLD_GBN"].ToString() == HoldType.ER)
                if (row["HLD_TYPE"].ToString() == HoldType.ER)
                {
                    isEr = true;
                    break;
                }
                pos += row["HLD_LEN"] == DBNull.Value ? 0 : (double)row["HLD_LEN"] / 10;
            }

            if (isEr)
                return pos;
            else
                return -1;
        }

        private string GetPegNumberString(double pos, double term)
        {
            double tmpPos = Math.Round(pos, 1);
            int pegOrder = (int)(tmpPos / term);
            double extra = ((int)Math.Round(((tmpPos % term) * 10), 2)) / 10.0;

            //int pegOrder = (int)(pos / term);
            //double extra = ((int)Math.Round(((pos % term) * 10), 2)) / 10.0;
            return string.Format("P{0}+{1}", pegOrder, extra);
        }
        
        private bool IsValidDock(string dockCode)
        {
            if (string.IsNullOrWhiteSpace(dockCode)) return false;

            Regex regex = new Regex("[1-9]");

            if (_Model.BatchKind == DockBatchType.기본도크배치)
            {
                bool isValid = regex.IsMatch(dockCode);
                if (isValid == false && dockCode == "G")
                    return true;

                return isValid;
            }
            else if (_Model.BatchKind == DockBatchType.상세도크배치 || _Model.BatchKind == DockBatchType.실행도크배치)
            {
                bool isValid = regex.IsMatch(dockCode);

                if (isValid == false && dockCode == "H")
                    return true;

                return isValid;
            }
            else
                return regex.IsMatch(dockCode);
        }

        private string GetSizeDesc(ShipBatchUnit ship)
        {
            string height = ship.Height.ToString();
            string width = ship.Width.ToString();
            string dep = ship.DEP.ToString();
            if (!height.Contains('.')) height = height += ".0";
            if (!width.Contains('.')) width = width += ".0";
            if (!dep.Contains('.')) dep = dep += ".0";
            return string.Format("({0} * {1} * {2})", width, height, dep);
        }


        #region 실행 도크 배치

        /// <summary>
        /// 실행 도크 배치
        /// </summary>
        /// <param name="dockCode"></param>
        private void SetExecBatchInfo(string dockCode)
        {
            int shipRow = GetDockShipRow(dockCode);
            int pegRow = GetDockPegRow(dockCode);

            DockUnit dock = _Model.DockManager.GetDockInfo(string.Format("{0}{1:D02}", _CurrentYear, _CurrentBatch));

            string mainEvt;
            string batchNo = string.Format("{0}{1:D02}", _CurrentYear, _CurrentBatch);

            List<ShipBatchUnit> shipList = _Model.GetShipList(batchNo); //_ShipManager.GetShipListByBatchNo(batchNo); //_Model.GetShipList();
            shipList = SortShip(shipList);

            foreach (ShipBatchUnit ship in shipList)
            {
                if (ship.IsAssignBatch == false)
                    continue;

                _Excel.SetText(shipRow, GetDockShipColumn(dockCode, 0), ship.FIG_SHP);

                if (ship.MAIN_EVT == "FT")
                    mainEvt = "L/C";
                else
                    mainEvt = "F/T";

                _Excel.SetText(shipRow, GetDockShipColumn(dockCode, 1), mainEvt);
                _Excel.SetText(shipRow, GetDockShipColumn(dockCode, 2), ship.LOA.ToString());
                _Excel.SetText(shipRow, GetDockShipColumn(dockCode, 3), ship.WID.ToString());
                _Excel.SetText(shipRow, GetDockShipColumn(dockCode, 4), ship.DEP.ToString());
                _Excel.SetText(pegRow, GetDockPegColumn(dockCode, 0), ship.FIG_SHP);

                if (ship.MAIN_EVT == "FT")
                    mainEvt = "L/C";
                else
                    mainEvt = "F/T";

                _Excel.SetText(pegRow, GetDockPegColumn(dockCode, 1), mainEvt);

                if (dock.PEG_TRM == 0)
                    continue;
                
                double pos;
                if (ship.SHP_ANG == 0)
                    pos = ship.X_POS;
                else
                    pos = ship.X_POS + ship.Width;

                if (dock.GateX > pos)
                {
                    pos = dock.Width - pos;
                }

                // #####################
                // ### AFT. SHIP END ###
                // #####################
                _Excel.SetText(pegRow, GetDockPegColumn(dockCode, 2), GetPegNumberString(pos, dock.PEG_TRM));

                DataTable table = _Model.ShipManager.GetAPHoldInfo(ship.SHP_COD);
                //if (table != null)
                //2013.09.27 도성민 수정
                if (table != null && table.Rows.Count > 0)
                {
                    string value = _Model.GetAPHoldInfo(ship.SHP_COD).ToString();// table.Rows[0]["APP_POS"].ToString();

                    if (string.IsNullOrWhiteSpace(value) || double.TryParse(value, out pos) == false)
                        pos = 0;

                    // 선미가 왼쪽
                    if (ship.SHP_ANG == 0)
                        pos = ship.X_POS - ship.BLD_POS + pos;
                    else
                        pos = ship.X_POS + ship.Width + ship.BLD_POS - pos;

                    if (dock.GateX > pos)
                    {
                        pos = dock.Width - pos;
                    }

                    // ###########
                    // ### A.P ### 선미에서 시작
                    // ###########
                    _Excel.SetText(pegRow, GetDockPegColumn(dockCode, 3), GetPegNumberString(pos, dock.PEG_TRM));

                    // 선수가 왼쪽인 경우
                    //if (ship.SHP_ANG == 180)
                    //    table = table.AsEnumerable().OrderByDescending(o => o["SEQ_NO"]).CopyToDataTable();

                    pos = GetERPosition(table);

                    if (pos >= 0)
                    {
                        // 선수 오른쪽
                        if (ship.SHP_ANG == 0)
                            pos = ship.X_POS + pos - ship.BLD_POS;
                        // 선수 왼쪽
                        else
                            pos = ship.X_POS + ship.Width - pos + ship.BLD_POS;

                        if (dock.GateX > pos)
                        {
                            pos = dock.Width - pos;
                        }

                        // ###################
                        // ### AFT.E/R BHD ### 건조 끝에서 시작
                        // ###################
                        _Excel.SetText(pegRow, GetDockPegColumn(dockCode, 4), GetPegNumberString(pos, dock.PEG_TRM));
                    }
                }

                // 호선 및 홀드 생성
                DrawShip(ship, dockCode);

                shipRow++;
                pegRow++;
            }

            // 구조물
            DrawAddItem(dockCode, true);
            // 도크 바깥쪽 치수
            DrawShipInterval(dockCode, shipList, false);
            // 도크 안쪽 치수
            DrawConstraintVlaue(dockCode, shipList, false);

            //draw next batch
            batchNo = string.Format("{0}{1:D02}", _CurrentYear, _CurrentBatch);
            string next = _Model.GetNextBatchNo(batchNo);
            shipList = _Model.GetShipList(next);

            if (shipList == null)
                return;

            foreach (ShipBatchUnit ship in shipList)
            {
                DrawNextShip(ship, dockCode);
            }
        }
        #endregion 실행 도크 배치

        private List<ShipBatchUnit> SortShip(List<ShipBatchUnit> shipList)
        {
            List<ShipBatchUnit> lcList = new List<ShipBatchUnit>();
            List<ShipBatchUnit> ftList = new List<ShipBatchUnit>();
            foreach (ShipBatchUnit ship in shipList)
            {
                if (ship.NextBatch == null) lcList.Add(ship);
                else ftList.Add(ship);
            }

            if (lcList.Count > 1) lcList = lcList.OrderBy(p => p.FIG_SHP).ToList();
            if (ftList.Count > 1) ftList = ftList.OrderBy(p => p.FIG_SHP).ToList();

            lcList.AddRange(ftList);
            return lcList;
        }


        /// <summary>
        /// 차기배치 호선 출력
        /// </summary>
        /// <param name="ship"></param>
        /// <param name="dockCode"></param>
        private void DrawNextShip(ShipBatchUnit ship, string dockCode)
        {
            if (ship.IsAssignBatch == false) return;

            if (!_ExecPosDic.ContainsKey(dockCode)) return;
            ExecDockPosInfo info = _ExecPosDic[dockCode];

            //column 하나의 논리적크기는 2.info.RowRatiom, row하나의 논리적 크기는 info.RowRatiom
            Rect rect = _Excel.GetCellPosition(info.NextStartRow, info.NextStartColumn);

            int fromCol = info.NextStartColumn + (int)((ship.X_POS / info.NextColumnRatio)) - 1;
            int fromRow = info.NextStartRow + (int)((ship.Y_POS / info.NextRowRatio));
            int toCol = info.NextStartColumn + (int)((ship.X_POS + ship.Width) / info.NextColumnRatio) - 1;
            int toRow = info.NextStartRow + (int)((ship.Y_POS + ship.Height) / info.NextRowRatio) - 1;
            double colFromOffset = (ship.X_POS % info.NextColumnRatio) / info.NextColumnRatio * rect.Width;
            double rowFromOffset = (ship.Y_POS % info.NextRowRatio) / info.NextRowRatio * rect.Height;
            double colToOffset = ((ship.X_POS + ship.Width) % info.NextColumnRatio) / info.NextColumnRatio * rect.Width;
            double rowToOffset = ((ship.Y_POS + ship.Height) % info.NextRowRatio) / info.NextRowRatio * rect.Height;

            if (!string.IsNullOrWhiteSpace(ship.TandemShape) && ship.TandemWidth == 0)
            {
                //_Excel.CreateShape(fromCol, fromRow, colFromOffset, rowFromOffset, toCol, toRow, colToOffset, rowToOffset,
                //    ship.ShapeData, Colors.LightGray, Colors.Blue, ship.SHP_ANG);

                //2013.07.03 도성민 F/T전 형성구획 색깔 회색->흰색으로 변경, 박수진CJ 요청
                //CreateShape(fromCol, fromRow, colFromOffset, rowFromOffset, toCol, toRow, colToOffset, rowToOffset,
                //    ship.ShapeData, Colors.LightGray, Colors.Blue, ship.SHP_ANG);
                CreateShape(fromCol, fromRow, colFromOffset, rowFromOffset, toCol, toRow, colToOffset, rowToOffset,
                    ship.ShapeData, Colors.White, Colors.Blue, ship.SHP_ANG);
            }
            else
            {
                //_Excel.CreateShape(fromCol, fromRow, colFromOffset, rowFromOffset, toCol, toRow, colToOffset, rowToOffset,
                //    ship.ShapeData, Colors.LightGreen, Colors.Blue, ship.SHP_ANG);
                CreateShape(fromCol, fromRow, colFromOffset, rowFromOffset, toCol, toRow, colToOffset, rowToOffset,
                    ship.ShapeData, Colors.LightGreen, Colors.Blue, ship.SHP_ANG);
            }

            if (!string.IsNullOrWhiteSpace(ship.TandemShape) && ship.TandemWidth > 0)
            {
                if (ship.BLD_COD == BuildType.TR || ship.BLD_COD == BuildType.TX)
                {
                    ShipBatchUnit prev = ship.PrevBatchs.FirstOrDefault();
                    if (prev != null)
                    {
                        if (ship.SHP_ANG == 0)
                        {
                            fromCol = info.NextStartColumn + (int)(((ship.X_POS + prev.BLD_POS) / info.NextColumnRatio)) - 1;
                            fromRow = info.NextStartRow + (int)((ship.Y_POS / info.NextRowRatio));
                            toCol = info.NextStartColumn + (int)(((ship.X_POS + prev.BLD_POS) + prev.TandemWidth) / info.NextColumnRatio) - 1;
                            toRow = info.NextStartRow + (int)((ship.Y_POS + ship.Height) / info.NextRowRatio) - 1;
                            colFromOffset = ((ship.X_POS + prev.BLD_POS) % info.NextColumnRatio) / info.NextColumnRatio * rect.Width;
                            rowFromOffset = (ship.Y_POS % info.NextRowRatio) / info.NextRowRatio * rect.Height;
                            colToOffset = (((ship.X_POS + prev.BLD_POS) + prev.TandemWidth) % info.NextColumnRatio) / info.NextColumnRatio * rect.Width;
                            rowToOffset = ((ship.Y_POS + ship.Height) % info.NextRowRatio) / info.NextRowRatio * rect.Height;
                        }
                        else
                        {
                            fromCol = info.NextStartColumn + (int)(((ship.X_POS + (ship.LOA - (prev.BLD_POS + prev.TandemWidth))) / info.NextColumnRatio)) - 1;
                            fromRow = info.NextStartRow + (int)((ship.Y_POS / info.NextRowRatio));
                            toCol = info.NextStartColumn + (int)((ship.X_POS + (ship.LOA - prev.BLD_POS)) / info.NextColumnRatio) - 1;
                            toRow = info.NextStartRow + (int)((ship.Y_POS + ship.Height) / info.NextRowRatio) - 1;
                            colFromOffset = ((ship.X_POS + (ship.LOA - (prev.BLD_POS + prev.TandemWidth))) % info.NextColumnRatio) / info.NextColumnRatio * rect.Width;
                            rowFromOffset = (ship.Y_POS % info.NextRowRatio) / info.NextRowRatio * rect.Height;
                            colToOffset = ((ship.X_POS + (ship.LOA - prev.BLD_POS)) % info.NextColumnRatio) / info.NextColumnRatio * rect.Width;
                            rowToOffset = ((ship.Y_POS + ship.Height) % info.NextRowRatio) / info.NextRowRatio * rect.Height;

                        }
                        string shape = HHIDockBizLogic.GetTendemGeometry(ship, BuildLevel.중간, ship.SHA_PNT, ship.SHA_TYP);
                        //_Excel.CreateShape(fromCol, fromRow, colFromOffset, rowFromOffset, toCol, toRow, colToOffset, rowToOffset,
                        //shape, Colors.LightGray, Colors.Blue, ship.SHP_ANG);

                        //2013.07.03 도성민 F/T전 형성구획 색깔 회색->흰색으로 변경, 박수진CJ 요청
                        //CreateShape(fromCol, fromRow, colFromOffset, rowFromOffset, toCol, toRow, colToOffset, rowToOffset,
                        //    shape, Colors.LightGray, Colors.Blue, ship.SHP_ANG);
                        CreateShape(fromCol, fromRow, colFromOffset, rowFromOffset, toCol, toRow, colToOffset, rowToOffset,
                            shape, Colors.White, Colors.Blue, ship.SHP_ANG);

                    }
                }
                else
                {
                    if (ship.SHP_ANG == 0)
                    {
                        fromCol = info.NextStartColumn + (int)((ship.X_POS / info.NextColumnRatio)) - 1;
                        fromRow = info.NextStartRow + (int)((ship.Y_POS / info.NextRowRatio));
                        toCol = info.NextStartColumn + (int)((ship.X_POS + ship.LOA - ship.TandemWidth) / info.NextColumnRatio) - 1;
                        toRow = info.NextStartRow + (int)((ship.Y_POS + ship.Height) / info.NextRowRatio) - 1;
                        colFromOffset = (ship.X_POS % info.NextColumnRatio) / info.NextColumnRatio * rect.Width;
                        rowFromOffset = (ship.Y_POS % info.NextRowRatio) / info.NextRowRatio * rect.Height;
                        colToOffset = ((ship.X_POS + ship.LOA - ship.TandemWidth) % info.NextColumnRatio) / info.NextColumnRatio * rect.Width;
                        rowToOffset = ((ship.Y_POS + ship.Height) % info.NextRowRatio) / info.NextRowRatio * rect.Height;
                    }
                    else
                    {
                        fromCol = info.NextStartColumn + (int)(((ship.X_POS + ship.TandemWidth) / info.NextColumnRatio)) - 1;
                        fromRow = info.NextStartRow + (int)((ship.Y_POS / info.NextRowRatio));
                        toCol = info.NextStartColumn + (int)((ship.X_POS + ship.Width) / info.NextColumnRatio) - 1;
                        toRow = info.NextStartRow + (int)((ship.Y_POS + ship.Height) / info.NextRowRatio) - 1;
                        colFromOffset = ((ship.X_POS + ship.TandemWidth) % info.NextColumnRatio) / info.NextColumnRatio * rect.Width;
                        rowFromOffset = (ship.Y_POS % info.NextRowRatio) / info.NextRowRatio * rect.Height;
                        colToOffset = ((ship.X_POS + ship.Width) % info.NextColumnRatio) / info.NextColumnRatio * rect.Width;
                        rowToOffset = ((ship.Y_POS + ship.Height) % info.NextRowRatio) / info.NextRowRatio * rect.Height;
                    }

                    string shape = HHIDockBizLogic.GetTendemGeometry(ship, BuildLevel.선미, ship.SHA_PNT, ship.SHA_TYP);
                    //_Excel.CreateShape(fromCol, fromRow, colFromOffset, rowFromOffset, toCol, toRow, colToOffset, rowToOffset,
                    //shape, Colors.LightGray, Colors.Blue, ship.SHP_ANG);

                    //2013.07.03 도성민 F/T전 형성구획 색깔 회색->흰색으로 변경, 박수진CJ 요청 

                    //CreateShape(fromCol, fromRow, colFromOffset, rowFromOffset, toCol, toRow, colToOffset, rowToOffset,
                    //    shape, Colors.LightGray, Colors.Blue, ship.SHP_ANG);
                    CreateShape(fromCol, fromRow, colFromOffset, rowFromOffset, toCol, toRow, colToOffset, rowToOffset,
                        shape, Colors.White, Colors.Blue, ship.SHP_ANG);
                }
            }

            string dizeDesc = GetSizeDesc(ship);
            string desc = string.Format("{0}\r\n{1}", ship.ShipDescription, dizeDesc);
            CreateExcelTextEx(info.NextStartColumn, info.NextStartRow, info.NextColumnRatio, info.NextRowRatio,
                rect, ship, ship.X_POS, ship.X_POS + ship.Width,
                ship.Y_POS, ship.Y_POS + ship.Height, desc, Colors.Black, Colors.White,
                "돋움", 1200, true, ExcelHorizentalAlignment.Center, ExcelVerticalAlignment.Center);

        }

        /// <summary>
        /// 라인 출력
        /// </summary>
        /// <param name="startCol"></param>
        /// <param name="startRow"></param>
        /// <param name="colRatio"></param>
        /// <param name="rowRatio"></param>
        /// <param name="rect"></param>
        /// <param name="ship"></param>
        /// <param name="x1"></param>
        /// <param name="x2"></param>
        /// <param name="y1"></param>
        /// <param name="y2"></param>
        /// <param name="color"></param>
        /// <param name="style"></param>
        private void CreateExcelLine(int startCol, int startRow, double colRatio, double rowRatio,
            Rect rect, ShipBatchUnit ship,
            double x1, double x2, double y1, double y2, Color color, LineDashValues style)
        {
            int fromCol = startCol + (int)(x1 / colRatio) - 1;
            int fromRow = startRow + (int)(y1 / rowRatio) - 1;
            int toCol = startCol + (int)(x2 / colRatio) - 1;
            int toRow = startRow + (int)(y2 / rowRatio) - 1;

            double colFromOffset = (x1 % colRatio) / colRatio * rect.Width;
            double rowFromOffset = (y1 % rowRatio) / rowRatio * rect.Height;
            double colToOffset = (x2 % colRatio) / colRatio * rect.Width;
            double rowToOffset = (y2 % rowRatio) / rowRatio * rect.Height;


            _Excel.CreateLine(fromCol, fromRow, colFromOffset, rowFromOffset, toCol, toRow, colToOffset, rowToOffset, color, style);
        }

        /// <summary>
        /// 화살표 라인 출력
        /// </summary>
        /// <param name="startCol"></param>
        /// <param name="startRow"></param>
        /// <param name="colRatio"></param>
        /// <param name="rowRatio"></param>
        /// <param name="rect"></param>
        /// <param name="x1"></param>
        /// <param name="x2"></param>
        /// <param name="y1"></param>
        /// <param name="y2"></param>
        /// <param name="color"></param>
        private void CreateExcelArrowLine(int startCol, int startRow, double colRatio, double rowRatio,
            Rect rect,
            double x1, double x2, double y1, double y2, Color color)
        {
            int fromCol = startCol + (int)(x1 / colRatio) - 1;
            int fromRow = startRow + (int)(y1 / rowRatio) - 1;
            int toCol = startCol + (int)(x2 / colRatio) - 1;
            int toRow = startRow + (int)(y2 / rowRatio) - 1;

            if (toCol < fromCol || toRow < fromRow) return;

            double colFromOffset = (x1 % colRatio) / colRatio * rect.Width;
            double rowFromOffset = (y1 % rowRatio) / rowRatio * rect.Height;
            double colToOffset = (x2 % colRatio) / colRatio * rect.Width;
            double rowToOffset = (y2 % rowRatio) / rowRatio * rect.Height;


            _Excel.CreateDArrowLine(fromCol, fromRow, colFromOffset, rowFromOffset, toCol, toRow, colToOffset, rowToOffset, color);
        }

        /// <summary>
        /// 문자 출력
        /// </summary>
        /// <param name="startCol"></param>
        /// <param name="startRow"></param>
        /// <param name="colRatio"></param>
        /// <param name="rowRatio"></param>
        /// <param name="rect"></param>
        /// <param name="ship"></param>
        /// <param name="x1"></param>
        /// <param name="x2"></param>
        /// <param name="y1"></param>
        /// <param name="y2"></param>
        /// <param name="text"></param>
        private void CreateExcelText(int startCol, int startRow, double colRatio, double rowRatio,
            Rect rect, ShipBatchUnit ship,
            double x1, double x2, double y1, double y2, string text, bool isExec, string shp_cod)
        {

            int fromCol = startCol + (int)(x1 / colRatio) - 1;
            int fromRow = startRow + (int)(y1 / rowRatio);
            int toCol = startCol + (int)(x2 / colRatio) - 1;
            int toRow = startRow + (int)(y2 / rowRatio) - 1;

            if (fromCol > toCol || fromRow > toRow) return;

            double totWid = (toCol - fromCol + 1) * rect.Width;

            double colFromOffset = (x1 % colRatio) / colRatio * rect.Width;
            double rowFromOffset = (y1 % rowRatio) / rowRatio * rect.Height;
            double colToOffset = (x2 % colRatio) / colRatio * rect.Width;
            double rowToOffset = (y2 % rowRatio) / rowRatio * rect.Height;
            
            _Excel.CreateDrawingText(fromCol, fromRow, colFromOffset, rowFromOffset, toCol, toRow, colToOffset, rowToOffset, text, isExec, shp_cod);
        }

        /// <summary>
        /// 문자 출력 확장
        /// </summary>
        /// <param name="startCol"></param>
        /// <param name="startRow"></param>
        /// <param name="colRatio"></param>
        /// <param name="rowRatio"></param>
        /// <param name="rect"></param>
        /// <param name="ship"></param>
        /// <param name="x1"></param>
        /// <param name="x2"></param>
        /// <param name="y1"></param>
        /// <param name="y2"></param>
        /// <param name="text"></param>
        /// <param name="fontColor"></param>
        /// <param name="backColor"></param>
        /// <param name="fontName"></param>
        /// <param name="fontSize"></param>
        /// <param name="isBold"></param>
        /// <param name="horzAlign"></param>
        /// <param name="vertAlign"></param>
        /// <param name="isAutoFit"></param>
        private void CreateExcelTextEx(int startCol, int startRow, double colRatio, double rowRatio,
            Rect rect, ShipBatchUnit ship,
            double x1, double x2, double y1, double y2, string text, System.Windows.Media.Color fontColor, System.Windows.Media.Color backColor, 
            string fontName = "돋움", int fontSize = 1000, bool isBold = false,
            ExcelHorizentalAlignment horzAlign = ExcelHorizentalAlignment.Center, ExcelVerticalAlignment vertAlign = ExcelVerticalAlignment.Center, bool isAutoFit = false, string name = "")
        {
            if (isAutoFit == true)
            {
                double textWidth = text.Length * 1.554;
                x1 = x1 + (x2 - x1) / 2 - textWidth / 2;
                x2 = x1 + textWidth;
            }

            int fromCol = startCol + (int)(x1 / colRatio) - 1;
            int fromRow = startRow + (int)(y1 / rowRatio);
            int toCol = startCol + (int)(x2 / colRatio) - 1;
            int toRow = startRow + (int)(y2 / rowRatio) - 1;

            if (fromCol > toCol || fromRow > toRow) return;

            double colFromOffset = (x1 % colRatio) / colRatio * rect.Width;
            double rowFromOffset = (y1 % rowRatio) / rowRatio * rect.Height;
            double colToOffset = (x2 % colRatio) / colRatio * rect.Width;
            double rowToOffset = (y2 % rowRatio) / rowRatio * rect.Height;
            
            //_Excel.CreateDrawingTextEx(fromCol, fromRow, colFromOffset, rowFromOffset, toCol, toRow, colToOffset, rowToOffset,
            //    text, fontColor, backColor, fontName, fontSize, isBold, horzAlign, vertAlign, isAutoFit);
            _Excel.CreateDrawingTextEx(fromCol, fromRow, colFromOffset, rowFromOffset, toCol, toRow, colToOffset, rowToOffset,
                text, fontColor, fontName, fontSize, isBold, horzAlign, vertAlign, isAutoFit, name);
        }

        /// <summary>
        /// 형상 출력
        /// </summary>
        /// <param name="startCol"></param>
        /// <param name="startRow"></param>
        /// <param name="colRatio"></param>
        /// <param name="rowRatio"></param>
        /// <param name="rect"></param>
        /// <param name="ship"></param>
        /// <param name="x1"></param>
        /// <param name="x2"></param>
        /// <param name="y1"></param>
        /// <param name="y2"></param>
        /// <param name="type"></param>
        /// <param name="backColor"></param>
        /// <param name="lineColor"></param>
        private void CreateExcelAutoShape(int startCol, int startRow, double colRatio, double rowRatio,
            Rect rect, ShipBatchUnit ship,
            double x1, double x2, double y1, double y2, ExcelAutoShapeType type, Color backColor, Color lineColor)
        {

            int fromCol = startCol + (int)(x1 / colRatio) - 1;
            int fromRow = startRow + (int)(y1 / rowRatio) - 1;
            int toCol = startCol + (int)(x2 / colRatio) - 1;
            int toRow = startRow + (int)(y2 / rowRatio) - 1;

            if (fromCol > toCol || fromRow > toRow) return;

            double colFromOffset = (x1 % colRatio) / colRatio * rect.Width;
            double rowFromOffset = (y1 % rowRatio) / rowRatio * rect.Height;
            double colToOffset = (x2 % colRatio) / colRatio * rect.Width;
            double rowToOffset = (y2 % rowRatio) / rowRatio * rect.Height;


            _Excel.CreateAutoShape(fromCol, fromRow, colFromOffset, rowFromOffset, toCol, toRow, colToOffset, rowToOffset, type, backColor, lineColor);
        }
        
        private string GetMEStatus(ShipBatchUnit ship)
        {
            DataTable table = _Model.GetShipExtraInfo(ship.SHP_COD);

            if (table == null || table.Rows.Count < 1)
                return string.Empty;

            foreach (DataRow row in table.Rows)
            {
                if (row["ETC_COD"].ToString() == ShipExtraInfoCode.ME탑재) return row["ETC_CNT"].ToString();
            }

            return string.Empty;
        }

        private void GetBatchShipSize(ShipBatchUnit ship, out double start, out double end)
        {
            if (ship.BLD_COD == BuildType.Straight)
            {
                start = 0;
                end = ship.LOA;
            }
            else if (ship.BLD_COD == BuildType.SemiTandem || ship.BLD_COD == BuildType.TX)
            {
                double realWidth = ship.TandemWidth;
                foreach (ShipBatchUnit prev in ship.PrevBatchs)
                {
                    realWidth += ship.TandemWidth;
                }
                if (ship.LOA == realWidth)
                {
                    start = 0;
                    end = ship.LOA;
                }

                string div = ship.GetCurrentDivision();
                if (div == BuildLevel.선미)
                {
                    start = 0;
                    end = ship.Width;
                }
                else if (div == BuildLevel.선수)
                {

                    start = ship.LOA - ship.Width + ship.BLD_LEN_REJ;
                    end = ship.LOA;
                }
                else
                {
                    start = ship.BLD_POS;
                    end = start + ship.TandemWidth;
                    foreach (ShipBatchUnit prev in ship.PrevBatchs)
                    {
                        if (prev.BLD_POS < start) start = prev.BLD_POS;
                        if (prev.BLD_POS + prev.TandemWidth > end) end = prev.BLD_POS + prev.TandemWidth;
                    }
                }
            }
            else
            {
                if (ship.Width == ship.LOA)
                {
                    start = 0;
                    end = ship.LOA;
                }
                string div = ship.GetCurrentDivision();
                if (div == BuildLevel.선미)
                {
                    start = 0;
                    end = ship.Width;
                }
                else if (div == BuildLevel.선수)
                {

                    start = ship.LOA - ship.Width;
                    end = ship.LOA;
                }
                else
                {
                    start = ship.BLD_POS;
                    end = start + ship.TandemWidth;
                    foreach (ShipBatchUnit prev in ship.PrevBatchs)
                    {
                        if (prev.BLD_POS < start) start = prev.BLD_POS;
                        if (prev.BLD_POS + prev.TandemWidth > end) end = prev.BLD_POS + prev.TandemWidth;
                    }
                }
            }
        }

        /// <summary>
        /// 수직 치수 보조선
        /// </summary>
        /// <param name="startCol"></param>
        /// <param name="startRow"></param>
        /// <param name="colRatio"></param>
        /// <param name="rowRatio"></param>
        /// <param name="rect"></param>
        /// <param name="x"></param>
        /// <param name="startExcelRow"></param>
        /// <param name="endy"></param>
        /// <param name="color"></param>
        /// <param name="style"></param>
        private void CreteShipVerticalLine(int startCol, int startRow, double colRatio, double rowRatio, Rect rect, double x, int startExcelRow, double endy, Color color, LineDashValues style)
        {
            int fromCol = startCol + (int)(x / colRatio) - 1;
            int fromRow = startRow;// _Model.BatchKind == "2" ? startRow : startExcelRow - 1;
            int toCol = fromCol;
            int toRow = startExcelRow;// _Model.BatchKind == "2" ? startExcelRow : startRow + (int)(endy / rowRatio) - 1;

            //20130524, from-to 역전시 에러 발생 처리
            if (toRow < fromRow) return;

            double colFromOffset = (x % colRatio) / colRatio * rect.Width;
            double rowFromOffset = 0;
            double colToOffset = colFromOffset;
            double rowToOffset = (endy % rowRatio) / rowRatio * rect.Height;

            _Excel.CreateLine(fromCol, fromRow, colFromOffset, rowFromOffset, toCol, toRow, colToOffset, rowToOffset, color, style);
        }

        /// <summary>
        /// 수평 치수 보조선
        /// </summary>
        /// <param name="startCol"></param>
        /// <param name="startRow"></param>
        /// <param name="colRatio"></param>
        /// <param name="rowRatio"></param>
        /// <param name="rect"></param>
        /// <param name="x1"></param>
        /// <param name="x2"></param>
        /// <param name="startExcelRow"></param>
        /// <param name="color"></param>
        private void CreateShipHorizentalLine(int startCol, int startRow, double colRatio, double rowRatio,
            Rect rect, double x1, double x2, int startExcelRow, Color color)
        {
            int fromCol = startCol + (int)(x1 / colRatio) - 1;
            int fromRow = startExcelRow - 1;
            int toCol = startCol + (int)(x2 / colRatio) - 1;
            int toRow = fromRow;

            if (fromCol > toCol || fromRow > toRow) return;

            double colFromOffset = (x1 % colRatio) / colRatio * rect.Width;
            double rowFromOffset = 0;
            double colToOffset = (x2 % colRatio) / colRatio * rect.Width;
            double rowToOffset = 0;

            _Excel.CreateDArrowLine(fromCol, fromRow, colFromOffset, rowFromOffset, toCol, toRow, colToOffset, rowToOffset, color);
        }

        /// <summary>
        /// 치수
        /// </summary>
        /// <param name="startCol"></param>
        /// <param name="startRow"></param>
        /// <param name="colRatio"></param>
        /// <param name="rowRatio"></param>
        /// <param name="rect"></param>
        /// <param name="x1"></param>
        /// <param name="x2"></param>
        /// <param name="excelRow"></param>
        /// <param name="text"></param>
        /// <param name="fontSize"></param>
        /// <param name="isAutoText"></param>
        private void CreateLineText(int startCol, int startRow, double colRatio, double rowRatio,
            Rect rect, double x1, double x2, int excelRow, string text, int fontSize = 1200, bool isAutoText = false, string name = "")
        {
            double textWidth = text.Length * 1.554;
            x1 = x1 + (x2 - x1) / 2 - textWidth / 2;
            x2 = x1 + textWidth;

            int fromCol = startCol + (int)(x1 / colRatio) - 1;
            int fromRow = excelRow;
            int toCol = startCol + (int)(x2 / colRatio) - 1;
            int toRow = excelRow + 1;

            if (toCol - fromCol < 2) toCol += (toCol - fromCol) + 1;

            if (fromCol > toCol || fromRow > toRow) return;

            double colFromOffset = (x1 % colRatio) / colRatio * rect.Width;
            double rowFromOffset = 0;
            double colToOffset = (x2 % colRatio) / colRatio * rect.Width;
            double rowToOffset = 0;

            //_Excel.CreateDrawingTextEx(fromCol, fromRow, colFromOffset, rowFromOffset, toCol, toRow, colToOffset, rowToOffset,
            //    text, Colors.Black, Colors.White, "돋움", fontSize, true, ExcelHorizentalAlignment.Center, ExcelVerticalAlignment.Top, true);
            _Excel.CreateDrawingTextEx(fromCol, fromRow, colFromOffset, rowFromOffset, toCol, toRow, colToOffset, rowToOffset,
                text, Colors.Black, "돋움", fontSize, true, ExcelHorizentalAlignment.Center, ExcelVerticalAlignment.Top, isAutoText, name);
        }

        private void SortItems(List<ShipBatchUnit> itemDic)
        {
            itemDic = itemDic.OrderBy(p => p.X_POS).ToList();
        }

        private List<double> GetResourceSperateValue(List<ShipBatchUnit> itemList, double max, out Dictionary<double, ShipBatchUnit> retDic)
        {
            retDic = new Dictionary<double, ShipBatchUnit>();
            
            DockUnit dock = _Model.DockManager.GetDockInfo(string.Format("{0}{1:D02}", _CurrentYear, _CurrentBatch));
            
            List<double> retList = new List<double>();

            // 1도크인 경우
            if (dock.Code == "1")
            {
                foreach (ShipBatchUnit item in itemList)
                {
                    if (item.LinePosition == "BOTTOM")
                    {
                        // 호선이 도크 안쪽에 있을 경우
                        if (item.X_POS >= Dock1Dim2 && item.X_POS + item.Width <= Dock1Dim2 + Dock1Dim3)
                        {
                            retList.Add(Dock1Dim2);
                            retList.Add(item.X_POS);
                            retList.Add(item.X_POS + item.Width);
                            retList.Add(Dock1Dim2 + Dock1Dim3);

                            retDic[Dock1Dim2] = item;
                            retDic[item.X_POS] = item;
                            retDic[item.X_POS + item.Width] = item;
                            retDic[Dock1Dim2 + Dock1Dim3] = item;
                        }
                        // 호선이 도크 왼쪽 밖으로 삐져나온 경우(오른쪽은 도크 안쪽에 위치)
                        else if (item.X_POS < Dock1Dim2 && item.X_POS + item.Width < Dock1Dim2 + Dock1Dim3)
                        {
                            retList.Add(item.X_POS);
                            retList.Add(Dock1Dim2);
                            retList.Add(item.X_POS + item.Width);
                            retList.Add(Dock1Dim3);

                            retDic[item.X_POS] = item;
                            retDic[Dock1Dim2] = item;
                            retDic[item.X_POS + item.Width] = item;
                            retDic[Dock1Dim3] = item;
                        }
                        // 호선이 도크 오른쪽 밖으로 삐져나온 경우
                        else if (item.X_POS > Dock1Dim2 && item.X_POS + item.Width > Dock1Dim2 + Dock1Dim3)
                        {
                            retList.Add(Dock1Dim2);
                            retList.Add(item.X_POS);
                            retList.Add(Dock1Dim2 + Dock1Dim3);
                            retList.Add(item.X_POS + item.Width);

                            retDic[Dock1Dim2] = item;
                            retDic[item.X_POS] = item;
                            retDic[Dock1Dim2 + Dock1Dim3] = item;
                            retDic[item.X_POS + item.Width] = item;
                        }
                        // 호선이 도크 왼쪽/오른쪽 모두 밖으로 삐져나온 경우
                        else if (item.X_POS < Dock1Dim2 && item.X_POS + item.Width > Dock1Dim2 + Dock1Dim3)
                        {
                            retList.Add(item.X_POS);
                            retList.Add(Dock1Dim2);
                            retList.Add(Dock1Dim3);
                            retList.Add(item.X_POS + item.Width);

                            retDic[item.X_POS] = item;
                            retDic[Dock1Dim2] = item;
                            retDic[Dock1Dim3] = item;
                            retDic[item.X_POS + item.Width] = item;
                        }
                    }
                    else
                    {
                        retList.Add(item.X_POS);
                        retList.Add(item.X_POS + item.Width);
                        retDic[item.X_POS] = item;
                        retDic[item.X_POS + item.Width] = item;
                    }
                }
            }
            else
            {
                foreach (ShipBatchUnit item in itemList)
                {
                    retList.Add(item.X_POS);
                    retList.Add(item.X_POS + item.Width);
                    retDic[item.X_POS] = item;
                    retDic[item.X_POS + item.Width] = item;
                }
            }

            if (retList.Count < 1)
                return retList;

            retList = retList.OrderBy(p => p).ToList();

            if (retList.Last() != max && retList.Last() < max)
            {
                retList.Add(max);
                retDic[max] = null;
            }

            return retList;
        }

        //#endregion

        //#region 이격거리
        
        private void ShowItemToItemInterval(int startCol, int startRow, double colRatio, double rowRatio, Rect rc, List<Rect> regions1, List<Rect> regions2, DockUnit res)
        {
            //TextBlock block;
            double os, of, value;
            foreach (Rect rect1 in regions1)
            {
                foreach (Rect rect2 in regions2)
                {
                    if (rect1.Left > rect2.Right || rect1.Right < rect2.Left)
                        continue;

                    if (rect1.Left < rect2.Left)
                        os = rect2.Left;
                    else
                        os = rect1.Left;

                    if (rect1.Right < rect2.Right)
                        of = rect1.Right;
                    else
                        of = rect2.Right;
                    
                    value = rect2.Top - rect1.Bottom + 0.04;
                    value = ((int)(value * 10)) / 10.0;
                    
                    CreateExcelTextEx(startCol, startRow, colRatio, rowRatio,
                                      rc, null, os + 5, of, rect1.Bottom, rect1.Bottom + 5, value.ToString(),
                                      Colors.Black, Colors.White, "돋움", 1800, true, ExcelHorizentalAlignment.Center, ExcelVerticalAlignment.Center, true, "InnerDim");

                    //if (value > 7)
                    {
                        CreateExcelArrowLine(startCol, startRow, colRatio, rowRatio, rc, os + ((of - os) / 2) - 5, os + ((of - os) / 2) - 5, rect1.Bottom, rect2.Top, Colors.Black);
                    }
                    //else
                    //{
                    //    CreateExcelLine(startCol, startRow, colRatio, rowRatio,
                    //    rc, null, os + ((of - os) / 2) - 5, os + ((of - os) / 2) - 5, rect1.Bottom, rect2.Top, Colors.Black, LineDashValues.Solid);
                    //}
                }
            }
        }

        private void GetItemDivision(out List<Rect> topList, out List<Rect> bottomList, DockUnit res, List<ShipBatchUnit> shipList)
        {
            topList = new List<Rect>();
            bottomList = new List<Rect>();

            Rect rect;
            double x, y, topInterval, bottomInterval, extraHeight = 0;

            //if (res.Code == "1") extraHeight = 47;

            foreach (ShipBatchUnit ele in shipList)
            {
                if (ele.IsAssignBatch == false)
                    continue;

                topInterval = ele.Y_POS;
                bottomInterval = (res.Height - extraHeight) - (ele.Y_POS + ele.Height);

                x = ((int)(ele.X_POS * 10)) / 10.0;
                y = ((int)(ele.Y_POS * 10)) / 10.0;

                if (topInterval <= bottomInterval)
                {
                    rect = new Rect(x, y, ele.Width, ele.Height);
                    topList.Add(rect);
                }
                else
                {
                    rect = new Rect(x, y, ele.Width, ele.Height);
                    bottomList.Add(rect);
                }
            }

            topList = topList.OrderBy(p => p.X).ToList();
            bottomList = bottomList.OrderBy(p => p.X).ToList();
        }
        //#endregion

        //#region dock specific data

        private int GetDockShipRow(string dockCode)
        {
            if (_ExecPosDic.ContainsKey(dockCode) == false) return 58;
            return _ExecPosDic[dockCode].ShipInfoRow;
        }

        private int GetDockShipColumn(string dockCode, int order)
        {
            if (_ExecPosDic.ContainsKey(dockCode) == false) return 10;
            return _ExecPosDic[dockCode].ShipInfoPositions[order];
        }

        private int GetDockPegRow(string dockCode)
        {
            if (_ExecPosDic.ContainsKey(dockCode) == false) return 58;
            return _ExecPosDic[dockCode].PegInfoRow;
        }

        private int GetDockPegColumn(string dockCode, int order)
        {
            if (_ExecPosDic.ContainsKey(dockCode) == false)
                return 10;

            return _ExecPosDic[dockCode].PegInfoPositions[order];
        }

        /// <summary>
        /// 실행용 엑셀파일 위치정보(엑셀파일 참조)
        /// </summary>
        public class ExecDockPosInfo
        {
            public ExecDockPosInfo(string dockCode)
            {
                _DockCode = dockCode;
            }

            #region properties

            private string _DockCode;
            public string DockCode
            {
                get { return _DockCode; }
                set { _DockCode = value; }
            }

            public int ShipInfoRow { get; set; } //호선정보 시작 row
            public int PegInfoRow { get; set; }  //Peg 정보 시작 row 

            private int[] _ShipInfoPositions; //호선 정보 시작 칼럼(정보별 칼럼 시작정보)
            public int[] ShipInfoPositions
            {
                get { return _ShipInfoPositions; }
                set { _ShipInfoPositions = value; }
            }

            private int[] _PegInfoPositions; //Peg 정보 시작 칼럼(정보별 칼럼 시작정보)
            public int[] PegInfoPositions
            {
                get { return _PegInfoPositions; }
                set { _PegInfoPositions = value; }
            }

            private int _DockStartRow; //도크 시작 row
            public int DockStartRow
            {
                get { return _DockStartRow; }
                set { _DockStartRow = value; }
            }

            public int DockEndRow;

            private int _DockStartColumn; //도크 시작 column
            public int DockStartColumn
            {
                get { return _DockStartColumn; }
                set { _DockStartColumn = value; }
            }

            private int _NextStartColumn;  //차기 배치 도크 시작 column
            public int NextStartColumn
            {
                get { return _NextStartColumn; }
                set { _NextStartColumn = value; }
            }

            private int _NextStartRow; //차기 배치 도크 시작 row
            public int NextStartRow
            {
                get { return _NextStartRow; }
                set { _NextStartRow = value; }
            }

            private int _ShipSizeLineColumn;  //호선 수치선 top 시작 칼럼
            public int ShipSizeLineColumn
            {
                get { return _ShipSizeLineColumn; }
                set { _ShipSizeLineColumn = value; }
            }

            private int _ShipSizeLineRow; //호선 수치선 top 시작 로우
            public int ShipSizeLineRow
            {
                get { return _ShipSizeLineRow; }
                set { _ShipSizeLineRow = value; }
            }

            public int ShipSizeBottomLineColumn; //호선 수치선 bottom 시작 칼럼
            public int ShipSizeBottomLineRow; //호선 수치선 bottom 시작 로우

            private int _ShipHoldLineColumn;   //hold 정보 표시 시작 컬럼(top)
            public int ShipHoldLineColumn
            {
                get { return _ShipHoldLineColumn; }
                set { _ShipHoldLineColumn = value; }
            }

            private int _ShipHoldLineRow; //hold 정보 표시 시작 로우(top)
            public int ShipHoldLineRow
            {
                get { return _ShipHoldLineRow; }
                set { _ShipHoldLineRow = value; }
            }

            public int ShipHoldBottomLineColunm;//hold 정보 표시 시작 컬럼(bottom)
            public int ShipHoldBottomLineRow;//hold 정보 표시 시작 로우(bottom)

            private double _RowRatio;  //도크 수직 비율(10미터당 엑셀 로우수가 4개면 2.5)
            public double RowRatio
            {
                get { return _RowRatio; }
                set { _RowRatio = value; }
            }

            private double _ColumnRatio; //도크 수평 비율
            public double ColumnRatio
            {
                get { return _ColumnRatio; }
                set { _ColumnRatio = value; }
            }

            private double _NextRowRatio; //차기배치 도크 수직 비율(10미터당 엑셀 로우수가 4개면 2.5)
            public double NextRowRatio
            {
                get { return _NextRowRatio; }
                set { _NextRowRatio = value; }
            }

            private double _NextColumnRatio; //차기배치 도크 수평 비율
            public double NextColumnRatio
            {
                get { return _NextColumnRatio; }
                set { _NextColumnRatio = value; }
            }
            #endregion
        }

        private void SetDefaultExecPosData()
        {
            //1 dock
            ExecDockPosInfo info = new ExecDockPosInfo("1");
            info.ShipInfoRow = 58;
            info.ShipInfoPositions = new int[5] { 10, 20, 30, 50, 70 };

            info.PegInfoRow = 58;
            info.PegInfoPositions = new int[5] { 97, 107, 117, 135, 153 };

            info.DockStartColumn = 10;
            info.DockStartRow = 20;

            info.NextStartColumn = 101;
            info.NextStartRow = 68;

            info.ShipSizeLineColumn = 10;
            info.ShipSizeLineRow = 15;

            info.ShipHoldLineColumn = 10;
            info.ShipHoldLineRow = 14;

            info.ColumnRatio = 2.5;
            info.RowRatio = 5;// 5; //10

            info.NextRowRatio = 80.0 / 8.0;
            info.NextColumnRatio = 387.8 / 66.0;

            info.DockEndRow = 35;// 45;
            info.ShipSizeBottomLineColumn = 10;
            info.ShipSizeBottomLineRow = 51;

            info.ShipHoldBottomLineColunm = 10;
            info.ShipHoldBottomLineRow = 52;

            _ExecPosDic.Add(info.DockCode, info);

            //2 dock
            info = new ExecDockPosInfo("2");
            info.ShipInfoRow = 48; //-> hull dimension 시작 입력 
            info.ShipInfoPositions = new int[5] { 12, 25, 38, 62, 86 };

            info.PegInfoRow = 48; // -> peg no.별 위치 입력
            info.PegInfoPositions = new int[5] { 119, 131, 143, 167, 191 };

            info.DockStartColumn = 12;  //도크 시작 위치
            info.DockStartRow = 20;

            info.NextStartColumn = 119;  // 차기 배치  시작 위치
            info.NextStartRow = 58;

            info.ShipSizeLineColumn = 12;  // 배 크기 수치선
            info.ShipSizeLineRow = 15;

            info.ShipHoldLineColumn = 12;  // 배 크기 수치선 바로 위
            info.ShipHoldLineRow = 14;

            info.ColumnRatio = 2.4885;
            info.RowRatio = 5;

            info.NextRowRatio = 80.0 / 8.0;
            info.NextColumnRatio = 497.8 / 93.0;

            info.DockEndRow = 35;
            info.ShipSizeBottomLineColumn = 12;
            info.ShipSizeBottomLineRow = 41;

            info.ShipHoldBottomLineColunm = 12;
            info.ShipHoldBottomLineRow = 42;

            _ExecPosDic.Add(info.DockCode, info);

            //3 dock
            info = new ExecDockPosInfo("3");
            info.ShipInfoRow = 69; //-> hull dimension 시작 입력 
            info.ShipInfoPositions = new int[5] { 10, 18, 26, 43, 59 };

            info.PegInfoRow = 69; // -> peg no.별 위치 입력
            info.PegInfoPositions = new int[5] { 80, 87, 94, 111, 128 };

            info.DockStartColumn = 10;  //도크 시작 위치
            info.DockStartRow = 20;

            info.NextStartColumn = 82;  // 차기 배치  시작 위치
            info.NextStartRow = 79;

            info.ShipSizeLineColumn = 10;  // 배 크기 수치선
            info.ShipSizeLineRow = 15;

            info.ShipHoldLineColumn = 10;  // 배 크기 수치선 바로 위
            info.ShipHoldLineRow = 14;

            info.ColumnRatio = 4.98;
            info.RowRatio = 2.5;

            info.NextRowRatio = 92.0 / 10.0;
            info.NextColumnRatio = 672 / 63.0;

            info.DockEndRow = 56;
            info.ShipSizeBottomLineColumn = 10;
            info.ShipSizeBottomLineRow = 62;

            info.ShipHoldBottomLineColunm = 10;
            info.ShipHoldBottomLineRow = 63;

            _ExecPosDic.Add(info.DockCode, info);

            //4 dock
            info = new ExecDockPosInfo("4");
            info.ShipInfoRow = 44; //-> hull dimension 시작 입력 
            info.ShipInfoPositions = new int[5] { 8, 17, 26, 46, 66 };

            info.PegInfoRow = 44; // -> peg no.별 위치 입력
            info.PegInfoPositions = new int[5] { 92, 101, 110, 130, 140 };

            info.DockStartColumn = 13;  //도크 시작 위치
            info.DockStartRow = 20;

            info.NextStartColumn = 92;  // 차기 배치  시작 위치
            info.NextStartRow = 52;

            info.ShipSizeLineColumn = 13;  // 배 크기 수치선
            info.ShipSizeLineRow = 15;

            info.ShipHoldLineColumn = 13;  // 배 크기 수치선 바로 위
            info.ShipHoldLineRow = 14;

            info.ColumnRatio = 2.5;
            info.RowRatio = 5;

            info.NextRowRatio = 65.0 / 9.0;
            info.NextColumnRatio = 382.5 / 76.0;

            info.DockEndRow = 32;
            info.ShipSizeBottomLineColumn = 13;
            info.ShipSizeBottomLineRow = 37;

            info.ShipHoldBottomLineColunm = 13;
            info.ShipHoldBottomLineRow = 38;

            _ExecPosDic.Add(info.DockCode, info);

            //5 dock
            info = new ExecDockPosInfo("5");
            info.ShipInfoRow = 44; //-> hull dimension 시작 입력 
            info.ShipInfoPositions = new int[5] { 8, 17, 26, 46, 66 };

            info.PegInfoRow = 44; // -> peg no.별 위치 입력
            info.PegInfoPositions = new int[5] { 92, 101, 110, 130, 150 };

            info.DockStartColumn = 13;  //도크 시작 위치
            info.DockStartRow = 20;

            info.NextStartColumn = 92;  // 차기 배치  시작 위치
            info.NextStartRow = 52;

            info.ShipSizeLineColumn = 13;  // 배 크기 수치선
            info.ShipSizeLineRow = 15;

            info.ShipHoldLineColumn = 13;  // 배 크기 수치선 바로 위
            info.ShipHoldLineRow = 14;

            info.ColumnRatio = 2.5;
            info.RowRatio = 5;

            info.NextRowRatio = 65.0 / 9.0;
            info.NextColumnRatio = 382.5 / 76.0;

            info.DockEndRow = 32;
            info.ShipSizeBottomLineColumn = 13;
            info.ShipSizeBottomLineRow = 37;

            info.ShipHoldBottomLineColunm = 13;
            info.ShipHoldBottomLineRow = 38;

            _ExecPosDic.Add(info.DockCode, info);

            //6 dock
            info = new ExecDockPosInfo("6");
            info.ShipInfoRow = 40; //-> hull dimension 시작 입력 
            info.ShipInfoPositions = new int[5] { 5, 12, 19, 35, 53 };

            info.PegInfoRow = 40; // -> peg no.별 위치 입력
            info.PegInfoPositions = new int[5] { 69, 76, 83, 104, 125 };

            info.DockStartColumn = 23;  //도크 시작 위치
            info.DockStartRow = 20;

            info.NextStartColumn = 69;  // 차기 배치  시작 위치
            info.NextStartRow = 48;

            info.ShipSizeLineColumn = 23;  // 배 크기 수치선
            info.ShipSizeLineRow = 15;

            info.ShipHoldLineColumn = 23;  // 배 크기 수치선 바로 위
            info.ShipHoldLineRow = 14;

            info.ColumnRatio = 2.5;
            info.RowRatio = 43.0 / 9.0;

            info.NextRowRatio = 43.0 / 8.0;
            info.NextColumnRatio = 260.0 / 74.0;

            info.DockEndRow = 28;
            info.ShipSizeBottomLineColumn = 12;
            info.ShipSizeBottomLineRow = 34;

            info.ShipHoldBottomLineColunm = 12;
            info.ShipHoldBottomLineRow = 35;

            _ExecPosDic.Add(info.DockCode, info);

            //7 dock
            info = new ExecDockPosInfo("7");
            info.ShipInfoRow = 38; //-> hull dimension 시작 입력 
            info.ShipInfoPositions = new int[5] { 5, 12, 19, 35, 53 };

            info.PegInfoRow = 38; // -> peg no.별 위치 입력
            info.PegInfoPositions = new int[5] { 69, 76, 83, 104, 125 };

            info.DockStartColumn = 23;  //도크 시작 위치
            info.DockStartRow = 18;

            info.NextStartColumn = 69;  // 차기 배치  시작 위치
            info.NextStartRow = 46;

            info.ShipSizeLineColumn = 23;  // 배 크기 수치선
            info.ShipSizeLineRow = 14;

            info.ShipHoldLineColumn = 23;  // 배 크기 수치선 바로 위
            info.ShipHoldLineRow = 13;

            info.ColumnRatio = 2.5;
            info.RowRatio = 25.0 / 9.0;

            info.NextRowRatio = 25.0 / 8.0;
            info.NextColumnRatio = 170.0 / 74.0;

            info.DockEndRow = 26;
            info.ShipSizeBottomLineColumn = 23;
            info.ShipSizeBottomLineRow = 32;

            info.ShipHoldBottomLineColunm = 23;
            info.ShipHoldBottomLineRow = 33;

            _ExecPosDic.Add(info.DockCode, info);

            //8 dock
            info = new ExecDockPosInfo("8");
            info.ShipInfoRow = 45; //-> hull dimension 시작 입력 
            info.ShipInfoPositions = new int[5] { 8, 19, 30, 54, 78 };

            info.PegInfoRow = 45; // -> peg no.별 위치 입력
            info.PegInfoPositions = new int[5] { 107, 118, 129, 153, 177 };

            info.DockStartColumn = 13;  //도크 시작 위치
            info.DockStartRow = 20;

            info.NextStartColumn = 107;  // 차기 배치  시작 위치
            info.NextStartRow = 53;

            info.ShipSizeLineColumn = 13;  // 배 크기 수치선
            info.ShipSizeLineRow = 15;

            info.ShipHoldLineColumn = 13;  // 배 크기 수치선 바로 위
            info.ShipHoldLineRow = 14;

            info.ColumnRatio = 2.5;
            info.RowRatio = 5.0;

            info.NextRowRatio = 70.0 / 8.0;
            info.NextColumnRatio = 460.0 / 91.0;

            info.DockEndRow = 33;
            info.ShipSizeBottomLineColumn = 13;
            info.ShipSizeBottomLineRow = 39;

            info.ShipHoldBottomLineColunm = 13;
            info.ShipHoldBottomLineRow = 40;

            _ExecPosDic.Add(info.DockCode, info);

            //9 dock
            info = new ExecDockPosInfo("9");
            info.ShipInfoRow = 44; //-> hull dimension 시작 입력 
            info.ShipInfoPositions = new int[5] { 8, 19, 30, 54, 78 };

            info.PegInfoRow = 44; // -> peg no.별 위치 입력
            info.PegInfoPositions = new int[5] { 107, 118, 129, 153, 177 };

            info.DockStartColumn = 13;  //도크 시작 위치
            info.DockStartRow = 19;

            info.NextStartColumn = 107;  // 차기 배치  시작 위치
            info.NextStartRow = 52;

            info.ShipSizeLineColumn = 13;  // 배 크기 수치선
            info.ShipSizeLineRow = 14;

            info.ShipHoldLineColumn = 13;  // 배 크기 수치선 바로 위
            info.ShipHoldLineRow = 13;

            info.ColumnRatio = 2.5;
            info.RowRatio = 5.0;

            info.NextRowRatio = 70.0 / 8.0;
            info.NextColumnRatio = 460.0 / 91.0;

            info.DockEndRow = 32;
            info.ShipSizeBottomLineColumn = 13;
            info.ShipSizeBottomLineRow = 38;

            info.ShipHoldBottomLineColunm = 13;
            info.ShipHoldBottomLineRow = 39;

            _ExecPosDic.Add(info.DockCode, info);

            //H dock
            info = new ExecDockPosInfo("H");
            info.ShipInfoRow = 89; //-> hull dimension 시작 입력 
            info.ShipInfoPositions = new int[5] { 3, 8, 17, 31, 43 };

            info.PegInfoRow = 89; // -> peg no.별 위치 입력
            info.PegInfoPositions = new int[5] { 60, 69, 78, 93, 107 };

            info.DockStartColumn = 7;  //도크 시작 위치
            info.DockStartRow = 20;

            info.NextStartColumn = 60;  // 차기 배치  시작 위치
            info.NextStartRow = 97;

            info.ShipSizeLineColumn = 7;  // 배 크기 수치선
            info.ShipSizeLineRow = 15;

            info.ShipHoldLineColumn = 7;  // 배 크기 수치선 바로 위
            info.ShipHoldLineRow = 14;

            info.ColumnRatio = 4.97;
            info.RowRatio = 2;

            info.NextRowRatio = 80.0 / 6.45;
            info.NextColumnRatio = 497.8 / 61;

            info.DockEndRow = 76;
            info.ShipSizeBottomLineColumn = 7;
            info.ShipSizeBottomLineRow = 82;

            info.ShipHoldBottomLineColunm = 7;
            info.ShipHoldBottomLineRow = 83;

            _ExecPosDic.Add(info.DockCode, info);
        }

        //#endregion

        //#endregion

        #region 상세
        
        /// <summary>
        /// 상세 도크 배치도
        /// </summary>
        /// <param name="dockCode"></param>
        private void SetDetailBatchInfo(string dockCode)
        {
            if (!_DetailPosDic.ContainsKey(dockCode))
                return;

            DetailDockPosInfo info = _DetailPosDic[dockCode];

            string draft;
            DockUnit dock = _Model.DockManager.GetDockInfo(string.Format("{0}{1:D02}", _CurrentYear, _CurrentBatch));//.GetDockInfoByCode(dockCode);

            #region 현재 배치

            int lcOrder = 0, ftOrder = 0, shipRow = info.ShipInfoRow1;
            
            // 메모
            string memo = _Model.GetBatchMemo(_CurrentBatch);
            _Excel.SetText(shipRow, info.ChangeColumn1, memo);

            // 비고
            string bigo = _Model.GetBatchBigo(_CurrentBatch);
            _Excel.SetText(shipRow, info.BigoColumn1, bigo);

            string batchNo = string.Format("{0}{1:D02}", _CurrentYear, _CurrentBatch);

            List<ShipBatchUnit> shipList = _ShipManager.GetShipListByBatchNo(batchNo);

            if (shipList.Count > 0)
            {
                if (shipList[0].SEC_SUP == "1")
                    _Excel.SetText(info.BatchJusuRow1, info.BatchJusuColumn1, "-> 2번 주수");
                else
                    _Excel.SetText(info.BatchJusuRow1, info.BatchJusuColumn1, "");
            }

            if (dock.CONF_YN == "Y")
                _Excel.SetText(info.ConfirmRow1, info.ConfirmColumn1, "확정");
            else
                _Excel.SetText(info.ConfirmRow1, info.ConfirmColumn1, "검토");

            foreach (ShipBatchUnit ship in shipList)
            {
                if (ship.IsAssignBatch == false)
                    continue;

                SetLinePosition(ship, dock);

                if (ship.NextBatch == null)
                {
                    _Excel.SetText(shipRow, info.LCPosition1[lcOrder], ship.FIG_SHP);
                    _Excel.SetText(shipRow + 1, info.LCPosition1[lcOrder], string.Format("{0:F1}%", ((ship.BLD_LEN - ship.BLD_LEN_REJ) / ship.LOA) * 100)); //2014.11.12 도성민 수정
                    draft = _Model.GetDraftValue(ship.SHP_COD);

                    if (!string.IsNullOrWhiteSpace(draft))
                        _Excel.SetText(shipRow + 2, info.LCPosition1[lcOrder], string.Format("{0}m", draft));

                    lcOrder++;
                }
                else
                {
                    _Excel.SetText(shipRow, info.FTPosition1[ftOrder], ship.FIG_SHP);
                    _Excel.SetText(shipRow + 1, info.FTPosition1[ftOrder], string.Format("{0:F1}%", ((ship.BLD_LEN - ship.BLD_LEN_REJ) / ship.LOA) * 100)); //2014.11.12 도성민 수정
                    draft = _Model.GetDraftValue(ship.SHP_COD);

                    if (!string.IsNullOrWhiteSpace(draft))
                        _Excel.SetText(shipRow + 2, info.FTPosition1[ftOrder], string.Format("{0}m", draft));

                    ftOrder++;
                }

                // 호선 및 홀드 생성
                DrawShip(ship, dockCode);
            }

            // 구조물
            DrawAddItem(dockCode, true);
            // 도크 바깥쪽 치수
            DrawShipInterval(dockCode, shipList, false);
            // 도크 안쪽 치수
            DrawConstraintVlaue(dockCode, shipList, false);

            #endregion 현재 배치


            #region 차기 배치

            lcOrder = 0;
            ftOrder = 0;
            shipRow = info.ShipInfoRow2;

            batchNo = string.Format("{0}{1:D02}", _CurrentYear, _CurrentBatch);
            string next = _Model.GetNextBatchNo(batchNo);
            string year = next.Substring(0, 4);
            int batch = int.Parse(next.Substring(4, 2));

            // 구간 배치에서 선택한 배치만 출력하게
            if (!string.IsNullOrEmpty(BatchsEnd) && int.Parse(next) > int.Parse(BatchsEnd))
                return;

            dock = _Model.DockManager.GetDockInfo(string.Format("{0}{1:D02}", year, batch));

            memo = _Model.GetBatchMemo(batch);
            _Excel.SetText(shipRow, info.ChangeColumn2, memo);

            bigo = _Model.GetBatchBigo(batch);
            _Excel.SetText(shipRow, info.BigoColumn2, bigo);

            shipList = _ShipManager.GetShipListByBatchNo(next);

            if (shipList.Count > 0)
            {
                if (shipList[0].SEC_SUP == "1") _Excel.SetText(info.BatchJusuRow2, info.BatchJusuColumn2, "-> 2번 주수");
                else _Excel.SetText(info.BatchJusuRow2, info.BatchJusuColumn2, "");
            }

            if (dock.CONF_YN == "Y")
                _Excel.SetText(info.ConfirmRow2, info.ConfirmColumn2, "확정");
            else
                _Excel.SetText(info.ConfirmRow2, info.ConfirmColumn2, "검토");

            foreach (ShipBatchUnit ship in shipList)
            {
                if (ship.IsAssignBatch == false)
                    continue;

                SetLinePosition(ship, dock);

                if (ship.NextBatch == null)
                {
                    _Excel.SetText(shipRow, info.LCPosition2[lcOrder], ship.FIG_SHP);
                    _Excel.SetText(shipRow + 1, info.LCPosition2[lcOrder], string.Format("{0:F1}%", (ship.BLD_LEN / ship.LOA) * 100));
                    draft = _Model.GetDraftValue(ship.SHP_COD);

                    if (!string.IsNullOrWhiteSpace(draft))
                        _Excel.SetText(shipRow + 2, info.LCPosition2[lcOrder], string.Format("{0}m", draft));

                    lcOrder++;
                }
                else
                {
                    _Excel.SetText(shipRow, info.FTPosition2[ftOrder], ship.FIG_SHP);
                    _Excel.SetText(shipRow + 1, info.FTPosition2[ftOrder], string.Format("{0:F1}%", (ship.BLD_LEN / ship.LOA) * 100));
                    draft = _Model.GetDraftValue(ship.SHP_COD);

                    if (!string.IsNullOrWhiteSpace(draft))
                        _Excel.SetText(shipRow + 2, info.FTPosition2[ftOrder], string.Format("{0}m", draft));

                    ftOrder++;
                }

                DrawDetailNextShip(ship, dockCode);
            }

            // 구조물
            DrawAddItem(dockCode, false);
            // 도크 바깥쪽 치수
            DrawShipInterval(dockCode, shipList, true);
            // 도크 안쪽 치수
            DrawConstraintVlaue(dockCode, shipList, true);

            #endregion 차기 배치
        }
        #endregion 상세

        void SetLinePosition(ShipBatchUnit ship, DockUnit dock)
        {
            double topInterval, bottomInterval, extraLimit;

            topInterval = ship.Y_POS;
            bottomInterval = dock.Height - (ship.Y_POS + ship.Height);

            ship.BatchInfo["MEA_POS"] = "1";

            if (topInterval <= bottomInterval)
                ship.BatchInfo["MEA_POS"] = "0";

            if (dock.Code == "1")
            {
                extraLimit = 47;
                if (ship.Y_POS + ship.Height / 2 <= extraLimit)
                {
                    ship.BatchInfo["MEA_POS"] = "0";
                }
                else
                {
                    ship.BatchInfo["MEA_POS"] = "1";
                }
            }
        }
        
        /// <summary>
        /// 상세, 실행 도크 배치 구조물 생성
        /// </summary>
        /// <param name="dockCode"></param>
        /// <param name="isCurentBatch"></param>
        void DrawAddItem(string dockCode, bool isCurentBatch)
        {
            if (!_DetailPosDic.ContainsKey(dockCode) && !_ExecPosDic.ContainsKey(dockCode))
                return;

            int DockStartRow = 0, DockStartColumn = 0, ShipLineRow = 0, DockEndRow = 0, ShipSizeBottomLineRow = 0;
            double ColumnRatio = 0, RowRatio = 0;

            ExecDockPosInfo exeInfo = null;
            DetailDockPosInfo dtlInfo = null;

            if (_Model.BatchKind == DockBatchType.실행도크배치)
            {
                exeInfo = _ExecPosDic[dockCode];

                DockStartRow = exeInfo.DockStartRow;
                DockStartColumn = exeInfo.DockStartColumn;
                // 치수선이 그려지는 Row Index
                ShipLineRow = 15;// info.ShipLineRow;
                DockEndRow = exeInfo.DockEndRow;
                ShipSizeBottomLineRow = exeInfo.ShipSizeBottomLineRow;

                ColumnRatio = exeInfo.ColumnRatio;
                RowRatio = exeInfo.RowRatio;
            }
            else if (_Model.BatchKind == DockBatchType.상세도크배치)
            {
                dtlInfo = _DetailPosDic[dockCode];

                if (isCurentBatch)
                {
                    DockStartRow = dtlInfo.DockStartRow1;
                    DockStartColumn = dtlInfo.DockStartColumn1;
                    ShipLineRow = dtlInfo.ShipLineRow1;
                    DockEndRow = dtlInfo.DockEndRow1;
                    ShipSizeBottomLineRow = dtlInfo.ShipSizeBottomLineRow1;
                }
                else
                {
                    DockStartRow = dtlInfo.DockStartRow2;
                    DockStartColumn = dtlInfo.DockStartColumn2;
                    ShipLineRow = dtlInfo.ShipLineRow2;
                    DockEndRow = dtlInfo.DockEndRow2;
                    ShipSizeBottomLineRow = dtlInfo.ShipSizeBottomLineRow2;
                }

                ColumnRatio = dtlInfo.ColumnRatio;
                RowRatio = dtlInfo.RowRatio;
            }

            // 사용자 추가 구조물
            DataTable dtUserAddItem = _Model.UserAddItems;
            // 도크 구조물
            DataTable dtAddItem = isCurentBatch ? _Model.DockBatchAddItems : _Model.DockBatchAddItemsNext;

            DataSet ds = new DataSet();
            if (dtAddItem != null && dtAddItem.Rows.Count > 0)
                ds.Tables.Add(dtAddItem.Copy());
            if (dtUserAddItem != null && dtUserAddItem.Rows.Count > 0)
                ds.Tables.Add(dtUserAddItem.Copy());

            if (ds.Tables.Count == 0)
                return;

            double x_pos, y_pos, width, height, calc_x;
            Color color = Colors.Blue;    // 배경색
            Color color_2 = Colors.Black; // 테두리 색
            string ct_txt = "";
            //string shape = "M 0,0 L 100,0 L 100,50 L 0,50 z";
            string shape_format = "M 0,0 L {0},0 L {1},{2} L 0,{3} z";
            string shape = "";

            //column 하나의 논리적크기는 2.info.RowRatiom, row하나의 논리적 크기는 info.RowRatiom
            Rect rect = _Excel.GetCellPosition(DockEndRow, DockStartColumn);

            string tg_gbn = "";

            foreach (DataTable dt in ds.Tables)
            {
                foreach (DataRow row in dt.Rows)
                {
                    tg_gbn = ConvertHelper.ToString(row["TG_GBN"]);

                    // 게이트인 경우 엑셀에 고정으로 표시되므로 여기서는 추가하지 않는다.
                    if (tg_gbn == "GAT")
                        continue;

                    Type tmpType = row["BG_COLOR_TR"].GetType();
                    System.Drawing.SolidBrush sb;

                    // 배경색
                    if (tmpType == typeof(System.Drawing.Color))
                        sb = new System.Drawing.SolidBrush((System.Drawing.Color)row["BG_COLOR_TR"]);
                    else
                        sb = new System.Drawing.SolidBrush(System.Drawing.Color.White);

                    color.R = sb.Color.R;
                    color.G = sb.Color.G;
                    color.B = sb.Color.B;

                    if (dockCode == "1")
                        calc_x = -30;
                    else
                        calc_x = 0;

                    x_pos = ConvertHelper.ToDouble(row["POS_X"]) / 10 + calc_x;
                    y_pos = ConvertHelper.ToDouble(row["POS_Y"]) / 10;
                    width = ConvertHelper.ToDouble(row["CT_WIDTH"]) / 10;
                    height = ConvertHelper.ToDouble(row["CT_HEIGHT"]) / 10;
                    ct_txt = ConvertHelper.ToString(row["CT_TXT"]);
                    
                    if (y_pos < 0)
                        y_pos = 0;

                    shape = string.Format(shape_format, width, width, height, height);

                    int fromCol, fromRow, toCol, toRow;

                    fromCol = DockStartColumn + (int)((x_pos / ColumnRatio)) - 1;

                    if (dockCode == "1")
                        fromRow = DockStartRow + (int)(y_pos / RowRatio);
                    else
                    {
                        if (tg_gbn == "ETC")
                            fromRow = DockStartRow + (int)(y_pos / RowRatio) - 1;
                        else
                            fromRow = DockEndRow + (int)((y_pos + height) / RowRatio) - 1;
                    }

                    toCol = DockStartColumn + (int)((x_pos + width) / ColumnRatio) - 1;
                    
                    if (dockCode == "1")
                        toRow = DockStartRow + (int)((y_pos + height) / RowRatio) - 1;
                    else
                    {
                        if (tg_gbn == "ETC")
                            toRow = DockStartRow + (int)((y_pos + height) / RowRatio) - 1;
                        else
                            toRow = DockEndRow;
                    }

                    double colFromOffset = (x_pos % ColumnRatio) / ColumnRatio * rect.Width;
                    double rowFromOffset = (y_pos % RowRatio) / RowRatio * rect.Height;
                    double colToOffset = ((x_pos + width) % ColumnRatio) / ColumnRatio * rect.Width;
                    double rowToOffset = ((y_pos + height) % RowRatio) / RowRatio * rect.Height;

                    _Excel.CreateShape(fromCol, fromRow, colFromOffset, rowFromOffset, toCol, toRow, colToOffset, rowToOffset, shape, color, color_2, 0);

                    if (dockCode == "1")
                        CreateExcelTextEx(DockStartColumn, DockStartRow, ColumnRatio, RowRatio, rect, null, x_pos, x_pos + width, y_pos, y_pos + height, ct_txt, Colors.Black, Colors.White, "돋움", 1200, true);
                    else
                    {
                        if (tg_gbn == "ETC")
                            CreateExcelTextEx(DockStartColumn, DockStartRow, ColumnRatio, RowRatio, rect, null, x_pos, x_pos + width, y_pos, y_pos + height, ct_txt, Colors.Black, Colors.White, "돋움", 1400, true);
                        else
                            CreateExcelTextEx(DockStartColumn, DockEndRow, ColumnRatio, RowRatio, rect, null, x_pos, x_pos + width, y_pos, height, ct_txt, Colors.Black, Colors.White, "돋움", 1200, true);
                    }
                }
            }
        }

        //#region draw ship

        /// <summary>
        /// 상세,실행 도크 배치 배 모양 그리기
        /// </summary>
        /// <param name="ship"></param>
        /// <param name="dockCode"></param>
        private void DrawShip(ShipBatchUnit ship, string dockCode)
        {
            if (!_DetailPosDic.ContainsKey(dockCode) && !_ExecPosDic.ContainsKey(dockCode))
                return;

            int DockStartRow = 0, DockStartColumn = 0, lineRow = 0, endRow = 0, bottomLineRow = 0;
            double ColumnRatio = 0, RowRatio = 0;

            ExecDockPosInfo exeInfo = null;
            DetailDockPosInfo dtlInfo = null;

            if (_Model.BatchKind == DockBatchType.실행도크배치)
            {
                exeInfo = _ExecPosDic[dockCode];

                DockStartRow = exeInfo.DockStartRow;
                DockStartColumn = exeInfo.DockStartColumn;
                // 치수선이 그려지는 Row Index
                lineRow = 15;// info.ShipLineRow;
                endRow = exeInfo.DockEndRow;
                bottomLineRow = exeInfo.ShipSizeBottomLineRow;

                ColumnRatio = exeInfo.ColumnRatio;
                RowRatio = exeInfo.RowRatio;
            }
            else if (_Model.BatchKind == DockBatchType.상세도크배치)
            {
                dtlInfo = _DetailPosDic[dockCode];

                DockStartRow = dtlInfo.DockStartRow1;
                DockStartColumn = dtlInfo.DockStartColumn1;
                lineRow = dtlInfo.ShipLineRow1;
                endRow = dtlInfo.DockEndRow1;
                bottomLineRow = dtlInfo.ShipSizeBottomLineRow1;

                ColumnRatio = dtlInfo.ColumnRatio;
                RowRatio = dtlInfo.RowRatio;
            }
            
            //column 하나의 논리적크기는 2.info.RowRatiom, row하나의 논리적 크기는 info.RowRatiom
            Rect rect = _Excel.GetCellPosition(DockStartRow, DockStartColumn);

            int fromCol = DockStartColumn + (int)((ship.X_POS / ColumnRatio)) - 1;
            int fromRow = DockStartRow + (int)((ship.Y_POS / RowRatio));
            int toCol = DockStartColumn + (int)((ship.X_POS + ship.Width) / ColumnRatio) - 1;
            int toRow = DockStartRow + (int)((ship.Y_POS + ship.Height) / RowRatio) - 1;
            double colFromOffset = (ship.X_POS % ColumnRatio) / ColumnRatio * rect.Width;
            double rowFromOffset = (ship.Y_POS % RowRatio) / RowRatio * rect.Height;
            double colToOffset = ((ship.X_POS + ship.Width) % ColumnRatio) / ColumnRatio * rect.Width;
            double rowToOffset = ((ship.Y_POS + ship.Height) % RowRatio) / RowRatio * rect.Height;

            if (!string.IsNullOrWhiteSpace(ship.TandemShape) && ship.TandemWidth == 0)
            {
                //_Excel.CreateShape(fromCol, fromRow, colFromOffset, rowFromOffset, toCol, toRow, colToOffset, rowToOffset,
                //    ship.ShapeData, Colors.LightGray, Colors.Blue, ship.SHP_ANG);

                //2013.07.03 도성민 F/T전 형성구획 색깔 회색->흰색으로 변경, 박수진CJ 요청 
                //CreateShape(fromCol, fromRow, colFromOffset, rowFromOffset, toCol, toRow, colToOffset, rowToOffset,
                //    ship.ShapeData, Colors.LightGray, Colors.Blue, ship.SHP_ANG);
                CreateShape(fromCol, fromRow, colFromOffset, rowFromOffset, toCol, toRow, colToOffset, rowToOffset, ship.ShapeData, Colors.White, Colors.Blue, ship.SHP_ANG);
            }
            else
            {
                //_Excel.CreateShape(fromCol, fromRow, colFromOffset, rowFromOffset, toCol, toRow, colToOffset, rowToOffset,
                //    ship.ShapeData, Colors.LightGreen, Colors.Blue, ship.SHP_ANG);
                CreateShape(fromCol, fromRow, colFromOffset, rowFromOffset, toCol, toRow, colToOffset, rowToOffset, ship.ShapeData, Colors.LightGreen, Colors.Blue, ship.SHP_ANG);
            }
            
            // A11이 아닌 부분 형상인 경우
            if (ship.PRO_LEN == 0 && !string.IsNullOrWhiteSpace(ship.TandemShape) && ship.TandemWidth > 0)
            {
                // 20130524, TandemWidth가 LOA 보다 클경우 문제 발생
                double tdWidth = ship.PrevBatchs != null && ship.PrevBatchs.Count > 0 ? ship.PrevBatchs.Max(m => m.TandemWidth) : ship.TandemWidth;

                if (tdWidth > ship.LOA)
                    tdWidth = ship.LOA;

                if (ship.BLD_COD == BuildType.TR || ship.BLD_COD == BuildType.TX)
                {
                    ShipBatchUnit prev = ship.PrevBatchs.FirstOrDefault();
                    if (prev != null)
                    {
                        double prevTdWidth = prev.TandemWidth;
                        if (prevTdWidth > prev.LOA) prevTdWidth = prev.LOA;

                        if (ship.SHP_ANG == 0)
                        {
                            fromCol = DockStartColumn + (int)(((ship.X_POS + prev.BLD_POS) / ColumnRatio)) - 1;
                            fromRow = DockStartRow + (int)((ship.Y_POS / RowRatio));
                            toCol = DockStartColumn + (int)(((ship.X_POS + prev.BLD_POS) + prevTdWidth) / ColumnRatio) - 1;
                            toRow = DockStartRow + (int)((ship.Y_POS + ship.Height) / RowRatio) - 1;
                            colFromOffset = ((ship.X_POS + prev.BLD_POS) % ColumnRatio) / ColumnRatio * rect.Width;
                            rowFromOffset = (ship.Y_POS % RowRatio) / RowRatio * rect.Height;
                            colToOffset = (((ship.X_POS + prev.BLD_POS) + prevTdWidth) % ColumnRatio) / ColumnRatio * rect.Width;
                            rowToOffset = ((ship.Y_POS + ship.Height) % RowRatio) / RowRatio * rect.Height;
                        }
                        else
                        {
                            fromCol = DockStartColumn + (int)(((ship.X_POS + (ship.LOA - (prev.BLD_POS + prevTdWidth))) / ColumnRatio)) - 1;
                            fromRow = DockStartRow + (int)((ship.Y_POS / RowRatio));
                            toCol = DockStartColumn + (int)((ship.X_POS + (ship.LOA - prev.BLD_POS)) / ColumnRatio) - 1;
                            toRow = DockStartRow + (int)((ship.Y_POS + ship.Height) / RowRatio) - 1;
                            colFromOffset = ((ship.X_POS + (ship.LOA - (prev.BLD_POS + prevTdWidth))) % ColumnRatio) / ColumnRatio * rect.Width;
                            rowFromOffset = (ship.Y_POS % RowRatio) / RowRatio * rect.Height;
                            colToOffset = ((ship.X_POS + (ship.LOA - prev.BLD_POS)) % ColumnRatio) / ColumnRatio * rect.Width;
                            rowToOffset = ((ship.Y_POS + ship.Height) % RowRatio) / RowRatio * rect.Height;
                        }

                        string shape = HHIDockBizLogic.GetTendemGeometry(ship, BuildLevel.선미, ship.SHA_PNT, ship.SHA_TYP);

                        //_Excel.CreateShape(fromCol, fromRow, colFromOffset, rowFromOffset, toCol, toRow, colToOffset, rowToOffset,
                        //    shape, Colors.LightGray, Colors.Blue, ship.SHP_ANG);

                        //2013.07.03 도성민 F/T전 형성구획 색깔 회색->흰색으로 변경, 박수진CJ 요청 
                        //CreateShape(fromCol, fromRow, colFromOffset, rowFromOffset, toCol, toRow, colToOffset, rowToOffset,
                        //    shape, Colors.LightGray, Colors.Blue, ship.SHP_ANG);
                        CreateShape(fromCol, fromRow, colFromOffset, rowFromOffset, toCol, toRow, colToOffset, rowToOffset, shape, Colors.White, Colors.Blue, ship.SHP_ANG);
                    }
                }
                else
                {
                    if (ship.SHP_ANG == 0)
                    {
                        fromCol = DockStartColumn + (int)((ship.X_POS / ColumnRatio)) - 1;
                        fromRow = DockStartRow + (int)((ship.Y_POS / RowRatio));
                        //toCol = info.DockStartColumn1 + (int)((ship.X_POS + ship.Width - tdWidth) / info.ColumnRatio) - 1;
                        toCol = DockStartColumn + (int)((ship.X_POS + tdWidth) / ColumnRatio) - 1;
                        toRow = DockStartRow + (int)((ship.Y_POS + ship.Height) / RowRatio) - 1;
                        colFromOffset = (ship.X_POS % ColumnRatio) / ColumnRatio * rect.Width;
                        rowFromOffset = (ship.Y_POS % RowRatio) / RowRatio * rect.Height;
                        colToOffset = ((ship.X_POS + ship.LOA - tdWidth) % ColumnRatio) / ColumnRatio * rect.Width;
                        rowToOffset = ((ship.Y_POS + ship.Height) % RowRatio) / RowRatio * rect.Height;
                    }
                    else
                    {
                        //fromCol = info.DockStartColumn1 + (int)(((ship.X_POS + tdWidth) / info.ColumnRatio)) - 1;
                        fromCol = DockStartColumn + (int)(((ship.X_POS + ship.Width - tdWidth) / ColumnRatio)) - 1;
                        fromRow = DockStartRow + (int)((ship.Y_POS / RowRatio));
                        toCol = DockStartColumn + (int)((ship.X_POS + ship.Width) / ColumnRatio) - 1;
                        toRow = DockStartRow + (int)((ship.Y_POS + ship.Height) / RowRatio) - 1;
                        colFromOffset = ((ship.X_POS + tdWidth) % ColumnRatio) / ColumnRatio * rect.Width;
                        rowFromOffset = (ship.Y_POS % RowRatio) / RowRatio * rect.Height;
                        colToOffset = ((ship.X_POS + ship.Width) % ColumnRatio) / ColumnRatio * rect.Width;
                        rowToOffset = ((ship.Y_POS + ship.Height) % RowRatio) / RowRatio * rect.Height;
                    }

                    string shape = HHIDockBizLogic.GetTendemGeometry(ship, BuildLevel.선미, ship.SHA_PNT, ship.SHA_TYP);
                    //_Excel.CreateShape(fromCol, fromRow, colFromOffset, rowFromOffset, toCol, toRow, colToOffset, rowToOffset,
                    //shape, Colors.LightGray, Colors.Blue, ship.SHP_ANG);
                    
                    //2013.07.03 도성민 F/T전 형성구획 색깔 회색->흰색으로 변경, 박수진CJ 요청
                    //CreateShape(fromCol, fromRow, colFromOffset, rowFromOffset, toCol, toRow, colToOffset, rowToOffset,
                    //    shape, Colors.LightGray, Colors.Blue, ship.SHP_ANG);
                    CreateShape(fromCol, fromRow, colFromOffset, rowFromOffset, toCol, toRow, colToOffset, rowToOffset,
                        shape, Colors.White, Colors.Blue, ship.SHP_ANG);
                }
            }

            string medh = string.Empty;
            //2013.07.25 도성민 수정
            //시작
            if (ship.MAIN_EVT == "FT")
            {
                string me = _Model.IsBeforeME(ship.SHP_COD) == true ? string.Empty : "M/E";
                string dh = _Model.IsBeforeDH(ship.SHP_COD) == true ? string.Empty : "D/H";

                if (!string.IsNullOrEmpty(me) && !string.IsNullOrEmpty(dh))
                    medh = string.Format("({0}, {1})", me, dh);
                else if (!string.IsNullOrEmpty(me) || !string.IsNullOrEmpty(dh))
                    medh = string.Format("({0} {1})", me, dh);
            }
            else
            {
                string me = _Model.IsBeforeME(ship.SHP_COD) == true ? "M/E" : string.Empty;
                string dh = _Model.IsBeforeDH(ship.SHP_COD) == true ? "D/H" : string.Empty;

                if (!string.IsNullOrEmpty(me) && !string.IsNullOrEmpty(dh))
                    medh = string.Format("({0}, {1})", me, dh);
                else if (!string.IsNullOrEmpty(me) || !string.IsNullOrEmpty(dh))
                    medh = string.Format("({0} {1})", me, dh);
            }

            string desc;
            string dizeDesc = GetSizeDesc(ship);
            if (ship.BLD_LVL == BuildLevel.중간)
            {
                string dir = "◀";
                if (ship.SHP_ANG == 0) dir = "▶";
                desc = string.Format("{0}\r\n{1}{2}{3}", ship.ShipDescription, dizeDesc, medh, dir);
            }
            else
                desc = string.Format("{0}\r\n{1}{2}", ship.ShipDescription, dizeDesc, medh);

            //string desc = string.Format("{0}\r\n{1}{2}", ship.ShipDescription, ship.ShipSizeDesc, medh);
            CreateExcelTextEx(DockStartColumn, DockStartRow, ColumnRatio, RowRatio,
                rect, ship, ship.X_POS, ship.X_POS + ship.Width,
                ship.Y_POS, ship.Y_POS + ship.Height, desc, Colors.Black, Colors.White,
                "돋움", 2000, true, isAutoFit:true, name:"Title");

            DrawAPHold(ship, dockCode, false);
        }

        private void CreateShape(int fromCol, int fromRow, double colFromOffset, double rowFromOffset
            , int toCol, int toRow, double colToOffset, double rowToOffset, string shape, Color color, Color color_2, int angle)
        {
            if (toCol < fromCol) toCol = fromCol;
            if (toRow < fromRow) toRow = fromRow;

            //shape = ChangeEndRound(shape);

            _Excel.CreateShape(fromCol, fromRow, colFromOffset, rowFromOffset, toCol, toRow, colToOffset, rowToOffset, shape, color, color_2, angle);
        }

        private void DrawDetailNextShip(ShipBatchUnit ship, string dockCode)
        {
            if (!_DetailPosDic.ContainsKey(dockCode)) return;
            DetailDockPosInfo info = _DetailPosDic[dockCode];

            //column 하나의 논리적크기는 2.info.RowRatiom, row하나의 논리적 크기는 info.RowRatiom
            Rect rect = _Excel.GetCellPosition(info.DockStartRow2, info.DockStartColumn2);

            int fromCol = info.DockStartColumn2 + (int)((ship.X_POS / info.ColumnRatio)) - 1;
            int fromRow = info.DockStartRow2 + (int)((ship.Y_POS / info.RowRatio));
            int toCol = info.DockStartColumn2 + (int)((ship.X_POS + ship.Width) / info.ColumnRatio) - 1;
            int toRow = info.DockStartRow2 + (int)((ship.Y_POS + ship.Height) / info.RowRatio) - 1;
            double colFromOffset = (ship.X_POS % info.ColumnRatio) / info.ColumnRatio * rect.Width;
            double rowFromOffset = (ship.Y_POS % info.RowRatio) / info.RowRatio * rect.Height;
            double colToOffset = ((ship.X_POS + ship.Width) % info.ColumnRatio) / info.ColumnRatio * rect.Width;
            double rowToOffset = ((ship.Y_POS + ship.Height) % info.RowRatio) / info.RowRatio * rect.Height;

            if (!string.IsNullOrWhiteSpace(ship.TandemShape) && ship.TandemWidth == 0)
            {
                //_Excel.CreateShape(fromCol, fromRow, colFromOffset, rowFromOffset, toCol, toRow, colToOffset, rowToOffset,
                //    ship.ShapeData, Colors.LightGray, Colors.Blue, ship.SHP_ANG);

                //2013.07.03 도성민 F/T전 형성구획 색깔 회색->흰색으로 변경, 박수진CJ 요청

                //CreateShape(fromCol, fromRow, colFromOffset, rowFromOffset, toCol, toRow, colToOffset, rowToOffset,
                //    ship.ShapeData, Colors.LightGray, Colors.Blue, ship.SHP_ANG);
                CreateShape(fromCol, fromRow, colFromOffset, rowFromOffset, toCol, toRow, colToOffset, rowToOffset,
                    ship.ShapeData, Colors.White, Colors.Blue, ship.SHP_ANG);
            }
            else
            {
                //_Excel.CreateShape(fromCol, fromRow, colFromOffset, rowFromOffset, toCol, toRow, colToOffset, rowToOffset,
                //    ship.ShapeData, Colors.LightGreen, Colors.Blue, ship.SHP_ANG);
                CreateShape(fromCol, fromRow, colFromOffset, rowFromOffset, toCol, toRow, colToOffset, rowToOffset,
                    ship.ShapeData, Colors.LightGreen, Colors.Blue, ship.SHP_ANG);
            }
            //_Excel.CreateShape(fromCol, fromRow, colFromOffset, rowFromOffset, toCol, toRow, colToOffset, rowToOffset,
            //        ship.ShapeData, Colors.LightGreen, Colors.Blue, ship.SHP_ANG);
            //CreateShape(fromCol, fromRow, colFromOffset, rowFromOffset, toCol, toRow, colToOffset, rowToOffset,
            //        ship.ShapeData, Colors.LightGreen, Colors.Blue, ship.SHP_ANG);

            //if (!string.IsNullOrWhiteSpace(ship.TandemShape) && ship.TandemWidth > 0)
            if (ship.PRO_LEN == 0 && !string.IsNullOrWhiteSpace(ship.TandemShape) && ship.TandemWidth > 0)
            {
                if (ship.BLD_COD == BuildType.TR || ship.BLD_COD == BuildType.TX)
                {
                    ShipBatchUnit prev = ship.PrevBatchs.FirstOrDefault();
                    if (prev != null)
                    {
                        if (ship.SHP_ANG == 0)
                        {
                            fromCol = info.DockStartColumn2 + (int)(((ship.X_POS + prev.BLD_POS) / info.ColumnRatio)) - 1;
                            fromRow = info.DockStartRow2 + (int)((ship.Y_POS / info.RowRatio));
                            toCol = info.DockStartColumn2 + (int)(((ship.X_POS + prev.BLD_POS) + prev.TandemWidth) / info.ColumnRatio) - 1;
                            toRow = info.DockStartRow2 + (int)((ship.Y_POS + ship.Height) / info.RowRatio) - 1;
                            colFromOffset = ((ship.X_POS + prev.BLD_POS) % info.ColumnRatio) / info.ColumnRatio * rect.Width;
                            rowFromOffset = (ship.Y_POS % info.RowRatio) / info.RowRatio * rect.Height;
                            colToOffset = (((ship.X_POS + prev.BLD_POS) + prev.TandemWidth) % info.ColumnRatio) / info.ColumnRatio * rect.Width;
                            rowToOffset = ((ship.Y_POS + ship.Height) % info.RowRatio) / info.RowRatio * rect.Height;
                        }
                        else
                        {
                            fromCol = info.DockStartColumn2 + (int)(((ship.X_POS + (ship.LOA - (prev.BLD_POS + prev.TandemWidth))) / info.ColumnRatio)) - 1;
                            fromRow = info.DockStartRow2 + (int)((ship.Y_POS / info.RowRatio));
                            toCol = info.DockStartColumn2 + (int)((ship.X_POS + (ship.LOA - prev.BLD_POS)) / info.ColumnRatio) - 1;
                            toRow = info.DockStartRow2 + (int)((ship.Y_POS + ship.Height) / info.RowRatio) - 1;
                            colFromOffset = ((ship.X_POS + (ship.LOA - (prev.BLD_POS + prev.TandemWidth))) % info.ColumnRatio) / info.ColumnRatio * rect.Width;
                            rowFromOffset = (ship.Y_POS % info.RowRatio) / info.RowRatio * rect.Height;
                            colToOffset = ((ship.X_POS + (ship.LOA - prev.BLD_POS)) % info.ColumnRatio) / info.ColumnRatio * rect.Width;
                            rowToOffset = ((ship.Y_POS + ship.Height) % info.RowRatio) / info.RowRatio * rect.Height;

                        }
                        string shape = HHIDockBizLogic.GetTendemGeometry(ship, BuildLevel.중간, ship.SHA_PNT, ship.SHA_TYP);
                        //_Excel.CreateShape(fromCol, fromRow, colFromOffset, rowFromOffset, toCol, toRow, colToOffset, rowToOffset,
                        //shape, Colors.LightGray, Colors.Blue, ship.SHP_ANG);

                        //2013.07.03 도성민 F/T전 형성구획 색깔 회색->흰색으로 변경, 박수진CJ 요청
                        //CreateShape(fromCol, fromRow, colFromOffset, rowFromOffset, toCol, toRow, colToOffset, rowToOffset,
                        //    shape, Colors.LightGray, Colors.Blue, ship.SHP_ANG);
                        CreateShape(fromCol, fromRow, colFromOffset, rowFromOffset, toCol, toRow, colToOffset, rowToOffset,
                            shape, Colors.White, Colors.Blue, ship.SHP_ANG);
                    }
                }
                else
                {
                    if (ship.SHP_ANG == 0)
                    {
                        fromCol = info.DockStartColumn2 + (int)((ship.X_POS / info.ColumnRatio)) - 1;
                        fromRow = info.DockStartRow2 + (int)((ship.Y_POS / info.RowRatio));
                        toCol = info.DockStartColumn2 + (int)((ship.X_POS + ship.LOA - ship.TandemWidth) / info.ColumnRatio) - 1;
                        toRow = info.DockStartRow2 + (int)((ship.Y_POS + ship.Height) / info.RowRatio) - 1;
                        colFromOffset = (ship.X_POS % info.ColumnRatio) / info.ColumnRatio * rect.Width;
                        rowFromOffset = (ship.Y_POS % info.RowRatio) / info.RowRatio * rect.Height;
                        colToOffset = ((ship.X_POS + ship.LOA - ship.TandemWidth) % info.ColumnRatio) / info.ColumnRatio * rect.Width;
                        rowToOffset = ((ship.Y_POS + ship.Height) % info.RowRatio) / info.RowRatio * rect.Height;
                    }
                    else
                    {
                        fromCol = info.DockStartColumn2 + (int)(((ship.X_POS + ship.TandemWidth) / info.ColumnRatio)) - 1;
                        fromRow = info.DockStartRow2 + (int)((ship.Y_POS / info.RowRatio));
                        toCol = info.DockStartColumn2 + (int)((ship.X_POS + ship.Width) / info.ColumnRatio) - 1;
                        toRow = info.DockStartRow2 + (int)((ship.Y_POS + ship.Height) / info.RowRatio) - 1;
                        colFromOffset = ((ship.X_POS + ship.TandemWidth) % info.ColumnRatio) / info.ColumnRatio * rect.Width;
                        rowFromOffset = (ship.Y_POS % info.RowRatio) / info.RowRatio * rect.Height;
                        colToOffset = ((ship.X_POS + ship.Width) % info.ColumnRatio) / info.ColumnRatio * rect.Width;
                        rowToOffset = ((ship.Y_POS + ship.Height) % info.RowRatio) / info.RowRatio * rect.Height;

                    }

                    string shape = HHIDockBizLogic.GetTendemGeometry(ship, BuildLevel.선미, ship.SHA_PNT, ship.SHA_TYP);
                    //_Excel.CreateShape(fromCol, fromRow, colFromOffset, rowFromOffset, toCol, toRow, colToOffset, rowToOffset,
                    //shape, Colors.LightGray, Colors.Blue, ship.SHP_ANG);

                    //2013.07.03 도성민 F/T전 형성구획 색깔 회색->흰색으로 변경, 박수진CJ 요청
                    //CreateShape(fromCol, fromRow, colFromOffset, rowFromOffset, toCol, toRow, colToOffset, rowToOffset,
                    //    shape, Colors.LightGray, Colors.Blue, ship.SHP_ANG);
                    CreateShape(fromCol, fromRow, colFromOffset, rowFromOffset, toCol, toRow, colToOffset, rowToOffset,
                        shape, Colors.White, Colors.Blue, ship.SHP_ANG);
                }
            }

            string medh = string.Empty;

            //2013.07.25 도성민 수정
            //시작
            if (ship.MAIN_EVT == "FT")
            {
                string me = _Model.IsBeforeME(ship.SHP_COD) == true ? string.Empty : "M/E";
                string dh = _Model.IsBeforeDH(ship.SHP_COD) == true ? string.Empty : "D/H";

                if (!string.IsNullOrEmpty(me) && !string.IsNullOrEmpty(dh))
                    medh = string.Format("({0}, {1})", me, dh);
                else if (!string.IsNullOrEmpty(me) || !string.IsNullOrEmpty(dh))
                    medh = string.Format("({0} {1})", me, dh);


            }
            else
            {
                string me = _Model.IsBeforeME(ship.SHP_COD) == true ? "M/E" : string.Empty;
                string dh = _Model.IsBeforeDH(ship.SHP_COD) == true ? "D/H" : string.Empty;

                if (!string.IsNullOrEmpty(me) && !string.IsNullOrEmpty(dh))
                    medh = string.Format("({0}, {1})", me, dh);
                else if (!string.IsNullOrEmpty(me) || !string.IsNullOrEmpty(dh))
                    medh = string.Format("({0} {1})", me, dh);

            }


            //string me = _Model.IsBeforeME(ship.SHP_COD) == true ? "M/E" : string.Empty;
            //string dh = _Model.IsBeforeDH(ship.SHP_COD) == true ? "D/H" : string.Empty;
            //if (!string.IsNullOrEmpty(me) && !string.IsNullOrEmpty(dh))
            //    medh = string.Format("({0}, {1})", me, dh);
            //else if (!string.IsNullOrEmpty(me) || !string.IsNullOrEmpty(dh))
            //    medh = string.Format("({0} {1})", me, dh);

            string desc;
            string dizeDesc = GetSizeDesc(ship);
            if (ship.BLD_LVL == BuildLevel.중간)
            {
                string dir = "◀";
                if (ship.SHP_ANG == 0) dir = "▶";
                desc = string.Format("{0}\r\n{1}{2}{3}", ship.ShipDescription, dizeDesc, medh, dir);
            }
            else desc = string.Format("{0}\r\n{1}{2}", ship.ShipDescription, dizeDesc, medh);

            //string desc = string.Format("{0}\r\n{1}{2}", ship.ShipDescription, ship.ShipSizeDesc, medh);
            CreateExcelTextEx(info.DockStartColumn2, info.DockStartRow2, info.ColumnRatio, info.RowRatio,
                rect, ship, ship.X_POS, ship.X_POS + ship.Width,
                ship.Y_POS, ship.Y_POS + ship.Height, desc, Colors.Black, Colors.White,
                "돋움", 2000, true, name:"Title");

            DrawAPHold(ship, dockCode, true);
        }

        /// <summary>
        /// AP 포인트 및 Hold 생성 (상세, 실행)
        /// </summary>
        /// <param name="ship"></param>
        /// <param name="dockCode"></param>
        /// <param name="isNext"></param>
        private void DrawAPHold(ShipBatchUnit ship, string dockCode, bool isNext)
        {
            if (ship.IsAssignBatch == false)
                return;

            double appPos = _Model.GetAPHoldInfo(ship.SHP_COD);// table.Rows[0]["APP_POS"] == DBNull.Value ? 0 : (double)table.Rows[0]["APP_POS"];
            
            int startRow = 0, startColumn = 0;
            double ColumnRatio = 0, RowRatio = 0;
            double incHeight = 0;
            bool isExec = _Model.BatchKind == DockBatchType.실행도크배치;

            // 실행 도크 배치
            if (isExec)
            {
                ExecDockPosInfo info = _ExecPosDic[dockCode];

                startRow = info.DockStartRow;
                startColumn = info.DockStartColumn;

                ColumnRatio = info.ColumnRatio;
                RowRatio = info.RowRatio;

                incHeight = 5;
            }
            // 상세 도크 배치
            else
            {
                DetailDockPosInfo info = _DetailPosDic[dockCode];

                if (isNext)
                {
                    startRow = info.DockStartRow2;
                    startColumn = info.DockStartColumn2;
                }
                else
                {
                    startRow = info.DockStartRow1;
                    startColumn = info.DockStartColumn1;
                }

                ColumnRatio = info.ColumnRatio;
                RowRatio = info.RowRatio;

                incHeight = 5;
            }

            Rect rect = _Excel.GetCellPosition(startRow, startColumn);

            // AP 포인트 표시 (AP 포인트 길이가 있고 AP포인트가 건조 시작위치 이후인 경우)
            if(appPos > 0)// && appPos >= ship.BLD_POS)
                DrawAPPoint(dockCode, rect, ship, appPos, isNext);

            DataTable table = _Model.ShipManager.GetAPHoldInfo(ship.SHP_COD);

            if (table == null || table.Rows.Count < 1)
                return;

            string meStatus = GetMEStatus(ship);

            double start, end;
            GetBatchShipSize(ship, out start, out end);

            Color color;
            bool isFirst = true, isRejFrist = true, isPrevME = false;
            double hldLen, pos, mePos = 0, curPos = 0;
            
            foreach (DataRow row in table.Rows)
            {
                hldLen = row.Field<double?>("HLD_LEN") == null ? 0 : row.Field<double>("HLD_LEN") / 10;

                if ((ship.BLD_COD == BuildType.SemiTandem || ship.BLD_COD == BuildType.TX)
                    && ship.NextBatch != null && isRejFrist == true
                    && ship.GetCurrentDivision() == BuildLevel.선미
                    )
                {
                    hldLen -= ship.BLD_LEN_REJ;
                    isRejFrist = false;
                }

                //if (curPos < ship.LOA - end || curPos > ship.LOA - start) { curPos += hldLen; continue; }
                if (curPos < start || curPos > end)
                {
                    /*gap += hldLen;*/
                    curPos += hldLen;
                    continue;
                }

                //if (isFirst == true && row["HLD_GBN"].ToString() == HoldType.선미) { curPos += hldLen; isFirst = false; continue; }
                if (isFirst == true && row["HLD_TYPE"].ToString() == HoldType.선미)
                {
                    curPos += hldLen - ship.BLD_POS;
                    isFirst = false;
                    continue;
                }

                //if (ship.SHP_ANG == 0) pos = curPos - (ship.LOA - end);
                //else pos = ship.Width - curPos - (ship.LOA - end);
                if (ship.SHP_ANG == 0)
                    pos = curPos - start;
                else
                    pos = ship.Width - (curPos - start);

                if (isPrevME)
                {
                    isPrevME = false;
                    color = Colors.Black;
                    //if (ship.NextBatch == null && meStatus == FTStatus.FT후)
                    //    color = Colors.Red;
                    //else if (ship.NextBatch != null && meStatus == FTStatus.FT전)
                    //    color = Colors.Red;

                    CreateExcelLine(startColumn, startRow, ColumnRatio, RowRatio, rect, ship, ship.X_POS + mePos, ship.X_POS + pos, ship.Y_POS, ship.Y_POS + ship.Height, color, LineDashValues.Dash);
                    CreateExcelLine(startColumn, startRow, ColumnRatio, RowRatio, rect, ship, ship.X_POS + pos, ship.X_POS + mePos, ship.Y_POS, ship.Y_POS + ship.Height, color, LineDashValues.Dash);
                }

                //if (row["HLD_GBN"].ToString() == HoldType.ER)
                if (row["HLD_TYPE"].ToString() == HoldType.ER)
                {
                    isPrevME = true;
                    mePos = pos;
                }
                CreateExcelLine(startColumn, startRow, ColumnRatio, RowRatio, rect, ship, ship.X_POS + pos, ship.X_POS + pos, ship.Y_POS, ship.Y_POS + ship.Height, Colors.Black, LineDashValues.Dash);

                //if (row["HLD_TYPE"].ToString() != HoldType.ER)
                {
                    string holdText = row["HLD_LABEL"].ToString();
                    string shp_cod = row["SHP_COD"].ToString();
                    double x1 = ship.X_POS + pos;
                    double x2 = ship.X_POS + pos + 5;

                    int fromCol = startColumn + (int)(x1 / ColumnRatio) - 1;
                    int toCol = startColumn + (int)(x2 / ColumnRatio) - 1;

                    double colFromOffset = (x1 % ColumnRatio) / ColumnRatio * rect.Width;
                    double colToOffset = (x2 % ColumnRatio) / ColumnRatio * rect.Width;

                    CreateExcelText(startColumn, startRow, ColumnRatio, RowRatio, rect, ship, ship.X_POS + pos, ship.X_POS + pos + 5, ship.Y_POS + ship.Height - incHeight, ship.Y_POS + ship.Height, holdText, isExec, shp_cod);

                    //if (ship.SHP_ANG == 0)
                    //    //CreateExcelText(startColumn, startRow, info.ColumnRatio, info.RowRatio, rect, ship, ship.X_POS + pos, ship.X_POS + pos + 5, ship.Y_POS + ship.Height - 5, ship.Y_POS + ship.Height, row["HLD_GBN"].ToString());
                    //    CreateExcelText(startColumn, startRow, ColumnRatio, RowRatio, rect, ship, ship.X_POS + pos, ship.X_POS + pos + 5, ship.Y_POS + ship.Height - incHeight, ship.Y_POS + ship.Height, holdText, isExec);
                    //else
                    //{
                    //    int fromCol = startColumn + (int)(ship.X_POS + pos / ColumnRatio) - 1;
                    //    int toCol = 0;

                    //    //CreateExcelText(startColumn, startRow, info.ColumnRatio, info.RowRatio, rect, ship, ship.X_POS + pos - 5, ship.X_POS + pos + 5 - 5, ship.Y_POS + ship.Height - 5, ship.Y_POS + ship.Height, row["HLD_GBN"].ToString());
                    //    CreateExcelText(startColumn, startRow, ColumnRatio, RowRatio, rect, ship, ship.X_POS + pos - addLeft, ship.X_POS + pos + addLeft - addLeft, ship.Y_POS + ship.Height - incHeight, ship.Y_POS + ship.Height, holdText, isExec);
                    //}
                }

                curPos += hldLen;
            }

            if (table.Rows.Count > 0)
            {
                if (ship.SHP_ANG == 0) pos = curPos - start;
                else pos = ship.Width - (curPos - start);

                if (curPos >= start && curPos <= end)
                {
                    if (isPrevME == true)
                    {
                        color = Colors.Black;
                        
                        CreateExcelLine(startColumn, startRow, ColumnRatio, RowRatio, rect, ship, ship.X_POS + mePos, ship.X_POS + pos, ship.Y_POS, ship.Y_POS + ship.Height, color, LineDashValues.Dash);
                        CreateExcelLine(startColumn, startRow, ColumnRatio, RowRatio, rect, ship, ship.X_POS + pos, ship.X_POS + mePos, ship.Y_POS, ship.Y_POS + ship.Height, color, LineDashValues.Dash);
                    }

                    CreateExcelLine(startColumn, startRow, ColumnRatio, RowRatio, rect, ship, ship.X_POS + pos, ship.X_POS + pos, ship.Y_POS, ship.Y_POS + ship.Height, Colors.Black, LineDashValues.Dash);
                }
            }
        }

        /// <summary>
        /// AP포인트 생성 (상세, 실행)
        /// </summary>
        /// <param name="dockCode"></param>
        /// <param name="rect"></param>
        /// <param name="ship"></param>
        /// <param name="apPos"></param>
        /// <param name="isNext"></param>
        private void DrawAPPoint(string dockCode, Rect rect, ShipBatchUnit ship, double apPos, bool isNext)
        {
            if (apPos == 0)
                return;

            int startRow = 0, startColumn = 0, lineRow = 0;
            double ColumnRatio = 0, RowRatio = 0;

            if (_Model.BatchKind == DockBatchType.실행도크배치)
            {
                ExecDockPosInfo info = _ExecPosDic[dockCode];

                startRow = info.DockStartRow;
                startColumn = info.DockStartColumn;
                lineRow = 15;

                ColumnRatio = info.ColumnRatio;
                RowRatio = info.RowRatio;
            }
            else
            {
                DetailDockPosInfo info = _DetailPosDic[dockCode];

                if (isNext)
                {
                    startRow = info.DockStartRow2;
                    startColumn = info.DockStartColumn2;
                    lineRow = info.ShipLineRow2;
                }
                else
                {
                    startRow = info.DockStartRow1;
                    startColumn = info.DockStartColumn1;
                    lineRow = info.ShipLineRow1;
                }

                ColumnRatio = info.ColumnRatio;
                RowRatio = info.RowRatio;
            }

            double x1, x2, y;
            double apRate = 2.5;// apPos / 2;

            // 전체 건조
            if (ship.BLD_COD == BuildType.Straight || ship.NextBatch == null)
            {
                y = ship.Y_POS + ship.Height / 2;

                // 선수가 오른쪽
                if (ship.SHP_ANG == 0)
                {
                    x1 = ship.X_POS - ship.BLD_POS;
                    x2 = x1 + apPos + apRate;

                    // 시작위치 ~ AP포인트 위치까지 점선
                    CreateExcelLine(startColumn, startRow, ColumnRatio, RowRatio, rect, ship, x1, x2, ship.Y_POS + ship.Height / 2, y, Colors.Black, LineDashValues.Dash);

                    // AP포인트 모양
                    CreateExcelAutoShape(startColumn, startRow, ColumnRatio, RowRatio, rect, ship, x1 + apPos - apRate, x1 + apPos + apRate, ship.Y_POS + ship.Height / 2 - apRate, ship.Y_POS + ship.Height / 2 + apRate, ExcelAutoShapeType.Ellipse, Colors.Red, Colors.Black);
                }
                // 선수가 왼쪽
                else
                {
                    x1 = ship.X_POS + ship.BLD_POS + ship.Width - apPos;
                    x2 = x1 + apPos;

                    // 시작위치 ~ AP포인트 위치까지 점선
                    CreateExcelLine(startColumn, startRow, ColumnRatio, RowRatio, rect, ship, x1, x2, ship.Y_POS + ship.Height / 2, y, Colors.Black, LineDashValues.Dash);

                    // AP포인트 모양
                    CreateExcelAutoShape(startColumn, startRow, ColumnRatio, RowRatio, rect, ship, x1 - apRate, x1 + apRate, ship.Y_POS + ship.Height / 2 - apRate, ship.Y_POS + ship.Height / 2 + apRate, ExcelAutoShapeType.Ellipse, Colors.Red, Colors.Black);
                }
            }
            // 부분 건조
            else
            {
                string div = ship.GetCurrentDivision();

                // 시작 부분이 선미가 아닌 경우 AP 포인트를 그리지 않는다.
                if (div == BuildLevel.선수 || div == BuildLevel.중간)
                    return;

                // 부분 건조
                if (ship.BLD_COD == BuildType.SemiTandem || ship.BLD_COD == BuildType.TX)
                {
                    if (ship.SHP_ANG == 0)
                        CreateExcelLine(startColumn, startRow, ColumnRatio, RowRatio, rect, ship, ship.X_POS - ship.BLD_LEN_REJ, ship.X_POS + apPos - apRate, ship.Y_POS + ship.Height / 2, ship.Y_POS + ship.Height / 2, Colors.Black, LineDashValues.Dash);
                    else
                        CreateExcelLine(startColumn, startRow, ColumnRatio, RowRatio, rect, ship, ship.X_POS + ship.LOA + ship.BLD_LEN_REJ, ship.X_POS + apPos + apRate, ship.Y_POS + ship.Height / 2, ship.Y_POS + ship.Height / 2, Colors.Black, LineDashValues.Dash);

                    CreateExcelAutoShape(startColumn, startRow, ColumnRatio, RowRatio, rect, ship, ship.X_POS + apPos - apRate, ship.X_POS + apPos + apRate, ship.Y_POS + ship.Height / 2 - apRate, ship.Y_POS + ship.Height / 2 + apRate, ExcelAutoShapeType.Ellipse, Colors.Red, Colors.Black);
                }
                else
                {
                    // 선수 오른쪽
                    if (ship.SHP_ANG == 0)
                    {
                        x1 = ship.X_POS - ship.BLD_POS;
                        x2 = x1 + apPos - apRate;

                        CreateExcelLine(startColumn, startRow, ColumnRatio, RowRatio, rect, ship, x1, x2, ship.Y_POS + ship.Height / 2, ship.Y_POS + ship.Height / 2, Colors.Black, LineDashValues.Dash);

                        // AP포인트 모양
                        CreateExcelAutoShape(startColumn, startRow, ColumnRatio, RowRatio, rect, ship, x1 + apPos - apRate, x1 + apPos + apRate, ship.Y_POS + ship.Height / 2 - apRate, ship.Y_POS + ship.Height / 2 + apRate, ExcelAutoShapeType.Ellipse, Colors.Red, Colors.Black);
                    }
                    else
                    {
                        x1 = ship.X_POS + ship.BLD_POS + ship.Width - apPos;
                        x2 = x1 + apPos;

                        CreateExcelLine(startColumn, startRow, ColumnRatio, RowRatio, rect, ship, x1, x2, ship.Y_POS + ship.Height / 2, ship.Y_POS + ship.Height / 2, Colors.Black, LineDashValues.Dash);

                        // AP포인트 모양
                        CreateExcelAutoShape(startColumn, startRow, ColumnRatio, RowRatio, rect, ship, x1 - apRate, x1 + apRate, ship.Y_POS + ship.Height / 2 - apRate, ship.Y_POS + ship.Height / 2 + apRate, ExcelAutoShapeType.Ellipse, Colors.Red, Colors.Black);
                    }
                }
            }

            #region AP 포인트 치수
            
            LineDashValues lineType = LineDashValues.Solid;

            // 선수가 오른쪽
            if (ship.SHP_ANG == 0)
            {
                x1 = ship.X_POS - ship.BLD_POS + apPos;
                x2 = ship.X_POS - ship.BLD_POS + apPos;

                if(ship.BLD_POS > apPos)
                    lineType = LineDashValues.Dash;

                // 수직 치수 보조선
                CreateExcelLine(startColumn, startRow, ColumnRatio, RowRatio, rect, ship, x1, x2, ship.Y_POS + 5, ship.Y_POS + ship.Height / 2, Colors.Black, lineType);
                // 수평 치수선
                CreateExcelArrowLine(startColumn, startRow, ColumnRatio, RowRatio, rect, ship.X_POS - ship.BLD_POS, ship.X_POS - ship.BLD_POS + apPos, ship.Y_POS + ship.Height / 3, ship.Y_POS + ship.Height / 3, Colors.Black);

                int endRow = startRow + (int)(ship.Y_POS / RowRatio) + 1;
                // 치수 텍스트
                CreateLineText(startColumn, startRow, ColumnRatio, RowRatio, rect, ship.X_POS - ship.BLD_POS, ship.X_POS - ship.BLD_POS + apPos, endRow, string.Format("{0:F1}", apPos), 1800, true, "InnerDim");
            }
            // 선수가 왼쪽
            else
            {
                x1 = ship.X_POS + ship.BLD_POS + ship.Width - apPos;
                x2 = x1 + apPos;

                if (ship.BLD_POS > apPos)
                    lineType = LineDashValues.Dash;

                // 수직 치수 보조선
                CreateExcelLine(startColumn, startRow, ColumnRatio, RowRatio, rect, ship, x1, x1, ship.Y_POS + 5, ship.Y_POS + ship.Height / 2, Colors.Black, lineType);
                // 수평 치수선
                CreateExcelArrowLine(startColumn, startRow, ColumnRatio, RowRatio, rect, x1, x2, ship.Y_POS + ship.Height / 3, ship.Y_POS + ship.Height / 3, Colors.Black);

                int endRow = startRow + (int)(ship.Y_POS / RowRatio) + 1;
                // 치수 텍스트
                CreateLineText(startColumn, startRow, ColumnRatio, RowRatio, rect, x1 + (apPos / 2), x1 + (apPos / 2) , endRow, string.Format("{0:F1}", apPos), 1800, true, "InnerDim");
            }

            
            #endregion
        }

        /// <summary>
        /// 도크 바깥쪽 치수 (상세, 실행)
        /// </summary>
        /// <param name="dockCode"></param>
        /// <param name="shipList"></param>
        /// <param name="isNext"></param>
        private void DrawShipInterval(string dockCode, List<ShipBatchUnit> shipList, bool isNext)
        {
            if (!_DetailPosDic.ContainsKey(dockCode) && !_ExecPosDic.ContainsKey(dockCode))
                return;
            
            int startRow = 0, startColumn = 0, lineRow = 0, endRow = 0, bottomLineRow = 0;
            double ColumnRatio = 0, RowRatio = 0;

            if (_Model.BatchKind == DockBatchType.실행도크배치)
            {
                ExecDockPosInfo info = _ExecPosDic[dockCode];

                startRow = info.DockStartRow;
                startColumn = info.DockStartColumn;
                // 치수선이 그려지는 Row Index
                lineRow = 15;// info.ShipLineRow;
                endRow = info.DockEndRow;
                bottomLineRow = info.ShipSizeBottomLineRow;

                ColumnRatio = info.ColumnRatio;
                RowRatio = info.RowRatio;
            }
            else if (_Model.BatchKind == DockBatchType.상세도크배치)
            {
                DetailDockPosInfo info = _DetailPosDic[dockCode];

                if (isNext == true)
                {
                    startRow = info.DockStartRow2;
                    startColumn = info.DockStartColumn2;
                    lineRow = info.ShipLineRow2;
                    endRow = info.DockEndRow2;
                    bottomLineRow = info.ShipSizeBottomLineRow2;
                }
                else
                {
                    startRow = info.DockStartRow1;
                    startColumn = info.DockStartColumn1;
                    lineRow = info.ShipLineRow1;
                    endRow = info.DockEndRow1;
                    bottomLineRow = info.ShipSizeBottomLineRow1;
                }

                ColumnRatio = info.ColumnRatio;
                RowRatio = info.RowRatio;
            }
            
            Rect rect = _Excel.GetCellPosition(startRow, startColumn);

            List<ShipBatchUnit> topDic = new List<ShipBatchUnit>();
            List<ShipBatchUnit> bottomDic = new List<ShipBatchUnit>();

            DockUnit dock = _Model.DockManager.GetDockInfo(string.Format("{0}{1:D02}", _CurrentYear, _CurrentBatch));
            
            foreach (ShipBatchUnit ele in shipList)
            {
                if (ele.IsAssignBatch == false)
                    continue;

                // ele.BatchInfo["MEA_POS"]에 값이 있어도 현재 호선이 도크의 상단에 있는지 하단에 있는지 설정하는 로직 적용
                if ((dock.Height / 2) - dock.Height * 0.1 < ele.Y_POS)
                    ele.LinePosition = "BOTTOM";
                else
                    ele.LinePosition = "TOP";

                /* 복선 배열 확인
                 *  -----------------------------------------------------------
                 *  case.1          case.2           case.3       case.4
                 *  -----------------------------------------------------------
                 *  ######>        ###########>        ######>        ######>
                 *     ######>       ######>        ######>         ##########>
                 */
                var tmp = shipList.Where(t => !t.FIG_SHP.Equals(ele.FIG_SHP) &&
                                              ((ele.X_POS <= t.X_POS && ele.X_POS + ele.BLD_LEN >= t.X_POS)             || // case.1
                                               (ele.X_POS <= t.X_POS && ele.X_POS + ele.BLD_LEN >= t.X_POS)             || // case.2
                                               (ele.X_POS >= t.X_POS && ele.X_POS + ele.BLD_LEN >= t.X_POS + t.BLD_LEN) || // case.3
                                               (ele.X_POS >= t.X_POS && ele.X_POS + ele.BLD_LEN <= t.X_POS + t.BLD_LEN))   // case.4
                );
                
                // 복선 배열이 아니면 치수선을 무조건 위쪽에 표시
                if (!tmp.Any())
                    ele.LinePosition = "TOP";

                if (ele.LinePosition == "TOP")
                    topDic.Add(ele);
                else
                    bottomDic.Add(ele);
            }

            SortItems(topDic);
            SortItems(bottomDic);
            
            bool isFirst = true;
            double start = 0;
            List<double> sepValues;
            #region top line
            {
                start = 0;
                isFirst = true;
                Dictionary<double, ShipBatchUnit> retDic;
                sepValues = GetResourceSperateValue(topDic, dock.Width, out retDic);
                ShipBatchUnit ship;
                foreach (double value in sepValues)
                {
                    if (isFirst == true)
                    {
                        isFirst = false;
                        //왼쪽 수직
                        //int startCol, int startRow, double colRatio, double rowRatio, Rect rect, double x, int startExcelRow, double endy, Color color, LineDashValues style
                        CreteShipVerticalLine(startColumn, lineRow - 1, ColumnRatio, RowRatio, rect, start, startRow, 0, Colors.Black, LineDashValues.Solid);
                    }
                    
                    //수평선
                    CreateShipHorizentalLine(startColumn, startRow, ColumnRatio, RowRatio, rect, start, value, lineRow, Colors.Black);
                    if (value - start > 10)
                        CreateLineText(startColumn, startRow, ColumnRatio, RowRatio, rect, start, value, lineRow, string.Format("{0:F1}", value - start), 1800, true, "OuterDim");
                    else
                        CreateLineText(startColumn, startRow, ColumnRatio, RowRatio, rect, start, value, lineRow, string.Format("{0:F1}", value - start), 1800, true, "OuterDim");

                    // 오른쪽 수직
                    ship = retDic[value];
                    if (ship != null)
                        CreteShipVerticalLine(startColumn, lineRow - 1, ColumnRatio, RowRatio, rect, value, startRow, ship.Y_POS, Colors.Black, LineDashValues.Solid);
                    else
                        CreteShipVerticalLine(startColumn, lineRow - 1, ColumnRatio, RowRatio, rect, value, startRow, 0, Colors.Black, LineDashValues.Solid);

                    start = value;
                }
            }
            #endregion
            
            #region bottom line
            {
                start = 0;
                isFirst = true;

                int dockEndRow = endRow;
                int addInc = 0;

                // 1도크인 경우
                if (dockCode.Equals("1"))
                {
                    if (_Model.BatchKind == DockBatchType.상세도크배치)
                    {
                        dockEndRow += 23; // 아래쪽 도크가 끝나는 Row Index
                        addInc = 11; // 텍스트 위치, endRow에서 몇 칸 내려오는지 설정
                    }
                    else
                    {
                        dockEndRow += 10; // 아래쪽 도크가 끝나는 Row Index
                        addInc = 5; // 텍스트 위치, endRow에서 몇 칸 내려오는지 설정
                    }

                    // 왼쪽 수직 치수선
                    CreateLineText(startColumn, endRow + 1, ColumnRatio, RowRatio, rect, start, Dock1Dim1, endRow + addInc, string.Format("{0:F1}", Dock1Dim1), 1800, true, "OuterDim");
                    CreateExcelArrowLine(startColumn, endRow + 1, ColumnRatio, RowRatio, rect, Dock1Dim2 / 2, Dock1Dim2 / 2, 0, Dock1Dim1, Colors.Black);
                    //CreateExcelLine(startColumn, endRow + 1, ColumnRatio, RowRatio, rect, null, 0, Dock1Dim2, dockEndRow, dockEndRow, Colors.Black, LineDashValues.Solid);
                }

                Dictionary<double, ShipBatchUnit> retDic;
                sepValues = GetResourceSperateValue(bottomDic, dock.Width, out retDic);
                ShipBatchUnit ship;
                foreach (double value in sepValues)
                {
                    if (isFirst == true)
                    {
                        isFirst = false;
                        //왼쪽 수직
                        CreteShipVerticalLine(startColumn, dockEndRow, ColumnRatio, RowRatio, rect, start, bottomLineRow, 0, Colors.Black, LineDashValues.Solid);
                    }

                    //수평선
                    CreateShipHorizentalLine(startColumn, dockEndRow, ColumnRatio, RowRatio, rect, start, value, bottomLineRow, Colors.Black);
                    if (value - start > 10)
                        CreateLineText(startColumn, dockEndRow, ColumnRatio, RowRatio, rect, start, value, bottomLineRow, string.Format("{0:F1}", value - start), 1800, true, "OuterDim");
                    else
                        CreateLineText(startColumn, dockEndRow, ColumnRatio, RowRatio, rect, start, value, bottomLineRow, string.Format("{0:F1}", value - start), 1800, true, "OuterDim");

                    // 오른쪽 수직
                    ship = retDic[value];
                    if (ship != null)
                        CreteShipVerticalLine(startColumn, dockEndRow, ColumnRatio, RowRatio, rect, value, bottomLineRow, 0, Colors.Black, LineDashValues.Solid);
                    else
                        CreteShipVerticalLine(startColumn, dockEndRow, ColumnRatio, RowRatio, rect, value, bottomLineRow, 0, Colors.Black, LineDashValues.Solid);

                    start = value;
                }
            }
            #endregion
        }

        /// <summary>
        /// 도크 안쪽 치수 (상세, 실행 도크 배치)
        /// </summary>
        /// <param name="dockCode"></param>
        /// <param name="shipList"></param>
        /// <param name="isNext"></param>
        private void DrawConstraintVlaue(string dockCode, List<ShipBatchUnit> shipList, bool isNext)
        {
            if (!_DetailPosDic.ContainsKey(dockCode) && !_ExecPosDic.ContainsKey(dockCode))
                return;

            int startRow = 0, startColumn = 0, lineRow = 0, endRow = 0, bottomLineRow = 0;
            double ColumnRatio = 0, RowRatio = 0;

            if (_Model.BatchKind == DockBatchType.실행도크배치)
            {
                ExecDockPosInfo info = _ExecPosDic[dockCode];

                startRow = info.DockStartRow;
                startColumn = info.DockStartColumn;
                // 치수선이 그려지는 Row Index
                lineRow = 15;// info.ShipLineRow;
                endRow = info.DockEndRow;
                bottomLineRow = info.ShipSizeBottomLineRow;

                ColumnRatio = info.ColumnRatio;
                RowRatio = info.RowRatio;
            }
            else if (_Model.BatchKind == DockBatchType.상세도크배치)
            {
                DetailDockPosInfo info = _DetailPosDic[dockCode];

                if (isNext == true)
                {
                    startRow = info.DockStartRow2;
                    startColumn = info.DockStartColumn2;
                    lineRow = info.ShipLineRow2;
                    endRow = info.DockEndRow2;
                    bottomLineRow = info.ShipSizeBottomLineRow2;
                }
                else
                {
                    startRow = info.DockStartRow1;
                    startColumn = info.DockStartColumn1;
                    lineRow = info.ShipLineRow1;
                    endRow = info.DockEndRow1;
                    bottomLineRow = info.ShipSizeBottomLineRow1;
                }

                ColumnRatio = info.ColumnRatio;
                RowRatio = info.RowRatio;
            }
            
            Rect rc = _Excel.GetCellPosition(startRow, startColumn);

            double value;
            List<Rect> topList, bottomList;

            DockUnit res = _Model.DockManager.GetDockInfo(string.Format("{0}{1:D02}", _CurrentYear, _CurrentBatch));

            double valTop = 0;
            int cnt = 0;

            GetItemDivision(out topList, out bottomList, res, shipList);

            // 도크 상단에 배치된 호선
            foreach (Rect rect in topList)
            {
                value = rect.Top + 0.04;
                value = ((int)(value * 10)) / 10.0;

                valTop = 0 + (value - 8) / 2;

                if (valTop < 0)
                    valTop = 0;
                
                CreateExcelTextEx(startColumn, startRow, ColumnRatio, RowRatio,
                                  rc, null, rect.Left, rect.Left + rect.Width, valTop, valTop + 8, string.Format("{0:F1}", value),
                                  Colors.Black, Colors.Yellow, "돋움", 1800, true, ExcelHorizentalAlignment.Center, ExcelVerticalAlignment.Top, true, "InnerDim");

                CreateExcelArrowLine(startColumn, startRow, ColumnRatio, RowRatio, rc, rect.Left + rect.Width / 2 - 5, rect.Left + rect.Width / 2 - 5, 0, rect.Top, Colors.Black);

                cnt++;

                // 1도크인 경우
                if (dockCode == "1" && cnt == topList.Count)
                {
                    #region 왼쪽 치수선
                    valTop = rect.Top + rect.Height;
                    value = res.Height - Dock1Dim1 - rect.Top - rect.Height;

                    double left = 0;

                    if(rect.Width > Dock1Dim2)
                        left = Dock1Dim2 / 3;
                    else
                        left = (rect.Width + rect.Left) / 2;

                    CreateExcelTextEx(startColumn, startRow, ColumnRatio, RowRatio,
                                      rc, null, left + 5, left + 5, valTop, valTop + value, string.Format("{0:F1}", value),
                                      Colors.Black, Colors.Yellow, "돋움", 1800, true, ExcelHorizentalAlignment.Center, ExcelVerticalAlignment.Top, true, "InnerDim");

                    CreateExcelArrowLine(startColumn, startRow, ColumnRatio, RowRatio, rc, left, left, valTop, valTop + value, Colors.Black);
                    #endregion


                    #region 상단 호선과 하단 호선과의 치수

                    // 하단 호선이 있는 경우
                    if(bottomList != null && bottomList.Count > 0)
                    {
                        Rect firstBottomShip = bottomList[0];

                        left = (firstBottomShip.X + Dock1Dim1 + firstBottomShip.Width) / 2; // 아래 있는 호선의 위치의 중간
                        value = firstBottomShip.Top - valTop;

                        CreateExcelTextEx(startColumn, startRow, ColumnRatio, RowRatio,
                                          rc, null, left + 5, left + 5, valTop, valTop + value, string.Format("{0:F1}", value),
                                          Colors.Black, Colors.Yellow, "돋움", 1800, true, ExcelHorizentalAlignment.Center, ExcelVerticalAlignment.Top, true, "InnerDim");

                        CreateExcelArrowLine(startColumn, startRow, ColumnRatio, RowRatio, rc, left, left, valTop, valTop + value, Colors.Black);
                    }

                    #endregion


                    #region 오른쪽 치수선

                    value = res.Height - Dock1Dim1 - rect.Top - rect.Height;

                    double lineLoc = rect.Left + (rect.Width - rect.Width / 5);
                    // 오른쪽을 기준으로 구조물과 충돌하는지 확인
                    double crossLen = GetChkAddItem(dockCode, lineLoc, res.Height - Dock1Dim1, isNext);

                    CreateExcelTextEx(startColumn, startRow, ColumnRatio, RowRatio,
                                      rc, null, lineLoc, lineLoc + 16, valTop - (crossLen == 0 ? 0 : crossLen / 2), valTop + 16, string.Format("{0:F1}", (value - crossLen)),
                                      Colors.Black, Colors.Yellow, "돋움", 1800, true, ExcelHorizentalAlignment.Center, ExcelVerticalAlignment.Top, true, "InnerDim");

                    CreateExcelArrowLine(startColumn, startRow, ColumnRatio, RowRatio, rc, lineLoc, lineLoc, rect.Bottom, res.Height - Dock1Dim1 - crossLen, Colors.Black);
                    #endregion
                }
                else
                {
                    bool isOverlapped = false;

                    foreach (Rect br in bottomList)
                    {
                        if (!(rect.Left > br.Right || rect.Right < br.Left))
                        {
                            isOverlapped = true;
                            break;
                        }
                    }

                    if (isOverlapped == false)
                    {
                        value = res.Height - rect.Bottom + 0.04;
                        value = ((int)(value * 10)) / 10.0;

                        valTop = rect.Bottom + (value - 8) / 2;

                        if ((value - 8) < 0)
                            valTop = rect.Bottom;

                        // 구조물과 충돌하는지 확인
                        double crossLen = GetChkAddItem(dockCode, rect.Left + rect.Width / 2 - 5, res.Height, isNext);

                        CreateExcelTextEx(startColumn, startRow, ColumnRatio, RowRatio,
                                          rc, null, rect.Left, rect.Left + rect.Width, valTop - (crossLen == 0 ? 0 : crossLen / 2), valTop + 8, string.Format("{0:F1}", (value - crossLen)),
                                          Colors.Black, Colors.Yellow, "돋움", 1800, true, ExcelHorizentalAlignment.Center, ExcelVerticalAlignment.Top, true, "InnerDim");

                        CreateExcelArrowLine(startColumn, startRow, ColumnRatio, RowRatio, rc, rect.Left + rect.Width / 2 - 5, rect.Left + rect.Width / 2 - 5, rect.Bottom, res.Height - crossLen, Colors.Black);
                    }
                }
            }

            List<Rect> middleList = new List<Rect>();
            if (res.Code == "1")
            {
                double extraLimit = res.Height - 47;
                int len = bottomList.Count;

                for (int index = len - 1; index >= 0; index--)
                {
                    if (bottomList[index].Top + bottomList[index].Height / 2 <= extraLimit)
                    {
                        middleList.Add(bottomList[index]);
                        bottomList.RemoveAt(index);
                    }
                }
            }

            // 도크 하단에 배치된 호선
            foreach (Rect rect in bottomList)
            {
                value = res.Height - rect.Bottom + 0.04;
                value = ((int)(value * 10)) / 10.0;

                valTop = rect.Bottom + (value - 8) / 2;

                if ((value - 8) < 0)
                    valTop = rect.Bottom;

                // 구조물과 충돌하는지 확인
                double crossLen = GetChkAddItem(dockCode, rect.Left + rect.Width / 2 - 5, res.Height, isNext);

                CreateExcelTextEx(startColumn, startRow, ColumnRatio, RowRatio,
                                  rc, null, rect.Left, rect.Left + rect.Width, valTop, valTop + 5 /*res.Height - 5, res.Height*/, string.Format("{0:F1}", value),
                                  Colors.Black, Colors.Yellow, "돋움", 1800, true, ExcelHorizentalAlignment.Center, ExcelVerticalAlignment.Bottom, true, "InnerDim");

                CreateExcelArrowLine(startColumn, startRow, ColumnRatio, RowRatio, rc, rect.Left + rect.Width / 2 - 5, rect.Left + rect.Width / 2 - 5, rect.Bottom, res.Height, Colors.Black);

                if (dockCode != "1")
                {
                    bool isOverlapped = false;
                    foreach (Rect br in topList)
                    {
                        if (!(rect.Left > br.Right || rect.Right < br.Left))
                        {
                            isOverlapped = true;
                            break;
                        }
                    }

                    if (isOverlapped == false)
                    {
                        value = rect.Top + 0.04;
                        value = ((int)(value * 10)) / 10.0;

                        valTop = 0 + (value - 8) / 2;

                        if (valTop < 0)
                            valTop = 0;

                        CreateExcelTextEx(startColumn, startRow, ColumnRatio, RowRatio,
                                          rc, null, rect.Left, rect.Left + rect.Width, valTop, valTop + 5, string.Format("{0:F1}", value),
                                          Colors.Black, Colors.Yellow, "돋움", 1800, true, ExcelHorizentalAlignment.Center, ExcelVerticalAlignment.Top, true, "InnerDim");
                        
                        CreateExcelArrowLine(startColumn, startRow, ColumnRatio, RowRatio, rc, rect.Left + rect.Width / 2 - 5, rect.Left + rect.Width / 2 - 5, 0, rect.Top, Colors.Black);
                    }
                }
            }

            // 1도크인 경우
            if (res.Code == "1")
            {
                ShowItemToItemInterval(startColumn, startRow, ColumnRatio, RowRatio, rc, topList, middleList, res);
                ShowItemToItemInterval(startColumn, startRow, ColumnRatio, RowRatio, rc, middleList, bottomList, res);
            }
            else
            {
                ShowItemToItemInterval(startColumn, startRow, ColumnRatio, RowRatio, rc, topList, bottomList, res);
            }
        }


        /// <summary>
        /// 사용자 추가 구조물 (상세, 실행)
        /// </summary>
        /// <param name="dockCode"></param>
        /// <param name="in_x_pos"></param>
        /// <param name="in_y_pos"></param>
        /// <param name="isNext"></param>
        /// <returns></returns>
        double GetChkAddItem(string dockCode, double in_x_pos, double in_y_pos, bool isNext)
        {
            double rtn_y_value = 0;

            int DockStartRow = 0;
            int DockStartColumn = 0;
            int DockEndRow = 0;
            double ColumnRatio = 0;
            double RowRatio = 0;

            if (_Model.BatchKind == DockBatchType.실행도크배치)
            {
                ExecDockPosInfo info = _ExecPosDic[dockCode];

                DockStartRow = info.DockStartRow;
                DockStartColumn = info.DockStartColumn;
                DockEndRow = info.DockEndRow;
                ColumnRatio = info.ColumnRatio;
                RowRatio = info.RowRatio;
            }
            else if(_Model.BatchKind == DockBatchType.상세도크배치)
            {
                DetailDockPosInfo info = _DetailPosDic[dockCode];

                DockStartRow = !isNext ? info.DockStartRow1 : info.DockStartRow2;
                DockStartColumn = !isNext ? info.DockStartColumn1 : info.DockStartColumn2;
                DockEndRow = !isNext ? info.DockEndRow1 : info.DockEndRow2;
                ColumnRatio = info.ColumnRatio;
                RowRatio = info.RowRatio;
            }

            // 사용자 추가 구조물
            DataTable dtUserAddItem = _Model.UserAddItems;
            // 도크 구조물
            DataTable dtAddItem = _Model.DockBatchAddItems;

            DataSet ds = new DataSet();
            if (dtAddItem != null)
                ds.Tables.Add(dtAddItem.Copy());
            if (dtUserAddItem != null)
                ds.Tables.Add(dtUserAddItem.Copy());

            if (ds.Tables.Count == 0)
                return 0;

            double x_pos, y_pos, width, height;

            //column 하나의 논리적크기는 2.info.RowRatiom, row하나의 논리적 크기는 info.RowRatiom
            Rect rect = _Excel.GetCellPosition(DockStartRow, DockStartColumn);

            foreach (DataTable dt in ds.Tables)
            {
                foreach (DataRow row in dt.Rows)
                {
                    // 게이트인 경우 엑셀로 표시
                    if (ConvertHelper.ToString(row["TG_GBN"]) == "GAT")
                        continue;

                    x_pos = ConvertHelper.ToDouble(row["POS_X"]) / 10;
                    y_pos = ConvertHelper.ToDouble(row["POS_Y"]) / 10;
                    width = ConvertHelper.ToDouble(row["CT_WIDTH"]) / 10;
                    height = ConvertHelper.ToDouble(row["CT_HEIGHT"]) / 10;
                    
                    int fromCol = DockStartColumn + (int)((x_pos / ColumnRatio)) - 1;
                    int fromRow = DockEndRow + (int)((y_pos + height) / RowRatio) - 1;
                    int toCol = DockStartColumn + (int)((x_pos + width) / ColumnRatio) - 1;
                    int toRow = DockEndRow;

                    int in_toCol = DockStartColumn + (int)(in_x_pos / ColumnRatio) - 1;
                    int in_toRow = DockStartRow + (int)(in_y_pos / RowRatio) - 1;
                    
                    // 수직 치수선이 구조물과 겹치는 경우
                    if (in_toCol > fromCol && in_toCol <= toCol && toRow == in_toRow)
                    {
                        return height;
                    }
                }
            }

            return rtn_y_value;
        }


        private void SetDefaultDetailPosData()
        {
            //1도크
            DetailDockPosInfo info = new DetailDockPosInfo("1");
            info.BatchTitleColumn1 = 2;  //상단 타이틀 위치
            info.BatchTitleRow1 = 3;
            info.BatchTitleColumn2 = 2; //하단 타이틀 위치 
            info.BatchTitleRow2 = 82;

            info.BatchJusuColumn1 = 98; // 상단 타이틀 옆 2회 주수 위치
            info.BatchJusuRow1 = 3;
            info.BatchJusuColumn2 = 98; //하단 타이틀 옆 2회 주수 위치
            info.BatchJusuRow2 = 82;

            info.ConfirmColumn1 = 170; // 상단 확정 위치
            info.ConfirmRow1 = 3;
            info.ConfirmColumn2 = 170; //하단 확정 위치
            info.ConfirmRow2 = 82;

            info.DockStartColumn1 = 5; // 상단 도크 시작
            info.DockStartRow1 = 8;
            info.DockStartColumn2 = 5; //하단 도크 시작
            info.DockStartRow2 = 87;

            info.ShipInfoRow1 = 77;  //상단 호선 번호 입력 위치
            info.ShipInfoRow2 = 156; // 하단

            info.LCPosition1 = new int[4] { 5, 13, 21, 29 };  // 상단 lc 칼럼 위치
            info.LCPosition2 = new int[4] { 5, 13, 21, 29 };  // 하단 lc 칼럼 

            info.FTPosition1 = new int[4] { 37, 45, 53, 61 };  //상단 ft 칼럼 위치
            info.FTPosition2 = new int[4] { 37, 45, 53, 61 };  //하단 ft 칼럼 위치

            info.ChangeColumn1 = 69; //상단 변경사항 칼럼 위치
            info.ChangeColumn2 = 69; //하단

            info.BigoColumn1 = 165;  //상단 비고 칼럼 위치
            info.BigoColumn2 = 165;  //하단

            info.ShipLineColumn1 = 5; // 상단호선 사이즈 라인 위치
            info.ShipLineRow1 = 6;

            info.ShipLineColumn2 = 5; //하단 호선 사이즈 라인 위치
            info.ShipLineRow2 = 85;

            info.RowRatio = 2;       // cell당 비율
            info.ColumnRatio = 2.5;


            info.DockEndRow1 = 47;
            info.ShipSizeBottomLineColumn1 = 5;
            info.ShipSizeBottomLineRow1 = 74;

            info.ShipHoldBottomLineColunm1 = 5;
            info.ShipHoldBottomLineRow1 = 75;

            info.DockEndRow2 = 126;
            info.ShipSizeBottomLineColumn2 = 5;
            info.ShipSizeBottomLineRow2 = 153;

            info.ShipHoldBottomLineColunm2 = 5;
            info.ShipHoldBottomLineRow2 = 154;

            _DetailPosDic.Add("1", info);

            //2도크

            info = new DetailDockPosInfo("2");
            info.BatchTitleColumn1 = 3;  //상단 타이틀 위치
            info.BatchTitleRow1 = 3;
            info.BatchTitleColumn2 = 3; //하단 타이틀 위치 
            info.BatchTitleRow2 = 60;

            info.BatchJusuColumn1 = 102; // 상단 타이틀 옆 2회 주수 위치
            info.BatchJusuRow1 = 3;
            info.BatchJusuColumn2 = 102; //하단 타이틀 옆 2회 주수 위치
            info.BatchJusuRow2 = 60;

            info.ConfirmColumn1 = 209; // 상단 확정 위치
            info.ConfirmRow1 = 3;
            info.ConfirmColumn2 = 209; //하단 확정 위치
            info.ConfirmRow2 = 60;

            info.DockStartColumn1 = 6; // 상단 도크 시작
            info.DockStartRow1 = 8;
            info.DockStartColumn2 = 6; //하단 도크 시작
            info.DockStartRow2 = 65;

            info.ShipInfoRow1 = 55;  //상단 호선 번호 입력 위치
            info.ShipInfoRow2 = 112; // 하단

            info.LCPosition1 = new int[4] { 6, 14, 22, 30 };  // 상단 lc 칼럼 위치
            info.LCPosition2 = new int[4] { 6, 14, 22, 30 };  // 하단 lc 칼럼 

            info.FTPosition1 = new int[4] { 38, 46, 54, 62 };  //상단 ft 칼럼 위치
            info.FTPosition2 = new int[4] { 38, 46, 54, 62 };  //하단 ft 칼럼 위치

            info.ChangeColumn1 = 70; //상단 변경사항 칼럼 위치
            info.ChangeColumn2 = 70; //하단

            info.BigoColumn1 = 186;  //상단 비고 칼럼 위치
            info.BigoColumn2 = 186;  //하단

            info.ShipLineColumn1 = 6; // 상단호선 사이즈 라인 위치
            info.ShipLineRow1 = 6;

            info.ShipLineColumn2 = 6; //하단 호선 사이즈 라인 위치
            info.ShipLineRow2 = 62;

            info.RowRatio = 2;       // cell당 비율
            info.ColumnRatio = 2.5;

            info.DockEndRow1 = 47;
            info.ShipSizeBottomLineColumn1 = 6;
            info.ShipSizeBottomLineRow1 = 52;

            info.ShipHoldBottomLineColunm1 = 6;
            info.ShipHoldBottomLineRow1 = 53;

            info.DockEndRow2 = 104;
            info.ShipSizeBottomLineColumn2 = 6;
            info.ShipSizeBottomLineRow2 = 109;

            info.ShipHoldBottomLineColunm2 = 6;
            info.ShipHoldBottomLineRow2 = 110;

            _DetailPosDic.Add("2", info);

            //3도크

            info = new DetailDockPosInfo("3");
            info.BatchTitleColumn1 = 3;  //상단 타이틀 위치
            info.BatchTitleRow1 = 3;
            info.BatchTitleColumn2 = 3; //하단 타이틀 위치 
            info.BatchTitleRow2 = 66;

            info.BatchJusuColumn1 = 70; // 상단 타이틀 옆 2회 주수 위치
            info.BatchJusuRow1 = 3;
            info.BatchJusuColumn2 = 70; //하단 타이틀 옆 2회 주수 위치
            info.BatchJusuRow2 = 66;

            info.ConfirmColumn1 = 145; // 상단 확정 위치
            info.ConfirmRow1 = 3;
            info.ConfirmColumn2 = 145; //하단 확정 위치
            info.ConfirmRow2 = 66;

            info.DockStartColumn1 = 7; // 상단 도크 시작
            info.DockStartRow1 = 8;
            info.DockStartColumn2 = 7; //하단 도크 시작
            info.DockStartRow2 = 71;

            info.ShipInfoRow1 = 61;  //상단 호선 번호 입력 위치
            info.ShipInfoRow2 = 124; // 하단

            info.LCPosition1 = new int[4] { 7, 13, 19, 25 };  // 상단 lc 칼럼 위치
            info.LCPosition2 = new int[4] { 7, 13, 19, 25 };  // 하단 lc 칼럼 

            info.FTPosition1 = new int[4] { 31, 37, 43, 49 };  //상단 ft 칼럼 위치
            info.FTPosition2 = new int[4] { 31, 37, 43, 49 };  //하단 ft 칼럼 위치

            info.ChangeColumn1 = 55; //상단 변경사항 칼럼 위치
            info.ChangeColumn2 = 55; //하단

            info.BigoColumn1 = 127;  //상단 비고 칼럼 위치
            info.BigoColumn2 = 127;  //하단

            info.ShipLineColumn1 = 7; // 상단호선 사이즈 라인 위치
            info.ShipLineRow1 = 5;

            info.ShipLineColumn2 = 7; //하단 호선 사이즈 라인 위치
            info.ShipLineRow2 = 68;

            info.RowRatio = 2;       // cell당 비율
            info.ColumnRatio = 5;


            info.DockEndRow1 = 53;
            info.ShipSizeBottomLineColumn1 = 7;
            info.ShipSizeBottomLineRow1 = 58;

            info.ShipHoldBottomLineColunm1 = 7;
            info.ShipHoldBottomLineRow1 = 59;

            info.DockEndRow2 = 116;
            info.ShipSizeBottomLineColumn2 = 7;
            info.ShipSizeBottomLineRow2 = 121;

            info.ShipHoldBottomLineColunm2 = 7;
            info.ShipHoldBottomLineRow2 = 122;

            _DetailPosDic.Add("3", info);


            //4도크

            info = new DetailDockPosInfo("4");
            info.BatchTitleColumn1 = 5;  //상단 타이틀 위치
            info.BatchTitleRow1 = 3;
            info.BatchTitleColumn2 = 5; //하단 타이틀 위치 
            info.BatchTitleRow2 = 52;

            info.BatchJusuColumn1 = 90; // 상단 타이틀 옆 2회 주수 위치
            info.BatchJusuRow1 = 3;
            info.BatchJusuColumn2 = 90; //하단 타이틀 옆 2회 주수 위치
            info.BatchJusuRow2 = 52;

            info.ConfirmColumn1 = 158; // 상단 확정 위치
            info.ConfirmRow1 = 3;
            info.ConfirmColumn2 = 158; //하단 확정 위치
            info.ConfirmRow2 = 52;

            info.DockStartColumn1 = 13; // 상단 도크 시작
            info.DockStartRow1 = 8;
            info.DockStartColumn2 = 13; //하단 도크 시작
            info.DockStartRow2 = 57;

            info.ShipInfoRow1 = 47;  //상단 호선 번호 입력 위치
            info.ShipInfoRow2 = 96; // 하단

            info.LCPosition1 = new int[2] { 15, 25 };  // 상단 lc 칼럼 위치
            info.LCPosition2 = new int[2] { 15, 25 };  // 하단 lc 칼럼 

            info.FTPosition1 = new int[2] { 35, 45 };  //상단 ft 칼럼 위치
            info.FTPosition2 = new int[2] { 35, 45 };  //하단 ft 칼럼 위치

            info.ChangeColumn1 = 55; //상단 변경사항 칼럼 위치
            info.ChangeColumn2 = 55; //하단

            info.BigoColumn1 = 134;  //상단 비고 칼럼 위치
            info.BigoColumn2 = 134;  //하단

            info.ShipLineColumn1 = 13; // 상단호선 사이즈 라인 위치
            info.ShipLineRow1 = 6;

            info.ShipLineColumn2 = 13; //하단 호선 사이즈 라인 위치
            info.ShipLineRow2 = 54;

            info.RowRatio = 2;       // cell당 비율
            info.ColumnRatio = 2.5;


            info.DockEndRow1 = 40;
            info.ShipSizeBottomLineColumn1 = 13;
            info.ShipSizeBottomLineRow1 = 44;

            info.ShipHoldBottomLineColunm1 = 13;
            info.ShipHoldBottomLineRow1 = 45;

            info.DockEndRow2 = 89;
            info.ShipSizeBottomLineColumn2 = 13;
            info.ShipSizeBottomLineRow2 = 93;

            info.ShipHoldBottomLineColunm2 = 13;
            info.ShipHoldBottomLineRow2 = 94;

            _DetailPosDic.Add("4", info);

            //5도크

            info = new DetailDockPosInfo("5");
            info.BatchTitleColumn1 = 5;  //상단 타이틀 위치
            info.BatchTitleRow1 = 3;
            info.BatchTitleColumn2 = 5; //하단 타이틀 위치 
            info.BatchTitleRow2 = 52;

            info.BatchJusuColumn1 = 90; // 상단 타이틀 옆 2회 주수 위치
            info.BatchJusuRow1 = 3;
            info.BatchJusuColumn2 = 90; //하단 타이틀 옆 2회 주수 위치
            info.BatchJusuRow2 = 52;

            info.ConfirmColumn1 = 158; // 상단 확정 위치
            info.ConfirmRow1 = 3;
            info.ConfirmColumn2 = 158; //하단 확정 위치
            info.ConfirmRow2 = 52;

            info.DockStartColumn1 = 13; // 상단 도크 시작
            info.DockStartRow1 = 8;
            info.DockStartColumn2 = 13; //하단 도크 시작
            info.DockStartRow2 = 57;

            info.ShipInfoRow1 = 47;  //상단 호선 번호 입력 위치
            info.ShipInfoRow2 = 96; // 하단

            info.LCPosition1 = new int[2] { 15, 25 };  // 상단 lc 칼럼 위치
            info.LCPosition2 = new int[2] { 15, 25 };  // 하단 lc 칼럼 

            info.FTPosition1 = new int[2] { 35, 45 };  //상단 ft 칼럼 위치
            info.FTPosition2 = new int[2] { 35, 45 };  //하단 ft 칼럼 위치

            info.ChangeColumn1 = 55; //상단 변경사항 칼럼 위치
            info.ChangeColumn2 = 55; //하단

            info.BigoColumn1 = 134;  //상단 비고 칼럼 위치
            info.BigoColumn2 = 134;  //하단

            info.ShipLineColumn1 = 13; // 상단호선 사이즈 라인 위치
            info.ShipLineRow1 = 6;

            info.ShipLineColumn2 = 13; //하단 호선 사이즈 라인 위치
            info.ShipLineRow2 = 54;

            info.RowRatio = 2;       // cell당 비율
            info.ColumnRatio = 2.5;


            info.DockEndRow1 = 40;
            info.ShipSizeBottomLineColumn1 = 13;
            info.ShipSizeBottomLineRow1 = 44;

            info.ShipHoldBottomLineColunm1 = 13;
            info.ShipHoldBottomLineRow1 = 45;

            info.DockEndRow2 = 89;
            info.ShipSizeBottomLineColumn2 = 13;
            info.ShipSizeBottomLineRow2 = 93;

            info.ShipHoldBottomLineColunm2 = 13;
            info.ShipHoldBottomLineRow2 = 94;

            _DetailPosDic.Add("5", info);

            //6도크

            info = new DetailDockPosInfo("6");
            info.BatchTitleColumn1 = 5;  //상단 타이틀 위치
            info.BatchTitleRow1 = 3;
            info.BatchTitleColumn2 = 5; //하단 타이틀 위치 
            info.BatchTitleRow2 = 42;

            info.BatchJusuColumn1 = 89; // 상단 타이틀 옆 2회 주수 위치
            info.BatchJusuRow1 = 3;
            info.BatchJusuColumn2 = 89; //하단 타이틀 옆 2회 주수 위치
            info.BatchJusuRow2 = 42;

            info.ConfirmColumn1 = 109; // 상단 확정 위치
            info.ConfirmRow1 = 3;
            info.ConfirmColumn2 = 109; //하단 확정 위치
            info.ConfirmRow2 = 42;

            info.DockStartColumn1 = 18; // 상단 도크 시작
            info.DockStartRow1 = 8;
            info.DockStartColumn2 = 18; //하단 도크 시작
            info.DockStartRow2 = 47;

            info.ShipInfoRow1 = 37;  //상단 호선 번호 입력 위치
            info.ShipInfoRow2 = 76; // 하단

            info.LCPosition1 = new int[2] { 14, 24 };  // 상단 lc 칼럼 위치
            info.LCPosition2 = new int[2] { 14, 24 };  // 하단 lc 칼럼 

            info.FTPosition1 = new int[2] { 34, 44 };  //상단 ft 칼럼 위치
            info.FTPosition2 = new int[2] { 34, 44 };  //하단 ft 칼럼 위치

            info.ChangeColumn1 = 54; //상단 변경사항 칼럼 위치
            info.ChangeColumn2 = 54; //하단

            info.BigoColumn1 = 97;  //상단 비고 칼럼 위치 
            info.BigoColumn2 = 97;  //하단

            info.ShipLineColumn1 = 18; // 상단호선 사이즈 라인 위치
            info.ShipLineRow1 = 6;

            info.ShipLineColumn2 = 18; //하단 호선 사이즈 라인 위치
            info.ShipLineRow2 = 45;

            info.RowRatio = 2;       // cell당 비율
            info.ColumnRatio = 2.5;

            info.DockEndRow1 = 30;
            info.ShipSizeBottomLineColumn1 = 18;
            info.ShipSizeBottomLineRow1 = 34;

            info.ShipHoldBottomLineColunm1 = 18;
            info.ShipHoldBottomLineRow1 = 35;

            info.DockEndRow2 = 69;
            info.ShipSizeBottomLineColumn2 = 18;
            info.ShipSizeBottomLineRow2 = 73;

            info.ShipHoldBottomLineColunm2 = 18;
            info.ShipHoldBottomLineRow2 = 74;

            _DetailPosDic.Add("6", info);

            //7도크

            info = new DetailDockPosInfo("7");
            info.BatchTitleColumn1 = 5;  //상단 타이틀 위치
            info.BatchTitleRow1 = 3;
            info.BatchTitleColumn2 = 5; //하단 타이틀 위치 
            info.BatchTitleRow2 = 39;

            info.BatchJusuColumn1 = 89; // 상단 타이틀 옆 2회 주수 위치
            info.BatchJusuRow1 = 3;
            info.BatchJusuColumn2 = 89; //하단 타이틀 옆 2회 주수 위치
            info.BatchJusuRow2 = 39;

            info.ConfirmColumn1 = 109; // 상단 확정 위치
            info.ConfirmRow1 = 3;
            info.ConfirmColumn2 = 109; //하단 확정 위치
            info.ConfirmRow2 = 39;

            info.DockStartColumn1 = 29; // 상단 도크 시작
            info.DockStartRow1 = 8;
            info.DockStartColumn2 = 29; //하단 도크 시작
            info.DockStartRow2 = 44;

            info.ShipInfoRow1 = 34;  //상단 호선 번호 입력 위치
            info.ShipInfoRow2 = 70; // 하단

            info.LCPosition1 = new int[2] { 14, 24 };  // 상단 lc 칼럼 위치
            info.LCPosition2 = new int[2] { 14, 24 };  // 하단 lc 칼럼 

            info.FTPosition1 = new int[2] { 34, 44 };  //상단 ft 칼럼 위치
            info.FTPosition2 = new int[2] { 34, 44 };  //하단 ft 칼럼 위치

            info.ChangeColumn1 = 54; //상단 변경사항 칼럼 위치
            info.ChangeColumn2 = 54; //하단

            info.BigoColumn1 = 97;  //상단 비고 칼럼 위치
            info.BigoColumn2 = 97;  //하단

            info.ShipLineColumn1 = 29; // 상단호선 사이즈 라인 위치
            info.ShipLineRow1 = 4;

            info.ShipLineColumn2 = 29; //하단 호선 사이즈 라인 위치
            info.ShipLineRow2 = 40;

            info.RowRatio = 1;       // cell당 비율
            info.ColumnRatio = 2.5;


            info.DockEndRow1 = 27;
            info.ShipSizeBottomLineColumn1 = 29;
            info.ShipSizeBottomLineRow1 = 31;

            info.ShipHoldBottomLineColunm1 = 29;
            info.ShipHoldBottomLineRow1 = 32;

            info.DockEndRow2 = 63;
            info.ShipSizeBottomLineColumn2 = 29;
            info.ShipSizeBottomLineRow2 = 67;

            info.ShipHoldBottomLineColunm2 = 29;
            info.ShipHoldBottomLineRow2 = 68;

            _DetailPosDic.Add("7", info);

            //8도크

            info = new DetailDockPosInfo("8");
            info.BatchTitleColumn1 = 5;  //상단 타이틀 위치
            info.BatchTitleRow1 = 3;
            info.BatchTitleColumn2 = 5; //하단 타이틀 위치 
            info.BatchTitleRow2 = 54;

            info.BatchJusuColumn1 = 107; // 상단 타이틀 옆 2회 주수 위치
            info.BatchJusuRow1 = 3;
            info.BatchJusuColumn2 = 107; //하단 타이틀 옆 2회 주수 위치
            info.BatchJusuRow2 = 54;

            info.ConfirmColumn1 = 188; // 상단 확정 위치
            info.ConfirmRow1 = 3;
            info.ConfirmColumn2 = 188; //하단 확정 위치
            info.ConfirmRow2 = 54;

            info.DockStartColumn1 = 13; // 상단 도크 시작
            info.DockStartRow1 = 7;
            info.DockStartColumn2 = 13; //하단 도크 시작
            info.DockStartRow2 = 58;

            info.ShipInfoRow1 = 49;  //상단 호선 번호 입력 위치
            info.ShipInfoRow2 = 100; // 하단

            info.LCPosition1 = new int[2] { 15, 25 };  // 상단 lc 칼럼 위치
            info.LCPosition2 = new int[2] { 15, 25 };  // 하단 lc 칼럼 

            info.FTPosition1 = new int[2] { 35, 45 };  //상단 ft 칼럼 위치
            info.FTPosition2 = new int[2] { 35, 45 };  //하단 ft 칼럼 위치

            info.ChangeColumn1 = 55; //상단 변경사항 칼럼 위치
            info.ChangeColumn2 = 55; //하단

            info.BigoColumn1 = 163;  //상단 비고 칼럼 위치
            info.BigoColumn2 = 163;  //하단

            info.ShipLineColumn1 = 13; // 상단호선 사이즈 라인 위치
            info.ShipLineRow1 = 5;

            info.ShipLineColumn2 = 13; //하단 호선 사이즈 라인 위치
            info.ShipLineRow2 = 56;

            info.RowRatio = 2;       // cell당 비율
            info.ColumnRatio = 2.5;

            info.DockEndRow1 = 41;
            info.ShipSizeBottomLineColumn1 = 13;
            info.ShipSizeBottomLineRow1 = 46;

            info.ShipHoldBottomLineColunm1 = 13;
            info.ShipHoldBottomLineRow1 = 47;

            info.DockEndRow2 = 92;
            info.ShipSizeBottomLineColumn2 = 13;
            info.ShipSizeBottomLineRow2 = 97;

            info.ShipHoldBottomLineColunm2 = 13;
            info.ShipHoldBottomLineRow2 = 98;

            _DetailPosDic.Add("8", info);

            //9도크

            info = new DetailDockPosInfo("9");
            info.BatchTitleColumn1 = 5;  //상단 타이틀 위치
            info.BatchTitleRow1 = 3;
            info.BatchTitleColumn2 = 5; //하단 타이틀 위치 
            info.BatchTitleRow2 = 54;

            info.BatchJusuColumn1 = 107; // 상단 타이틀 옆 2회 주수 위치
            info.BatchJusuRow1 = 3;
            info.BatchJusuColumn2 = 107; //하단 타이틀 옆 2회 주수 위치
            info.BatchJusuRow2 = 54;

            info.ConfirmColumn1 = 188; // 상단 확정 위치
            info.ConfirmRow1 = 3;
            info.ConfirmColumn2 = 188; //하단 확정 위치
            info.ConfirmRow2 = 54;

            info.DockStartColumn1 = 13; // 상단 도크 시작
            info.DockStartRow1 = 7;
            info.DockStartColumn2 = 13; //하단 도크 시작
            info.DockStartRow2 = 58;

            info.ShipInfoRow1 = 49;  //상단 호선 번호 입력 위치
            info.ShipInfoRow2 = 100; // 하단

            info.LCPosition1 = new int[2] { 15, 25 };  // 상단 lc 칼럼 위치
            info.LCPosition2 = new int[2] { 15, 25 };  // 하단 lc 칼럼 

            info.FTPosition1 = new int[2] { 35, 45 };  //상단 ft 칼럼 위치
            info.FTPosition2 = new int[2] { 35, 45 };  //하단 ft 칼럼 위치

            info.ChangeColumn1 = 55; //상단 변경사항 칼럼 위치
            info.ChangeColumn2 = 55; //하단

            info.BigoColumn1 = 163;  //상단 비고 칼럼 위치
            info.BigoColumn2 = 163;  //하단

            info.ShipLineColumn1 = 13; // 상단호선 사이즈 라인 위치
            info.ShipLineRow1 = 5;

            info.ShipLineColumn2 = 13; //하단 호선 사이즈 라인 위치
            info.ShipLineRow2 = 56;

            info.RowRatio = 2;       // cell당 비율
            info.ColumnRatio = 2.5;

            info.DockEndRow1 = 41;
            info.ShipSizeBottomLineColumn1 = 13;
            info.ShipSizeBottomLineRow1 = 46;

            info.ShipHoldBottomLineColunm1 = 13;
            info.ShipHoldBottomLineRow1 = 47;

            info.DockEndRow2 = 92;
            info.ShipSizeBottomLineColumn2 = 13;
            info.ShipSizeBottomLineRow2 = 97;

            info.ShipHoldBottomLineColunm2 = 13;
            info.ShipHoldBottomLineRow2 = 98;

            _DetailPosDic.Add("9", info);

            //H도크

            info = new DetailDockPosInfo("H");
            info.BatchTitleColumn1 = 3;  //상단 타이틀 위치
            info.BatchTitleRow1 = 3;
            info.BatchTitleColumn2 = 3; //하단 타이틀 위치 
            info.BatchTitleRow2 = 75;

            info.BatchJusuColumn1 = 107; // 상단 타이틀 옆 2회 주수 위치
            info.BatchJusuRow1 = 3;
            info.BatchJusuColumn2 = 107; //하단 타이틀 옆 2회 주수 위치
            info.BatchJusuRow2 = 75;

            info.ConfirmColumn1 = 209; // 상단 확정 위치
            info.ConfirmRow1 = 3;
            info.ConfirmColumn2 = 209; //하단 확정 위치
            info.ConfirmRow2 = 75;

            info.DockStartColumn1 = 6; // 상단 도크 시작
            info.DockStartRow1 = 7;
            info.DockStartColumn2 = 6; //하단 도크 시작
            info.DockStartRow2 = 79;

            info.ShipInfoRow1 = 70;  //상단 호선 번호 입력 위치
            info.ShipInfoRow2 = 142; // 하단

            info.LCPosition1 = new int[4] { 6, 14, 22, 30 };  // 상단 lc 칼럼 위치
            info.LCPosition2 = new int[4] { 6, 14, 22, 30 };  // 하단 lc 칼럼 

            info.FTPosition1 = new int[4] { 38, 46, 54, 62 };  //상단 ft 칼럼 위치
            info.FTPosition2 = new int[4] { 38, 46, 54, 62 };  //하단 ft 칼럼 위치

            info.ChangeColumn1 = 70; //상단 변경사항 칼럼 위치
            info.ChangeColumn2 = 70; //하단

            info.BigoColumn1 = 186;  //상단 비고 칼럼 위치
            info.BigoColumn2 = 186;  //하단

            info.ShipLineColumn1 = 6; // 상단호선 사이즈 라인 위치
            info.ShipLineRow1 = 5;

            info.ShipLineColumn2 = 6; //하단 호선 사이즈 라인 위치
            info.ShipLineRow2 = 77;

            info.RowRatio = 2.05;       // cell당 비율
            info.ColumnRatio = 2.45;

            info.DockEndRow1 = 61;
            info.ShipSizeBottomLineColumn1 = 6;
            info.ShipSizeBottomLineRow1 = 67;

            info.ShipHoldBottomLineColunm1 = 6;
            info.ShipHoldBottomLineRow1 = 68;

            info.DockEndRow2 = 133;
            info.ShipSizeBottomLineColumn2 = 6;
            info.ShipSizeBottomLineRow2 = 139;

            info.ShipHoldBottomLineColunm2 = 6;
            info.ShipHoldBottomLineRow2 = 140;

            _DetailPosDic.Add("H", info);
        }
        
        #region 구간 출력

        private int GetExcelSheetCount(string startYear, int startBatch, string endYear, int endBatch)
        {
            int sum = 1;
            string startBatchNo = string.Format("{0}{1:D02}", startYear, startBatch);
            string endBatchNo = string.Format("{0}{1:D02}", endYear, endBatch);
            string curBatchNo = startBatchNo;

            while (curBatchNo != endBatchNo)
            {
                sum++;
                curBatchNo = _Model.GetNextBatchNo(curBatchNo);

                if (string.IsNullOrEmpty(curBatchNo))
                    return 0;
            }

            return (sum + 1) / 2;
        }

        private string GetNewSheetName(string startYear, int startBatch, int offset, int order)
        {
            string startBatchNo = string.Format("{0}{1:D02}", startYear, startBatch);
            for (int index = 0; index < offset; index++)
            {
                startBatchNo = _Model.GetNextBatchNo(startBatchNo);
            }
            int batch = int.Parse(startBatchNo.Substring(4, 2));
            return string.Format("{0}-{1}배치({2})", batch, batch + 1, order);
        }

        private string GetDockBatchNo(string startYear, int startBatch, int offset)
        {
            string startBatchNo = string.Format("{0}{1:D02}", startYear, startBatch);
            for (int index = 0; index < offset; index++)
            {
                startBatchNo = _Model.GetNextBatchNo(startBatchNo);
            }
            return startBatchNo;
        }

        #endregion
    }

    

    /// <summary>
    /// 상세용 엑셀파일 위치정보(엑셀파일 참조)
    /// </summary>
    public class DetailDockPosInfo
    {
        public DetailDockPosInfo(string dockCode)
        {
            DockCode = dockCode;
        }

        public string DockCode;
        public int BatchTitleColumn1;  //상단 배치 타이틀 시작 칼럼
        public int BatchTitleRow1;     //상단 배치 타이틀 시작 로우
        public int BatchTitleColumn2;  //하단 배치 타이틀 시작 칼럼
        public int BatchTitleRow2;     //하단 배치 타이틀 시작 로우

        public int BatchJusuColumn1;   //상단 주수 정보 시작컬럼
        public int BatchJusuRow1;
        public int BatchJusuColumn2;   //하단 주수 정보 시작컬럼
        public int BatchJusuRow2;

        public int ConfirmColumn1;     //상단 확정 정보 시작컬럼
        public int ConfirmRow1;
        public int ConfirmColumn2;     //하단 확정 정보 시작컬럼
        public int ConfirmRow2;

        public int ShipInfoRow1;       //상단 호선 정보 시작 로우
        public int ShipInfoRow2;       //하단 호선 정보 시작 로우

        public int[] LCPosition1;      //상단 L/C 정보 시작 칼럼   
        public int[] LCPosition2;      //하단 L/C 정보 시작 칼럼   

        public int[] FTPosition1;      //상단 F/T 정보 시작 칼럼   
        public int[] FTPosition2;      //하단 F/T 정보 시작 칼럼    

        public int ChangeColumn1;      //상단 변경사항 시작 칼럼 
        public int ChangeColumn2;      //하단 변경사항 시작 칼럼

        public int BigoColumn1;        //상단 비고 시작 칼럼      
        public int BigoColumn2;        //하단 비고 시작 칼럼

        public int DockStartColumn1;    //상단 도크 시작 칼럼
        public int DockStartRow1;       //상단 도크 시작 로우

        public int DockStartColumn2;    //상단 도크 시작 칼럼 
        public int DockStartRow2;       //상단 도크 시작 로우

        public double ColumnRatio;      //도크 수평 비율(10미터당 엑셀 로우수가 4개면 2.5) 
        public double RowRatio;         //도크 수직 비율(10미터당 엑셀 로우수가 4개면 2.5)

        public int ShipLineColumn1;     //상단 호선 수치선 시작 컬럼(TOP)
        public int ShipLineRow1;        //상단 호선 수치선 시작 로우(TOP)

        public int ShipLineColumn2;     //하단 호선 수치선 시작 컬럼
        public int ShipLineRow2;        //하단 호선 수치선 시작 로우

        public int DockEndRow1;               //상단 도크 마지막 로우
        public int ShipSizeBottomLineColumn1; //상단 호선 수치선 시작 컬럼(BOTTOM) 
        public int ShipSizeBottomLineRow1;    //상단 호선 수치선 시작 로우(BOTTOM)

        public int ShipHoldBottomLineColunm1; //상단 홀드라인 시작 컬럼(BOTTOM)  
        public int ShipHoldBottomLineRow1;    //상단 홀드라인 시작 로우(BOTTOM)

        public int DockEndRow2;               //하단 도크 마지막 로우
        public int ShipSizeBottomLineColumn2; //하단 호선 수치선 시작 컬럼(BOTTOM) 
        public int ShipSizeBottomLineRow2;    //하단 호선 수치선 시작 로우(BOTTOM)

        public int ShipHoldBottomLineColunm2; //하단 홀드라인 시작 컬럼(BOTTOM)  
        public int ShipHoldBottomLineRow2;    //하단 홀드라인 시작 로우(BOTTOM)
    }



    //기본용 엑셀파일 위치정보(엑셀파일 참조)
    public class BasicDockPosInfo
    {
        public BasicDockPosInfo(string dockCode)
        {
            _DockCode = dockCode;
        }

        #region properties

        private string _DockCode;
        public string DockCode
        {
            get { return _DockCode; }
            set { _DockCode = value; }
        }

        public int ShipInfoRow { get; set; }

        private int[] _ShipInfoPositions;
        public int[] ShipInfoPositions
        {
            get { return _ShipInfoPositions; }
            set { _ShipInfoPositions = value; }
        }

        private int _DockStartRow;
        public int DockStartRow
        {
            get { return _DockStartRow; }
            set { _DockStartRow = value; }
        }

        public int DockEndRow;

        private int _DockStartColumn;
        public int DockStartColumn
        {
            get { return _DockStartColumn; }
            set { _DockStartColumn = value; }
        }

        private int _ShipSizeLineColumn;
        public int ShipSizeLineColumn
        {
            get { return _ShipSizeLineColumn; }
            set { _ShipSizeLineColumn = value; }
        }

        private int _ShipSizeLineRow;
        public int ShipSizeLineRow
        {
            get { return _ShipSizeLineRow; }
            set { _ShipSizeLineRow = value; }
        }

        public int ShipSizeBottomLineColumn;
        public int ShipSizeBottomLineRow;

        private double _RowRatio;
        public double RowRatio
        {
            get { return _RowRatio; }
            set { _RowRatio = value; }
        }

        private double _ColumnRatio;
        public double ColumnRatio
        {
            get { return _ColumnRatio; }
            set { _ColumnRatio = value; }
        }

        #endregion
    }
}
