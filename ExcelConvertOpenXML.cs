using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml;
using A = DocumentFormat.OpenXml.Drawing;
using Xdr = DocumentFormat.OpenXml.Drawing.Spreadsheet;
using System.Windows;
using System.Windows.Media;
using System.ComponentModel;
using DocumentFormat.OpenXml.Drawing;
using Drawing = DocumentFormat.OpenXml.Spreadsheet.Drawing;

namespace HHI.SHP.PS002.DCK.Excel
{
    public class ExcelConvertOpenXML
    {
        #region members

        private SpreadsheetDocument _SheetDocument;
        private Worksheet _Worksheet;
        private SharedStringTablePart _StringTable;

        private const Int64 _ColumnCont = 100000;
        private const Int64 _RowCont = 10000;

        // 도형 ID와 이름을 관리하기 위한 클래스 변수를 선언
        private uint currentShapeId = 1000; // 시작 ID 값
        private int lineCounter = 1; // 라인 카운터
        #endregion
        public ExcelConvertOpenXML()
        {
        }

        #region public methods

        public bool Open(string fileName)
        {
            Release();
            if (File.Exists(fileName) == false) return false;

            try
            {
                _SheetDocument = SpreadsheetDocument.Open(fileName, true);
                _StringTable = GetSharedStringTablePart(_SheetDocument);
                _Worksheet = GetWorkSheet(0);
            }
            catch (Exception e)
            {
            }
            return true;
        }

        public bool CopySheet(string sheetName, string clonedSheetName)
        {
            if (_SheetDocument == null) return false;

            WorkbookPart workbookPart = _SheetDocument.WorkbookPart;
            WorksheetPart sourceSheetPart = GetWorkSheetPart(workbookPart, sheetName);
            if (sourceSheetPart == null) return false;

            SpreadsheetDocument tempSheet = SpreadsheetDocument.Create(new MemoryStream(), _SheetDocument.DocumentType);
            WorkbookPart tempWorkbookPart = tempSheet.AddWorkbookPart();
            WorksheetPart tempWorksheetPart = tempWorkbookPart.AddPart<WorksheetPart>(sourceSheetPart);
            WorksheetPart clonedSheet = workbookPart.AddPart<WorksheetPart>(tempWorksheetPart);

            int numTableDefParts = sourceSheetPart.GetPartsCountOfType<TableDefinitionPart>();
            if (numTableDefParts != 0) FixupTableParts(clonedSheet, numTableDefParts);

            SheetViews sheetViews = clonedSheet.Worksheet.GetFirstChild<SheetViews>();
            SheetView sheetView = sheetViews.Elements<SheetView>().FirstOrDefault();
            sheetView.ZoomScale = 55;

            //CleanView(clonedSheet);

            Sheets sheets = workbookPart.Workbook.GetFirstChild<Sheets>();
            Sheet copiedSheet = new Sheet();
            copiedSheet.Name = clonedSheetName;
            copiedSheet.Id = workbookPart.GetIdOfPart(clonedSheet);
            copiedSheet.SheetId = (uint)sheets.ChildElements.Count + 1;
            
            sheets.Append(copiedSheet);

            workbookPart.Workbook.Save();
            
            return false;
        }        


        /// <summary>
        /// 워크시트 줌 스케일 설정
        /// </summary>
        /// <param name="sheetName"></param>
        /// <param name="zoomScale"></param>
        public void SetSheetZoomScale(string sheetName, UInt32Value zoomScale)
        {
            if (_SheetDocument == null)
                return;

            WorkbookPart workbookPart = _SheetDocument.WorkbookPart;
            WorksheetPart worksheetPart = (WorksheetPart)workbookPart.GetPartById(sheetName);

            if (worksheetPart == null)
                return;
            
            SheetViews sheetViews = worksheetPart.Worksheet.GetFirstChild<SheetViews>();
            if (sheetViews == null)
            {
                sheetViews = new SheetViews();
                worksheetPart.Worksheet.InsertAt(sheetViews, 0);
            }

            SheetView sheetView = sheetViews.Elements<SheetView>().FirstOrDefault();
            if (sheetView == null)
            {
                sheetView = new SheetView { WorkbookViewId = 0 };
                sheetViews.Append(sheetView);
            }

            sheetView.ZoomScale = zoomScale;
            worksheetPart.Worksheet.Save();
        }

        public string GetDefaultSheetName()
        {
            if (_SheetDocument == null) return string.Empty;
            Sheet sheet = GetSheet(0);
            return sheet.Name.Value;
        }

        public bool ChangeSheetName(string originName, string changeName)
        {
            Sheet st = GetSheet(originName);
            if (st == null) return false;
            st.Name = new StringValue(changeName);
            return true;
        }

        public bool SetWorkSheet(string sheetName)
        {
            Sheet sh = GetSheet(sheetName);
            if (sh == null) return false;

            WorksheetPart worksheetPart = (WorksheetPart)_SheetDocument.WorkbookPart.GetPartById(sh.Id);

            if (worksheetPart == null) return false;
            _Worksheet = worksheetPart.Worksheet;
            return true;
        }


        public void SetText(int row, int col, string text)
        {
            if (_Worksheet == null) return;
            string colName = GetExcelColName(col);
            Cell cell = GetCell(_Worksheet, colName, row);

            if (cell == null) return;

            int index = InsertSharedStringItem(text, _StringTable);
            cell.CellValue = new CellValue(index.ToString());
            cell.DataType = new EnumValue<CellValues>(CellValues.SharedString);

        }

        public void Save(string fileName)
        {
            if (_SheetDocument == null) return;
            _SheetDocument.WorkbookPart.Workbook.Save();
            _SheetDocument.Close();
        }

        public void Close()
        {
            if (_SheetDocument == null) return;
            _SheetDocument.Close();
        }

        public void Export(string originFileName, string targetFileName, ExcelFileFormat format)
        {
            Save(originFileName);

            ExcelConvert conv = new ExcelConvert();
            conv.Open(originFileName);
            conv.Save(targetFileName, format);
        }

        public void ExecuteExcel(string fileName)
        {
            FileInfo fi = new FileInfo(fileName);
            if (fi.Exists)
            {
                System.Diagnostics.Process.Start(fileName);
            }
        }


        public Rect GetCellPosition(int row, int col)
        {
            col++; // 1부터 시작한다.

            double left = 0, top = 0, width = 0, height = 0;
            List<Column> cols = _Worksheet.WorksheetPart.Worksheet.GetFirstChild<Columns>().Elements<Column>().ToList();
            for (int index = 0; index < cols.Count; index++)
            {
                if (cols[index].Width == null) continue;
                if (col >= cols[index].Min && col <= cols[index].Max)
                {
                    left += (col - cols[index].Min) * cols[index].Width;
                    width = cols[index].Width;
                    break;
                }
                left += (cols[index].Max - cols[index].Min + 1) * cols[index].Width;
            }

            List<Row> rows = _Worksheet.WorksheetPart.Worksheet.GetFirstChild<SheetData>().Elements<Row>().ToList();
            for (int index = 0; index < rows.Count; index++)
            {
                if (rows[index].Height == null) continue;
                if (row == index)
                {
                    height = rows[index].Height;
                    break;
                }
                top += rows[index].Height;
            }

            return new Rect(left, top, width, height);
        }

        public void CreateShape(int fromCol, int fromRow, double fromColOffset, double fromRowOffset,
            int toCol, int toRow, double toColOffset, double toRowOffset,
            string geometryString, System.Windows.Media.Color backColor, System.Windows.Media.Color outlineColor,
            double rotation)
        {
            if (_Worksheet.WorksheetPart.DrawingsPart == null) CreateDrawingPart();

            Xdr.TwoCellAnchor cellAnchor = CreateCellAnchor(fromCol, fromRow, fromColOffset, fromRowOffset,
                                                                        toCol, toRow, toColOffset, toRowOffset);
            A.PathList pathList1 = GetPathList(geometryString);

            A.CustomGeometry customGeometry1 = CreateCrustomGeomery();
            customGeometry1.Append(pathList1);

            A.SolidFill fill = CreateFillColor(backColor);

            A.Outline outline = CreateOutline(outlineColor);

            Xdr.ShapeProperties shapeProperties1 = new Xdr.ShapeProperties();
            A.Transform2D transform = new A.Transform2D();
            transform.Rotation = rotation == 0 ? 0 : 10800000;

            shapeProperties1.Append(transform);
            shapeProperties1.Append(customGeometry1);
            shapeProperties1.Append(fill);
            shapeProperties1.Append(outline);


            Xdr.Shape shape1 = CreateShape();
            shape1.Append(shapeProperties1);

            // Shape 오류 확인
            if (!CheckAndFixRequiredShapeElements(shape1).StartsWith("OK"))
            {

            }

            cellAnchor.Append(shape1);
            cellAnchor.Append(new Xdr.ClientData());

            // CellAnchor 오류 확인
            if (!ValidateTwoCellAnchor(cellAnchor).StartsWith("OK"))
            {

            }

            _Worksheet.WorksheetPart.DrawingsPart.WorksheetDrawing.Append(cellAnchor);

            // 검증
            CreateValidTwoCellAnchor(fromCol, fromColOffset, fromRow, fromRowOffset, toCol, toColOffset, toRow, toRowOffset);
        }

        public void CreateLine(int fromCol, int fromRow, double fromColOffset, double fromRowOffset,
            int toCol, int toRow, double toColOffset, double toRowOffset,
            System.Windows.Media.Color lineColor, LineDashValues style)
        {
            if (_Worksheet.WorksheetPart.DrawingsPart == null) CreateDrawingPart();

            bool isFlip = false;
            if (fromCol > toCol)
            {
                isFlip = true;
                int temp = fromCol;
                fromCol = toCol;
                toCol = temp;

                double dtemp = fromColOffset;
                fromColOffset = toColOffset;
                toColOffset = dtemp;
            }

            if (fromRow > toRow)
            {
                isFlip = true;
                int temp = fromRow;
                fromRow = toRow;
                toRow = temp;

                double dtemp = fromRowOffset;
                fromRowOffset = toRowOffset;
                toRowOffset = dtemp;
            }


            Xdr.TwoCellAnchor cellAnchor = new Xdr.TwoCellAnchor();
            Xdr.FromMarker fromMarker1 = new Xdr.FromMarker();
            Xdr.ColumnId columnId1 = new Xdr.ColumnId();
            columnId1.Text = (fromCol).ToString();
            Xdr.ColumnOffset columnOffset1 = new Xdr.ColumnOffset();
            columnOffset1.Text = ((int)(fromColOffset * _ColumnCont)).ToString(); //"1554687"; //
            Xdr.RowId rowId1 = new Xdr.RowId();
            rowId1.Text = (fromRow).ToString();
            Xdr.RowOffset rowOffset1 = new Xdr.RowOffset();
            rowOffset1.Text = ((int)(fromRowOffset * _RowCont)).ToString();

            fromMarker1.Append(columnId1);
            fromMarker1.Append(columnOffset1);
            fromMarker1.Append(rowId1);
            fromMarker1.Append(rowOffset1);

            Xdr.ToMarker toMarker1 = new Xdr.ToMarker();
            Xdr.ColumnId columnId2 = new Xdr.ColumnId();
            columnId2.Text = toCol.ToString();
            Xdr.ColumnOffset columnOffset2 = new Xdr.ColumnOffset();
            columnOffset2.Text = ((int)(toColOffset * _ColumnCont)).ToString();
            Xdr.RowId rowId2 = new Xdr.RowId();
            rowId2.Text = toRow.ToString();
            Xdr.RowOffset rowOffset2 = new Xdr.RowOffset();
            rowOffset2.Text = ((int)(toRowOffset * _RowCont)).ToString();

            toMarker1.Append(columnId2);
            toMarker1.Append(columnOffset2);
            toMarker1.Append(rowId2);
            toMarker1.Append(rowOffset2);

            cellAnchor.Append(fromMarker1);
            cellAnchor.Append(toMarker1);


            Xdr.Shape shape3 = new Xdr.Shape() { Macro = "", TextLink = "" };

            Xdr.NonVisualShapeProperties nonVisualShapeProperties3 = new Xdr.NonVisualShapeProperties();
            Xdr.NonVisualDrawingProperties nonVisualDrawingProperties3 = new Xdr.NonVisualDrawingProperties()
            {
                Id = (UInt32Value)GetNextShapeId(),
                Name = GetNextLineName()
            };

            Xdr.NonVisualShapeDrawingProperties nonVisualShapeDrawingProperties3 = new Xdr.NonVisualShapeDrawingProperties();
            A.ShapeLocks shapeLocks3 = new A.ShapeLocks() { NoChangeShapeType = true };

            nonVisualShapeDrawingProperties3.Append(shapeLocks3);

            nonVisualShapeProperties3.Append(nonVisualDrawingProperties3);
            nonVisualShapeProperties3.Append(nonVisualShapeDrawingProperties3);

            Xdr.ShapeProperties shapeProperties3 = new Xdr.ShapeProperties() { BlackWhiteMode = A.BlackWhiteModeValues.Auto };

            A.Transform2D transform2D3 = new A.Transform2D() { VerticalFlip = isFlip };

            A.PresetGeometry presetGeometry2 = new A.PresetGeometry() { Preset = A.ShapeTypeValues.Line };
            A.AdjustValueList adjustValueList3 = new A.AdjustValueList();

            presetGeometry2.Append(adjustValueList3);
            A.NoFill noFill1 = new A.NoFill();

            A.Outline outline3 = CreateOutline(lineColor);
            A.PresetDash presetDash1 = new A.PresetDash() { Val = (A.PresetLineDashValues)style };

            //outline3.Append(solidFill5);
            outline3.Append(presetDash1);


            shapeProperties3.Append(transform2D3);
            shapeProperties3.Append(presetGeometry2);
            shapeProperties3.Append(noFill1);
            shapeProperties3.Append(outline3);

            shape3.Append(nonVisualShapeProperties3);
            shape3.Append(shapeProperties3);
            Xdr.ClientData clientData3 = new Xdr.ClientData();

            // Shape 오류 확인
            if (!CheckAndFixRequiredShapeElements(shape3).StartsWith("OK"))
            {

            }

            cellAnchor.Append(shape3);
            cellAnchor.Append(clientData3);
            
            // CellAnchor 오류 확인
            if (!ValidateTwoCellAnchor(cellAnchor).StartsWith("OK"))
            {

            }

            _Worksheet.WorksheetPart.DrawingsPart.WorksheetDrawing.Append(cellAnchor);

            // 검증
            CreateValidTwoCellAnchor(fromCol, fromColOffset, fromRow, fromRowOffset, toCol, toColOffset, toRow, toRowOffset);
        }

        /// <summary>
        /// HOLD 표기 문자
        /// </summary>
        /// <param name="fromCol"></param>
        /// <param name="fromRow"></param>
        /// <param name="fromColOffset"></param>
        /// <param name="fromRowOffset"></param>
        /// <param name="toCol"></param>
        /// <param name="toRow"></param>
        /// <param name="toColOffset"></param>
        /// <param name="toRowOffset"></param>
        /// <param name="text"></param>
        public void CreateDrawingText(int fromCol, int fromRow, double fromColOffset, double fromRowOffset,
            int toCol, int toRow, double toColOffset, double toRowOffset,
            string text, bool isExec, string shp_cod)
        {
            if (string.IsNullOrEmpty(text))
                return;

            if (_Worksheet.WorksheetPart.DrawingsPart == null) CreateDrawingPart();

            Xdr.TwoCellAnchor twoCellAnchor1 = new Xdr.TwoCellAnchor();

            Xdr.FromMarker fromMarker1 = new Xdr.FromMarker();
            Xdr.ColumnId columnId1 = new Xdr.ColumnId();
            columnId1.Text = (fromCol).ToString();
            Xdr.ColumnOffset columnOffset1 = new Xdr.ColumnOffset();
            columnOffset1.Text = ((int)(fromColOffset * _ColumnCont)).ToString();
            Xdr.RowId rowId1 = new Xdr.RowId();
            rowId1.Text = (fromRow - 1).ToString();
            Xdr.RowOffset rowOffset1 = new Xdr.RowOffset();
            rowOffset1.Text = ((int)(fromRowOffset * _RowCont)).ToString();

            fromMarker1.Append(columnId1);
            fromMarker1.Append(columnOffset1);
            fromMarker1.Append(rowId1);
            fromMarker1.Append(rowOffset1);

            Xdr.ToMarker toMarker1 = new Xdr.ToMarker();
            Xdr.ColumnId columnId2 = new Xdr.ColumnId();
            columnId2.Text = (text.Length <= 2 ? toCol : toCol + 1).ToString();
            Xdr.ColumnOffset columnOffset2 = new Xdr.ColumnOffset();
            columnOffset2.Text = ((int)(toColOffset * _ColumnCont)).ToString();
            Xdr.RowId rowId2 = new Xdr.RowId();
            rowId2.Text = toRow.ToString();
            Xdr.RowOffset rowOffset2 = new Xdr.RowOffset();
            rowOffset2.Text = ((int)(toRowOffset * _RowCont)).ToString();

            toMarker1.Append(columnId2);
            toMarker1.Append(columnOffset2);
            toMarker1.Append(rowId2);
            toMarker1.Append(rowOffset2);

            Xdr.Shape shape1 = new Xdr.Shape() { Macro = "", TextLink = "" };

            Xdr.NonVisualShapeProperties nonVisualShapeProperties1 = new Xdr.NonVisualShapeProperties();
            Xdr.NonVisualDrawingProperties nonVisualDrawingProperties1 = new Xdr.NonVisualDrawingProperties() { Id = (UInt32Value)1026U, Name = $"HoldText:{shp_cod}" };

            Xdr.NonVisualShapeDrawingProperties nonVisualShapeDrawingProperties1 = new Xdr.NonVisualShapeDrawingProperties() { TextBox = true };
            A.ShapeLocks shapeLocks1 = new A.ShapeLocks() { NoChangeArrowheads = true };

            nonVisualShapeDrawingProperties1.Append(shapeLocks1);

            nonVisualShapeProperties1.Append(nonVisualDrawingProperties1);
            nonVisualShapeProperties1.Append(nonVisualShapeDrawingProperties1);

            Xdr.ShapeProperties shapeProperties1 = new Xdr.ShapeProperties() { BlackWhiteMode = A.BlackWhiteModeValues.Auto };

            A.Transform2D transform2D1 = new A.Transform2D();
            A.Offset offset1 = new A.Offset() { X = 0L, Y = 0L };
            //A.Extents extents1 = new A.Extents() { Cx = 542925L, Cy = 209550L };
            A.Extents extents1 = new A.Extents() { Cx = 1209675L, Cy = 1009650L };

            transform2D1.Append(offset1);
            transform2D1.Append(extents1);

            A.PresetGeometry presetGeometry1 = new A.PresetGeometry() { Preset = A.ShapeTypeValues.Rectangle };
            A.AdjustValueList adjustValueList1 = new A.AdjustValueList();

            presetGeometry1.Append(adjustValueList1);
            
            #region 변경된 부분: 텍스트 박스 배경을 흰색, 테두리를 검은색으로 설정
            // 배경을 흰색으로 설정
            A.SolidFill fill1 = new A.SolidFill();
            A.RgbColorModelHex backgroundColor = new A.RgbColorModelHex() { Val = "FFFFFF" };  // 흰색 배경
            fill1.Append(backgroundColor);

            A.Outline outline4 = new A.Outline() { Width = 9525 };

            // 테두리 색상을 검은색으로 설정
            A.SolidFill outlineFill = new A.SolidFill();
            A.RgbColorModelHex outlineColor = new A.RgbColorModelHex() { Val = "000000" };  // 검은색 테두리
            outlineFill.Append(outlineColor);
            outline4.Append(outlineFill);

            A.Miter miter1 = new A.Miter() { Limit = 800000 };
            A.HeadEnd headEnd1 = new A.HeadEnd();
            A.TailEnd tailEnd1 = new A.TailEnd();

            outline4.Append(miter1);
            outline4.Append(headEnd1);
            outline4.Append(tailEnd1);

            shapeProperties1.Append(transform2D1);
            shapeProperties1.Append(presetGeometry1);
            shapeProperties1.Append(fill1);  // 배경 색상 추가
            shapeProperties1.Append(outline4);  // 테두리 추가
            #endregion

            #region 변경된 부분: 텍스트 박스 설정 변경
            Xdr.TextBody textBody1 = new Xdr.TextBody();
            A.BodyProperties bodyProperties1 = new A.BodyProperties()
            {
                VerticalOverflow = A.TextVerticalOverflowValues.Clip,
                HorizontalOverflow = A.TextHorizontalOverflowValues.Clip, // 텍스트 넘침 허용 해제
                Wrap = A.TextWrappingValues.None, // "도형의 텍스트 배치" 체크 해제
                LeftInset = 0,                    // 여백 설정
                TopInset = 0,
                RightInset = 0,
                BottomInset = 0,
                Anchor = A.TextAnchoringTypeValues.Top,
                UpRight = true
            };

            // "도형을 텍스트 크기에 맞춤" 설정 추가
            //bodyProperties1.Append(new A.ShapeAutoFit());

            A.ListStyle listStyle1 = new A.ListStyle();

            // 수평 가운데 맞춤 설정
            A.Paragraph paragraph1 = new A.Paragraph();
            A.ParagraphProperties paragraphProperties1 = new A.ParagraphProperties()
            {
                Alignment = A.TextAlignmentTypeValues.Center,  // 수평 가운데 맞춤
                RightToLeft = false
            };

            A.DefaultRunProperties defaultRunProperties1 = new A.DefaultRunProperties() { FontSize = 1300 };
            paragraphProperties1.Append(defaultRunProperties1);

            A.Run run1 = new A.Run();
            A.RunProperties runProperties1 = new A.RunProperties()
            {
                Language = "ko-KR",
                AlternativeLanguage = "en-US",
                FontSize = isExec ? 1600 : 1400,
                Bold = isExec,
                Italic = false,
                Underline = A.TextUnderlineValues.None,
                Strike = A.TextStrikeValues.NoStrike,
                Baseline = 0
            };

            // 텍스트 색상을 검은색으로 설정
            A.SolidFill solidFill6 = new A.SolidFill();
            A.RgbColorModelHex rgbColorModelHex14 = new A.RgbColorModelHex() { Val = "000000" };
            solidFill6.Append(rgbColorModelHex14);

            runProperties1.Append(solidFill6);

            // 폰트 설정 (옵션)
            A.LatinFont latinFont3 = new A.LatinFont() { Typeface = "돋움" };
            A.EastAsianFont eastAsianFont3 = new A.EastAsianFont() { Typeface = "돋움" };
            runProperties1.Append(latinFont3);
            runProperties1.Append(eastAsianFont3);

            A.Text text1 = new A.Text();
            text1.Text = text;

            run1.Append(runProperties1);
            run1.Append(text1);

            paragraph1.Append(paragraphProperties1);
            paragraph1.Append(run1);

            textBody1.Append(bodyProperties1);
            textBody1.Append(listStyle1);
            textBody1.Append(paragraph1);
            #endregion

            shape1.Append(nonVisualShapeProperties1);
            shape1.Append(shapeProperties1);
            shape1.Append(textBody1);
            Xdr.ClientData clientData1 = new Xdr.ClientData();

            // Shape 오류 확인
            if (!CheckAndFixRequiredShapeElements(shape1).StartsWith("OK"))
            {

            }

            twoCellAnchor1.Append(fromMarker1);
            twoCellAnchor1.Append(toMarker1);
            twoCellAnchor1.Append(shape1);
            twoCellAnchor1.Append(clientData1);

            _Worksheet.WorksheetPart.DrawingsPart.WorksheetDrawing.Append(twoCellAnchor1);

            // 검증 및 수정
            CreateValidTwoCellAnchor(fromCol, fromColOffset, fromRow, fromRowOffset, toCol, toColOffset, toRow, toRowOffset);
        }

        

        /// <summary>
        /// 치수 텍스트
        /// </summary>
        /// <param name="fromCol"></param>
        /// <param name="fromRow"></param>
        /// <param name="fromColOffset"></param>
        /// <param name="fromRowOffset"></param>
        /// <param name="toCol"></param>
        /// <param name="toRow"></param>
        /// <param name="toColOffset"></param>
        /// <param name="toRowOffset"></param>
        /// <param name="text"></param>
        /// <param name="fontColor"></param>
        /// <param name="fontName"></param>
        /// <param name="fontSize"></param>
        /// <param name="isBold"></param>
        /// <param name="horzAlign"></param>
        /// <param name="vertAlign"></param>
        /// <param name="isAutoText"></param>
        public void CreateDrawingTextEx(int fromCol, int fromRow, double fromColOffset, double fromRowOffset,
                                        int toCol, int toRow, double toColOffset, double toRowOffset, string text, System.Windows.Media.Color fontColor,
                                        string fontName = "돋움", int fontSize = 1000, bool isBold = false,
                                        ExcelHorizentalAlignment horzAlign = ExcelHorizentalAlignment.Center, ExcelVerticalAlignment vertAlign = ExcelVerticalAlignment.Center, bool isAutoText = false, string name = ""
        )
        {
            if (_Worksheet.WorksheetPart.DrawingsPart == null)
                CreateDrawingPart();

            Xdr.TwoCellAnchor twoCellAnchor1 = new Xdr.TwoCellAnchor();

            Xdr.FromMarker fromMarker1 = new Xdr.FromMarker();
            Xdr.ColumnId columnId1 = new Xdr.ColumnId();
            columnId1.Text = (fromCol).ToString();
            Xdr.ColumnOffset columnOffset1 = new Xdr.ColumnOffset();
            columnOffset1.Text = ((int)(fromColOffset * _ColumnCont)).ToString(); //"1554687"; //
            Xdr.RowId rowId1 = new Xdr.RowId();
            rowId1.Text = (fromRow - 1).ToString();
            Xdr.RowOffset rowOffset1 = new Xdr.RowOffset();
            rowOffset1.Text = ((int)(fromRowOffset * _RowCont)).ToString();

            fromMarker1.Append(columnId1);
            fromMarker1.Append(columnOffset1);
            fromMarker1.Append(rowId1);
            fromMarker1.Append(rowOffset1);

            Xdr.ToMarker toMarker1 = new Xdr.ToMarker();
            Xdr.ColumnId columnId2 = new Xdr.ColumnId();
            columnId2.Text = toCol.ToString();
            Xdr.ColumnOffset columnOffset2 = new Xdr.ColumnOffset();
            columnOffset2.Text = ((int)(toColOffset * _ColumnCont)).ToString();
            Xdr.RowId rowId2 = new Xdr.RowId();
            rowId2.Text = toRow.ToString();
            Xdr.RowOffset rowOffset2 = new Xdr.RowOffset();
            rowOffset2.Text = ((int)(toRowOffset * _RowCont)).ToString();

            toMarker1.Append(columnId2);
            toMarker1.Append(columnOffset2);
            toMarker1.Append(rowId2);
            toMarker1.Append(rowOffset2);

            if (string.IsNullOrEmpty(name))
                name = "TextBox 1";

            Xdr.Shape shape1 = new Xdr.Shape() { Macro = "", TextLink = "" };
            Xdr.NonVisualShapeProperties nonVisualShapeProperties1 = new Xdr.NonVisualShapeProperties();
            Xdr.NonVisualDrawingProperties nonVisualDrawingProperties1 = new Xdr.NonVisualDrawingProperties() { Id = (UInt32Value)1025U, Name = name };
            Xdr.NonVisualShapeDrawingProperties nonVisualShapeDrawingProperties1 = new Xdr.NonVisualShapeDrawingProperties() { TextBox = true };
            A.ShapeLocks shapeLocks1 = new A.ShapeLocks() { NoChangeArrowheads = true };
            nonVisualShapeDrawingProperties1.Append(shapeLocks1);
            nonVisualShapeProperties1.Append(nonVisualDrawingProperties1);
            nonVisualShapeProperties1.Append(nonVisualShapeDrawingProperties1);

            Xdr.ShapeProperties shapeProperties1 = new Xdr.ShapeProperties() { BlackWhiteMode = A.BlackWhiteModeValues.Auto };

            A.Transform2D transform2D1 = new A.Transform2D();
            A.Offset offset1 = new A.Offset() { X = 1209675L, Y = 1009650L };
            //A.Extents extents1 = new A.Extents() { Cx = 542925L, Cy = 209550L };
            //A.Extents extents1 = new A.Extents() { Cx = 800000L, Cy = 209550L };

            (long width, long height) = CalculateTextSize(text, fontSize);
            A.Extents extents1 = new A.Extents() { Cx = width, Cy = height };

            transform2D1.Append(offset1);
            transform2D1.Append(extents1);

            A.PresetGeometry presetGeometry1 = new A.PresetGeometry() { Preset = A.ShapeTypeValues.Rectangle };
            A.AdjustValueList adjustValueList1 = new A.AdjustValueList();

            presetGeometry1.Append(adjustValueList1);
            A.NoFill noFill1 = new A.NoFill();

            A.Outline outline4 = new A.Outline();
            A.NoFill noFill2 = new A.NoFill();
            A.Miter miter1 = new A.Miter() { Limit = 800000 };
            A.HeadEnd headEnd1 = new A.HeadEnd();
            A.TailEnd tailEnd1 = new A.TailEnd();

            outline4.Append(noFill2);
            outline4.Append(miter1);
            outline4.Append(headEnd1);
            outline4.Append(tailEnd1);

            shapeProperties1.Append(transform2D1);
            shapeProperties1.Append(presetGeometry1);
            shapeProperties1.Append(noFill1);
            shapeProperties1.Append(outline4);

            Xdr.TextBody textBody1 = new Xdr.TextBody();
            A.BodyProperties bodyProperties1;

            if (isAutoText == true)
            {
                bodyProperties1 = new A.BodyProperties()
                {
                    //VerticalOverflow = A.TextVerticalOverflowValues.Overflow,
                    //HorizontalOverflow = A.TextHorizontalOverflowValues.Overflow, // 텍스트 넘침 허용
                    Wrap = A.TextWrappingValues.None, // "도형의 텍스트 배치" 체크 해제
                    LeftInset = 0,                    // 여백 설정
                    TopInset = 0,
                    RightInset = 0,
                    BottomInset = 0,
                    //Anchor = (A.TextAnchoringTypeValues)vertAlign,
                    Anchor = A.TextAnchoringTypeValues.Center,
                    UpRight = true
                };

                bodyProperties1.Append(new A.ShapeAutoFit());
            }
            else
            {
                bodyProperties1 = new A.BodyProperties()
                {
                    VerticalOverflow = A.TextVerticalOverflowValues.Clip,
                    Wrap = A.TextWrappingValues.Square,
                    LeftInset = 0,//36576,
                    TopInset = 0,//18288,
                    RightInset = 0,
                    BottomInset = 0,
                    Anchor = (A.TextAnchoringTypeValues)vertAlign,
                    UpRight = true
                };
            }

            A.ListStyle listStyle1 = new A.ListStyle();
            A.Paragraph paragraph1 = new A.Paragraph();

            A.ParagraphProperties paragraphProperties1 = new A.ParagraphProperties() { Alignment = (A.TextAlignmentTypeValues)horzAlign, RightToLeft = false };
            A.DefaultRunProperties defaultRunProperties1 = new A.DefaultRunProperties() { FontSize = 1000 };

            paragraphProperties1.Append(defaultRunProperties1);

            A.Run run1 = new A.Run();

            A.RunProperties runProperties1 = new A.RunProperties() { Language = "ko-KR", AlternativeLanguage = "en-US", FontSize = fontSize, Bold = isBold, Italic = false, Underline = A.TextUnderlineValues.None, Strike = A.TextStrikeValues.NoStrike, Baseline = 0 };

            A.SolidFill solidFill6 = new A.SolidFill();
            A.RgbColorModelHex rgbColorModelHex14 = new A.RgbColorModelHex() { Val = ColorToHexString(fontColor) };

            solidFill6.Append(rgbColorModelHex14);
            A.LatinFont latinFont3 = new A.LatinFont() { Typeface = fontName };
            A.EastAsianFont eastAsianFont3 = new A.EastAsianFont() { Typeface = fontName };

            runProperties1.Append(solidFill6);
            runProperties1.Append(latinFont3);
            runProperties1.Append(eastAsianFont3);
            A.Text text1 = new A.Text();
            text1.Text = text;

            run1.Append(runProperties1);
            run1.Append(text1);

            paragraph1.Append(paragraphProperties1);
            paragraph1.Append(run1);
            
            shape1.Append(nonVisualShapeProperties1);
            shape1.Append(shapeProperties1);
            
            textBody1.Append(bodyProperties1);
            textBody1.Append(listStyle1);
            textBody1.Append(paragraph1);

            shape1.Append(textBody1);
            Xdr.ClientData clientData1 = new Xdr.ClientData();

            // Shape 오류 확인
            if (!CheckAndFixRequiredShapeElements(shape1).StartsWith("OK"))
            {

            }

            twoCellAnchor1.Append(fromMarker1);
            twoCellAnchor1.Append(toMarker1);
            twoCellAnchor1.Append(shape1);
            twoCellAnchor1.Append(clientData1);

            _Worksheet.WorksheetPart.DrawingsPart.WorksheetDrawing.Append(twoCellAnchor1);

            // 검증
            CreateValidTwoCellAnchor(fromCol, fromColOffset, fromRow, fromRowOffset, toCol, toColOffset, toRow, toRowOffset);
        }



        private (long width, long height) CalculateTextSize(string text, double fontSize)
        {
            // 텍스트의 길이와 폰트 크기에 따라 텍스트 박스 크기 계산
            // 실제로는 폰트 메트릭스를 사용하여 정확한 크기를 계산해야 하지만,
            // 여기서는 간단하게 비례식을 사용합니다.

            double pixelsPerCharacter = fontSize * 0.5; // 임의의 비례값
            double widthInPixels = text.Length * pixelsPerCharacter;
            double heightInPixels = fontSize * 1.5; // 임의의 높이 계산

            // 픽셀을 EMU로 변환 (1 픽셀 = 9525 EMU)
            long width = (long)(widthInPixels * 9525);
            long height = (long)(heightInPixels * 9525);

            return (width, height);
        }

        public void CreateAutoShape(int fromCol, int fromRow, double fromColOffset, double fromRowOffset,
            int toCol, int toRow, double toColOffset, double toRowOffset,
            ExcelAutoShapeType shapeType, System.Windows.Media.Color backColor, System.Windows.Media.Color lineColor)
        {
            if (_Worksheet.WorksheetPart.DrawingsPart == null) CreateDrawingPart();

            Xdr.TwoCellAnchor twoCellAnchor1 = new Xdr.TwoCellAnchor();

            Xdr.FromMarker fromMarker1 = new Xdr.FromMarker();
            Xdr.ColumnId columnId1 = new Xdr.ColumnId();
            columnId1.Text = (fromCol).ToString();
            Xdr.ColumnOffset columnOffset1 = new Xdr.ColumnOffset();
            columnOffset1.Text = ((int)(fromColOffset * _ColumnCont)).ToString(); //"1554687"; //
            Xdr.RowId rowId1 = new Xdr.RowId();
            rowId1.Text = (fromRow).ToString();
            Xdr.RowOffset rowOffset1 = new Xdr.RowOffset();
            rowOffset1.Text = ((int)(fromRowOffset * _RowCont)).ToString();

            fromMarker1.Append(columnId1);
            fromMarker1.Append(columnOffset1);
            fromMarker1.Append(rowId1);
            fromMarker1.Append(rowOffset1);

            Xdr.ToMarker toMarker1 = new Xdr.ToMarker();
            Xdr.ColumnId columnId2 = new Xdr.ColumnId();
            columnId2.Text = toCol.ToString();
            Xdr.ColumnOffset columnOffset2 = new Xdr.ColumnOffset();
            columnOffset2.Text = ((int)(toColOffset * _ColumnCont)).ToString();
            Xdr.RowId rowId2 = new Xdr.RowId();
            rowId2.Text = toRow.ToString();
            Xdr.RowOffset rowOffset2 = new Xdr.RowOffset();
            rowOffset2.Text = ((int)(toRowOffset * _RowCont)).ToString();

            toMarker1.Append(columnId2);
            toMarker1.Append(columnOffset2);
            toMarker1.Append(rowId2);
            toMarker1.Append(rowOffset2);

            Xdr.Shape shape1 = new Xdr.Shape() { Macro = "", TextLink = "" };

            Xdr.NonVisualShapeProperties nonVisualShapeProperties1 = new Xdr.NonVisualShapeProperties();
            Xdr.NonVisualDrawingProperties nonVisualDrawingProperties1 = new Xdr.NonVisualDrawingProperties() { Id = (UInt32Value)1026U, Name = "Oval 2" };

            Xdr.NonVisualShapeDrawingProperties nonVisualShapeDrawingProperties1 = new Xdr.NonVisualShapeDrawingProperties();
            A.ShapeLocks shapeLocks1 = new A.ShapeLocks() { NoChangeArrowheads = true };

            nonVisualShapeDrawingProperties1.Append(shapeLocks1);

            nonVisualShapeProperties1.Append(nonVisualDrawingProperties1);
            nonVisualShapeProperties1.Append(nonVisualShapeDrawingProperties1);

            Xdr.ShapeProperties shapeProperties1 = new Xdr.ShapeProperties() { BlackWhiteMode = A.BlackWhiteModeValues.Auto };

            A.Transform2D transform2D1 = new A.Transform2D();
            //A.Offset offset1 = new A.Offset() { X = 1704975L, Y = 1266825L };
            //A.Extents extents1 = new A.Extents() { Cx = 1171575L, Cy = 1123950L };

            //transform2D1.Append(offset1);
            //transform2D1.Append(extents1);

            A.PresetGeometry presetGeometry1 = new A.PresetGeometry() { Preset = (A.ShapeTypeValues)shapeType };
            A.AdjustValueList adjustValueList1 = new A.AdjustValueList();

            presetGeometry1.Append(adjustValueList1);

            A.SolidFill solidFill6 = new A.SolidFill();
            A.RgbColorModelHex rgbColorModelHex14 = new A.RgbColorModelHex() { Val = ColorToHexString(backColor) };

            solidFill6.Append(rgbColorModelHex14);

            A.Outline outline4 = new A.Outline();

            A.SolidFill solidFill7 = new A.SolidFill();
            A.RgbColorModelHex rgbColorModelHex15 = new A.RgbColorModelHex() { Val = ColorToHexString(lineColor) };

            solidFill7.Append(rgbColorModelHex15);
            A.Round round1 = new A.Round();
            A.HeadEnd headEnd1 = new A.HeadEnd();
            A.TailEnd tailEnd1 = new A.TailEnd();

            outline4.Append(solidFill7);
            outline4.Append(round1);
            outline4.Append(headEnd1);
            outline4.Append(tailEnd1);

            shapeProperties1.Append(transform2D1);
            shapeProperties1.Append(presetGeometry1);
            shapeProperties1.Append(solidFill6);
            shapeProperties1.Append(outline4);

            shape1.Append(nonVisualShapeProperties1);
            shape1.Append(shapeProperties1);
            Xdr.ClientData clientData1 = new Xdr.ClientData();

            // Shape 오류 확인
            if (!CheckAndFixRequiredShapeElements(shape1).StartsWith("OK"))
            {

            }

            twoCellAnchor1.Append(fromMarker1);
            twoCellAnchor1.Append(toMarker1);
            twoCellAnchor1.Append(shape1);
            twoCellAnchor1.Append(clientData1);

            _Worksheet.WorksheetPart.DrawingsPart.WorksheetDrawing.Append(twoCellAnchor1);

            // 검증
            CreateValidTwoCellAnchor(fromCol, fromColOffset, fromRow, fromRowOffset, toCol, toColOffset, toRow, toRowOffset);
        }

        /// <summary>
        /// 새로운 도형 ID 생성 메서드
        /// </summary>
        /// <returns></returns>
        private uint GetNextShapeId()
        {
            return ++currentShapeId;
        }

        /// <summary>
        /// 새로운 라인 이름 생성 메서드
        /// </summary>
        /// <returns></returns>
        private string GetNextLineName()
        {
            return "Line " + (lineCounter++);
        }


        public void CreateDArrowLine(int fromCol, int fromRow, double fromColOffset, double fromRowOffset,
            int toCol, int toRow, double toColOffset, double toRowOffset,
            System.Windows.Media.Color lineColor)
        {
            if (_Worksheet.WorksheetPart.DrawingsPart == null) CreateDrawingPart();

            Xdr.TwoCellAnchor twoCellAnchor1 = new Xdr.TwoCellAnchor();

            Xdr.FromMarker fromMarker1 = new Xdr.FromMarker();
            Xdr.ColumnId columnId1 = new Xdr.ColumnId();
            columnId1.Text = (fromCol).ToString();
            Xdr.ColumnOffset columnOffset1 = new Xdr.ColumnOffset();
            columnOffset1.Text = ((int)(fromColOffset * _ColumnCont)).ToString(); //"1554687"; //
            Xdr.RowId rowId1 = new Xdr.RowId();
            rowId1.Text = (fromRow).ToString();
            Xdr.RowOffset rowOffset1 = new Xdr.RowOffset();
            rowOffset1.Text = ((int)(fromRowOffset * _RowCont)).ToString();

            fromMarker1.Append(columnId1);
            fromMarker1.Append(columnOffset1);
            fromMarker1.Append(rowId1);
            fromMarker1.Append(rowOffset1);

            Xdr.ToMarker toMarker1 = new Xdr.ToMarker();
            Xdr.ColumnId columnId2 = new Xdr.ColumnId();
            columnId2.Text = toCol.ToString();
            Xdr.ColumnOffset columnOffset2 = new Xdr.ColumnOffset();
            columnOffset2.Text = ((int)(toColOffset * _ColumnCont)).ToString();
            Xdr.RowId rowId2 = new Xdr.RowId();
            rowId2.Text = toRow.ToString();
            Xdr.RowOffset rowOffset2 = new Xdr.RowOffset();
            rowOffset2.Text = ((int)(toRowOffset * _RowCont)).ToString();

            toMarker1.Append(columnId2);
            toMarker1.Append(columnOffset2);
            toMarker1.Append(rowId2);
            toMarker1.Append(rowOffset2);

            Xdr.Shape shape1 = new Xdr.Shape() { Macro = "", TextLink = "" };

            Xdr.NonVisualShapeProperties nonVisualShapeProperties1 = new Xdr.NonVisualShapeProperties();
            Xdr.NonVisualDrawingProperties nonVisualDrawingProperties1 = new Xdr.NonVisualDrawingProperties()
            {
                Id = (UInt32Value)GetNextShapeId(),
                Name = GetNextLineName()
            };

            Xdr.NonVisualShapeDrawingProperties nonVisualShapeDrawingProperties1 = new Xdr.NonVisualShapeDrawingProperties();
            A.ShapeLocks shapeLocks1 = new A.ShapeLocks() { NoChangeShapeType = true };

            nonVisualShapeDrawingProperties1.Append(shapeLocks1);

            nonVisualShapeProperties1.Append(nonVisualDrawingProperties1);
            nonVisualShapeProperties1.Append(nonVisualShapeDrawingProperties1);

            Xdr.ShapeProperties shapeProperties1 = new Xdr.ShapeProperties() { BlackWhiteMode = A.BlackWhiteModeValues.Auto };

            A.Transform2D transform2D1 = new A.Transform2D();
            //A.Offset offset1 = new A.Offset() { X = 1819275L, Y = 790575L };
            //A.Extents extents1 = new A.Extents() { Cx = 1504950L, Cy = 0L };

            //transform2D1.Append(offset1);
            //transform2D1.Append(extents1);

            A.PresetGeometry presetGeometry1 = new A.PresetGeometry() { Preset = A.ShapeTypeValues.Line };
            A.AdjustValueList adjustValueList1 = new A.AdjustValueList();

            presetGeometry1.Append(adjustValueList1);
            A.NoFill noFill1 = new A.NoFill();

            A.Outline outline4 = new A.Outline();

            A.SolidFill solidFill6 = new A.SolidFill();
            A.RgbColorModelHex rgbColorModelHex14 = new A.RgbColorModelHex() { Val = ColorToHexString(lineColor) };

            solidFill6.Append(rgbColorModelHex14);
            A.Round round1 = new A.Round();
            A.HeadEnd headEnd1 = new A.HeadEnd() { Type = A.LineEndValues.Triangle, Width = A.LineEndWidthValues.Medium, Length = A.LineEndLengthValues.Medium };
            A.TailEnd tailEnd1 = new A.TailEnd() { Type = A.LineEndValues.Triangle, Width = A.LineEndWidthValues.Medium, Length = A.LineEndLengthValues.Medium };

            outline4.Append(solidFill6);
            outline4.Append(round1);
            outline4.Append(headEnd1);
            outline4.Append(tailEnd1);

            shapeProperties1.Append(transform2D1);
            shapeProperties1.Append(presetGeometry1);
            shapeProperties1.Append(noFill1);
            shapeProperties1.Append(outline4);

            shape1.Append(nonVisualShapeProperties1);
            shape1.Append(shapeProperties1);
            Xdr.ClientData clientData1 = new Xdr.ClientData();

            // Shape 오류 확인
            if(!CheckAndFixRequiredShapeElements(shape1).StartsWith("OK"))
            {

            }

            twoCellAnchor1.Append(fromMarker1);
            twoCellAnchor1.Append(toMarker1);
            twoCellAnchor1.Append(shape1);
            twoCellAnchor1.Append(clientData1);


            _Worksheet.WorksheetPart.DrawingsPart.WorksheetDrawing.Append(twoCellAnchor1);

            // 검증
            CreateValidTwoCellAnchor(fromCol, fromColOffset, fromRow, fromRowOffset, toCol, toColOffset, toRow, toRowOffset);
        }

        #endregion


        public void CreateValidTwoCellAnchor(int fromCol, double fromColOffset, int fromRow, double fromRowOffset, int toCol, double toColOffset, int toRow, double toRowOffset)
        {
            return;
            // 1. 드로잉 파트 추가 또는 가져오기
            DrawingsPart drawingsPart;
            if (_Worksheet.WorksheetPart.DrawingsPart == null)
                CreateDrawingPart();
            
            drawingsPart = _Worksheet.WorksheetPart.DrawingsPart;

            // 2. WorksheetDrawing 객체 생성 또는 가져오기
            Xdr.WorksheetDrawing worksheetDrawing;
            if (drawingsPart.WorksheetDrawing != null)
            {
                worksheetDrawing = drawingsPart.WorksheetDrawing;
            }
            else
            {
                worksheetDrawing = new Xdr.WorksheetDrawing();
                worksheetDrawing.AddNamespaceDeclaration("xdr", "http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing");
                worksheetDrawing.AddNamespaceDeclaration("a", "http://schemas.openxmlformats.org/drawingml/2006/main");
            }

            // 3. TwoCellAnchor 생성
            Xdr.TwoCellAnchor cellAnchor = new Xdr.TwoCellAnchor();
            cellAnchor.EditAs = Xdr.EditAsValues.TwoCell;

            // 4. FromMarker 생성
            Xdr.FromMarker fromMarker = new Xdr.FromMarker();

            // 값의 범위 확인 (음수나 너무 큰 값 방지)
            fromMarker.Append(new Xdr.ColumnId() { Text = Math.Max(0, fromCol).ToString() });
            fromMarker.Append(new Xdr.ColumnOffset()
            {
                Text = Math.Max(0, Math.Min(914400, (int)(fromColOffset * _ColumnCont))).ToString()
            });
            fromMarker.Append(new Xdr.RowId() { Text = Math.Max(0, fromRow).ToString() });
            fromMarker.Append(new Xdr.RowOffset()
            {
                Text = Math.Max(0, Math.Min(914400, (int)(fromRowOffset * _RowCont))).ToString()
            });

            // 5. ToMarker 생성
            Xdr.ToMarker toMarker = new Xdr.ToMarker();

            toMarker.Append(new Xdr.ColumnId() { Text = Math.Max(0, toCol).ToString() });
            toMarker.Append(new Xdr.ColumnOffset()
            {
                Text = Math.Max(0, Math.Min(914400, (int)(toColOffset * _ColumnCont))).ToString()
            });
            toMarker.Append(new Xdr.RowId() { Text = Math.Max(0, toRow).ToString() });
            toMarker.Append(new Xdr.RowOffset()
            {
                Text = Math.Max(0, Math.Min(914400, (int)(toRowOffset * _RowCont))).ToString()
            });

            // 6. Marker들을 TwoCellAnchor에 추가
            cellAnchor.Append(fromMarker);
            cellAnchor.Append(toMarker);

            // 7. Shape 생성
            Xdr.Shape shape = new Xdr.Shape();

            // 8. NonVisualShapeProperties 설정
            uint shapeId = 1;

            // 기존 요소의 ID 중 가장 큰 값 + 1을 사용
            if (worksheetDrawing.ChildElements.Count > 0)
            {
                foreach (var anchor in worksheetDrawing.ChildElements)
                {
                    foreach (var element in anchor.ChildElements)
                    {
                        if (element is Xdr.Shape)
                        {
                            var existingShape = element as Xdr.Shape;
                            var nvProps = existingShape.GetFirstChild<Xdr.NonVisualShapeProperties>();
                            if (nvProps != null)
                            {
                                var drawingProps = nvProps.GetFirstChild<Xdr.NonVisualDrawingProperties>();
                                if (drawingProps != null && drawingProps.Id != null)
                                {
                                    shapeId = Math.Max(shapeId, drawingProps.Id + 1);
                                }
                            }
                        }
                    }
                }
            }

            var nvShapeProps = new Xdr.NonVisualShapeProperties(
                new Xdr.NonVisualDrawingProperties() { Id = shapeId, Name = $"Shape {shapeId}" },
                new Xdr.NonVisualShapeDrawingProperties(
                    new DocumentFormat.OpenXml.Drawing.ShapeLocks() { NoGrouping = true }
                )
            );
            shape.Append(nvShapeProps);

            // 9. ShapeProperties 설정
            var shapeProps = new Xdr.ShapeProperties();

            // 10. Transform2D 설정 (선택사항)
            var transform = new DocumentFormat.OpenXml.Drawing.Transform2D();
            transform.Append(new DocumentFormat.OpenXml.Drawing.Offset() { X = 0, Y = 0 });
            transform.Append(new DocumentFormat.OpenXml.Drawing.Extents() { Cx = 914400, Cy = 914400 });
            shapeProps.Append(transform);

            // 11. 도형 타입 설정 (사각형)
            var presetGeometry = new DocumentFormat.OpenXml.Drawing.PresetGeometry()
            {
                Preset = DocumentFormat.OpenXml.Drawing.ShapeTypeValues.Rectangle
            };
            presetGeometry.Append(new DocumentFormat.OpenXml.Drawing.AdjustValueList());
            shapeProps.Append(presetGeometry);

            // 12. 도형 채우기 설정
            shapeProps.Append(new DocumentFormat.OpenXml.Drawing.SolidFill(
                new DocumentFormat.OpenXml.Drawing.RgbColorModelHex() { Val = "FF0000" }
            ));

            // 13. 도형 테두리 설정
            var outline = new DocumentFormat.OpenXml.Drawing.Outline() { Width = 9525 }; // 1pt
            outline.Append(new DocumentFormat.OpenXml.Drawing.SolidFill(
                new DocumentFormat.OpenXml.Drawing.RgbColorModelHex() { Val = "000000" }
            ));
            shapeProps.Append(outline);

            shape.Append(shapeProps);

            // 14. TwoCellAnchor에 Shape 추가
            cellAnchor.Append(shape);

            // 15. 필수 ClientData 추가
            cellAnchor.Append(new Xdr.ClientData());

            // 16. 드로잉에 TwoCellAnchor 추가
            worksheetDrawing.Append(cellAnchor);

            // 17. 드로잉 저장
            //drawingsPart.WorksheetDrawing = worksheetDrawing;
        }


        public string ValidateTwoCellAnchor(Xdr.TwoCellAnchor cellAnchor)
        {
            bool isValid = true;
            string msg = "";

            // 1. FromMarker와 ToMarker가 존재하는지 확인
            var fromMarker = cellAnchor.GetFirstChild<Xdr.FromMarker>();
            if (fromMarker == null)
            {
                msg = "오류: FromMarker가 없습니다. TwoCellAnchor는 반드시 FromMarker를 포함해야 합니다.";
                isValid = false;
            }
            else
            {
                // FromMarker 내부 요소 검증
                var columnId = fromMarker.GetFirstChild<Xdr.ColumnId>();
                var columnOffset = fromMarker.GetFirstChild<Xdr.ColumnOffset>();
                var rowId = fromMarker.GetFirstChild<Xdr.RowId>();
                var rowOffset = fromMarker.GetFirstChild<Xdr.RowOffset>();

                if (columnId == null || string.IsNullOrEmpty(columnId.Text))
                {
                    msg += "오류: FromMarker의 ColumnId가 없거나 비어 있습니다.";
                    isValid = false;
                }

                if (columnOffset == null || string.IsNullOrEmpty(columnOffset.Text))
                {
                    msg += "오류: FromMarker의 ColumnOffset이 없거나 비어 있습니다.";
                    isValid = false;
                }
                else if (!int.TryParse(columnOffset.Text, out _))
                {
                    msg += $"오류: FromMarker의 ColumnOffset({columnOffset.Text})이 유효한 정수가 아닙니다.";
                    isValid = false;
                }

                if (rowId == null || string.IsNullOrEmpty(rowId.Text))
                {
                    msg += "오류: FromMarker의 RowId가 없거나 비어 있습니다.";
                    isValid = false;
                }

                if (rowOffset == null || string.IsNullOrEmpty(rowOffset.Text))
                {
                    msg += "오류: FromMarker의 RowOffset이 없거나 비어 있습니다.";
                    isValid = false;
                }
                else if (!int.TryParse(rowOffset.Text, out _))
                {
                    msg += $"오류: FromMarker의 RowOffset({rowOffset.Text})이 유효한 정수가 아닙니다.";
                    isValid = false;
                }
            }

            var toMarker = cellAnchor.GetFirstChild<Xdr.ToMarker>();
            if (toMarker == null)
            {
                msg += "오류: ToMarker가 없습니다. TwoCellAnchor는 반드시 ToMarker를 포함해야 합니다.";
                isValid = false;
            }
            else
            {
                // ToMarker 내부 요소 검증
                var columnId = toMarker.GetFirstChild<Xdr.ColumnId>();
                var columnOffset = toMarker.GetFirstChild<Xdr.ColumnOffset>();
                var rowId = toMarker.GetFirstChild<Xdr.RowId>();
                var rowOffset = toMarker.GetFirstChild<Xdr.RowOffset>();

                if (columnId == null || string.IsNullOrEmpty(columnId.Text))
                {
                    msg += "오류: ToMarker의 ColumnId가 없거나 비어 있습니다.";
                    isValid = false;
                }

                if (columnOffset == null || string.IsNullOrEmpty(columnOffset.Text))
                {
                    msg += "오류: ToMarker의 ColumnOffset이 없거나 비어 있습니다.";
                    isValid = false;
                }
                else if (!int.TryParse(columnOffset.Text, out _))
                {
                    msg += $"오류: ToMarker의 ColumnOffset({columnOffset.Text})이 유효한 정수가 아닙니다.";
                    isValid = false;
                }

                if (rowId == null || string.IsNullOrEmpty(rowId.Text))
                {
                    msg += "오류: ToMarker의 RowId가 없거나 비어 있습니다.";
                    isValid = false;
                }

                if (rowOffset == null || string.IsNullOrEmpty(rowOffset.Text))
                {
                    msg += "오류: ToMarker의 RowOffset이 없거나 비어 있습니다.";
                    isValid = false;
                }
                else if (!int.TryParse(rowOffset.Text, out _))
                {
                    msg += $"오류: ToMarker의 RowOffset({rowOffset.Text})이 유효한 정수가 아닙니다.";
                    isValid = false;
                }
            }

            // 2. 위치 값의 논리적 일관성 검증
            if (fromMarker != null && toMarker != null)
            {
                int fromCol, fromRow, toCol, toRow;

                if (int.TryParse(fromMarker.GetFirstChild<Xdr.ColumnId>()?.Text, out fromCol) &&
                    int.TryParse(fromMarker.GetFirstChild<Xdr.RowId>()?.Text, out fromRow) &&
                    int.TryParse(toMarker.GetFirstChild<Xdr.ColumnId>()?.Text, out toCol) &&
                    int.TryParse(toMarker.GetFirstChild<Xdr.RowId>()?.Text, out toRow))
                {
                    // 일반적으로 from 위치는 to 위치보다 작거나 같아야 함
                    if (fromCol > toCol)
                    {
                        msg += $"경고: FromMarker 열({fromCol})이 ToMarker 열({toCol})보다 큽니다.";
                    }

                    if (fromRow > toRow)
                    {
                        msg += $"경고: FromMarker 행({fromRow})이 ToMarker 행({toRow})보다 큽니다.";
                    }
                }
            }

            // 3. 자식 요소가 올바른 순서로 있는지 확인
            var childrenTypes = cellAnchor.ChildElements.Select(e => e.GetType().Name).ToList();
            msg += $"TwoCellAnchor 자식 요소 순서: {string.Join(", ", childrenTypes)}";

            // 4. ClientData가 있는지 확인 
            if (cellAnchor.GetFirstChild<Xdr.ClientData>() == null)
            {
                msg += "오류: ClientData가 없습니다. TwoCellAnchor는 반드시 ClientData를 포함해야 합니다.";
                isValid = false;
            }

            // 5. EditAs 속성 확인 (선택적)
            if (cellAnchor.EditAs != null)
            {
                var validValues = new[] { "oneCell", "twoCell", "absolute" };
                if (!validValues.Contains(cellAnchor.EditAs.Value.ToString()))
                {
                    Console.WriteLine($"경고: EditAs 값({cellAnchor.EditAs.Value})이 유효하지 않습니다. 유효한 값: {string.Join(", ", validValues)}");
                }
            }

            // 6. 오프셋 범위 확인 
            // (EMU 단위로 0~914400 사이의 값이 일반적, 914400 EMU = 1 인치)
            if (fromMarker != null)
            {
                var colOffset = fromMarker.GetFirstChild<Xdr.ColumnOffset>();
                var rowOffset = fromMarker.GetFirstChild<Xdr.RowOffset>();

                if (colOffset != null && int.TryParse(colOffset.Text, out int colOffsetValue))
                {
                    if (colOffsetValue < 0 || colOffsetValue > 914400)
                    {
                        Console.WriteLine($"경고: FromMarker의 ColumnOffset({colOffsetValue})이 일반적인 범위(0~914400)를 벗어납니다.");
                    }
                }

                if (rowOffset != null && int.TryParse(rowOffset.Text, out int rowOffsetValue))
                {
                    if (rowOffsetValue < 0 || rowOffsetValue > 914400)
                    {
                        Console.WriteLine($"경고: FromMarker의 RowOffset({rowOffsetValue})이 일반적인 범위(0~914400)를 벗어납니다.");
                    }
                }
            }

            // 7. Shape나 다른 도형 요소가 있는지 확인
            bool hasValidDrawingElement = false;

            if (cellAnchor.GetFirstChild<Xdr.Shape>() != null)
            {
                hasValidDrawingElement = true;
            }
            else if (cellAnchor.GetFirstChild<Xdr.Picture>() != null)
            {
                hasValidDrawingElement = true;
            }
            else if (cellAnchor.GetFirstChild<Xdr.GraphicFrame>() != null)
            {
                hasValidDrawingElement = true;
            }
            else if (cellAnchor.GetFirstChild<Xdr.GroupShape>() != null)
            {
                hasValidDrawingElement = true;
            }
            else if (cellAnchor.GetFirstChild<Xdr.ConnectionShape>() != null)
            {
                hasValidDrawingElement = true;
            }

            if (!hasValidDrawingElement)
            {
                Console.WriteLine("오류: TwoCellAnchor에 유효한 도형 요소(Shape, Picture, GraphicFrame, GroupShape, ConnectionShape)가 없습니다.");
                isValid = false;
            }

            if(!isValid)
            {

            }

            return msg;
        }

        public string CheckAndFixRequiredShapeElements(Xdr.Shape shape)
        {
            bool wasFixed = false;
            string msg = "OK";

            // NonVisualShapeProperties 체크 및 수정
            if (shape.GetFirstChild<Xdr.NonVisualShapeProperties>() == null)
            {
                msg = "오류 감지: NonVisualShapeProperties가 없습니다. 기본값으로 추가합니다.";

                var nvProps = new Xdr.NonVisualShapeProperties(
                    new Xdr.NonVisualDrawingProperties() { Id = 1, Name = "Shape 1" },
                    new Xdr.NonVisualShapeDrawingProperties(
                        new DocumentFormat.OpenXml.Drawing.ShapeLocks() { NoGrouping = true }
                    )
                );

                shape.Append(nvProps);
                wasFixed = true;
            }
            else
            {
                // NonVisualDrawingProperties ID 체크
                var nvDrawProps = shape.GetFirstChild<Xdr.NonVisualShapeProperties>()?.GetFirstChild<Xdr.NonVisualDrawingProperties>();
                if (nvDrawProps == null)
                {
                    msg = "오류 감지: NonVisualDrawingProperties가 없습니다. 기본값으로 추가합니다.";

                    var newNvDrawProps = new Xdr.NonVisualDrawingProperties() { Id = 1, Name = "Shape 1" };
                    shape.GetFirstChild<Xdr.NonVisualShapeProperties>().Append(newNvDrawProps);
                    wasFixed = true;
                }
                else if (nvDrawProps.Id == 0)
                {
                    msg = "오류 감지: Shape ID가 설정되지 않았습니다. 기본값(1)으로 설정합니다.";
                    nvDrawProps.Id = 1;
                    wasFixed = true;
                }
            }

            // ShapeProperties 체크 및 수정
            var shapeProps = shape.GetFirstChild<Xdr.ShapeProperties>();
            if (shapeProps == null)
            {
                msg += "오류 감지: ShapeProperties가 없습니다. 기본값으로 추가합니다.";
                shapeProps = new Xdr.ShapeProperties();
                shape.Append(shapeProps);
                wasFixed = true;
            }

            // Transform2D 체크 및 수정
            var transform = shapeProps.GetFirstChild<DocumentFormat.OpenXml.Drawing.Transform2D>();
            if (transform == null)
            {
                msg += "오류 감지: Transform2D가 없습니다. 기본값으로 추가합니다.";

                transform = new DocumentFormat.OpenXml.Drawing.Transform2D();
                transform.Append(new DocumentFormat.OpenXml.Drawing.Offset() { X = 0, Y = 0 });
                transform.Append(new DocumentFormat.OpenXml.Drawing.Extents() { Cx = 914400, Cy = 914400 });

                shapeProps.Append(transform);
                wasFixed = true;
            }

            // Geometry 체크 및 수정
            var presetGeometry = shapeProps.GetFirstChild<DocumentFormat.OpenXml.Drawing.PresetGeometry>();
            var customGeometry = shapeProps.GetFirstChild<DocumentFormat.OpenXml.Drawing.CustomGeometry>();

            if (presetGeometry == null && customGeometry == null)
            {
                msg += "오류 감지: Geometry 정보가 없습니다. 기본 사각형으로 설정합니다.";

                presetGeometry = new DocumentFormat.OpenXml.Drawing.PresetGeometry()
                {
                    Preset = DocumentFormat.OpenXml.Drawing.ShapeTypeValues.Rectangle
                };
                presetGeometry.Append(new DocumentFormat.OpenXml.Drawing.AdjustValueList());

                shapeProps.Append(presetGeometry);
                wasFixed = true;
            }

            // 앵커 설정 확인
            var anchor = shape.Parent as Xdr.AbsoluteAnchor; // 또는 TwoCellAnchor, OneCellAnchor
            if (anchor != null)
            {
                // ClientData가 없는 경우 추가
                if (anchor.GetFirstChild<Xdr.ClientData>() == null)
                {
                    msg += "앵커에 ClientData가 없습니다. 추가합니다.";
                    anchor.Append(new Xdr.ClientData());
                }
            }

            if (wasFixed)
            {
                msg += "Shape 요소들이 자동으로 수정되었습니다. 검증 성공.";
            }
            else
            {
                msg += "OK-모든 필수 요소가 올바르게 설정되어 있습니다.";
            }

            if(!msg.StartsWith("OK"))
            {
            }

            return msg;
        }


        #region private methods

        #region drawing

        private Xdr.Shape CreateShape()
        {
            Xdr.Shape shape1 = new Xdr.Shape() { Macro = "", TextLink = "" };

            Xdr.NonVisualShapeProperties nonVisualShapeProperties1 = new Xdr.NonVisualShapeProperties();
            Xdr.NonVisualDrawingProperties nonVisualDrawingProperties1 = new Xdr.NonVisualDrawingProperties() { Id = (UInt32Value)12U, Name = "자유형 11" };
            Xdr.NonVisualShapeDrawingProperties nonVisualShapeDrawingProperties1 = new Xdr.NonVisualShapeDrawingProperties();

            nonVisualShapeProperties1.Append(nonVisualDrawingProperties1);
            nonVisualShapeProperties1.Append(nonVisualShapeDrawingProperties1);

            shape1.Append(nonVisualShapeProperties1);

            return shape1;
        }

        private A.SolidFill CreateFillColor(System.Windows.Media.Color color)
        {
            A.SolidFill fill = new A.SolidFill();
            A.RgbColorModelHex rgbColorModelHex14 = new A.RgbColorModelHex() { Val = ColorToHexString(color) };
            fill.Append(rgbColorModelHex14);
            return fill;
        }

        private A.Outline CreateOutline(System.Windows.Media.Color color)
        {
            A.Outline outline = new A.Outline();
            A.SolidFill fill = CreateFillColor(color);
            outline.Append(fill);
            return outline;
        }

        private string ColorToHexString(System.Windows.Media.Color color)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendFormat("{0:x2}", color.R);
            builder.AppendFormat("{0:x2}", color.G);
            builder.AppendFormat("{0:x2}", color.B);
            return builder.ToString();
        }

        private A.CustomGeometry CreateCrustomGeomery()
        {
            A.CustomGeometry customGeometry1 = new A.CustomGeometry();
            A.AdjustValueList adjustValueList1 = new A.AdjustValueList();
            A.ShapeGuideList shapeGuideList1 = new A.ShapeGuideList();
            A.AdjustHandleList adjustHandleList1 = new A.AdjustHandleList();
            A.ConnectionSiteList connectionSiteList1 = new A.ConnectionSiteList();
            A.Rectangle rectangle1 = new A.Rectangle() { Left = "0", Top = "0", Right = "0", Bottom = "0" };

            customGeometry1.Append(adjustValueList1);
            customGeometry1.Append(shapeGuideList1);
            customGeometry1.Append(adjustHandleList1);
            customGeometry1.Append(connectionSiteList1);
            customGeometry1.Append(rectangle1);

            return customGeometry1;
        }

        private Int64 GetColumnLength(int fromCol, int toCol, double offset)
        {
            Rect from = GetCellPosition(0, fromCol);
            Rect to = GetCellPosition(0, toCol);

            return (Int64)((Math.Abs(to.Left - from.Left) + offset) * _ColumnCont);
        }

        private Int64 GetRowLength(int fromRow, int toRow, double offset)
        {
            Rect from = GetCellPosition(fromRow, 0);
            Rect to = GetCellPosition(toRow, 0);

            return (Int64)((Math.Abs(to.Top - from.Top) + offset) * _RowCont);
        }

        private A.PathList GetPathList(string geoData)
        {
            A.PathList pathList1 = new A.PathList();
            A.Path path1 = new A.Path();


            Geometry geo = (Geometry)TypeDescriptor.GetConverter(typeof(StreamGeometry)).ConvertFromString(geoData);
            path1.Width = (int)geo.Bounds.Width;
            path1.Height = (int)geo.Bounds.Height;

            char com;
            string value;
            PointType curType = PointType.None;
            HashSet<char> keySet = new HashSet<char>(new char[] { 'M', 'L', 'C', 'Z', 'z' });
            char[] compChars = geoData.ToCharArray();
            StringBuilder builder = new StringBuilder();
            List<int> ptList = new List<int>();
            A.Point firstPoint = null;
            for (int index = 0; index < compChars.Length; index++)
            {
                com = compChars[index];
                if (keySet.Contains(compChars[index]))
                {
                    if (curType != PointType.None)
                    {
                        if (!string.IsNullOrEmpty(builder.ToString()))
                        {
                            value = builder.ToString();
                            if (!string.IsNullOrWhiteSpace(value))
                            {
                                double dv;
                                if (double.TryParse(value, out dv) == true)
                                {
                                    ptList.Add((int)dv);
                                }
                            }
                            builder.Clear();
                        }

                        if (curType == PointType.Move && ptList.Count == 2)
                        {
                            A.MoveTo moveTo1 = new A.MoveTo();
                            A.Point point1 = new A.Point();
                            point1.X = (ptList[0]).ToString();// ((Int64)(ptList[0] * _ColumnCont)).ToString();
                            point1.Y = (ptList[1]).ToString();// ((Int64)(ptList[1] * _RowCont)).ToString();
                            moveTo1.Append(point1);
                            path1.Append(moveTo1);
                            firstPoint = point1;

                            //Debug.WriteLine(string.Format("MoveTo - X : {0}, Y : {1}", ptList[0], ptList[1]));
                        }
                        else if (curType == PointType.Line && ptList.Count >= 2)
                        {
                            for (int ptIdx = 0; ptIdx < ptList.Count; ptIdx += 2)
                            {
                                if (ptIdx + 1 >= ptList.Count)
                                {
                                    continue;
                                }
                                A.LineTo to = new A.LineTo();
                                A.Point pt = new A.Point();
                                pt.X = (ptList[ptIdx]).ToString(); //((Int64)(ptList[ptIdx] * _ColumnCont)).ToString();
                                pt.Y = (ptList[ptIdx + 1]).ToString(); //((Int64)(ptList[ptIdx + 1] * _RowCont)).ToString();
                                to.Append(pt);
                                path1.Append(to);

                                //Debug.WriteLine(string.Format("LineTo - X : {0}, Y : {1}", ptList[ptIdx], ptList[ptIdx + 1]));
                            }
                        }
                        else if (curType == PointType.Curve && ptList.Count == 6)
                        {
                            A.CubicBezierCurveTo cubicBezierCurveTo6 = new A.CubicBezierCurveTo();
                            A.Point point17 = new A.Point() { X = (ptList[0]).ToString(), Y = (ptList[1]).ToString() };
                            A.Point point18 = new A.Point() { X = (ptList[2]).ToString(), Y = (ptList[3]).ToString() };
                            A.Point point19 = new A.Point() { X = (ptList[4]).ToString(), Y = (ptList[5]).ToString() };

                            //A.Point point17 = new A.Point() { X = ((Int64)(ptList[0] * _ColumnCont)).ToString(), Y = ((Int64)(ptList[1] * _RowCont)).ToString() };
                            //A.Point point18 = new A.Point() { X = ((Int64)(ptList[2] * _ColumnCont)).ToString(), Y = ((Int64)(ptList[3] * _RowCont)).ToString() };
                            //A.Point point19 = new A.Point() { X = ((Int64)(ptList[4] * _ColumnCont)).ToString(), Y = ((Int64)(ptList[5] * _RowCont)).ToString() };

                            cubicBezierCurveTo6.Append(point17);
                            cubicBezierCurveTo6.Append(point18);
                            cubicBezierCurveTo6.Append(point19);
                            path1.Append(cubicBezierCurveTo6);

                            //Debug.WriteLine(string.Format("Bezier - [{0}, {1}], [{2}, {3}], [{4}, {5}]",
                            //    ptList[0], ptList[1], ptList[2], ptList[3], ptList[4], ptList[5]));
                        }
                        ptList.Clear();
                    }

                    if (com == 'M') curType = PointType.Move;
                    else if (com == 'L') curType = PointType.Line;
                    else if (com == 'C') curType = PointType.Curve;
                    else if (com == 'Z' || com == 'z') curType = PointType.Close;
                }
                else if (com == ' ' || com == ',')
                {
                    value = builder.ToString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        double dv;
                        if (double.TryParse(value, out dv) == true)
                        {
                            ptList.Add((int)dv);
                        }
                    }
                    builder.Clear();
                }
                else
                {
                    builder.Append(com);
                }
            }

            if (curType == PointType.Close && firstPoint != null)
            {
                A.LineTo to = new A.LineTo();
                A.Point pt = new A.Point();
                pt.X = firstPoint.X;
                pt.Y = firstPoint.Y;
                to.Append(pt);
                path1.Append(to);

                //Debug.WriteLine(string.Format("LineTo - X : {0}, Y : {1}", firstPoint.X, firstPoint.Y));
            }

            pathList1.Append(path1);
            return pathList1;
        }


        private Xdr.TwoCellAnchor CreateCellAnchor(int fromCol, int fromRow, double fromColOffset, double fromRowOffset,
            int toCol, int toRow, double toColOffset, double toRowOffset)
        {
            Xdr.TwoCellAnchor twoCellAnchor1 = new Xdr.TwoCellAnchor();
            Xdr.FromMarker fromMarker1 = new Xdr.FromMarker();
            Xdr.ColumnId columnId1 = new Xdr.ColumnId();
            columnId1.Text = (fromCol).ToString();
            Xdr.ColumnOffset columnOffset1 = new Xdr.ColumnOffset();
            columnOffset1.Text = ((int)(fromColOffset * _ColumnCont)).ToString(); //"1554687"; //
            Xdr.RowId rowId1 = new Xdr.RowId();
            rowId1.Text = (fromRow - 1).ToString();
            Xdr.RowOffset rowOffset1 = new Xdr.RowOffset();
            rowOffset1.Text = ((int)(fromRowOffset * _RowCont)).ToString();

            fromMarker1.Append(columnId1);
            fromMarker1.Append(columnOffset1);
            fromMarker1.Append(rowId1);
            fromMarker1.Append(rowOffset1);

            Xdr.ToMarker toMarker1 = new Xdr.ToMarker();
            Xdr.ColumnId columnId2 = new Xdr.ColumnId();
            columnId2.Text = toCol.ToString();
            Xdr.ColumnOffset columnOffset2 = new Xdr.ColumnOffset();
            columnOffset2.Text = ((int)(toColOffset * _ColumnCont)).ToString();
            Xdr.RowId rowId2 = new Xdr.RowId();
            rowId2.Text = toRow.ToString();
            Xdr.RowOffset rowOffset2 = new Xdr.RowOffset();
            rowOffset2.Text = ((int)(toRowOffset * _RowCont)).ToString();

            toMarker1.Append(columnId2);
            toMarker1.Append(columnOffset2);
            toMarker1.Append(rowId2);
            toMarker1.Append(rowOffset2);

            twoCellAnchor1.Append(fromMarker1);
            twoCellAnchor1.Append(toMarker1);

            return twoCellAnchor1;
        }

        #endregion

        private void CreateDrawingPart()
        {
            //DrawingsPart drawingsPart1 = _Worksheet.WorksheetPart.AddNewPart<DrawingsPart>();
            //Xdr.WorksheetDrawing worksheetDrawing1 = new Xdr.WorksheetDrawing();
            //worksheetDrawing1.AddNamespaceDeclaration("xdr", "http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing");
            //worksheetDrawing1.AddNamespaceDeclaration("a", "http://schemas.openxmlformats.org/drawingml/2006/main");

            //drawingsPart1.WorksheetDrawing = worksheetDrawing1;

            // 1. DrawingsPart 생성
            DrawingsPart drawingsPart1 = _Worksheet.WorksheetPart.AddNewPart<DrawingsPart>();

            // 2. WorksheetDrawing 초기화
            drawingsPart1.WorksheetDrawing = new Xdr.WorksheetDrawing();
            drawingsPart1.WorksheetDrawing.Save();

            // 3. 워크시트에 Drawing 요소 추가 및 DrawingsPart와 연결
            string drawingsPartId = _Worksheet.WorksheetPart.GetIdOfPart(drawingsPart1);
            DocumentFormat.OpenXml.Spreadsheet.Drawing drawing = new DocumentFormat.OpenXml.Spreadsheet.Drawing() { Id = drawingsPartId };

            // 워크시트의 SheetData 이후에 Drawing 요소를 추가합니다.
            // 위치는 필요에 따라 조정할 수 있습니다.
            _Worksheet.WorksheetPart.Worksheet.Append(drawing);
            _Worksheet.WorksheetPart.Worksheet.Save();

        }

        private void Release()
        {
            if (_SheetDocument != null)
            {
                _SheetDocument.Dispose();
            }
        }

        private List<Sheet> GetSheetList()
        {
            if (_SheetDocument == null) return null;
            return _SheetDocument.WorkbookPart.Workbook.GetFirstChild<Sheets>().Elements<Sheet>().ToList();
        }

        private Sheet GetSheet(int index)
        {
            List<Sheet> sheetList = GetSheetList();
            if (sheetList == null) return null;
            if (index < 0 || index >= sheetList.Count) return null;
            return sheetList[index];
        }

        private Sheet GetSheet(string sheetName)
        {
            List<Sheet> sheetList = GetSheetList();
            if (sheetList == null) return null;
            foreach (Sheet st in sheetList)
            {
                if (st.Name.Value == sheetName) return st;
            }
            return null;
        }

        private string GetWorkSheetId(Worksheet ws)
        {
            WorksheetPart worksheetPart;
            List<Sheet> sheetList = GetSheetList();
            foreach (Sheet sh in sheetList)
            {
                worksheetPart = (WorksheetPart)_SheetDocument.WorkbookPart.GetPartById(sh.Id);
                if (object.ReferenceEquals(ws.WorksheetPart, worksheetPart)) return sh.Id;
            }
            return string.Empty;
        }

        private string GetExcelColName(int col)
        {
            col--;
            int pre = col / 26;
            if (pre > 0) return string.Format("{0}{1}", Convert.ToChar(64 + pre), Convert.ToChar(col - (26 * pre) + 65));
            //if (pre > 0) return string.Format("{0}{1}", Convert.ToChar(64 + pre), Convert.ToChar((col+1) % 26 + 64));
            else return string.Format("{0}", Convert.ToChar(col + 65));
        }

        private string GetExcelCellName(int col, int row)
        {
            return GetExcelColName(col) + row.ToString();
        }

        private Worksheet GetWorkSheet(int index)
        {
            Sheet sh = GetSheet(index);
            if (sh == null) return null;

            WorksheetPart worksheetPart = (WorksheetPart)_SheetDocument.WorkbookPart.GetPartById(sh.Id);

            if (worksheetPart == null) return null;
            return worksheetPart.Worksheet;
        }

        private Cell GetCell(Worksheet worksheet, string columnName, int rowIndex)
        {
            Row row = GetRow(worksheet, rowIndex);

            if (row == null) return null;

            return row.Elements<Cell>().Where(c => string.Compare
                   (c.CellReference.Value, columnName +
                   rowIndex, true) == 0).First();
        }


        private Row GetRow(Worksheet worksheet, int rowIndex)
        {
            var tmp = worksheet.GetFirstChild<SheetData>().Elements<Row>().Where(r => r.RowIndex == rowIndex);

            if (tmp.Any())
                return worksheet.GetFirstChild<SheetData>().Elements<Row>().Where(r => r.RowIndex == rowIndex).First();
            else
                return null;
        }

        private SharedStringItem GetSharedStringItemById(WorkbookPart workbookPart, int id)
        {
            return workbookPart.SharedStringTablePart.SharedStringTable.Elements<SharedStringItem>().ElementAt(id);
        }

        private SharedStringTablePart GetSharedStringTablePart(SpreadsheetDocument spreadSheet)
        {
            if (spreadSheet.WorkbookPart.GetPartsOfType<SharedStringTablePart>().Count() > 0)
            {
                return spreadSheet.WorkbookPart.GetPartsOfType<SharedStringTablePart>().First();
            }
            else
            {
                return spreadSheet.WorkbookPart.AddNewPart<SharedStringTablePart>();
            }


        }


        private int InsertSharedStringItem(string text, SharedStringTablePart shareStringPart)
        {
            if (shareStringPart.SharedStringTable == null)
            {
                shareStringPart.SharedStringTable = new SharedStringTable();
            }

            int i = 0;
            foreach (SharedStringItem item in shareStringPart.SharedStringTable.Elements<SharedStringItem>())
            {
                if (item.InnerText == text)
                {
                    return i;
                }
                i++;
            }

            shareStringPart.SharedStringTable.AppendChild(new SharedStringItem(new DocumentFormat.OpenXml.Spreadsheet.Text(text)));
            shareStringPart.SharedStringTable.Save();

            return i;
        }

        private WorksheetPart GetWorkSheetPart(WorkbookPart workbookPart, string sheetName)
        {

            Sheet st = workbookPart.Workbook.Descendants<Sheet>()
            .Where(s => s.Name.Value.Equals(sheetName))
            .FirstOrDefault();
            if (st == null) return null;
            return workbookPart.GetPartById(st.Id) as WorksheetPart;
        }

        private void FixupTableParts(WorksheetPart worksheetPart, int numTableDefParts)
        {
            int tableId = numTableDefParts;
            foreach (TableDefinitionPart tableDefPart in worksheetPart.TableDefinitionParts)
            {
                tableId++;
                tableDefPart.Table.Id = (uint)tableId;
                tableDefPart.Table.DisplayName = "CopiedTable" + tableId;
                tableDefPart.Table.Name = "CopiedTable" + tableId;
                tableDefPart.Table.Save();
            }
        }

        static void CleanView(WorksheetPart worksheetPart)
        {
            SheetViews views = worksheetPart.Worksheet.GetFirstChild<SheetViews>();
            if (views != null)
            {
                views.Remove();
                worksheetPart.Worksheet.Save();
            }
        }



        #endregion

        #region ErectionNetwork

        /// <summary>
        /// 노드들의 포지션 계산.
        /// </summary>
        /// <param name="posX"></param>
        /// <param name="posY"></param>
        /// <param name="row"></param>
        /// <param name="rowOffset"></param>
        /// <param name="col"></param>
        /// <param name="colOffset"></param>
        private void CalculationPosition(double posX, double posY, ref int row, ref double rowOffset, ref int col, ref double colOffset)
        {
            double width = 2;
            double height = 16;
            double tempRow = 0;
            double tempCol = 0;

            tempRow = (posY / height);
            row = (int)tempRow;
            rowOffset = (posY % height) * _RowCont;
            rowOffset = (int)(rowOffset);

            tempCol = (posX / width);
            col = (int)tempCol;
            colOffset = (posX % width) * _ColumnCont;
            colOffset = (int)(colOffset);
        }

        /// <summary>
        /// 절점노드 그리는 메소드.
        /// </summary>
        /// <param name="leftText"></param>
        /// <param name="rightText"></param>
        /// <param name="bottomText"></param>
        /// <param name="posX"></param>
        /// <param name="posY"></param>
        /// <param name="netDay"></param>
        /// <returns></returns>
        public UInt32Value DrawingsOctagonNode(string leftText, string rightText, string bottomText, double posX, double posY, bool netDay)
        {
            int NodeHeight = 50;
            int NodeWidth = 5;
            int fromCol = 0;
            int fromRow = 0;
            double fromColOffset = 0;
            double fromRowOffset = 0;
            int toCol = 0;
            int toRow = 0;
            double toColOffset = 0;
            double toRowOffset = 0;

            CalculationPosition(posX / 10.0, posY, ref fromRow, ref fromRowOffset, ref fromCol, ref fromColOffset);
            // CalculationPosition((posX / 10.0) + NodeWidth, posY + NodeHeight, ref toRow, ref toRowOffset, ref toCol, ref toColOffset);

            toCol = fromCol + 2;
            toColOffset = fromColOffset + 115000;
            if (toColOffset > 230000)
            {
                toCol = toCol + 1;
                toColOffset = toColOffset - 230000;
            }

            toRow = fromRow + 3;
            toRowOffset = fromRowOffset + 25000;
            if (toRowOffset > 200000)
            {
                toRow = toRow + 1;
                toRowOffset = toRowOffset - 200000;
            }
            if (_Worksheet.WorksheetPart.DrawingsPart == null) CreateDrawingPart();



            Xdr.TwoCellAnchor twoCellAnchor1 = new Xdr.TwoCellAnchor();

            Xdr.FromMarker fromMarker1 = new Xdr.FromMarker();
            Xdr.ColumnId columnId1 = new Xdr.ColumnId();
            columnId1.Text = fromCol.ToString();
            Xdr.ColumnOffset columnOffset1 = new Xdr.ColumnOffset();
            columnOffset1.Text = fromColOffset.ToString();
            Xdr.RowId rowId1 = new Xdr.RowId();
            rowId1.Text = fromRow.ToString();
            Xdr.RowOffset rowOffset1 = new Xdr.RowOffset();
            rowOffset1.Text = fromRowOffset.ToString();

            fromMarker1.Append(columnId1);
            fromMarker1.Append(columnOffset1);
            fromMarker1.Append(rowId1);
            fromMarker1.Append(rowOffset1);

            Xdr.ToMarker toMarker1 = new Xdr.ToMarker();
            Xdr.ColumnId columnId2 = new Xdr.ColumnId();
            columnId2.Text = toCol.ToString();
            Xdr.ColumnOffset columnOffset2 = new Xdr.ColumnOffset();
            columnOffset2.Text = toColOffset.ToString();
            Xdr.RowId rowId2 = new Xdr.RowId();
            rowId2.Text = toRow.ToString();
            Xdr.RowOffset rowOffset2 = new Xdr.RowOffset();
            rowOffset2.Text = toRowOffset.ToString();

            toMarker1.Append(columnId2);
            toMarker1.Append(columnOffset2);
            toMarker1.Append(rowId2);
            toMarker1.Append(rowOffset2);

            Xdr.GroupShape groupShape1 = new Xdr.GroupShape();

            Xdr.NonVisualGroupShapeProperties nonVisualGroupShapeProperties1 = new Xdr.NonVisualGroupShapeProperties();
            Xdr.NonVisualDrawingProperties nonVisualDrawingProperties1 = new Xdr.NonVisualDrawingProperties() { Id = (UInt32Value)1040U, Name = "Group 16" };

            Xdr.NonVisualGroupShapeDrawingProperties nonVisualGroupShapeDrawingProperties1 = new Xdr.NonVisualGroupShapeDrawingProperties();
            A.GroupShapeLocks groupShapeLocks1 = new A.GroupShapeLocks();

            nonVisualGroupShapeDrawingProperties1.Append(groupShapeLocks1);

            nonVisualGroupShapeProperties1.Append(nonVisualDrawingProperties1);
            nonVisualGroupShapeProperties1.Append(nonVisualGroupShapeDrawingProperties1);

            Xdr.GroupShapeProperties groupShapeProperties1 = new Xdr.GroupShapeProperties() { BlackWhiteMode = A.BlackWhiteModeValues.Auto };

            A.TransformGroup transformGroup1 = new A.TransformGroup();
            A.Offset offset1 = new A.Offset() { X = 333375L, Y = 1676400L };
            A.Extents extents1 = new A.Extents() { Cx = 838200L, Cy = 838200L };
            A.ChildOffset childOffset1 = new A.ChildOffset() { X = 35L, Y = 176L };
            A.ChildExtents childExtents1 = new A.ChildExtents() { Cx = 88L, Cy = 88L };

            transformGroup1.Append(offset1);
            transformGroup1.Append(extents1);
            transformGroup1.Append(childOffset1);
            transformGroup1.Append(childExtents1);

            groupShapeProperties1.Append(transformGroup1);

            Xdr.Shape shape1 = new Xdr.Shape() { Macro = "", TextLink = "" };

            Xdr.NonVisualShapeProperties nonVisualShapeProperties1 = new Xdr.NonVisualShapeProperties();
            Xdr.NonVisualDrawingProperties nonVisualDrawingProperties2 = new Xdr.NonVisualDrawingProperties() { Id = (UInt32Value)1039U, Name = "AutoShape 15" };

            Xdr.NonVisualShapeDrawingProperties nonVisualShapeDrawingProperties1 = new Xdr.NonVisualShapeDrawingProperties();
            A.ShapeLocks shapeLocks1 = new A.ShapeLocks() { NoChangeArrowheads = true };

            nonVisualShapeDrawingProperties1.Append(shapeLocks1);

            nonVisualShapeProperties1.Append(nonVisualDrawingProperties2);
            nonVisualShapeProperties1.Append(nonVisualShapeDrawingProperties1);

            Xdr.ShapeProperties shapeProperties1 = new Xdr.ShapeProperties() { BlackWhiteMode = A.BlackWhiteModeValues.Auto };

            A.Transform2D transform2D1 = new A.Transform2D();
            A.Offset offset2 = new A.Offset() { X = 35L, Y = 176L };
            A.Extents extents2 = new A.Extents() { Cx = 88L, Cy = 88L };

            transform2D1.Append(offset2);
            transform2D1.Append(extents2);

            A.PresetGeometry presetGeometry1 = new A.PresetGeometry() { Preset = A.ShapeTypeValues.Octagon };

            A.AdjustValueList adjustValueList1 = new A.AdjustValueList();
            A.ShapeGuide shapeGuide1 = new A.ShapeGuide() { Name = "adj", Formula = "val 29287" };

            adjustValueList1.Append(shapeGuide1);

            presetGeometry1.Append(adjustValueList1);

            A.SolidFill solidFill1 = new A.SolidFill();
            A.RgbColorModelHex rgbColorModelHex1 = new A.RgbColorModelHex() { Val = "CCFFFF" };

            solidFill1.Append(rgbColorModelHex1);

            A.Outline outline1 = new A.Outline() { Width = 9525 };

            A.SolidFill solidFill2 = new A.SolidFill();
            A.RgbColorModelHex rgbColorModelHex2 = new A.RgbColorModelHex() { Val = "000000" };

            solidFill2.Append(rgbColorModelHex2);
            A.Miter miter1 = new A.Miter() { Limit = 800000 };
            A.HeadEnd headEnd1 = new A.HeadEnd();
            A.TailEnd tailEnd1 = new A.TailEnd();

            outline1.Append(solidFill2);
            outline1.Append(miter1);
            outline1.Append(headEnd1);
            outline1.Append(tailEnd1);

            shapeProperties1.Append(transform2D1);
            shapeProperties1.Append(presetGeometry1);
            shapeProperties1.Append(solidFill1);
            shapeProperties1.Append(outline1);

            shape1.Append(nonVisualShapeProperties1);
            shape1.Append(shapeProperties1);

            Xdr.Shape shape2 = new Xdr.Shape() { Macro = "", TextLink = "" };

            Xdr.NonVisualShapeProperties nonVisualShapeProperties2 = new Xdr.NonVisualShapeProperties();
            Xdr.NonVisualDrawingProperties nonVisualDrawingProperties3 = new Xdr.NonVisualDrawingProperties()
            {
                Id = (UInt32Value)GetNextShapeId(),
                Name = GetNextLineName()
            };

            Xdr.NonVisualShapeDrawingProperties nonVisualShapeDrawingProperties2 = new Xdr.NonVisualShapeDrawingProperties();
            A.ShapeLocks shapeLocks2 = new A.ShapeLocks() { NoChangeShapeType = true };

            nonVisualShapeDrawingProperties2.Append(shapeLocks2);

            nonVisualShapeProperties2.Append(nonVisualDrawingProperties3);
            nonVisualShapeProperties2.Append(nonVisualShapeDrawingProperties2);

            Xdr.ShapeProperties shapeProperties2 = new Xdr.ShapeProperties() { BlackWhiteMode = A.BlackWhiteModeValues.Auto };

            A.Transform2D transform2D2 = new A.Transform2D();
            A.Offset offset3 = new A.Offset() { X = 36L, Y = 219L };
            A.Extents extents3 = new A.Extents() { Cx = 86L, Cy = 0L };

            transform2D2.Append(offset3);
            transform2D2.Append(extents3);

            A.PresetGeometry presetGeometry2 = new A.PresetGeometry() { Preset = A.ShapeTypeValues.Line };
            A.AdjustValueList adjustValueList2 = new A.AdjustValueList();

            presetGeometry2.Append(adjustValueList2);
            A.NoFill noFill1 = new A.NoFill();

            A.Outline outline2 = new A.Outline() { Width = 9525 };

            A.SolidFill solidFill3 = new A.SolidFill();
            A.RgbColorModelHex rgbColorModelHex3 = new A.RgbColorModelHex() { Val = "000000" };

            solidFill3.Append(rgbColorModelHex3);
            A.Round round1 = new A.Round();
            A.HeadEnd headEnd2 = new A.HeadEnd();
            A.TailEnd tailEnd2 = new A.TailEnd();

            outline2.Append(solidFill3);
            outline2.Append(round1);
            outline2.Append(headEnd2);
            outline2.Append(tailEnd2);

            shapeProperties2.Append(transform2D2);
            shapeProperties2.Append(presetGeometry2);
            shapeProperties2.Append(noFill1);
            shapeProperties2.Append(outline2);

            shape2.Append(nonVisualShapeProperties2);
            shape2.Append(shapeProperties2);

            Xdr.Shape shape3 = new Xdr.Shape() { Macro = "", TextLink = "" };

            Xdr.NonVisualShapeProperties nonVisualShapeProperties3 = new Xdr.NonVisualShapeProperties();
            Xdr.NonVisualDrawingProperties nonVisualDrawingProperties4 = new Xdr.NonVisualDrawingProperties()
            {
                Id = (UInt32Value)GetNextShapeId(),
                Name = GetNextLineName()
            };

            Xdr.NonVisualShapeDrawingProperties nonVisualShapeDrawingProperties3 = new Xdr.NonVisualShapeDrawingProperties();
            A.ShapeLocks shapeLocks3 = new A.ShapeLocks() { NoChangeShapeType = true };

            nonVisualShapeDrawingProperties3.Append(shapeLocks3);

            nonVisualShapeProperties3.Append(nonVisualDrawingProperties4);
            nonVisualShapeProperties3.Append(nonVisualShapeDrawingProperties3);

            Xdr.ShapeProperties shapeProperties3 = new Xdr.ShapeProperties() { BlackWhiteMode = A.BlackWhiteModeValues.Auto };

            A.Transform2D transform2D3 = new A.Transform2D();
            A.Offset offset4 = new A.Offset() { X = 79L, Y = 176L };
            A.Extents extents4 = new A.Extents() { Cx = 0L, Cy = 42L };

            transform2D3.Append(offset4);
            transform2D3.Append(extents4);

            A.PresetGeometry presetGeometry3 = new A.PresetGeometry() { Preset = A.ShapeTypeValues.Line };
            A.AdjustValueList adjustValueList3 = new A.AdjustValueList();

            presetGeometry3.Append(adjustValueList3);
            A.NoFill noFill2 = new A.NoFill();

            A.Outline outline3 = new A.Outline() { Width = 9525 };

            A.SolidFill solidFill4 = new A.SolidFill();
            A.RgbColorModelHex rgbColorModelHex4 = new A.RgbColorModelHex() { Val = "000000" };

            solidFill4.Append(rgbColorModelHex4);
            A.Round round2 = new A.Round();
            A.HeadEnd headEnd3 = new A.HeadEnd();
            A.TailEnd tailEnd3 = new A.TailEnd();

            outline3.Append(solidFill4);
            outline3.Append(round2);
            outline3.Append(headEnd3);
            outline3.Append(tailEnd3);

            shapeProperties3.Append(transform2D3);
            shapeProperties3.Append(presetGeometry3);
            shapeProperties3.Append(noFill2);
            shapeProperties3.Append(outline3);

            shape3.Append(nonVisualShapeProperties3);
            shape3.Append(shapeProperties3);

            // Shape 오류 확인
            if (!CheckAndFixRequiredShapeElements(shape1).StartsWith("OK"))
            {

            }
            // Shape 오류 확인
            if (!CheckAndFixRequiredShapeElements(shape2).StartsWith("OK"))
            {

            }
            // Shape 오류 확인
            if (!CheckAndFixRequiredShapeElements(shape3).StartsWith("OK"))
            {

            }

            groupShape1.Append(nonVisualGroupShapeProperties1);
            groupShape1.Append(groupShapeProperties1);
            groupShape1.Append(shape1);
            groupShape1.Append(shape2);
            groupShape1.Append(shape3);
            Xdr.ClientData clientData1 = new Xdr.ClientData();

            twoCellAnchor1.Append(fromMarker1);
            twoCellAnchor1.Append(toMarker1);
            twoCellAnchor1.Append(groupShape1);
            twoCellAnchor1.Append(clientData1);
            
            // CellAnchor 오류 확인
            if (!ValidateTwoCellAnchor(twoCellAnchor1).StartsWith("OK"))
            {

            }

            _Worksheet.WorksheetPart.DrawingsPart.WorksheetDrawing.Append(twoCellAnchor1);

            // 검증
            CreateValidTwoCellAnchor(fromCol, fromColOffset, fromRow, fromRowOffset, toCol, toColOffset, toRow, toRowOffset);

            if (netDay)
            {
                DrawingsDayTextBox(leftText, posX + 5, posY + 5, TextAlignmentTypeValues.Right);
            }
            else
            {
                DrawingsDayTextBox(leftText, posX - 15, posY + 5, TextAlignmentTypeValues.Right);
            }
            DrawingsDayTextBox(rightText, posX + 25, posY + 5, TextAlignmentTypeValues.Left);
            DrawingsBottomTextBox1(bottomText, posX, posY + 25);

            return nonVisualDrawingProperties1.Id;

        }


        /// <summary>
        /// 탑재노드 그리는 메소드.
        /// </summary>
        /// <param name="leftText"></param>
        /// <param name="rightText"></param>
        /// <param name="bottomText"></param>
        /// <param name="starText"></param>
        /// <param name="posX"></param>
        /// <param name="posY"></param>
        /// <param name="netDay"></param>
        /// <returns></returns>
        public UInt32Value DrawingsEllipseNode(string leftText, string rightText, string bottomText, string starText, double posX, double posY, bool netDay)
        {
            int NodeHeight = 50;
            double NodeWidth = 5.0;
            int fromCol = 0;
            int fromRow = 0;
            double fromColOffset = 0;
            double fromRowOffset = 0;
            int toCol = 0;
            int toRow = 0;
            double toColOffset = 0;
            double toRowOffset = 0;

            CalculationPosition((posX / 10.0), posY, ref fromRow, ref fromRowOffset, ref fromCol, ref fromColOffset);
            //  CalculationPosition((posX / 10.0) + NodeWidth, posY + NodeHeight, ref toRow, ref toRowOffset, ref toCol, ref toColOffset);

            toCol = fromCol + 2;
            toColOffset = fromColOffset + 115000;
            if (toColOffset > 230000)
            {
                toCol = toCol + 1;
                toColOffset = toColOffset - 230000;
            }

            toRow = fromRow + 3;
            toRowOffset = fromRowOffset + 25000;
            if (toRowOffset > 200000)
            {
                toRow = toRow + 1;
                toRowOffset = toRowOffset - 200000;
            }

            if (_Worksheet.WorksheetPart.DrawingsPart == null) CreateDrawingPart();

            Xdr.TwoCellAnchor twoCellAnchor1 = new Xdr.TwoCellAnchor();

            Xdr.FromMarker fromMarker1 = new Xdr.FromMarker();
            Xdr.ColumnId columnId1 = new Xdr.ColumnId();
            columnId1.Text = fromCol.ToString();
            Xdr.ColumnOffset columnOffset1 = new Xdr.ColumnOffset();
            columnOffset1.Text = fromColOffset.ToString();
            Xdr.RowId rowId1 = new Xdr.RowId();
            rowId1.Text = fromRow.ToString();
            Xdr.RowOffset rowOffset1 = new Xdr.RowOffset();
            rowOffset1.Text = (fromRowOffset).ToString();

            fromMarker1.Append(columnId1);
            fromMarker1.Append(columnOffset1);
            fromMarker1.Append(rowId1);
            fromMarker1.Append(rowOffset1);

            Xdr.ToMarker toMarker1 = new Xdr.ToMarker();
            Xdr.ColumnId columnId2 = new Xdr.ColumnId();
            columnId2.Text = toCol.ToString();
            Xdr.ColumnOffset columnOffset2 = new Xdr.ColumnOffset();
            columnOffset2.Text = toColOffset.ToString();
            Xdr.RowId rowId2 = new Xdr.RowId();
            rowId2.Text = toRow.ToString();
            Xdr.RowOffset rowOffset2 = new Xdr.RowOffset();
            rowOffset2.Text = toRowOffset.ToString();

            toMarker1.Append(columnId2);
            toMarker1.Append(columnOffset2);
            toMarker1.Append(rowId2);
            toMarker1.Append(rowOffset2);

            Xdr.GroupShape groupShape1 = new Xdr.GroupShape();

            Xdr.NonVisualGroupShapeProperties nonVisualGroupShapeProperties1 = new Xdr.NonVisualGroupShapeProperties();
            Xdr.NonVisualDrawingProperties nonVisualDrawingProperties1 = new Xdr.NonVisualDrawingProperties() { Id = (UInt32Value)1029U, Name = "Group 14" };

            Xdr.NonVisualGroupShapeDrawingProperties nonVisualGroupShapeDrawingProperties1 = new Xdr.NonVisualGroupShapeDrawingProperties();
            A.GroupShapeLocks groupShapeLocks1 = new A.GroupShapeLocks() { NoChangeAspect = true };

            nonVisualGroupShapeDrawingProperties1.Append(groupShapeLocks1);

            nonVisualGroupShapeProperties1.Append(nonVisualDrawingProperties1);
            nonVisualGroupShapeProperties1.Append(nonVisualGroupShapeDrawingProperties1);

            Xdr.GroupShapeProperties groupShapeProperties1 = new Xdr.GroupShapeProperties() { BlackWhiteMode = A.BlackWhiteModeValues.Auto };

            A.TransformGroup transformGroup1 = new A.TransformGroup();
            A.Offset offset1 = new A.Offset() { X = 342900L, Y = 1857375L };
            A.Extents extents1 = new A.Extents() { Cx = 619125L, Cy = 619125L };
            A.ChildOffset childOffset1 = new A.ChildOffset() { X = 41L, Y = 164L };
            A.ChildExtents childExtents1 = new A.ChildExtents() { Cx = 87L, Cy = 86L };

            transformGroup1.Append(offset1);
            transformGroup1.Append(extents1);
            transformGroup1.Append(childOffset1);
            transformGroup1.Append(childExtents1);

            groupShapeProperties1.Append(transformGroup1);

            Xdr.Shape shape1 = new Xdr.Shape() { Macro = "", TextLink = "" };

            Xdr.NonVisualShapeProperties nonVisualShapeProperties1 = new Xdr.NonVisualShapeProperties();
            Xdr.NonVisualDrawingProperties nonVisualDrawingProperties2 = new Xdr.NonVisualDrawingProperties() { Id = (UInt32Value)1030U, Name = "Oval 10" };

            Xdr.NonVisualShapeDrawingProperties nonVisualShapeDrawingProperties1 = new Xdr.NonVisualShapeDrawingProperties();
            A.ShapeLocks shapeLocks1 = new A.ShapeLocks() { NoChangeAspect = true, NoChangeArrowheads = true };

            nonVisualShapeDrawingProperties1.Append(shapeLocks1);

            nonVisualShapeProperties1.Append(nonVisualDrawingProperties2);
            nonVisualShapeProperties1.Append(nonVisualShapeDrawingProperties1);

            Xdr.ShapeProperties shapeProperties1 = new Xdr.ShapeProperties() { BlackWhiteMode = A.BlackWhiteModeValues.Auto };

            A.Transform2D transform2D1 = new A.Transform2D();
            A.Offset offset2 = new A.Offset() { X = 42L, Y = 164L };
            A.Extents extents2 = new A.Extents() { Cx = 86L, Cy = 86L };

            transform2D1.Append(offset2);
            transform2D1.Append(extents2);

            A.PresetGeometry presetGeometry1 = new A.PresetGeometry() { Preset = A.ShapeTypeValues.Ellipse };
            A.AdjustValueList adjustValueList1 = new A.AdjustValueList();

            presetGeometry1.Append(adjustValueList1);

            A.SolidFill solidFill1 = new A.SolidFill();
            A.RgbColorModelHex rgbColorModelHex1 = new A.RgbColorModelHex() { Val = "FFFFFF" };

            solidFill1.Append(rgbColorModelHex1);

            A.Outline outline1 = new A.Outline() { Width = 9525 };

            A.SolidFill solidFill2 = new A.SolidFill();
            A.RgbColorModelHex rgbColorModelHex2 = new A.RgbColorModelHex() { Val = "000000" };

            solidFill2.Append(rgbColorModelHex2);
            A.Round round1 = new A.Round();
            A.HeadEnd headEnd1 = new A.HeadEnd();
            A.TailEnd tailEnd1 = new A.TailEnd();

            outline1.Append(solidFill2);
            outline1.Append(round1);
            outline1.Append(headEnd1);
            outline1.Append(tailEnd1);

            shapeProperties1.Append(transform2D1);
            shapeProperties1.Append(presetGeometry1);
            shapeProperties1.Append(solidFill1);
            shapeProperties1.Append(outline1);

            shape1.Append(nonVisualShapeProperties1);
            shape1.Append(shapeProperties1);

            Xdr.Shape shape2 = new Xdr.Shape() { Macro = "", TextLink = "" };

            Xdr.NonVisualShapeProperties nonVisualShapeProperties2 = new Xdr.NonVisualShapeProperties();
            Xdr.NonVisualDrawingProperties nonVisualDrawingProperties3 = new Xdr.NonVisualDrawingProperties()
            {
                Id = (UInt32Value)GetNextShapeId(),
                Name = GetNextLineName()
            };

            Xdr.NonVisualShapeDrawingProperties nonVisualShapeDrawingProperties2 = new Xdr.NonVisualShapeDrawingProperties();
            A.ShapeLocks shapeLocks2 = new A.ShapeLocks() { NoChangeAspect = true, NoChangeShapeType = true };

            nonVisualShapeDrawingProperties2.Append(shapeLocks2);

            nonVisualShapeProperties2.Append(nonVisualDrawingProperties3);
            nonVisualShapeProperties2.Append(nonVisualShapeDrawingProperties2);

            Xdr.ShapeProperties shapeProperties2 = new Xdr.ShapeProperties() { BlackWhiteMode = A.BlackWhiteModeValues.Auto };

            A.Transform2D transform2D2 = new A.Transform2D();
            A.Offset offset3 = new A.Offset() { X = 41L, Y = 207L };
            A.Extents extents3 = new A.Extents() { Cx = 86L, Cy = 0L };

            transform2D2.Append(offset3);
            transform2D2.Append(extents3);

            A.PresetGeometry presetGeometry2 = new A.PresetGeometry() { Preset = A.ShapeTypeValues.Line };
            A.AdjustValueList adjustValueList2 = new A.AdjustValueList();

            presetGeometry2.Append(adjustValueList2);
            A.NoFill noFill1 = new A.NoFill();

            A.Outline outline2 = new A.Outline() { Width = 9525 };

            A.SolidFill solidFill3 = new A.SolidFill();
            A.RgbColorModelHex rgbColorModelHex3 = new A.RgbColorModelHex() { Val = "000000" };

            solidFill3.Append(rgbColorModelHex3);
            A.Round round2 = new A.Round();
            A.HeadEnd headEnd2 = new A.HeadEnd();
            A.TailEnd tailEnd2 = new A.TailEnd();

            outline2.Append(solidFill3);
            outline2.Append(round2);
            outline2.Append(headEnd2);
            outline2.Append(tailEnd2);

            shapeProperties2.Append(transform2D2);
            shapeProperties2.Append(presetGeometry2);
            shapeProperties2.Append(noFill1);
            shapeProperties2.Append(outline2);

            shape2.Append(nonVisualShapeProperties2);
            shape2.Append(shapeProperties2);

            Xdr.Shape shape3 = new Xdr.Shape() { Macro = "", TextLink = "" };

            Xdr.NonVisualShapeProperties nonVisualShapeProperties3 = new Xdr.NonVisualShapeProperties();
            Xdr.NonVisualDrawingProperties nonVisualDrawingProperties4 = new Xdr.NonVisualDrawingProperties()
            {
                Id = (UInt32Value)GetNextShapeId(),
                Name = GetNextLineName()
            };

            Xdr.NonVisualShapeDrawingProperties nonVisualShapeDrawingProperties3 = new Xdr.NonVisualShapeDrawingProperties();
            A.ShapeLocks shapeLocks3 = new A.ShapeLocks() { NoChangeAspect = true, NoChangeShapeType = true };

            nonVisualShapeDrawingProperties3.Append(shapeLocks3);

            nonVisualShapeProperties3.Append(nonVisualDrawingProperties4);
            nonVisualShapeProperties3.Append(nonVisualShapeDrawingProperties3);

            Xdr.ShapeProperties shapeProperties3 = new Xdr.ShapeProperties() { BlackWhiteMode = A.BlackWhiteModeValues.Auto };

            A.Transform2D transform2D3 = new A.Transform2D();
            A.Offset offset4 = new A.Offset() { X = 84L, Y = 164L };
            A.Extents extents4 = new A.Extents() { Cx = 0L, Cy = 42L };

            transform2D3.Append(offset4);
            transform2D3.Append(extents4);

            A.PresetGeometry presetGeometry3 = new A.PresetGeometry() { Preset = A.ShapeTypeValues.Line };
            A.AdjustValueList adjustValueList3 = new A.AdjustValueList();

            presetGeometry3.Append(adjustValueList3);
            A.NoFill noFill2 = new A.NoFill();

            A.Outline outline3 = new A.Outline() { Width = 9525 };

            A.SolidFill solidFill4 = new A.SolidFill();
            A.RgbColorModelHex rgbColorModelHex4 = new A.RgbColorModelHex() { Val = "000000" };

            solidFill4.Append(rgbColorModelHex4);
            A.Round round3 = new A.Round();
            A.HeadEnd headEnd3 = new A.HeadEnd();
            A.TailEnd tailEnd3 = new A.TailEnd();

            outline3.Append(solidFill4);
            outline3.Append(round3);
            outline3.Append(headEnd3);
            outline3.Append(tailEnd3);

            shapeProperties3.Append(transform2D3);
            shapeProperties3.Append(presetGeometry3);
            shapeProperties3.Append(noFill2);
            shapeProperties3.Append(outline3);

            shape3.Append(nonVisualShapeProperties3);
            shape3.Append(shapeProperties3);


            // Shape 오류 확인
            if (!CheckAndFixRequiredShapeElements(shape1).StartsWith("OK"))
            {

            }
            // Shape 오류 확인
            if (!CheckAndFixRequiredShapeElements(shape2).StartsWith("OK"))
            {

            }
            // Shape 오류 확인
            if (!CheckAndFixRequiredShapeElements(shape3).StartsWith("OK"))
            {

            }


            groupShape1.Append(nonVisualGroupShapeProperties1);
            groupShape1.Append(groupShapeProperties1);
            groupShape1.Append(shape1);
            groupShape1.Append(shape2);
            groupShape1.Append(shape3);
            Xdr.ClientData clientData1 = new Xdr.ClientData();

            twoCellAnchor1.Append(fromMarker1);
            twoCellAnchor1.Append(toMarker1);
            twoCellAnchor1.Append(groupShape1);
            twoCellAnchor1.Append(clientData1);


            // CellAnchor 오류 확인
            if (!ValidateTwoCellAnchor(twoCellAnchor1).StartsWith("OK"))
            {

            }

            _Worksheet.WorksheetPart.DrawingsPart.WorksheetDrawing.Append(twoCellAnchor1);

            // 검증
            CreateValidTwoCellAnchor(fromCol, fromColOffset, fromRow, fromRowOffset, toCol, toColOffset, toRow, toRowOffset);

            if (netDay)
            {
                DrawingsDayTextBox(leftText, posX + 5, posY + 5, TextAlignmentTypeValues.Right);
            }
            else
            {
                DrawingsDayTextBox(leftText, posX - 15, posY + 5, TextAlignmentTypeValues.Right);
            }


            DrawingsDayTextBox(rightText, posX + 25, posY + 5, TextAlignmentTypeValues.Left);
            DrawingsBottomTextBox1(bottomText, posX, posY + 25);
            if (String.IsNullOrEmpty(starText) == false && starText != "")
                DrawingsStarTextBox(starText, posX, posY + 50);
            return nonVisualDrawingProperties1.Id;
        }

        public void DrawingsEndLine(string pitchText, double posX, double posY)
        {
            int NodeHeight = 50;
            double NodeWidth = 2.55;
            int fromCol = 0;
            int fromRow = 0;
            double fromColOffset = 0;
            double fromRowOffset = 0;
            int toCol = 0;
            int toRow = 0;
            double toColOffset = 0;
            double toRowOffset = 0;


            int toCol1 = 0;
            int toRow1 = 0;
            double toColOffset1 = 0;
            double toRowOffset1 = 0;



            CalculationPosition((posX / 10), posY, ref fromRow, ref fromRowOffset, ref fromCol, ref fromColOffset);
            CalculationPosition(((posX + 23) / 10), posY + NodeHeight, ref toRow, ref toRowOffset, ref toCol, ref toColOffset);
            CalculationPosition(((posX + 15) / 10), posY + NodeHeight, ref toRow1, ref toRowOffset1, ref toCol1, ref toColOffset1);

            DrawingsTextBox(toCol1, toColOffset1, toRow - 1, toRowOffset, pitchText);

            DrawingsConnection(fromRow, fromRowOffset, toCol, toColOffset, toRow, toRowOffset, toCol, toColOffset);


            int col1 = toCol;
            int col2 = toCol;
            double coloffset2 = toColOffset;
            double coloffset1 = toColOffset;

            int row1 = fromRow;
            int row2 = fromRow;
            double rowoffset2 = fromRowOffset;
            double rowoffset1 = fromRowOffset;

            coloffset1 = coloffset1 + 115000;
            if (coloffset1 > 230000)
            {
                col1 = col1 + 1;
                coloffset1 = coloffset1 - 230000;
            }
            coloffset2 = coloffset2 - 115000;
            if (coloffset2 < 0)
            {
                col2 = col2 - 1;
                coloffset2 = 230000 + coloffset2;
            }


            rowoffset1 = rowoffset1 + 100000;
            if (rowoffset1 > 200000)
            {
                row1 = row1 + 1;
                rowoffset1 = rowoffset1 - 200000;
            }

            rowoffset2 = rowoffset2 - 100000;
            if (rowoffset1 < 0)
            {
                row2 = row2 - 1;
                rowoffset2 = 200000 + rowoffset2;
            }

            DrawingsConnection(row2, rowoffset2, col2, coloffset2, row1, rowoffset1, col1, coloffset1);



        }

        private void DrawingsConnection(int fromRow, double fromRowOffset, int fromCol, double fromColOffset, int toRow, double toRowOffset, int toCol, double toColOffset)
        {
            if (_Worksheet.WorksheetPart.DrawingsPart == null) CreateDrawingPart();

            Xdr.TwoCellAnchor twoCellAnchor1 = new Xdr.TwoCellAnchor();

            Xdr.FromMarker fromMarker1 = new Xdr.FromMarker();
            Xdr.ColumnId columnId1 = new Xdr.ColumnId();
            columnId1.Text = fromCol.ToString();
            Xdr.ColumnOffset columnOffset1 = new Xdr.ColumnOffset();
            columnOffset1.Text = fromColOffset.ToString();
            Xdr.RowId rowId1 = new Xdr.RowId();
            rowId1.Text = fromRow.ToString();
            Xdr.RowOffset rowOffset1 = new Xdr.RowOffset();
            rowOffset1.Text = fromRowOffset.ToString();

            fromMarker1.Append(columnId1);
            fromMarker1.Append(columnOffset1);
            fromMarker1.Append(rowId1);
            fromMarker1.Append(rowOffset1);

            Xdr.ToMarker toMarker1 = new Xdr.ToMarker();
            Xdr.ColumnId columnId2 = new Xdr.ColumnId();
            columnId2.Text = toCol.ToString();
            Xdr.ColumnOffset columnOffset2 = new Xdr.ColumnOffset();
            columnOffset2.Text = toColOffset.ToString();
            Xdr.RowId rowId2 = new Xdr.RowId();
            rowId2.Text = toRow.ToString();
            Xdr.RowOffset rowOffset2 = new Xdr.RowOffset();
            rowOffset2.Text = toRowOffset.ToString();

            toMarker1.Append(columnId2);
            toMarker1.Append(columnOffset2);
            toMarker1.Append(rowId2);
            toMarker1.Append(rowOffset2);

            Xdr.Shape shape1 = new Xdr.Shape() { Macro = "", TextLink = "" };

            Xdr.NonVisualShapeProperties nonVisualShapeProperties1 = new Xdr.NonVisualShapeProperties();
            Xdr.NonVisualDrawingProperties nonVisualDrawingProperties1 = new Xdr.NonVisualDrawingProperties()
            {
                Id = (UInt32Value)GetNextShapeId(),
                Name = GetNextLineName()
            };

            Xdr.NonVisualShapeDrawingProperties nonVisualShapeDrawingProperties1 = new Xdr.NonVisualShapeDrawingProperties();
            A.ShapeLocks shapeLocks1 = new A.ShapeLocks() { NoChangeShapeType = true };

            nonVisualShapeDrawingProperties1.Append(shapeLocks1);

            nonVisualShapeProperties1.Append(nonVisualDrawingProperties1);
            nonVisualShapeProperties1.Append(nonVisualShapeDrawingProperties1);

            Xdr.ShapeProperties shapeProperties1 = new Xdr.ShapeProperties() { BlackWhiteMode = A.BlackWhiteModeValues.Auto };

            A.Transform2D transform2D1 = new A.Transform2D() { VerticalFlip = true };
            A.Offset offset1 = new A.Offset() { X = 2276475L, Y = 3390900L };
            A.Extents extents1 = new A.Extents() { Cx = 0L, Cy = 628650L };

            transform2D1.Append(offset1);
            transform2D1.Append(extents1);

            A.PresetGeometry presetGeometry1 = new A.PresetGeometry() { Preset = A.ShapeTypeValues.Line };
            A.AdjustValueList adjustValueList1 = new A.AdjustValueList();

            presetGeometry1.Append(adjustValueList1);
            A.NoFill noFill1 = new A.NoFill();

            A.Outline outline1 = new A.Outline() { Width = 9525 };

            A.SolidFill solidFill1 = new A.SolidFill();
            A.RgbColorModelHex rgbColorModelHex1 = new A.RgbColorModelHex() { Val = "000000" };

            solidFill1.Append(rgbColorModelHex1);
            A.Round round1 = new A.Round();
            A.HeadEnd headEnd1 = new A.HeadEnd();
            A.TailEnd tailEnd1 = new A.TailEnd();

            outline1.Append(solidFill1);
            outline1.Append(round1);
            outline1.Append(headEnd1);
            outline1.Append(tailEnd1);

            shapeProperties1.Append(transform2D1);
            shapeProperties1.Append(presetGeometry1);
            shapeProperties1.Append(noFill1);
            shapeProperties1.Append(outline1);

            shape1.Append(nonVisualShapeProperties1);
            shape1.Append(shapeProperties1);
            Xdr.ClientData clientData1 = new Xdr.ClientData();

            // Shape 오류 확인
            if (!CheckAndFixRequiredShapeElements(shape1).StartsWith("OK"))
            {

            }

            twoCellAnchor1.Append(fromMarker1);
            twoCellAnchor1.Append(toMarker1);
            twoCellAnchor1.Append(shape1);
            twoCellAnchor1.Append(clientData1);

            _Worksheet.WorksheetPart.DrawingsPart.WorksheetDrawing.Append(twoCellAnchor1);

            // 검증
            CreateValidTwoCellAnchor(fromCol, fromColOffset, fromRow, fromRowOffset, toCol, toColOffset, toRow, toRowOffset);
        }

        /// <summary>
        /// 연결선 그리는 메소드.
        /// </summary>
        /// <param name="startID"></param>
        /// <param name="endID"></param>
        /// <param name="stPOSX"></param>
        /// <param name="stPOSY"></param>
        /// <param name="endPOSX"></param>
        /// <param name="endPOSY"></param>
        /// <param name="thickness"></param>
        /// <param name="colorRGB"></param>
        /// <param name="isPolyLine"></param>
        public void DrawingsConnection(UInt32Value startID, UInt32Value endID, double stPOSX, double stPOSY, double endPOSX, double endPOSY, double thickness, string colorRGB, bool isPolyLine = true)
        {
            int NodeHeight = 50;
            int NodeWidth = 5;
            int fromCol = 0;
            int fromRow = 0;
            double fromColOffset = 0;
            double fromRowOffset = 0;
            int toCol = 0;
            int toRow = 0;
            double toColOffset = 0;
            double toRowOffset = 0;
            bool IsHorizontalFlip = false;
            bool IsVerticalFlip = false;

            if (stPOSX > endPOSX)
            {
                double tempPosX = stPOSX;
                stPOSX = endPOSX;
                endPOSX = tempPosX;
                IsHorizontalFlip = true;
            }

            if (stPOSY > endPOSY)
            {
                double tempPosY = stPOSY;
                stPOSY = endPOSY;
                endPOSY = tempPosY;
                IsVerticalFlip = true;
            }

            CalculationPosition(stPOSX / 10.0, stPOSY, ref fromRow, ref fromRowOffset, ref fromCol, ref fromColOffset);
            CalculationPosition(endPOSX / 10.0, endPOSY, ref toRow, ref toRowOffset, ref toCol, ref toColOffset);
            if (_Worksheet.WorksheetPart.DrawingsPart == null) CreateDrawingPart();


            // Xdr.TwoCellAnchor twoCellAnchor1 = new Xdr.TwoCellAnchor();



            Xdr.TwoCellAnchor twoCellAnchor3 = new Xdr.TwoCellAnchor();

            Xdr.FromMarker fromMarker3 = new Xdr.FromMarker();
            Xdr.ColumnId columnId5 = new Xdr.ColumnId();
            columnId5.Text = fromCol.ToString();
            Xdr.ColumnOffset columnOffset5 = new Xdr.ColumnOffset();
            columnOffset5.Text = fromColOffset.ToString();
            Xdr.RowId rowId5 = new Xdr.RowId();
            rowId5.Text = fromRow.ToString();
            Xdr.RowOffset rowOffset5 = new Xdr.RowOffset();
            rowOffset5.Text = fromRowOffset.ToString();

            fromMarker3.Append(columnId5);
            fromMarker3.Append(columnOffset5);
            fromMarker3.Append(rowId5);
            fromMarker3.Append(rowOffset5);

            Xdr.ToMarker toMarker3 = new Xdr.ToMarker();
            Xdr.ColumnId columnId6 = new Xdr.ColumnId();
            columnId6.Text = toCol.ToString();
            Xdr.ColumnOffset columnOffset6 = new Xdr.ColumnOffset();
            columnOffset6.Text = toColOffset.ToString();
            Xdr.RowId rowId6 = new Xdr.RowId();
            rowId6.Text = toRow.ToString();
            Xdr.RowOffset rowOffset6 = new Xdr.RowOffset();
            rowOffset6.Text = toRowOffset.ToString();

            toMarker3.Append(columnId6);
            toMarker3.Append(columnOffset6);
            toMarker3.Append(rowId6);
            toMarker3.Append(rowOffset6);

            Xdr.ConnectionShape connectionShape1 = new Xdr.ConnectionShape() { Macro = "" };

            Xdr.NonVisualConnectionShapeProperties nonVisualConnectionShapeProperties1 = new Xdr.NonVisualConnectionShapeProperties();
            Xdr.NonVisualDrawingProperties nonVisualDrawingProperties3 = new Xdr.NonVisualDrawingProperties() { Id = (UInt32Value)1028U, Name = "AutoShape 4" };

            Xdr.NonVisualConnectorShapeDrawingProperties nonVisualConnectorShapeDrawingProperties1 = new Xdr.NonVisualConnectorShapeDrawingProperties();
            A.ConnectionShapeLocks connectionShapeLocks1 = new A.ConnectionShapeLocks() { NoChangeShapeType = true };
            //A.StartConnection startConnection1 = new A.StartConnection() { Id = startID, Index = (UInt32Value)0U };
            //A.EndConnection endConnection1 = new A.EndConnection() { Id = endID, Index = (UInt32Value)2U };

            nonVisualConnectorShapeDrawingProperties1.Append(connectionShapeLocks1);
            //nonVisualConnectorShapeDrawingProperties1.Append(startConnection1);
            // nonVisualConnectorShapeDrawingProperties1.Append(endConnection1);

            nonVisualConnectionShapeProperties1.Append(nonVisualDrawingProperties3);
            nonVisualConnectionShapeProperties1.Append(nonVisualConnectorShapeDrawingProperties1);

            Xdr.ShapeProperties shapeProperties3 = new Xdr.ShapeProperties() { BlackWhiteMode = A.BlackWhiteModeValues.Auto };

            //좌표에 따라서 회전
            A.Transform2D transform2D3 = new A.Transform2D() { HorizontalFlip = IsHorizontalFlip, VerticalFlip = IsVerticalFlip };

            A.Offset offset3 = new A.Offset() { X = 1352550L, Y = 3114675L };
            A.Extents extents3 = new A.Extents() { Cx = 1209675L, Cy = 2333625L };

            transform2D3.Append(offset3);
            transform2D3.Append(extents3);

            A.PresetGeometry presetGeometry3;

            //if (isPolyLine)
            //    presetGeometry3 = new A.PresetGeometry() { Preset = A.ShapeTypeValues.BentConnector2 };
            //else
            presetGeometry3 = new A.PresetGeometry() { Preset = A.ShapeTypeValues.StraightConnector1 };

            A.AdjustValueList adjustValueList3 = new A.AdjustValueList();

            presetGeometry3.Append(adjustValueList3);
            A.NoFill noFill1 = new A.NoFill();

            //선굵기
            A.Outline outline3 = new A.Outline() { Width = (Int32Value)((int)(9525 * thickness)) };

            A.SolidFill solidFill5 = new A.SolidFill();

            A.RgbColorModelHex rgbColorModelHex5;


            //선색
            rgbColorModelHex5 = new A.RgbColorModelHex() { Val = colorRGB };



            solidFill5.Append(rgbColorModelHex5);
            A.Round round1 = new A.Round();
            A.HeadEnd headEnd3 = new A.HeadEnd();
            A.TailEnd tailEnd3;
            if (isPolyLine)
                tailEnd3 = new A.TailEnd() { Type = A.LineEndValues.Arrow, Width = A.LineEndWidthValues.Large, Length = A.LineEndLengthValues.Large };
            else
                tailEnd3 = new A.TailEnd();

            outline3.Append(solidFill5);
            outline3.Append(round1);
            outline3.Append(headEnd3);
            outline3.Append(tailEnd3);

            shapeProperties3.Append(transform2D3);
            shapeProperties3.Append(presetGeometry3);
            shapeProperties3.Append(noFill1);
            shapeProperties3.Append(outline3);

            connectionShape1.Append(nonVisualConnectionShapeProperties1);
            connectionShape1.Append(shapeProperties3);
            Xdr.ClientData clientData3 = new Xdr.ClientData();

            twoCellAnchor3.Append(fromMarker3);
            twoCellAnchor3.Append(toMarker3);
            twoCellAnchor3.Append(connectionShape1);
            twoCellAnchor3.Append(clientData3);


            _Worksheet.WorksheetPart.DrawingsPart.WorksheetDrawing.Append(twoCellAnchor3);

            // 검증
            CreateValidTwoCellAnchor(fromCol, fromColOffset, fromRow, fromRowOffset, toCol, toColOffset, toRow, toRowOffset);
        }



        /// <summary>
        /// 피치 텍스트박스를 그리는 메소드.
        /// </summary>
        /// <param name="stPOSX"></param>
        /// <param name="stPOSY"></param>
        /// <param name="pitchText"></param>
        public void DrawingsTextBox(double stPOSX, double stPOSY, string pitchText)
        {
            int fromCol = 0;
            int fromRow = 0;
            double fromColOffset = 0;
            double fromRowOffset = 0;

            CalculationPosition(stPOSX / 10.0, stPOSY, ref fromRow, ref fromRowOffset, ref fromCol, ref fromColOffset);

            if (_Worksheet.WorksheetPart.DrawingsPart == null) CreateDrawingPart();

            Xdr.OneCellAnchor oneCellAnchor1 = new Xdr.OneCellAnchor();

            Xdr.FromMarker fromMarker1 = new Xdr.FromMarker();
            Xdr.ColumnId columnId1 = new Xdr.ColumnId();
            columnId1.Text = fromCol.ToString();
            Xdr.ColumnOffset columnOffset1 = new Xdr.ColumnOffset();
            columnOffset1.Text = fromCol.ToString();
            Xdr.RowId rowId1 = new Xdr.RowId();
            rowId1.Text = fromRow.ToString();
            Xdr.RowOffset rowOffset1 = new Xdr.RowOffset();
            rowOffset1.Text = fromRowOffset.ToString();

            fromMarker1.Append(columnId1);
            fromMarker1.Append(columnOffset1);
            fromMarker1.Append(rowId1);
            fromMarker1.Append(rowOffset1);
            Xdr.Extent extent1 = new Xdr.Extent() { Cx = 379848L, Cy = 218521L };

            Xdr.Shape shape1 = new Xdr.Shape() { Macro = "", TextLink = "" };

            Xdr.NonVisualShapeProperties nonVisualShapeProperties1 = new Xdr.NonVisualShapeProperties();
            Xdr.NonVisualDrawingProperties nonVisualDrawingProperties1 = new Xdr.NonVisualDrawingProperties() { Id = (UInt32Value)1026U, Name = "Text Box 2" };

            Xdr.NonVisualShapeDrawingProperties nonVisualShapeDrawingProperties1 = new Xdr.NonVisualShapeDrawingProperties() { TextBox = true };
            A.ShapeLocks shapeLocks1 = new A.ShapeLocks() { NoChangeArrowheads = true };

            nonVisualShapeDrawingProperties1.Append(shapeLocks1);

            nonVisualShapeProperties1.Append(nonVisualDrawingProperties1);
            nonVisualShapeProperties1.Append(nonVisualShapeDrawingProperties1);

            Xdr.ShapeProperties shapeProperties1 = new Xdr.ShapeProperties() { BlackWhiteMode = A.BlackWhiteModeValues.Auto };

            A.Transform2D transform2D1 = new A.Transform2D();
            A.Offset offset1 = new A.Offset() { X = 1409700L, Y = 1000125L };
            A.Extents extents1 = new A.Extents() { Cx = 266700L, Cy = 219075L };

            transform2D1.Append(offset1);
            transform2D1.Append(extents1);

            A.PresetGeometry presetGeometry1 = new A.PresetGeometry() { Preset = A.ShapeTypeValues.Rectangle };
            A.AdjustValueList adjustValueList1 = new A.AdjustValueList();

            presetGeometry1.Append(adjustValueList1);
            A.NoFill noFill1 = new A.NoFill();

            A.Outline outline1 = new A.Outline() { Width = 1 };
            A.NoFill noFill2 = new A.NoFill();
            A.Miter miter1 = new A.Miter() { Limit = 800000 };
            A.HeadEnd headEnd1 = new A.HeadEnd();
            A.TailEnd tailEnd1 = new A.TailEnd();

            outline1.Append(noFill2);
            outline1.Append(miter1);
            outline1.Append(headEnd1);
            outline1.Append(tailEnd1);
            A.EffectList effectList1 = new A.EffectList();

            shapeProperties1.Append(transform2D1);
            shapeProperties1.Append(presetGeometry1);
            shapeProperties1.Append(noFill1);
            shapeProperties1.Append(outline1);
            shapeProperties1.Append(effectList1);

            Xdr.TextBody textBody1 = new Xdr.TextBody();

            A.BodyProperties bodyProperties1 = new A.BodyProperties() { Wrap = A.TextWrappingValues.None, LeftInset = 27432, TopInset = 18288, RightInset = 0, BottomInset = 0, Anchor = A.TextAnchoringTypeValues.Top, UpRight = true };
            A.ShapeAutoFit shapeAutoFit1 = new A.ShapeAutoFit();

            bodyProperties1.Append(shapeAutoFit1);
            A.ListStyle listStyle1 = new A.ListStyle();

            A.Paragraph paragraph1 = new A.Paragraph();

            A.ParagraphProperties paragraphProperties1 = new A.ParagraphProperties() { Alignment = A.TextAlignmentTypeValues.Left, RightToLeft = false };
            A.DefaultRunProperties defaultRunProperties1 = new A.DefaultRunProperties() { FontSize = 1000 };

            paragraphProperties1.Append(defaultRunProperties1);

            A.Run run1 = new A.Run();

            A.RunProperties runProperties1 = new A.RunProperties() { Language = "en-US", AlternativeLanguage = "ko-KR", FontSize = 1200, Bold = true, Italic = false, Underline = A.TextUnderlineValues.None, Strike = A.TextStrikeValues.NoStrike, Baseline = 0 };

            A.SolidFill solidFill1 = new A.SolidFill();
            A.RgbColorModelHex rgbColorModelHex1 = new A.RgbColorModelHex() { Val = "000000" };

            solidFill1.Append(rgbColorModelHex1);
            A.LatinFont latinFont1 = new A.LatinFont() { Typeface = "돋움" };
            A.EastAsianFont eastAsianFont1 = new A.EastAsianFont() { Typeface = "돋움" };

            runProperties1.Append(solidFill1);
            runProperties1.Append(latinFont1);
            runProperties1.Append(eastAsianFont1);
            A.Text text1 = new A.Text();
            text1.Text = pitchText;

            run1.Append(runProperties1);
            run1.Append(text1);

            paragraph1.Append(paragraphProperties1);
            paragraph1.Append(run1);

            textBody1.Append(bodyProperties1);
            textBody1.Append(listStyle1);
            textBody1.Append(paragraph1);

            shape1.Append(nonVisualShapeProperties1);
            shape1.Append(shapeProperties1);
            shape1.Append(textBody1);
            Xdr.ClientData clientData1 = new Xdr.ClientData();

            // Shape 오류 확인
            if (!CheckAndFixRequiredShapeElements(shape1).StartsWith("OK"))
            {

            }

            oneCellAnchor1.Append(fromMarker1);
            oneCellAnchor1.Append(extent1);
            oneCellAnchor1.Append(shape1);
            oneCellAnchor1.Append(clientData1);

            _Worksheet.WorksheetPart.DrawingsPart.WorksheetDrawing.Append(oneCellAnchor1);

            // 검증
            //CreateValidTwoCellAnchor(fromCol, fromColOffset, fromRow, fromRowOffset, toCol, toColOffset, toRow, toRowOffset);
        }

        public void DrawingsTextBox(int fromCol, double fromColOffset, int fromRow, double fromRowOffset, string pitchText)
        {
            //int fromCol = 0;
            //int fromRow = 0;
            //double fromColOffset = 0;
            //double fromRowOffset = 0;

            //CalculationPosition(stPOSX / 10.0, stPOSY, ref fromRow, ref fromRowOffset, ref fromCol, ref fromColOffset);

            if (_Worksheet.WorksheetPart.DrawingsPart == null) CreateDrawingPart();

            Xdr.OneCellAnchor oneCellAnchor1 = new Xdr.OneCellAnchor();

            Xdr.FromMarker fromMarker1 = new Xdr.FromMarker();
            Xdr.ColumnId columnId1 = new Xdr.ColumnId();
            columnId1.Text = fromCol.ToString();
            Xdr.ColumnOffset columnOffset1 = new Xdr.ColumnOffset();
            columnOffset1.Text = fromColOffset.ToString();
            Xdr.RowId rowId1 = new Xdr.RowId();
            rowId1.Text = fromRow.ToString();
            Xdr.RowOffset rowOffset1 = new Xdr.RowOffset();
            rowOffset1.Text = fromRowOffset.ToString();

            fromMarker1.Append(columnId1);
            fromMarker1.Append(columnOffset1);
            fromMarker1.Append(rowId1);
            fromMarker1.Append(rowOffset1);
            Xdr.Extent extent1 = new Xdr.Extent() { Cx = 379848L, Cy = 218521L };

            Xdr.Shape shape1 = new Xdr.Shape() { Macro = "", TextLink = "" };

            Xdr.NonVisualShapeProperties nonVisualShapeProperties1 = new Xdr.NonVisualShapeProperties();
            Xdr.NonVisualDrawingProperties nonVisualDrawingProperties1 = new Xdr.NonVisualDrawingProperties() { Id = (UInt32Value)1026U, Name = "Text Box 2" };

            Xdr.NonVisualShapeDrawingProperties nonVisualShapeDrawingProperties1 = new Xdr.NonVisualShapeDrawingProperties() { TextBox = true };
            A.ShapeLocks shapeLocks1 = new A.ShapeLocks() { NoChangeArrowheads = true };

            nonVisualShapeDrawingProperties1.Append(shapeLocks1);

            nonVisualShapeProperties1.Append(nonVisualDrawingProperties1);
            nonVisualShapeProperties1.Append(nonVisualShapeDrawingProperties1);

            Xdr.ShapeProperties shapeProperties1 = new Xdr.ShapeProperties() { BlackWhiteMode = A.BlackWhiteModeValues.Auto };

            A.Transform2D transform2D1 = new A.Transform2D();
            A.Offset offset1 = new A.Offset() { X = 1409700L, Y = 1000125L };
            A.Extents extents1 = new A.Extents() { Cx = 266700L, Cy = 219075L };

            transform2D1.Append(offset1);
            transform2D1.Append(extents1);

            A.PresetGeometry presetGeometry1 = new A.PresetGeometry() { Preset = A.ShapeTypeValues.Rectangle };
            A.AdjustValueList adjustValueList1 = new A.AdjustValueList();

            presetGeometry1.Append(adjustValueList1);
            A.NoFill noFill1 = new A.NoFill();

            A.Outline outline1 = new A.Outline() { Width = 1 };
            A.NoFill noFill2 = new A.NoFill();
            A.Miter miter1 = new A.Miter() { Limit = 800000 };
            A.HeadEnd headEnd1 = new A.HeadEnd();
            A.TailEnd tailEnd1 = new A.TailEnd();

            outline1.Append(noFill2);
            outline1.Append(miter1);
            outline1.Append(headEnd1);
            outline1.Append(tailEnd1);
            A.EffectList effectList1 = new A.EffectList();

            shapeProperties1.Append(transform2D1);
            shapeProperties1.Append(presetGeometry1);
            shapeProperties1.Append(noFill1);
            shapeProperties1.Append(outline1);
            shapeProperties1.Append(effectList1);

            Xdr.TextBody textBody1 = new Xdr.TextBody();

            A.BodyProperties bodyProperties1 = new A.BodyProperties() { Wrap = A.TextWrappingValues.None, LeftInset = 27432, TopInset = 18288, RightInset = 0, BottomInset = 0, Anchor = A.TextAnchoringTypeValues.Top, UpRight = true };
            A.ShapeAutoFit shapeAutoFit1 = new A.ShapeAutoFit();

            bodyProperties1.Append(shapeAutoFit1);
            A.ListStyle listStyle1 = new A.ListStyle();

            A.Paragraph paragraph1 = new A.Paragraph();

            A.ParagraphProperties paragraphProperties1 = new A.ParagraphProperties() { Alignment = A.TextAlignmentTypeValues.Left, RightToLeft = false };
            A.DefaultRunProperties defaultRunProperties1 = new A.DefaultRunProperties() { FontSize = 1000 };

            paragraphProperties1.Append(defaultRunProperties1);

            A.Run run1 = new A.Run();

            A.RunProperties runProperties1 = new A.RunProperties() { Language = "en-US", AlternativeLanguage = "ko-KR", FontSize = 1200, Bold = true, Italic = false, Underline = A.TextUnderlineValues.None, Strike = A.TextStrikeValues.NoStrike, Baseline = 0 };

            A.SolidFill solidFill1 = new A.SolidFill();
            A.RgbColorModelHex rgbColorModelHex1 = new A.RgbColorModelHex() { Val = "000000" };

            solidFill1.Append(rgbColorModelHex1);
            A.LatinFont latinFont1 = new A.LatinFont() { Typeface = "돋움" };
            A.EastAsianFont eastAsianFont1 = new A.EastAsianFont() { Typeface = "돋움" };

            runProperties1.Append(solidFill1);
            runProperties1.Append(latinFont1);
            runProperties1.Append(eastAsianFont1);
            A.Text text1 = new A.Text();
            text1.Text = pitchText;

            run1.Append(runProperties1);
            run1.Append(text1);

            paragraph1.Append(paragraphProperties1);
            paragraph1.Append(run1);

            textBody1.Append(bodyProperties1);
            textBody1.Append(listStyle1);
            textBody1.Append(paragraph1);

            shape1.Append(nonVisualShapeProperties1);
            shape1.Append(shapeProperties1);
            shape1.Append(textBody1);
            Xdr.ClientData clientData1 = new Xdr.ClientData();

            // Shape 오류 확인
            if (!CheckAndFixRequiredShapeElements(shape1).StartsWith("OK"))
            {

            }

            oneCellAnchor1.Append(fromMarker1);
            oneCellAnchor1.Append(extent1);
            oneCellAnchor1.Append(shape1);
            oneCellAnchor1.Append(clientData1);

            _Worksheet.WorksheetPart.DrawingsPart.WorksheetDrawing.Append(oneCellAnchor1);

            // 검증
            //CreateValidTwoCellAnchor(fromCol, fromColOffset, fromRow, fromRowOffset, toCol, toColOffset, toRow, toRowOffset);
        }

        /// <summary>
        /// EntLnt 텍스트박스를 그리는 메소드.
        /// </summary>
        /// <param name="text"></param>
        /// <param name="posX"></param>
        /// <param name="posY"></param>
        /// <param name="alignment"></param>
        private void DrawingsDayTextBox(string text, double posX, double posY, TextAlignmentTypeValues alignment)
        {

            int fromCol = 0;
            int fromRow = 0;
            double fromColOffset = 0;
            double fromRowOffset = 0;

            CalculationPosition((posX / 10), posY, ref fromRow, ref fromRowOffset, ref fromCol, ref fromColOffset);

            if (_Worksheet.WorksheetPart.DrawingsPart == null) CreateDrawingPart();

            Xdr.OneCellAnchor oneCellAnchor1 = new Xdr.OneCellAnchor();

            Xdr.FromMarker fromMarker1 = new Xdr.FromMarker();
            Xdr.ColumnId columnId1 = new Xdr.ColumnId();
            columnId1.Text = fromCol.ToString();
            Xdr.ColumnOffset columnOffset1 = new Xdr.ColumnOffset();
            columnOffset1.Text = fromColOffset.ToString();
            Xdr.RowId rowId1 = new Xdr.RowId();
            rowId1.Text = fromRow.ToString();
            Xdr.RowOffset rowOffset1 = new Xdr.RowOffset();
            rowOffset1.Text = fromRowOffset.ToString();

            fromMarker1.Append(columnId1);
            fromMarker1.Append(columnOffset1);
            fromMarker1.Append(rowId1);
            fromMarker1.Append(rowOffset1);
            Xdr.Extent extent1 = new Xdr.Extent() { Cx = 228600L, Cy = 219075L };

            Xdr.Shape shape1 = new Xdr.Shape() { Macro = "", TextLink = "" };

            Xdr.NonVisualShapeProperties nonVisualShapeProperties1 = new Xdr.NonVisualShapeProperties();
            Xdr.NonVisualDrawingProperties nonVisualDrawingProperties1 = new Xdr.NonVisualDrawingProperties() { Id = (UInt32Value)1026U, Name = "Text Box 2" };

            Xdr.NonVisualShapeDrawingProperties nonVisualShapeDrawingProperties1 = new Xdr.NonVisualShapeDrawingProperties() { TextBox = true };
            A.ShapeLocks shapeLocks1 = new A.ShapeLocks() { NoChangeArrowheads = true };

            nonVisualShapeDrawingProperties1.Append(shapeLocks1);

            nonVisualShapeProperties1.Append(nonVisualDrawingProperties1);
            nonVisualShapeProperties1.Append(nonVisualShapeDrawingProperties1);

            Xdr.ShapeProperties shapeProperties1 = new Xdr.ShapeProperties() { BlackWhiteMode = A.BlackWhiteModeValues.Auto };

            A.Transform2D transform2D1 = new A.Transform2D();
            A.Offset offset1 = new A.Offset() { X = 381000L, Y = 1828800L };
            A.Extents extents1 = new A.Extents() { Cx = 228600L, Cy = 219075L };

            transform2D1.Append(offset1);
            transform2D1.Append(extents1);

            A.PresetGeometry presetGeometry1 = new A.PresetGeometry() { Preset = A.ShapeTypeValues.Rectangle };
            A.AdjustValueList adjustValueList1 = new A.AdjustValueList();

            presetGeometry1.Append(adjustValueList1);
            A.NoFill noFill1 = new A.NoFill();

            A.Outline outline1 = new A.Outline() { Width = 1 };
            A.NoFill noFill2 = new A.NoFill();
            A.Miter miter1 = new A.Miter() { Limit = 800000 };
            A.HeadEnd headEnd1 = new A.HeadEnd();
            A.TailEnd tailEnd1 = new A.TailEnd();

            outline1.Append(noFill2);
            outline1.Append(miter1);
            outline1.Append(headEnd1);
            outline1.Append(tailEnd1);
            A.EffectList effectList1 = new A.EffectList();

            shapeProperties1.Append(transform2D1);
            shapeProperties1.Append(presetGeometry1);
            shapeProperties1.Append(noFill1);
            shapeProperties1.Append(outline1);
            shapeProperties1.Append(effectList1);

            Xdr.TextBody textBody1 = new Xdr.TextBody();

            A.BodyProperties bodyProperties1 = new A.BodyProperties() { Wrap = A.TextWrappingValues.None, LeftInset = 18288, TopInset = 18288, RightInset = 0, BottomInset = 0, Anchor = A.TextAnchoringTypeValues.Top, UpRight = true };
            A.ShapeAutoFit shapeAutoFit1 = new A.ShapeAutoFit();

            bodyProperties1.Append(shapeAutoFit1);
            A.ListStyle listStyle1 = new A.ListStyle();

            A.Paragraph paragraph1 = new A.Paragraph();

            A.ParagraphProperties paragraphProperties1 = new A.ParagraphProperties() { Alignment = alignment, RightToLeft = false };
            A.DefaultRunProperties defaultRunProperties1 = new A.DefaultRunProperties() { FontSize = 1000 };

            paragraphProperties1.Append(defaultRunProperties1);

            A.Run run1 = new A.Run();

            A.RunProperties runProperties1 = new A.RunProperties() { Language = "en-US", AlternativeLanguage = "ko-KR", FontSize = 1200, Bold = false, Italic = false, Underline = A.TextUnderlineValues.None, Strike = A.TextStrikeValues.NoStrike, Baseline = 0 };

            A.SolidFill solidFill1 = new A.SolidFill();
            A.RgbColorModelHex rgbColorModelHex1 = new A.RgbColorModelHex() { Val = "000000" };

            solidFill1.Append(rgbColorModelHex1);
            A.LatinFont latinFont1 = new A.LatinFont() { Typeface = "돋움" };
            A.EastAsianFont eastAsianFont1 = new A.EastAsianFont() { Typeface = "돋움" };

            runProperties1.Append(solidFill1);
            runProperties1.Append(latinFont1);
            runProperties1.Append(eastAsianFont1);
            A.Text text1 = new A.Text();
            text1.Text = text;

            run1.Append(runProperties1);
            run1.Append(text1);

            paragraph1.Append(paragraphProperties1);
            paragraph1.Append(run1);

            textBody1.Append(bodyProperties1);
            textBody1.Append(listStyle1);
            textBody1.Append(paragraph1);

            shape1.Append(nonVisualShapeProperties1);
            shape1.Append(shapeProperties1);
            shape1.Append(textBody1);
            Xdr.ClientData clientData1 = new Xdr.ClientData();
            
            // Shape 오류 확인
            if (!CheckAndFixRequiredShapeElements(shape1).StartsWith("OK"))
            {

            }

            oneCellAnchor1.Append(fromMarker1);
            oneCellAnchor1.Append(extent1);
            oneCellAnchor1.Append(shape1);
            oneCellAnchor1.Append(clientData1);

            _Worksheet.WorksheetPart.DrawingsPart.WorksheetDrawing.Append(oneCellAnchor1);

            // 검증
            //CreateValidTwoCellAnchor(fromCol, fromColOffset, fromRow, fromRowOffset, toCol, toColOffset, toRow, toRowOffset);
        }

        /// <summary>
        /// KL지정 텍스트박스를 그리는 메소드
        /// </summary>
        /// <param name="text"></param>
        /// <param name="posX"></param>
        /// <param name="posY"></param>
        private void DrawingsStarTextBox(string text, double posX, double posY)
        {
            int NodeHeight = 16;
            double NodeWidth = 5;
            int fromCol = 0;
            int fromRow = 0;
            double fromColOffset = 0;
            double fromRowOffset = 0;
            int toCol = 0;
            int toRow = 0;
            double toColOffset = 0;
            double toRowOffset = 0;


            CalculationPosition((posX / 10), posY, ref fromRow, ref fromRowOffset, ref fromCol, ref fromColOffset);
            CalculationPosition((posX / 10) + NodeWidth, posY + NodeHeight, ref toRow, ref toRowOffset, ref toCol, ref toColOffset);
            if (_Worksheet.WorksheetPart.DrawingsPart == null) CreateDrawingPart();

            Xdr.TwoCellAnchor twoCellAnchor1 = new Xdr.TwoCellAnchor() { EditAs = Xdr.EditAsValues.OneCell };

            Xdr.FromMarker fromMarker1 = new Xdr.FromMarker();
            Xdr.ColumnId columnId1 = new Xdr.ColumnId();
            columnId1.Text = fromCol.ToString();
            Xdr.ColumnOffset columnOffset1 = new Xdr.ColumnOffset();
            columnOffset1.Text = fromColOffset.ToString();
            Xdr.RowId rowId1 = new Xdr.RowId();
            rowId1.Text = fromRow.ToString();
            Xdr.RowOffset rowOffset1 = new Xdr.RowOffset();
            rowOffset1.Text = fromRowOffset.ToString();

            fromMarker1.Append(columnId1);
            fromMarker1.Append(columnOffset1);
            fromMarker1.Append(rowId1);
            fromMarker1.Append(rowOffset1);

            Xdr.ToMarker toMarker1 = new Xdr.ToMarker();
            Xdr.ColumnId columnId2 = new Xdr.ColumnId();
            columnId2.Text = toCol.ToString();
            Xdr.ColumnOffset columnOffset2 = new Xdr.ColumnOffset();
            columnOffset2.Text = toColOffset.ToString();
            Xdr.RowId rowId2 = new Xdr.RowId();
            rowId2.Text = toRow.ToString();
            Xdr.RowOffset rowOffset2 = new Xdr.RowOffset();
            rowOffset2.Text = toRowOffset.ToString();

            toMarker1.Append(columnId2);
            toMarker1.Append(columnOffset2);
            toMarker1.Append(rowId2);
            toMarker1.Append(rowOffset2);

            Xdr.Shape shape1 = new Xdr.Shape() { Macro = "", TextLink = "" };

            Xdr.NonVisualShapeProperties nonVisualShapeProperties1 = new Xdr.NonVisualShapeProperties();
            Xdr.NonVisualDrawingProperties nonVisualDrawingProperties1 = new Xdr.NonVisualDrawingProperties() { Id = (UInt32Value)1025U, Name = "Text Box 17" };

            Xdr.NonVisualShapeDrawingProperties nonVisualShapeDrawingProperties1 = new Xdr.NonVisualShapeDrawingProperties() { TextBox = true };
            A.ShapeLocks shapeLocks1 = new A.ShapeLocks() { NoChangeArrowheads = true };

            nonVisualShapeDrawingProperties1.Append(shapeLocks1);

            nonVisualShapeProperties1.Append(nonVisualDrawingProperties1);
            nonVisualShapeProperties1.Append(nonVisualShapeDrawingProperties1);

            Xdr.ShapeProperties shapeProperties1 = new Xdr.ShapeProperties() { BlackWhiteMode = A.BlackWhiteModeValues.Auto };

            A.Transform2D transform2D1 = new A.Transform2D();
            A.Offset offset1 = new A.Offset() { X = 466725L, Y = 2428875L };
            A.Extents extents1 = new A.Extents() { Cx = 723900L, Cy = 200025L };

            transform2D1.Append(offset1);
            transform2D1.Append(extents1);

            A.PresetGeometry presetGeometry1 = new A.PresetGeometry() { Preset = A.ShapeTypeValues.Rectangle };
            A.AdjustValueList adjustValueList1 = new A.AdjustValueList();

            presetGeometry1.Append(adjustValueList1);
            A.NoFill noFill1 = new A.NoFill();

            A.Outline outline1 = new A.Outline() { Width = 9525 };
            A.NoFill noFill2 = new A.NoFill();
            A.Miter miter1 = new A.Miter() { Limit = 800000 };
            A.HeadEnd headEnd1 = new A.HeadEnd();
            A.TailEnd tailEnd1 = new A.TailEnd();

            outline1.Append(noFill2);
            outline1.Append(miter1);
            outline1.Append(headEnd1);
            outline1.Append(tailEnd1);

            shapeProperties1.Append(transform2D1);
            shapeProperties1.Append(presetGeometry1);
            shapeProperties1.Append(noFill1);
            shapeProperties1.Append(outline1);

            Xdr.TextBody textBody1 = new Xdr.TextBody();
            A.BodyProperties bodyProperties1 = new A.BodyProperties() { VerticalOverflow = A.TextVerticalOverflowValues.Clip, Wrap = A.TextWrappingValues.Square, LeftInset = 0, TopInset = 0, RightInset = 0, BottomInset = 0, Anchor = A.TextAnchoringTypeValues.Top, UpRight = true };
            A.ListStyle listStyle1 = new A.ListStyle();

            A.Paragraph paragraph1 = new A.Paragraph();

            A.ParagraphProperties paragraphProperties1 = new A.ParagraphProperties() { Alignment = A.TextAlignmentTypeValues.Center, RightToLeft = false };
            A.DefaultRunProperties defaultRunProperties1 = new A.DefaultRunProperties() { FontSize = 1000 };

            paragraphProperties1.Append(defaultRunProperties1);

            A.Run run1 = new A.Run();

            A.RunProperties runProperties1 = new A.RunProperties() { Language = "ko-KR", AlternativeLanguage = "en-US", FontSize = 1200, Bold = true, Italic = false, Underline = A.TextUnderlineValues.None, Strike = A.TextStrikeValues.NoStrike, Baseline = 0 };

            A.SolidFill solidFill1 = new A.SolidFill();
            A.RgbColorModelHex rgbColorModelHex1 = new A.RgbColorModelHex() { Val = "000000" };

            solidFill1.Append(rgbColorModelHex1);
            A.LatinFont latinFont1 = new A.LatinFont() { Typeface = "돋움" };
            A.EastAsianFont eastAsianFont1 = new A.EastAsianFont() { Typeface = "돋움" };

            runProperties1.Append(solidFill1);
            runProperties1.Append(latinFont1);
            runProperties1.Append(eastAsianFont1);
            A.Text text1 = new A.Text();
            text1.Text = text;

            run1.Append(runProperties1);
            run1.Append(text1);

            paragraph1.Append(paragraphProperties1);
            paragraph1.Append(run1);

            textBody1.Append(bodyProperties1);
            textBody1.Append(listStyle1);
            textBody1.Append(paragraph1);

            shape1.Append(nonVisualShapeProperties1);
            shape1.Append(shapeProperties1);
            shape1.Append(textBody1);
            Xdr.ClientData clientData1 = new Xdr.ClientData();

            // Shape 오류 확인
            if (!CheckAndFixRequiredShapeElements(shape1).StartsWith("OK"))
            {

            }

            twoCellAnchor1.Append(fromMarker1);
            twoCellAnchor1.Append(toMarker1);
            twoCellAnchor1.Append(shape1);
            twoCellAnchor1.Append(clientData1);

            _Worksheet.WorksheetPart.DrawingsPart.WorksheetDrawing.Append(twoCellAnchor1);

            // 검증
            CreateValidTwoCellAnchor(fromCol, fromColOffset, fromRow, fromRowOffset, toCol, toColOffset, toRow, toRowOffset);
        }


        private void DrawingsBottomTextBox(string text, double posX, double posY)
        {
            int NodeHeight = 182;
            double NodeWidth = 6;
            int fromCol = 0;
            int fromRow = 0;
            double fromColOffset = 0;
            double fromRowOffset = 0;
            int toCol = 0;
            int toRow = 0;
            double toColOffset = 0;
            double toRowOffset = 0;

            CalculationPosition((posX / 10), posY, ref fromRow, ref fromRowOffset, ref fromCol, ref fromColOffset);
            CalculationPosition((posX / 10) + NodeWidth, posY + NodeHeight, ref toRow, ref toRowOffset, ref toCol, ref toColOffset);
            if (_Worksheet.WorksheetPart.DrawingsPart == null) CreateDrawingPart();

            Xdr.TwoCellAnchor twoCellAnchor1 = new Xdr.TwoCellAnchor() { EditAs = Xdr.EditAsValues.OneCell };

            Xdr.FromMarker fromMarker1 = new Xdr.FromMarker();
            Xdr.ColumnId columnId1 = new Xdr.ColumnId();
            columnId1.Text = fromCol.ToString();
            Xdr.ColumnOffset columnOffset1 = new Xdr.ColumnOffset();
            columnOffset1.Text = fromColOffset.ToString();
            Xdr.RowId rowId1 = new Xdr.RowId();
            rowId1.Text = fromRow.ToString();
            Xdr.RowOffset rowOffset1 = new Xdr.RowOffset();
            rowOffset1.Text = fromRowOffset.ToString();

            fromMarker1.Append(columnId1);
            fromMarker1.Append(columnOffset1);
            fromMarker1.Append(rowId1);
            fromMarker1.Append(rowOffset1);

            Xdr.ToMarker toMarker1 = new Xdr.ToMarker();
            Xdr.ColumnId columnId2 = new Xdr.ColumnId();
            columnId2.Text = toCol.ToString();
            Xdr.ColumnOffset columnOffset2 = new Xdr.ColumnOffset();
            columnOffset2.Text = toColOffset.ToString();
            Xdr.RowId rowId2 = new Xdr.RowId();
            rowId2.Text = toRow.ToString();
            Xdr.RowOffset rowOffset2 = new Xdr.RowOffset();
            rowOffset2.Text = toRowOffset.ToString();

            toMarker1.Append(columnId2);
            toMarker1.Append(columnOffset2);
            toMarker1.Append(rowId2);
            toMarker1.Append(rowOffset2);

            Xdr.Shape shape1 = new Xdr.Shape() { Macro = "", TextLink = "" };

            Xdr.NonVisualShapeProperties nonVisualShapeProperties1 = new Xdr.NonVisualShapeProperties();
            Xdr.NonVisualDrawingProperties nonVisualDrawingProperties1 = new Xdr.NonVisualDrawingProperties() { Id = (UInt32Value)1025U, Name = "Text Box 17" };

            Xdr.NonVisualShapeDrawingProperties nonVisualShapeDrawingProperties1 = new Xdr.NonVisualShapeDrawingProperties() { TextBox = true };
            A.ShapeLocks shapeLocks1 = new A.ShapeLocks() { NoChangeArrowheads = true };

            nonVisualShapeDrawingProperties1.Append(shapeLocks1);

            nonVisualShapeProperties1.Append(nonVisualDrawingProperties1);
            nonVisualShapeProperties1.Append(nonVisualShapeDrawingProperties1);

            Xdr.ShapeProperties shapeProperties1 = new Xdr.ShapeProperties() { BlackWhiteMode = A.BlackWhiteModeValues.Auto };

            A.Transform2D transform2D1 = new A.Transform2D();
            A.Offset offset1 = new A.Offset() { X = 381000L, Y = 2590800L };
            A.Extents extents1 = new A.Extents() { Cx = 685800L, Cy = 2162175L };

            transform2D1.Append(offset1);
            transform2D1.Append(extents1);

            A.PresetGeometry presetGeometry1 = new A.PresetGeometry() { Preset = A.ShapeTypeValues.Rectangle };
            A.AdjustValueList adjustValueList1 = new A.AdjustValueList();

            presetGeometry1.Append(adjustValueList1);
            A.NoFill noFill1 = new A.NoFill();

            A.Outline outline1 = new A.Outline() { Width = 9525 };
            A.NoFill noFill2 = new A.NoFill();
            A.Miter miter1 = new A.Miter() { Limit = 800000 };
            A.HeadEnd headEnd1 = new A.HeadEnd();
            A.TailEnd tailEnd1 = new A.TailEnd();

            outline1.Append(noFill2);
            outline1.Append(miter1);
            outline1.Append(headEnd1);
            outline1.Append(tailEnd1);

            shapeProperties1.Append(transform2D1);
            shapeProperties1.Append(presetGeometry1);
            shapeProperties1.Append(noFill1);
            shapeProperties1.Append(outline1);

            Xdr.TextBody textBody1 = new Xdr.TextBody();
            A.BodyProperties bodyProperties1 = new A.BodyProperties() { VerticalOverflow = A.TextVerticalOverflowValues.Clip, Wrap = A.TextWrappingValues.Square, LeftInset = 27432, TopInset = 18288, RightInset = 0, BottomInset = 0, Anchor = A.TextAnchoringTypeValues.Top, UpRight = true };
            A.ListStyle listStyle1 = new A.ListStyle();

            A.Paragraph paragraph1 = new A.Paragraph();

            A.ParagraphProperties paragraphProperties1 = new A.ParagraphProperties() { Alignment = A.TextAlignmentTypeValues.Center, RightToLeft = false };
            A.DefaultRunProperties defaultRunProperties1 = new A.DefaultRunProperties() { FontSize = 1000 };

            paragraphProperties1.Append(defaultRunProperties1);

            A.Run run1 = new A.Run();

            A.RunProperties runProperties1 = new A.RunProperties() { Language = "en-US", AlternativeLanguage = "ko-KR", FontSize = 1200, Bold = true, Italic = false, Underline = A.TextUnderlineValues.None, Strike = A.TextStrikeValues.NoStrike, Baseline = 0 };

            A.SolidFill solidFill1 = new A.SolidFill();
            A.RgbColorModelHex rgbColorModelHex1 = new A.RgbColorModelHex() { Val = "000000" };

            solidFill1.Append(rgbColorModelHex1);
            A.LatinFont latinFont1 = new A.LatinFont() { Typeface = "돋움" };
            A.EastAsianFont eastAsianFont1 = new A.EastAsianFont() { Typeface = "돋움" };

            runProperties1.Append(solidFill1);
            runProperties1.Append(latinFont1);
            runProperties1.Append(eastAsianFont1);
            A.Text text1 = new A.Text();
            text1.Text = text;

            run1.Append(runProperties1);
            run1.Append(text1);

            paragraph1.Append(paragraphProperties1);
            paragraph1.Append(run1);

            textBody1.Append(bodyProperties1);
            textBody1.Append(listStyle1);
            textBody1.Append(paragraph1);

            shape1.Append(nonVisualShapeProperties1);
            shape1.Append(shapeProperties1);
            shape1.Append(textBody1);
            Xdr.ClientData clientData1 = new Xdr.ClientData();

            // Shape 오류 확인
            if (!CheckAndFixRequiredShapeElements(shape1).StartsWith("OK"))
            {

            }

            twoCellAnchor1.Append(fromMarker1);
            twoCellAnchor1.Append(toMarker1);
            twoCellAnchor1.Append(shape1);
            twoCellAnchor1.Append(clientData1);

            _Worksheet.WorksheetPart.DrawingsPart.WorksheetDrawing.Append(twoCellAnchor1);

            // 검증
            CreateValidTwoCellAnchor(fromCol, fromColOffset, fromRow, fromRowOffset, toCol, toColOffset, toRow, toRowOffset);
        }

        /// <summary>
        /// BlockString 텍스트박스를 그리는 메소드
        /// </summary>
        /// <param name="text"></param>
        /// <param name="posX"></param>
        /// <param name="posY"></param>
        private void DrawingsBottomTextBox1(string text, double posX, double posY)
        {

            int fromCol = 0;
            int fromRow = 0;
            double fromColOffset = 0;
            double fromRowOffset = 0;


            CalculationPosition((posX / 10), posY, ref fromRow, ref fromRowOffset, ref fromCol, ref fromColOffset);
            //   CalculationPosition((posX / 10) + NodeWidth, posY + NodeHeight, ref toRow, ref toRowOffset, ref toCol, ref toColOffset);
            if (_Worksheet.WorksheetPart.DrawingsPart == null) CreateDrawingPart();

            Xdr.OneCellAnchor oneCellAnchor1 = new Xdr.OneCellAnchor();

            Xdr.FromMarker fromMarker1 = new Xdr.FromMarker();
            Xdr.ColumnId columnId1 = new Xdr.ColumnId();
            columnId1.Text = fromCol.ToString();
            Xdr.ColumnOffset columnOffset1 = new Xdr.ColumnOffset();
            columnOffset1.Text = fromColOffset.ToString();
            Xdr.RowId rowId1 = new Xdr.RowId();
            rowId1.Text = fromRow.ToString();
            Xdr.RowOffset rowOffset1 = new Xdr.RowOffset();
            rowOffset1.Text = fromRowOffset.ToString();

            fromMarker1.Append(columnId1);
            fromMarker1.Append(columnOffset1);
            fromMarker1.Append(rowId1);
            fromMarker1.Append(rowOffset1);
            Xdr.Extent extent1 = new Xdr.Extent() { Cx = 1299010L, Cy = 618631L };

            Xdr.Shape shape1 = new Xdr.Shape() { Macro = "", TextLink = "" };

            Xdr.NonVisualShapeProperties nonVisualShapeProperties1 = new Xdr.NonVisualShapeProperties();
            Xdr.NonVisualDrawingProperties nonVisualDrawingProperties1 = new Xdr.NonVisualDrawingProperties() { Id = (UInt32Value)1025U, Name = "Text Box 1" };

            Xdr.NonVisualShapeDrawingProperties nonVisualShapeDrawingProperties1 = new Xdr.NonVisualShapeDrawingProperties() { TextBox = true };
            A.ShapeLocks shapeLocks1 = new A.ShapeLocks() { NoChangeArrowheads = true };

            nonVisualShapeDrawingProperties1.Append(shapeLocks1);

            nonVisualShapeProperties1.Append(nonVisualDrawingProperties1);
            nonVisualShapeProperties1.Append(nonVisualShapeDrawingProperties1);

            Xdr.ShapeProperties shapeProperties1 = new Xdr.ShapeProperties() { BlackWhiteMode = A.BlackWhiteModeValues.Auto };

            A.Transform2D transform2D1 = new A.Transform2D();
            A.Offset offset1 = new A.Offset() { X = 485775L, Y = 447675L };
            A.Extents extents1 = new A.Extents() { Cx = 676532L, Cy = 218521L };

            transform2D1.Append(offset1);
            transform2D1.Append(extents1);

            A.PresetGeometry presetGeometry1 = new A.PresetGeometry() { Preset = A.ShapeTypeValues.Rectangle };
            A.AdjustValueList adjustValueList1 = new A.AdjustValueList();

            presetGeometry1.Append(adjustValueList1);
            A.NoFill noFill1 = new A.NoFill();

            A.Outline outline1 = new A.Outline() { Width = 1 };
            A.NoFill noFill2 = new A.NoFill();
            A.Miter miter1 = new A.Miter() { Limit = 800000 };
            A.HeadEnd headEnd1 = new A.HeadEnd();
            A.TailEnd tailEnd1 = new A.TailEnd();

            outline1.Append(noFill2);
            outline1.Append(miter1);
            outline1.Append(headEnd1);
            outline1.Append(tailEnd1);
            A.EffectList effectList1 = new A.EffectList();

            shapeProperties1.Append(transform2D1);
            shapeProperties1.Append(presetGeometry1);
            shapeProperties1.Append(noFill1);
            shapeProperties1.Append(outline1);
            shapeProperties1.Append(effectList1);

            Xdr.TextBody textBody1 = new Xdr.TextBody();

            A.BodyProperties bodyProperties1 = new A.BodyProperties() { Wrap = A.TextWrappingValues.None, LeftInset = 27432, TopInset = 18288, RightInset = 0, BottomInset = 0, Anchor = A.TextAnchoringTypeValues.Top, UpRight = true };
            A.ShapeAutoFit shapeAutoFit1 = new A.ShapeAutoFit();

            bodyProperties1.Append(shapeAutoFit1);
            A.ListStyle listStyle1 = new A.ListStyle();

            A.Paragraph paragraph1 = new A.Paragraph();

            A.ParagraphProperties paragraphProperties1 = new A.ParagraphProperties() { Alignment = A.TextAlignmentTypeValues.Left, RightToLeft = false };
            A.DefaultRunProperties defaultRunProperties1 = new A.DefaultRunProperties() { FontSize = 1000 };

            paragraphProperties1.Append(defaultRunProperties1);

            A.Run run1 = new A.Run();

            A.RunProperties runProperties1 = new A.RunProperties() { Language = "en-US", AlternativeLanguage = "ko-KR", FontSize = 1200, Bold = true, Italic = false, Underline = A.TextUnderlineValues.None, Strike = A.TextStrikeValues.NoStrike, Baseline = 0 };

            A.SolidFill solidFill1 = new A.SolidFill();
            A.RgbColorModelHex rgbColorModelHex1 = new A.RgbColorModelHex() { Val = "000000" };

            solidFill1.Append(rgbColorModelHex1);
            A.LatinFont latinFont1 = new A.LatinFont() { Typeface = "돋움" };
            A.EastAsianFont eastAsianFont1 = new A.EastAsianFont() { Typeface = "돋움" };

            runProperties1.Append(solidFill1);
            runProperties1.Append(latinFont1);
            runProperties1.Append(eastAsianFont1);
            A.Text text1 = new A.Text();
            text1.Text = text;

            run1.Append(runProperties1);
            run1.Append(text1);

            paragraph1.Append(paragraphProperties1);
            paragraph1.Append(run1);

            textBody1.Append(bodyProperties1);
            textBody1.Append(listStyle1);
            textBody1.Append(paragraph1);

            shape1.Append(nonVisualShapeProperties1);
            shape1.Append(shapeProperties1);
            shape1.Append(textBody1);
            Xdr.ClientData clientData1 = new Xdr.ClientData();

            // Shape 오류 확인
            if (!CheckAndFixRequiredShapeElements(shape1).StartsWith("OK"))
            {

            }

            oneCellAnchor1.Append(fromMarker1);
            oneCellAnchor1.Append(extent1);
            oneCellAnchor1.Append(shape1);
            oneCellAnchor1.Append(clientData1);

            _Worksheet.WorksheetPart.DrawingsPart.WorksheetDrawing.Append(oneCellAnchor1);

            // 검증
            //CreateValidTwoCellAnchor(fromCol, fromColOffset, fromRow, fromRowOffset, toCol, toColOffset, toRow, toRowOffset);
        }


        public void DrawingsMainEvent(string KL, string LC, string FT, string FT2, string FT3, string week1, string week2, string week3)
        {
            if (_Worksheet.WorksheetPart.DrawingsPart == null) CreateDrawingPart();

            Xdr.TwoCellAnchor twoCellAnchor1 = new Xdr.TwoCellAnchor();

            Xdr.FromMarker fromMarker1 = new Xdr.FromMarker();
            Xdr.ColumnId columnId1 = new Xdr.ColumnId();
            columnId1.Text = "101";
            Xdr.ColumnOffset columnOffset1 = new Xdr.ColumnOffset();
            columnOffset1.Text = "0";
            Xdr.RowId rowId1 = new Xdr.RowId();
            rowId1.Text = "2";
            Xdr.RowOffset rowOffset1 = new Xdr.RowOffset();
            rowOffset1.Text = "0";

            fromMarker1.Append(columnId1);
            fromMarker1.Append(columnOffset1);
            fromMarker1.Append(rowId1);
            fromMarker1.Append(rowOffset1);

            Xdr.ToMarker toMarker1 = new Xdr.ToMarker();
            Xdr.ColumnId columnId2 = new Xdr.ColumnId();
            columnId2.Text = "105";
            Xdr.ColumnOffset columnOffset2 = new Xdr.ColumnOffset();
            columnOffset2.Text = "350000";
            Xdr.RowId rowId2 = new Xdr.RowId();
            rowId2.Text = "5";
            Xdr.RowOffset rowOffset2 = new Xdr.RowOffset();
            rowOffset2.Text = "0";

            toMarker1.Append(columnId2);
            toMarker1.Append(columnOffset2);
            toMarker1.Append(rowId2);
            toMarker1.Append(rowOffset2);

            Xdr.GroupShape groupShape1 = new Xdr.GroupShape();

            Xdr.NonVisualGroupShapeProperties nonVisualGroupShapeProperties1 = new Xdr.NonVisualGroupShapeProperties();
            Xdr.NonVisualDrawingProperties nonVisualDrawingProperties1 = new Xdr.NonVisualDrawingProperties() { Id = (UInt32Value)1035U, Name = "Group 11" };

            Xdr.NonVisualGroupShapeDrawingProperties nonVisualGroupShapeDrawingProperties1 = new Xdr.NonVisualGroupShapeDrawingProperties();
            A.GroupShapeLocks groupShapeLocks1 = new A.GroupShapeLocks();

            nonVisualGroupShapeDrawingProperties1.Append(groupShapeLocks1);

            nonVisualGroupShapeProperties1.Append(nonVisualDrawingProperties1);
            nonVisualGroupShapeProperties1.Append(nonVisualGroupShapeDrawingProperties1);

            Xdr.GroupShapeProperties groupShapeProperties1 = new Xdr.GroupShapeProperties() { BlackWhiteMode = A.BlackWhiteModeValues.Auto };

            A.TransformGroup transformGroup1 = new A.TransformGroup();
            A.Offset offset1 = new A.Offset() { X = 1076325L, Y = 2447925L };
            A.Extents extents1 = new A.Extents() { Cx = 2876550L, Cy = 552450L };
            A.ChildOffset childOffset1 = new A.ChildOffset() { X = 107L, Y = 329L };
            A.ChildExtents childExtents1 = new A.ChildExtents() { Cx = 302L, Cy = 58L };

            transformGroup1.Append(offset1);
            transformGroup1.Append(extents1);
            transformGroup1.Append(childOffset1);
            transformGroup1.Append(childExtents1);

            groupShapeProperties1.Append(transformGroup1);

            Xdr.Shape shape1 = new Xdr.Shape() { Macro = "", TextLink = "" };

            Xdr.NonVisualShapeProperties nonVisualShapeProperties1 = new Xdr.NonVisualShapeProperties();
            Xdr.NonVisualDrawingProperties nonVisualDrawingProperties2 = new Xdr.NonVisualDrawingProperties() { Id = (UInt32Value)1025U, Name = "Rectangle 333" };

            Xdr.NonVisualShapeDrawingProperties nonVisualShapeDrawingProperties1 = new Xdr.NonVisualShapeDrawingProperties();
            A.ShapeLocks shapeLocks1 = new A.ShapeLocks() { NoChangeArrowheads = true };

            nonVisualShapeDrawingProperties1.Append(shapeLocks1);

            nonVisualShapeProperties1.Append(nonVisualDrawingProperties2);
            nonVisualShapeProperties1.Append(nonVisualShapeDrawingProperties1);

            Xdr.ShapeProperties shapeProperties1 = new Xdr.ShapeProperties() { BlackWhiteMode = A.BlackWhiteModeValues.Auto };

            A.Transform2D transform2D1 = new A.Transform2D();
            A.Offset offset2 = new A.Offset() { X = 137L, Y = 352L };
            A.Extents extents2 = new A.Extents() { Cx = 74L, Cy = 8L };

            transform2D1.Append(offset2);
            transform2D1.Append(extents2);

            A.PresetGeometry presetGeometry1 = new A.PresetGeometry() { Preset = A.ShapeTypeValues.Rectangle };
            A.AdjustValueList adjustValueList1 = new A.AdjustValueList();

            presetGeometry1.Append(adjustValueList1);

            A.SolidFill solidFill1 = new A.SolidFill();
            A.RgbColorModelHex rgbColorModelHex1 = new A.RgbColorModelHex() { Val = "FFFFFF" };

            solidFill1.Append(rgbColorModelHex1);

            A.Outline outline1 = new A.Outline() { Width = 12700 };

            A.SolidFill solidFill2 = new A.SolidFill();
            A.RgbColorModelHex rgbColorModelHex2 = new A.RgbColorModelHex() { Val = "000000" };

            solidFill2.Append(rgbColorModelHex2);
            A.Miter miter1 = new A.Miter() { Limit = 800000 };
            A.HeadEnd headEnd1 = new A.HeadEnd();
            A.TailEnd tailEnd1 = new A.TailEnd();

            outline1.Append(solidFill2);
            outline1.Append(miter1);
            outline1.Append(headEnd1);
            outline1.Append(tailEnd1);

            shapeProperties1.Append(transform2D1);
            shapeProperties1.Append(presetGeometry1);
            shapeProperties1.Append(solidFill1);
            shapeProperties1.Append(outline1);

            shape1.Append(nonVisualShapeProperties1);
            shape1.Append(shapeProperties1);

            Xdr.Shape shape2 = new Xdr.Shape() { Macro = "", TextLink = "" };

            Xdr.NonVisualShapeProperties nonVisualShapeProperties2 = new Xdr.NonVisualShapeProperties();
            Xdr.NonVisualDrawingProperties nonVisualDrawingProperties3 = new Xdr.NonVisualDrawingProperties() { Id = (UInt32Value)1026U, Name = "Rectangle 334" };

            Xdr.NonVisualShapeDrawingProperties nonVisualShapeDrawingProperties2 = new Xdr.NonVisualShapeDrawingProperties();
            A.ShapeLocks shapeLocks2 = new A.ShapeLocks() { NoChangeArrowheads = true };

            nonVisualShapeDrawingProperties2.Append(shapeLocks2);

            nonVisualShapeProperties2.Append(nonVisualDrawingProperties3);
            nonVisualShapeProperties2.Append(nonVisualShapeDrawingProperties2);

            Xdr.ShapeProperties shapeProperties2 = new Xdr.ShapeProperties() { BlackWhiteMode = A.BlackWhiteModeValues.Auto };

            A.Transform2D transform2D2 = new A.Transform2D();
            A.Offset offset3 = new A.Offset() { X = 281L, Y = 352L };
            A.Extents extents3 = new A.Extents() { Cx = 73L, Cy = 8L };

            transform2D2.Append(offset3);
            transform2D2.Append(extents3);

            A.PresetGeometry presetGeometry2 = new A.PresetGeometry() { Preset = A.ShapeTypeValues.Rectangle };
            A.AdjustValueList adjustValueList2 = new A.AdjustValueList();

            presetGeometry2.Append(adjustValueList2);

            A.SolidFill solidFill3 = new A.SolidFill();
            A.RgbColorModelHex rgbColorModelHex3 = new A.RgbColorModelHex() { Val = "000000" };

            solidFill3.Append(rgbColorModelHex3);

            A.Outline outline2 = new A.Outline() { Width = 12700 };

            A.SolidFill solidFill4 = new A.SolidFill();
            A.RgbColorModelHex rgbColorModelHex4 = new A.RgbColorModelHex() { Val = "000000" };

            solidFill4.Append(rgbColorModelHex4);
            A.Miter miter2 = new A.Miter() { Limit = 800000 };
            A.HeadEnd headEnd2 = new A.HeadEnd();
            A.TailEnd tailEnd2 = new A.TailEnd();

            outline2.Append(solidFill4);
            outline2.Append(miter2);
            outline2.Append(headEnd2);
            outline2.Append(tailEnd2);

            shapeProperties2.Append(transform2D2);
            shapeProperties2.Append(presetGeometry2);
            shapeProperties2.Append(solidFill3);
            shapeProperties2.Append(outline2);

            shape2.Append(nonVisualShapeProperties2);
            shape2.Append(shapeProperties2);

            Xdr.Shape shape3 = new Xdr.Shape() { Macro = "", TextLink = "" };

            Xdr.NonVisualShapeProperties nonVisualShapeProperties3 = new Xdr.NonVisualShapeProperties();
            Xdr.NonVisualDrawingProperties nonVisualDrawingProperties4 = new Xdr.NonVisualDrawingProperties() { Id = (UInt32Value)1027U, Name = "Text Box 335" };

            Xdr.NonVisualShapeDrawingProperties nonVisualShapeDrawingProperties3 = new Xdr.NonVisualShapeDrawingProperties() { TextBox = true };
            A.ShapeLocks shapeLocks3 = new A.ShapeLocks() { NoChangeArrowheads = true };

            nonVisualShapeDrawingProperties3.Append(shapeLocks3);

            nonVisualShapeProperties3.Append(nonVisualDrawingProperties4);
            nonVisualShapeProperties3.Append(nonVisualShapeDrawingProperties3);

            Xdr.ShapeProperties shapeProperties3 = new Xdr.ShapeProperties() { BlackWhiteMode = A.BlackWhiteModeValues.Auto };

            A.Transform2D transform2D3 = new A.Transform2D();
            A.Offset offset4 = new A.Offset() { X = 107L, Y = 365L };
            A.Extents extents4 = new A.Extents() { Cx = 61L, Cy = 23L };

            transform2D3.Append(offset4);
            transform2D3.Append(extents4);

            A.PresetGeometry presetGeometry3 = new A.PresetGeometry() { Preset = A.ShapeTypeValues.Rectangle };
            A.AdjustValueList adjustValueList3 = new A.AdjustValueList();

            presetGeometry3.Append(adjustValueList3);
            A.NoFill noFill1 = new A.NoFill();

            A.Outline outline3 = new A.Outline() { Width = 1 };
            A.NoFill noFill2 = new A.NoFill();
            A.Miter miter3 = new A.Miter() { Limit = 800000 };
            A.HeadEnd headEnd3 = new A.HeadEnd();
            A.TailEnd tailEnd3 = new A.TailEnd();

            outline3.Append(noFill2);
            outline3.Append(miter3);
            outline3.Append(headEnd3);
            outline3.Append(tailEnd3);

            shapeProperties3.Append(transform2D3);
            shapeProperties3.Append(presetGeometry3);
            shapeProperties3.Append(noFill1);
            shapeProperties3.Append(outline3);

            Xdr.TextBody textBody1 = new Xdr.TextBody();

            A.BodyProperties bodyProperties1 = new A.BodyProperties() { Wrap = A.TextWrappingValues.None, LeftInset = 27432, TopInset = 18288, RightInset = 0, BottomInset = 0, Anchor = A.TextAnchoringTypeValues.Top, UpRight = true };
            A.ShapeAutoFit shapeAutoFit1 = new A.ShapeAutoFit();

            bodyProperties1.Append(shapeAutoFit1);
            A.ListStyle listStyle1 = new A.ListStyle();

            A.Paragraph paragraph1 = new A.Paragraph();

            A.ParagraphProperties paragraphProperties1 = new A.ParagraphProperties() { Alignment = A.TextAlignmentTypeValues.Left, RightToLeft = false };
            A.DefaultRunProperties defaultRunProperties1 = new A.DefaultRunProperties() { FontSize = 1000 };

            paragraphProperties1.Append(defaultRunProperties1);

            A.Run run1 = new A.Run();

            A.RunProperties runProperties1 = new A.RunProperties() { Language = "en-US", AlternativeLanguage = "ko-KR", FontSize = 1200, Bold = true, Italic = false, Underline = A.TextUnderlineValues.None, Strike = A.TextStrikeValues.NoStrike, Baseline = 0 };

            A.SolidFill solidFill5 = new A.SolidFill();
            A.RgbColorModelHex rgbColorModelHex5 = new A.RgbColorModelHex() { Val = "000000" };

            solidFill5.Append(rgbColorModelHex5);
            A.LatinFont latinFont1 = new A.LatinFont() { Typeface = "돋움" };
            A.EastAsianFont eastAsianFont1 = new A.EastAsianFont() { Typeface = "돋움" };

            runProperties1.Append(solidFill5);
            runProperties1.Append(latinFont1);
            runProperties1.Append(eastAsianFont1);
            A.Text text1 = new A.Text();
            text1.Text = KL;

            run1.Append(runProperties1);
            run1.Append(text1);

            paragraph1.Append(paragraphProperties1);
            paragraph1.Append(run1);

            textBody1.Append(bodyProperties1);
            textBody1.Append(listStyle1);
            textBody1.Append(paragraph1);

            shape3.Append(nonVisualShapeProperties3);
            shape3.Append(shapeProperties3);
            shape3.Append(textBody1);

            Xdr.Shape shape4 = new Xdr.Shape() { Macro = "", TextLink = "" };

            Xdr.NonVisualShapeProperties nonVisualShapeProperties4 = new Xdr.NonVisualShapeProperties();
            Xdr.NonVisualDrawingProperties nonVisualDrawingProperties5 = new Xdr.NonVisualDrawingProperties() { Id = (UInt32Value)1028U, Name = "Text Box 336" };

            Xdr.NonVisualShapeDrawingProperties nonVisualShapeDrawingProperties4 = new Xdr.NonVisualShapeDrawingProperties() { TextBox = true };
            A.ShapeLocks shapeLocks4 = new A.ShapeLocks() { NoChangeArrowheads = true };

            nonVisualShapeDrawingProperties4.Append(shapeLocks4);

            nonVisualShapeProperties4.Append(nonVisualDrawingProperties5);
            nonVisualShapeProperties4.Append(nonVisualShapeDrawingProperties4);

            Xdr.ShapeProperties shapeProperties4 = new Xdr.ShapeProperties() { BlackWhiteMode = A.BlackWhiteModeValues.Auto };

            A.Transform2D transform2D4 = new A.Transform2D();
            A.Offset offset5 = new A.Offset() { X = 256L, Y = 365L };
            A.Extents extents5 = new A.Extents() { Cx = 37L, Cy = 23L };

            transform2D4.Append(offset5);
            transform2D4.Append(extents5);

            A.PresetGeometry presetGeometry4 = new A.PresetGeometry() { Preset = A.ShapeTypeValues.Rectangle };
            A.AdjustValueList adjustValueList4 = new A.AdjustValueList();

            presetGeometry4.Append(adjustValueList4);
            A.NoFill noFill3 = new A.NoFill();

            A.Outline outline4 = new A.Outline() { Width = 1 };
            A.NoFill noFill4 = new A.NoFill();
            A.Miter miter4 = new A.Miter() { Limit = 800000 };
            A.HeadEnd headEnd4 = new A.HeadEnd();
            A.TailEnd tailEnd4 = new A.TailEnd();

            outline4.Append(noFill4);
            outline4.Append(miter4);
            outline4.Append(headEnd4);
            outline4.Append(tailEnd4);

            shapeProperties4.Append(transform2D4);
            shapeProperties4.Append(presetGeometry4);
            shapeProperties4.Append(noFill3);
            shapeProperties4.Append(outline4);

            Xdr.TextBody textBody2 = new Xdr.TextBody();

            A.BodyProperties bodyProperties2 = new A.BodyProperties() { Wrap = A.TextWrappingValues.None, LeftInset = 27432, TopInset = 18288, RightInset = 0, BottomInset = 0, Anchor = A.TextAnchoringTypeValues.Top, UpRight = true };
            A.ShapeAutoFit shapeAutoFit2 = new A.ShapeAutoFit();

            bodyProperties2.Append(shapeAutoFit2);
            A.ListStyle listStyle2 = new A.ListStyle();

            A.Paragraph paragraph2 = new A.Paragraph();

            A.ParagraphProperties paragraphProperties2 = new A.ParagraphProperties() { Alignment = A.TextAlignmentTypeValues.Left, RightToLeft = false };
            A.DefaultRunProperties defaultRunProperties2 = new A.DefaultRunProperties() { FontSize = 1000 };

            paragraphProperties2.Append(defaultRunProperties2);

            A.Run run2 = new A.Run();

            A.RunProperties runProperties2 = new A.RunProperties() { Language = "en-US", AlternativeLanguage = "ko-KR", FontSize = 1200, Bold = true, Italic = false, Underline = A.TextUnderlineValues.None, Strike = A.TextStrikeValues.NoStrike, Baseline = 0 };

            A.SolidFill solidFill6 = new A.SolidFill();
            A.RgbColorModelHex rgbColorModelHex6 = new A.RgbColorModelHex() { Val = "000000" };

            solidFill6.Append(rgbColorModelHex6);
            A.LatinFont latinFont2 = new A.LatinFont() { Typeface = "돋움" };
            A.EastAsianFont eastAsianFont2 = new A.EastAsianFont() { Typeface = "돋움" };

            runProperties2.Append(solidFill6);
            runProperties2.Append(latinFont2);
            runProperties2.Append(eastAsianFont2);
            A.Text text2 = new A.Text();
            text2.Text = FT2;

            run2.Append(runProperties2);
            run2.Append(text2);

            paragraph2.Append(paragraphProperties2);
            paragraph2.Append(run2);

            textBody2.Append(bodyProperties2);
            textBody2.Append(listStyle2);
            textBody2.Append(paragraph2);

            shape4.Append(nonVisualShapeProperties4);
            shape4.Append(shapeProperties4);
            shape4.Append(textBody2);

            Xdr.Shape shape5 = new Xdr.Shape() { Macro = "", TextLink = "" };

            Xdr.NonVisualShapeProperties nonVisualShapeProperties5 = new Xdr.NonVisualShapeProperties();
            Xdr.NonVisualDrawingProperties nonVisualDrawingProperties6 = new Xdr.NonVisualDrawingProperties() { Id = (UInt32Value)1029U, Name = "Text Box 337" };

            Xdr.NonVisualShapeDrawingProperties nonVisualShapeDrawingProperties5 = new Xdr.NonVisualShapeDrawingProperties() { TextBox = true };
            A.ShapeLocks shapeLocks5 = new A.ShapeLocks() { NoChangeArrowheads = true };

            nonVisualShapeDrawingProperties5.Append(shapeLocks5);

            nonVisualShapeProperties5.Append(nonVisualDrawingProperties6);
            nonVisualShapeProperties5.Append(nonVisualShapeDrawingProperties5);

            Xdr.ShapeProperties shapeProperties5 = new Xdr.ShapeProperties() { BlackWhiteMode = A.BlackWhiteModeValues.Auto };

            A.Transform2D transform2D5 = new A.Transform2D();
            A.Offset offset6 = new A.Offset() { X = 349L, Y = 366L };
            A.Extents extents6 = new A.Extents() { Cx = 61L, Cy = 23L };

            transform2D5.Append(offset6);
            transform2D5.Append(extents6);

            A.PresetGeometry presetGeometry5 = new A.PresetGeometry() { Preset = A.ShapeTypeValues.Rectangle };
            A.AdjustValueList adjustValueList5 = new A.AdjustValueList();

            presetGeometry5.Append(adjustValueList5);
            A.NoFill noFill5 = new A.NoFill();

            A.Outline outline5 = new A.Outline() { Width = 1 };
            A.NoFill noFill6 = new A.NoFill();
            A.Miter miter5 = new A.Miter() { Limit = 800000 };
            A.HeadEnd headEnd5 = new A.HeadEnd();
            A.TailEnd tailEnd5 = new A.TailEnd();

            outline5.Append(noFill6);
            outline5.Append(miter5);
            outline5.Append(headEnd5);
            outline5.Append(tailEnd5);

            shapeProperties5.Append(transform2D5);
            shapeProperties5.Append(presetGeometry5);
            shapeProperties5.Append(noFill5);
            shapeProperties5.Append(outline5);

            Xdr.TextBody textBody3 = new Xdr.TextBody();

            A.BodyProperties bodyProperties3 = new A.BodyProperties() { Wrap = A.TextWrappingValues.None, LeftInset = 27432, TopInset = 18288, RightInset = 0, BottomInset = 0, Anchor = A.TextAnchoringTypeValues.Top, UpRight = true };
            A.ShapeAutoFit shapeAutoFit3 = new A.ShapeAutoFit();

            bodyProperties3.Append(shapeAutoFit3);
            A.ListStyle listStyle3 = new A.ListStyle();

            A.Paragraph paragraph3 = new A.Paragraph();

            A.ParagraphProperties paragraphProperties3 = new A.ParagraphProperties() { Alignment = A.TextAlignmentTypeValues.Left, RightToLeft = false };
            A.DefaultRunProperties defaultRunProperties3 = new A.DefaultRunProperties() { FontSize = 1000 };

            paragraphProperties3.Append(defaultRunProperties3);

            A.Run run3 = new A.Run();

            A.RunProperties runProperties3 = new A.RunProperties() { Language = "en-US", AlternativeLanguage = "ko-KR", FontSize = 1200, Bold = true, Italic = false, Underline = A.TextUnderlineValues.None, Strike = A.TextStrikeValues.NoStrike, Baseline = 0 };

            A.SolidFill solidFill7 = new A.SolidFill();
            A.RgbColorModelHex rgbColorModelHex7 = new A.RgbColorModelHex() { Val = "000000" };

            solidFill7.Append(rgbColorModelHex7);
            A.LatinFont latinFont3 = new A.LatinFont() { Typeface = "돋움" };
            A.EastAsianFont eastAsianFont3 = new A.EastAsianFont() { Typeface = "돋움" };

            runProperties3.Append(solidFill7);
            runProperties3.Append(latinFont3);
            runProperties3.Append(eastAsianFont3);
            A.Text text3 = new A.Text();
            text3.Text = LC;

            run3.Append(runProperties3);
            run3.Append(text3);

            paragraph3.Append(paragraphProperties3);
            paragraph3.Append(run3);

            textBody3.Append(bodyProperties3);
            textBody3.Append(listStyle3);
            textBody3.Append(paragraph3);

            shape5.Append(nonVisualShapeProperties5);
            shape5.Append(shapeProperties5);
            shape5.Append(textBody3);

            Xdr.Shape shape6 = new Xdr.Shape() { Macro = "", TextLink = "" };

            Xdr.NonVisualShapeProperties nonVisualShapeProperties6 = new Xdr.NonVisualShapeProperties();
            Xdr.NonVisualDrawingProperties nonVisualDrawingProperties7 = new Xdr.NonVisualDrawingProperties() { Id = (UInt32Value)1030U, Name = "Text Box 338" };

            Xdr.NonVisualShapeDrawingProperties nonVisualShapeDrawingProperties6 = new Xdr.NonVisualShapeDrawingProperties() { TextBox = true };
            A.ShapeLocks shapeLocks6 = new A.ShapeLocks() { NoChangeArrowheads = true };

            nonVisualShapeDrawingProperties6.Append(shapeLocks6);

            nonVisualShapeProperties6.Append(nonVisualDrawingProperties7);
            nonVisualShapeProperties6.Append(nonVisualShapeDrawingProperties6);

            Xdr.ShapeProperties shapeProperties6 = new Xdr.ShapeProperties() { BlackWhiteMode = A.BlackWhiteModeValues.Auto };

            A.Transform2D transform2D6 = new A.Transform2D();
            A.Offset offset7 = new A.Offset() { X = 193L, Y = 366L };
            A.Extents extents7 = new A.Extents() { Cx = 37L, Cy = 23L };

            transform2D6.Append(offset7);
            transform2D6.Append(extents7);

            A.PresetGeometry presetGeometry6 = new A.PresetGeometry() { Preset = A.ShapeTypeValues.Rectangle };
            A.AdjustValueList adjustValueList6 = new A.AdjustValueList();

            presetGeometry6.Append(adjustValueList6);
            A.NoFill noFill7 = new A.NoFill();

            A.Outline outline6 = new A.Outline() { Width = 1 };
            A.NoFill noFill8 = new A.NoFill();
            A.Miter miter6 = new A.Miter() { Limit = 800000 };
            A.HeadEnd headEnd6 = new A.HeadEnd();
            A.TailEnd tailEnd6 = new A.TailEnd();

            outline6.Append(noFill8);
            outline6.Append(miter6);
            outline6.Append(headEnd6);
            outline6.Append(tailEnd6);

            shapeProperties6.Append(transform2D6);
            shapeProperties6.Append(presetGeometry6);
            shapeProperties6.Append(noFill7);
            shapeProperties6.Append(outline6);

            Xdr.TextBody textBody4 = new Xdr.TextBody();

            A.BodyProperties bodyProperties4 = new A.BodyProperties() { Wrap = A.TextWrappingValues.None, LeftInset = 27432, TopInset = 18288, RightInset = 0, BottomInset = 0, Anchor = A.TextAnchoringTypeValues.Top, UpRight = true };
            A.ShapeAutoFit shapeAutoFit4 = new A.ShapeAutoFit();

            bodyProperties4.Append(shapeAutoFit4);
            A.ListStyle listStyle4 = new A.ListStyle();

            A.Paragraph paragraph4 = new A.Paragraph();

            A.ParagraphProperties paragraphProperties4 = new A.ParagraphProperties() { Alignment = A.TextAlignmentTypeValues.Left, RightToLeft = false };
            A.DefaultRunProperties defaultRunProperties4 = new A.DefaultRunProperties() { FontSize = 1000 };

            paragraphProperties4.Append(defaultRunProperties4);

            A.Run run4 = new A.Run();

            A.RunProperties runProperties4 = new A.RunProperties() { Language = "en-US", AlternativeLanguage = "ko-KR", FontSize = 1200, Bold = true, Italic = false, Underline = A.TextUnderlineValues.None, Strike = A.TextStrikeValues.NoStrike, Baseline = 0 };

            A.SolidFill solidFill8 = new A.SolidFill();
            A.RgbColorModelHex rgbColorModelHex8 = new A.RgbColorModelHex() { Val = "000000" };

            solidFill8.Append(rgbColorModelHex8);
            A.LatinFont latinFont4 = new A.LatinFont() { Typeface = "돋움" };
            A.EastAsianFont eastAsianFont4 = new A.EastAsianFont() { Typeface = "돋움" };

            runProperties4.Append(solidFill8);
            runProperties4.Append(latinFont4);
            runProperties4.Append(eastAsianFont4);
            A.Text text4 = new A.Text();
            text4.Text = FT;

            run4.Append(runProperties4);
            run4.Append(text4);

            paragraph4.Append(paragraphProperties4);
            paragraph4.Append(run4);

            textBody4.Append(bodyProperties4);
            textBody4.Append(listStyle4);
            textBody4.Append(paragraph4);

            shape6.Append(nonVisualShapeProperties6);
            shape6.Append(shapeProperties6);
            shape6.Append(textBody4);

            Xdr.Shape shape7 = new Xdr.Shape() { Macro = "", TextLink = "" };

            Xdr.NonVisualShapeProperties nonVisualShapeProperties7 = new Xdr.NonVisualShapeProperties();
            Xdr.NonVisualDrawingProperties nonVisualDrawingProperties8 = new Xdr.NonVisualDrawingProperties()
            {
                Id = (UInt32Value)GetNextShapeId(),
                Name = GetNextLineName()
            };

            Xdr.NonVisualShapeDrawingProperties nonVisualShapeDrawingProperties7 = new Xdr.NonVisualShapeDrawingProperties();
            A.ShapeLocks shapeLocks7 = new A.ShapeLocks() { NoChangeShapeType = true };

            nonVisualShapeDrawingProperties7.Append(shapeLocks7);

            nonVisualShapeProperties7.Append(nonVisualDrawingProperties8);
            nonVisualShapeProperties7.Append(nonVisualShapeDrawingProperties7);

            Xdr.ShapeProperties shapeProperties7 = new Xdr.ShapeProperties() { BlackWhiteMode = A.BlackWhiteModeValues.Auto };

            A.Transform2D transform2D7 = new A.Transform2D();
            A.Offset offset8 = new A.Offset() { X = 214L, Y = 356L };
            A.Extents extents8 = new A.Extents() { Cx = 65L, Cy = 0L };

            transform2D7.Append(offset8);
            transform2D7.Append(extents8);

            A.PresetGeometry presetGeometry7 = new A.PresetGeometry() { Preset = A.ShapeTypeValues.Line };
            A.AdjustValueList adjustValueList7 = new A.AdjustValueList();

            presetGeometry7.Append(adjustValueList7);
            A.NoFill noFill9 = new A.NoFill();

            A.Outline outline7 = new A.Outline() { Width = 63500 };

            A.SolidFill solidFill9 = new A.SolidFill();
            A.RgbColorModelHex rgbColorModelHex9 = new A.RgbColorModelHex() { Val = "000000" };

            solidFill9.Append(rgbColorModelHex9);
            A.PresetDash presetDash1 = new A.PresetDash() { Val = A.PresetLineDashValues.SystemDot };
            A.Round round1 = new A.Round();
            A.HeadEnd headEnd7 = new A.HeadEnd();
            A.TailEnd tailEnd7 = new A.TailEnd();

            outline7.Append(solidFill9);
            outline7.Append(presetDash1);
            outline7.Append(round1);
            outline7.Append(headEnd7);
            outline7.Append(tailEnd7);

            shapeProperties7.Append(transform2D7);
            shapeProperties7.Append(presetGeometry7);
            shapeProperties7.Append(noFill9);
            shapeProperties7.Append(outline7);

            shape7.Append(nonVisualShapeProperties7);
            shape7.Append(shapeProperties7);

            Xdr.Shape shape8 = new Xdr.Shape() { Macro = "", TextLink = "" };

            Xdr.NonVisualShapeProperties nonVisualShapeProperties8 = new Xdr.NonVisualShapeProperties();
            Xdr.NonVisualDrawingProperties nonVisualDrawingProperties9 = new Xdr.NonVisualDrawingProperties() { Id = (UInt32Value)1032U, Name = "Rectangle 340" };

            Xdr.NonVisualShapeDrawingProperties nonVisualShapeDrawingProperties8 = new Xdr.NonVisualShapeDrawingProperties();
            A.ShapeLocks shapeLocks8 = new A.ShapeLocks() { NoChangeArrowheads = true };

            nonVisualShapeDrawingProperties8.Append(shapeLocks8);

            nonVisualShapeProperties8.Append(nonVisualDrawingProperties9);
            nonVisualShapeProperties8.Append(nonVisualShapeDrawingProperties8);

            Xdr.ShapeProperties shapeProperties8 = new Xdr.ShapeProperties() { BlackWhiteMode = A.BlackWhiteModeValues.Auto };

            A.Transform2D transform2D8 = new A.Transform2D();
            A.Offset offset9 = new A.Offset() { X = 156L, Y = 331L };
            A.Extents extents9 = new A.Extents() { Cx = 26L, Cy = 21L };

            transform2D8.Append(offset9);
            transform2D8.Append(extents9);

            A.PresetGeometry presetGeometry8 = new A.PresetGeometry() { Preset = A.ShapeTypeValues.Rectangle };
            A.AdjustValueList adjustValueList8 = new A.AdjustValueList();

            presetGeometry8.Append(adjustValueList8);
            A.NoFill noFill10 = new A.NoFill();

            A.Outline outline8 = new A.Outline() { Width = 9525 };
            A.NoFill noFill11 = new A.NoFill();
            A.Miter miter7 = new A.Miter() { Limit = 800000 };
            A.HeadEnd headEnd8 = new A.HeadEnd();
            A.TailEnd tailEnd8 = new A.TailEnd();

            outline8.Append(noFill11);
            outline8.Append(miter7);
            outline8.Append(headEnd8);
            outline8.Append(tailEnd8);

            shapeProperties8.Append(transform2D8);
            shapeProperties8.Append(presetGeometry8);
            shapeProperties8.Append(noFill10);
            shapeProperties8.Append(outline8);

            Xdr.TextBody textBody5 = new Xdr.TextBody();

            A.BodyProperties bodyProperties5 = new A.BodyProperties() { Wrap = A.TextWrappingValues.None, LeftInset = 27432, TopInset = 18288, RightInset = 0, BottomInset = 0, Anchor = A.TextAnchoringTypeValues.Top, UpRight = true };
            A.ShapeAutoFit shapeAutoFit5 = new A.ShapeAutoFit();

            bodyProperties5.Append(shapeAutoFit5);
            A.ListStyle listStyle5 = new A.ListStyle();

            A.Paragraph paragraph5 = new A.Paragraph();

            A.ParagraphProperties paragraphProperties5 = new A.ParagraphProperties() { Alignment = A.TextAlignmentTypeValues.Left, RightToLeft = false };
            A.DefaultRunProperties defaultRunProperties5 = new A.DefaultRunProperties() { FontSize = 1000 };

            paragraphProperties5.Append(defaultRunProperties5);

            A.Run run5 = new A.Run();

            A.RunProperties runProperties5 = new A.RunProperties() { Language = "en-US", AlternativeLanguage = "ko-KR", FontSize = 1100, Bold = true, Italic = false, Underline = A.TextUnderlineValues.None, Strike = A.TextStrikeValues.NoStrike, Baseline = 0 };

            A.SolidFill solidFill10 = new A.SolidFill();
            A.RgbColorModelHex rgbColorModelHex10 = new A.RgbColorModelHex() { Val = "000000" };

            solidFill10.Append(rgbColorModelHex10);
            A.LatinFont latinFont5 = new A.LatinFont() { Typeface = "돋움" };
            A.EastAsianFont eastAsianFont5 = new A.EastAsianFont() { Typeface = "돋움" };

            runProperties5.Append(solidFill10);
            runProperties5.Append(latinFont5);
            runProperties5.Append(eastAsianFont5);
            A.Text text5 = new A.Text();
            text5.Text = week1;

            run5.Append(runProperties5);
            run5.Append(text5);

            A.Run run6 = new A.Run();

            A.RunProperties runProperties6 = new A.RunProperties() { Language = "ko-KR", AlternativeLanguage = "en-US", FontSize = 1100, Bold = true, Italic = false, Underline = A.TextUnderlineValues.None, Strike = A.TextStrikeValues.NoStrike, Baseline = 0 };

            A.SolidFill solidFill11 = new A.SolidFill();
            A.RgbColorModelHex rgbColorModelHex11 = new A.RgbColorModelHex() { Val = "000000" };

            solidFill11.Append(rgbColorModelHex11);
            A.LatinFont latinFont6 = new A.LatinFont() { Typeface = "돋움" };
            A.EastAsianFont eastAsianFont6 = new A.EastAsianFont() { Typeface = "돋움" };

            runProperties6.Append(solidFill11);
            runProperties6.Append(latinFont6);
            runProperties6.Append(eastAsianFont6);
            A.Text text6 = new A.Text();
            text6.Text = "주";

            run6.Append(runProperties6);
            run6.Append(text6);

            paragraph5.Append(paragraphProperties5);
            paragraph5.Append(run5);
            paragraph5.Append(run6);

            textBody5.Append(bodyProperties5);
            textBody5.Append(listStyle5);
            textBody5.Append(paragraph5);

            shape8.Append(nonVisualShapeProperties8);
            shape8.Append(shapeProperties8);
            shape8.Append(textBody5);

            Xdr.Shape shape9 = new Xdr.Shape() { Macro = "", TextLink = "" };

            Xdr.NonVisualShapeProperties nonVisualShapeProperties9 = new Xdr.NonVisualShapeProperties();
            Xdr.NonVisualDrawingProperties nonVisualDrawingProperties10 = new Xdr.NonVisualDrawingProperties() { Id = (UInt32Value)1033U, Name = "Rectangle 341" };

            Xdr.NonVisualShapeDrawingProperties nonVisualShapeDrawingProperties9 = new Xdr.NonVisualShapeDrawingProperties();
            A.ShapeLocks shapeLocks9 = new A.ShapeLocks() { NoChangeArrowheads = true };

            nonVisualShapeDrawingProperties9.Append(shapeLocks9);

            nonVisualShapeProperties9.Append(nonVisualDrawingProperties10);
            nonVisualShapeProperties9.Append(nonVisualShapeDrawingProperties9);

            Xdr.ShapeProperties shapeProperties9 = new Xdr.ShapeProperties() { BlackWhiteMode = A.BlackWhiteModeValues.Auto };

            A.Transform2D transform2D9 = new A.Transform2D();
            A.Offset offset10 = new A.Offset() { X = 232L, Y = 329L };
            A.Extents extents10 = new A.Extents() { Cx = 26L, Cy = 21L };

            transform2D9.Append(offset10);
            transform2D9.Append(extents10);

            A.PresetGeometry presetGeometry9 = new A.PresetGeometry() { Preset = A.ShapeTypeValues.Rectangle };
            A.AdjustValueList adjustValueList9 = new A.AdjustValueList();

            presetGeometry9.Append(adjustValueList9);
            A.NoFill noFill12 = new A.NoFill();

            A.Outline outline9 = new A.Outline() { Width = 9525 };
            A.NoFill noFill13 = new A.NoFill();
            A.Miter miter8 = new A.Miter() { Limit = 800000 };
            A.HeadEnd headEnd9 = new A.HeadEnd();
            A.TailEnd tailEnd9 = new A.TailEnd();

            outline9.Append(noFill13);
            outline9.Append(miter8);
            outline9.Append(headEnd9);
            outline9.Append(tailEnd9);

            shapeProperties9.Append(transform2D9);
            shapeProperties9.Append(presetGeometry9);
            shapeProperties9.Append(noFill12);
            shapeProperties9.Append(outline9);

            Xdr.TextBody textBody6 = new Xdr.TextBody();

            A.BodyProperties bodyProperties6 = new A.BodyProperties() { Wrap = A.TextWrappingValues.None, LeftInset = 27432, TopInset = 18288, RightInset = 0, BottomInset = 0, Anchor = A.TextAnchoringTypeValues.Top, UpRight = true };
            A.ShapeAutoFit shapeAutoFit6 = new A.ShapeAutoFit();

            bodyProperties6.Append(shapeAutoFit6);
            A.ListStyle listStyle6 = new A.ListStyle();

            A.Paragraph paragraph6 = new A.Paragraph();

            A.ParagraphProperties paragraphProperties6 = new A.ParagraphProperties() { Alignment = A.TextAlignmentTypeValues.Left, RightToLeft = false };
            A.DefaultRunProperties defaultRunProperties6 = new A.DefaultRunProperties() { FontSize = 1000 };

            paragraphProperties6.Append(defaultRunProperties6);

            A.Run run7 = new A.Run();

            A.RunProperties runProperties7 = new A.RunProperties() { Language = "en-US", AlternativeLanguage = "ko-KR", FontSize = 1100, Bold = true, Italic = false, Underline = A.TextUnderlineValues.None, Strike = A.TextStrikeValues.NoStrike, Baseline = 0 };

            A.SolidFill solidFill12 = new A.SolidFill();
            A.RgbColorModelHex rgbColorModelHex12 = new A.RgbColorModelHex() { Val = "000000" };

            solidFill12.Append(rgbColorModelHex12);
            A.LatinFont latinFont7 = new A.LatinFont() { Typeface = "돋움" };
            A.EastAsianFont eastAsianFont7 = new A.EastAsianFont() { Typeface = "돋움" };

            runProperties7.Append(solidFill12);
            runProperties7.Append(latinFont7);
            runProperties7.Append(eastAsianFont7);
            A.Text text7 = new A.Text();
            text7.Text = week2;

            run7.Append(runProperties7);
            run7.Append(text7);

            A.Run run8 = new A.Run();

            A.RunProperties runProperties8 = new A.RunProperties() { Language = "ko-KR", AlternativeLanguage = "en-US", FontSize = 1100, Bold = true, Italic = false, Underline = A.TextUnderlineValues.None, Strike = A.TextStrikeValues.NoStrike, Baseline = 0 };

            A.SolidFill solidFill13 = new A.SolidFill();
            A.RgbColorModelHex rgbColorModelHex13 = new A.RgbColorModelHex() { Val = "000000" };

            solidFill13.Append(rgbColorModelHex13);
            A.LatinFont latinFont8 = new A.LatinFont() { Typeface = "돋움" };
            A.EastAsianFont eastAsianFont8 = new A.EastAsianFont() { Typeface = "돋움" };

            runProperties8.Append(solidFill13);
            runProperties8.Append(latinFont8);
            runProperties8.Append(eastAsianFont8);
            A.Text text8 = new A.Text();
            text8.Text = "주";

            run8.Append(runProperties8);
            run8.Append(text8);

            paragraph6.Append(paragraphProperties6);
            paragraph6.Append(run7);
            paragraph6.Append(run8);

            textBody6.Append(bodyProperties6);
            textBody6.Append(listStyle6);
            textBody6.Append(paragraph6);

            shape9.Append(nonVisualShapeProperties9);
            shape9.Append(shapeProperties9);
            shape9.Append(textBody6);

            Xdr.Shape shape10 = new Xdr.Shape() { Macro = "", TextLink = "" };

            Xdr.NonVisualShapeProperties nonVisualShapeProperties10 = new Xdr.NonVisualShapeProperties();
            Xdr.NonVisualDrawingProperties nonVisualDrawingProperties11 = new Xdr.NonVisualDrawingProperties() { Id = (UInt32Value)1034U, Name = "Rectangle 342" };

            Xdr.NonVisualShapeDrawingProperties nonVisualShapeDrawingProperties10 = new Xdr.NonVisualShapeDrawingProperties();
            A.ShapeLocks shapeLocks10 = new A.ShapeLocks() { NoChangeArrowheads = true };

            nonVisualShapeDrawingProperties10.Append(shapeLocks10);

            nonVisualShapeProperties10.Append(nonVisualDrawingProperties11);
            nonVisualShapeProperties10.Append(nonVisualShapeDrawingProperties10);

            Xdr.ShapeProperties shapeProperties10 = new Xdr.ShapeProperties() { BlackWhiteMode = A.BlackWhiteModeValues.Auto };

            A.Transform2D transform2D10 = new A.Transform2D();
            A.Offset offset11 = new A.Offset() { X = 303L, Y = 329L };
            A.Extents extents11 = new A.Extents() { Cx = 26L, Cy = 21L };

            transform2D10.Append(offset11);
            transform2D10.Append(extents11);

            A.PresetGeometry presetGeometry10 = new A.PresetGeometry() { Preset = A.ShapeTypeValues.Rectangle };
            A.AdjustValueList adjustValueList10 = new A.AdjustValueList();

            presetGeometry10.Append(adjustValueList10);
            A.NoFill noFill14 = new A.NoFill();

            A.Outline outline10 = new A.Outline() { Width = 9525 };
            A.NoFill noFill15 = new A.NoFill();
            A.Miter miter9 = new A.Miter() { Limit = 800000 };
            A.HeadEnd headEnd10 = new A.HeadEnd();
            A.TailEnd tailEnd10 = new A.TailEnd();

            outline10.Append(noFill15);
            outline10.Append(miter9);
            outline10.Append(headEnd10);
            outline10.Append(tailEnd10);

            shapeProperties10.Append(transform2D10);
            shapeProperties10.Append(presetGeometry10);
            shapeProperties10.Append(noFill14);
            shapeProperties10.Append(outline10);

            Xdr.TextBody textBody7 = new Xdr.TextBody();

            A.BodyProperties bodyProperties7 = new A.BodyProperties() { Wrap = A.TextWrappingValues.None, LeftInset = 27432, TopInset = 18288, RightInset = 0, BottomInset = 0, Anchor = A.TextAnchoringTypeValues.Top, UpRight = true };
            A.ShapeAutoFit shapeAutoFit7 = new A.ShapeAutoFit();

            bodyProperties7.Append(shapeAutoFit7);
            A.ListStyle listStyle7 = new A.ListStyle();

            A.Paragraph paragraph7 = new A.Paragraph();

            A.ParagraphProperties paragraphProperties7 = new A.ParagraphProperties() { Alignment = A.TextAlignmentTypeValues.Left, RightToLeft = false };
            A.DefaultRunProperties defaultRunProperties7 = new A.DefaultRunProperties() { FontSize = 1000 };

            paragraphProperties7.Append(defaultRunProperties7);

            A.Run run9 = new A.Run();

            A.RunProperties runProperties9 = new A.RunProperties() { Language = "en-US", AlternativeLanguage = "ko-KR", FontSize = 1100, Bold = true, Italic = false, Underline = A.TextUnderlineValues.None, Strike = A.TextStrikeValues.NoStrike, Baseline = 0 };

            A.SolidFill solidFill14 = new A.SolidFill();
            A.RgbColorModelHex rgbColorModelHex14 = new A.RgbColorModelHex() { Val = "000000" };

            solidFill14.Append(rgbColorModelHex14);
            A.LatinFont latinFont9 = new A.LatinFont() { Typeface = "돋움" };
            A.EastAsianFont eastAsianFont9 = new A.EastAsianFont() { Typeface = "돋움" };

            runProperties9.Append(solidFill14);
            runProperties9.Append(latinFont9);
            runProperties9.Append(eastAsianFont9);
            A.Text text9 = new A.Text();
            text9.Text = week3;

            run9.Append(runProperties9);
            run9.Append(text9);

            A.Run run10 = new A.Run();

            A.RunProperties runProperties10 = new A.RunProperties() { Language = "ko-KR", AlternativeLanguage = "en-US", FontSize = 1100, Bold = true, Italic = false, Underline = A.TextUnderlineValues.None, Strike = A.TextStrikeValues.NoStrike, Baseline = 0 };

            A.SolidFill solidFill15 = new A.SolidFill();
            A.RgbColorModelHex rgbColorModelHex15 = new A.RgbColorModelHex() { Val = "000000" };

            solidFill15.Append(rgbColorModelHex15);
            A.LatinFont latinFont10 = new A.LatinFont() { Typeface = "돋움" };
            A.EastAsianFont eastAsianFont10 = new A.EastAsianFont() { Typeface = "돋움" };

            runProperties10.Append(solidFill15);
            runProperties10.Append(latinFont10);
            runProperties10.Append(eastAsianFont10);
            A.Text text10 = new A.Text();
            text10.Text = "주";

            run10.Append(runProperties10);
            run10.Append(text10);

            paragraph7.Append(paragraphProperties7);
            paragraph7.Append(run9);
            paragraph7.Append(run10);

            textBody7.Append(bodyProperties7);
            textBody7.Append(listStyle7);
            textBody7.Append(paragraph7);

            shape10.Append(nonVisualShapeProperties10);
            shape10.Append(shapeProperties10);
            shape10.Append(textBody7);

            groupShape1.Append(nonVisualGroupShapeProperties1);
            groupShape1.Append(groupShapeProperties1);
            groupShape1.Append(shape1);
            groupShape1.Append(shape2);
            groupShape1.Append(shape3);
            groupShape1.Append(shape4);
            groupShape1.Append(shape5);
            groupShape1.Append(shape6);
            groupShape1.Append(shape7);
            groupShape1.Append(shape8);
            groupShape1.Append(shape9);
            groupShape1.Append(shape10);
            Xdr.ClientData clientData1 = new Xdr.ClientData();

            twoCellAnchor1.Append(fromMarker1);
            twoCellAnchor1.Append(toMarker1);
            twoCellAnchor1.Append(groupShape1);
            twoCellAnchor1.Append(clientData1);

            _Worksheet.WorksheetPart.DrawingsPart.WorksheetDrawing.Append(twoCellAnchor1);

            // 검증
            //CreateValidTwoCellAnchor(fromCol, fromColOffset, fromRow, fromRowOffset, toCol, toColOffset, toRow, toRowOffset);
        }


        /// <summary>
        /// MainEvent를 그리는 메소드 FT2가 없는경우 .
        /// </summary>
        /// <param name="KL"></param>
        /// <param name="LC"></param>
        /// <param name="FT"></param>
        /// <param name="FT2"></param>
        /// <param name="FT3"></param>
        public void DrawingsMainEvent(string KL, string LC, string FT, string week1, string week2)
        {
            if (_Worksheet.WorksheetPart.DrawingsPart == null) CreateDrawingPart();

            string fweek1 = "";
            string fweek2 = "";
            string fweek3 = "";

            if (string.IsNullOrEmpty(week2) || week2 == "")
            {
                fweek2 = week1;
            }
            else
            {
                fweek1 = week1;
                fweek3 = week2;
            }

            Xdr.TwoCellAnchor twoCellAnchor1 = new Xdr.TwoCellAnchor();

            Xdr.FromMarker fromMarker1 = new Xdr.FromMarker();
            Xdr.ColumnId columnId1 = new Xdr.ColumnId();
            columnId1.Text = "101";
            Xdr.ColumnOffset columnOffset1 = new Xdr.ColumnOffset();
            columnOffset1.Text = "0";
            Xdr.RowId rowId1 = new Xdr.RowId();
            rowId1.Text = "2";
            Xdr.RowOffset rowOffset1 = new Xdr.RowOffset();
            rowOffset1.Text = "0";

            fromMarker1.Append(columnId1);
            fromMarker1.Append(columnOffset1);
            fromMarker1.Append(rowId1);
            fromMarker1.Append(rowOffset1);

            Xdr.ToMarker toMarker1 = new Xdr.ToMarker();
            Xdr.ColumnId columnId2 = new Xdr.ColumnId();
            columnId2.Text = "105";
            Xdr.ColumnOffset columnOffset2 = new Xdr.ColumnOffset();
            columnOffset2.Text = "350000";
            Xdr.RowId rowId2 = new Xdr.RowId();
            rowId2.Text = "5";
            Xdr.RowOffset rowOffset2 = new Xdr.RowOffset();
            rowOffset2.Text = "0";

            toMarker1.Append(columnId2);
            toMarker1.Append(columnOffset2);
            toMarker1.Append(rowId2);
            toMarker1.Append(rowOffset2);

            Xdr.GroupShape groupShape1 = new Xdr.GroupShape();

            Xdr.NonVisualGroupShapeProperties nonVisualGroupShapeProperties1 = new Xdr.NonVisualGroupShapeProperties();
            Xdr.NonVisualDrawingProperties nonVisualDrawingProperties1 = new Xdr.NonVisualDrawingProperties() { Id = (UInt32Value)1044U, Name = "Group 20" };

            Xdr.NonVisualGroupShapeDrawingProperties nonVisualGroupShapeDrawingProperties1 = new Xdr.NonVisualGroupShapeDrawingProperties();
            A.GroupShapeLocks groupShapeLocks1 = new A.GroupShapeLocks();

            nonVisualGroupShapeDrawingProperties1.Append(groupShapeLocks1);

            nonVisualGroupShapeProperties1.Append(nonVisualDrawingProperties1);
            nonVisualGroupShapeProperties1.Append(nonVisualGroupShapeDrawingProperties1);

            Xdr.GroupShapeProperties groupShapeProperties1 = new Xdr.GroupShapeProperties() { BlackWhiteMode = A.BlackWhiteModeValues.Auto };

            A.TransformGroup transformGroup1 = new A.TransformGroup();
            A.Offset offset1 = new A.Offset() { X = 542925L, Y = 2552700L };
            A.Extents extents1 = new A.Extents() { Cx = 2876550L, Cy = 552450L };
            A.ChildOffset childOffset1 = new A.ChildOffset() { X = 57L, Y = 268L };
            A.ChildExtents childExtents1 = new A.ChildExtents() { Cx = 302L, Cy = 58L };

            transformGroup1.Append(offset1);
            transformGroup1.Append(extents1);
            transformGroup1.Append(childOffset1);
            transformGroup1.Append(childExtents1);

            groupShapeProperties1.Append(transformGroup1);

            Xdr.Shape shape1 = new Xdr.Shape() { Macro = "", TextLink = "" };

            Xdr.NonVisualShapeProperties nonVisualShapeProperties1 = new Xdr.NonVisualShapeProperties();
            Xdr.NonVisualDrawingProperties nonVisualDrawingProperties2 = new Xdr.NonVisualDrawingProperties() { Id = (UInt32Value)1034U, Name = "Rectangle 333" };

            Xdr.NonVisualShapeDrawingProperties nonVisualShapeDrawingProperties1 = new Xdr.NonVisualShapeDrawingProperties();
            A.ShapeLocks shapeLocks1 = new A.ShapeLocks() { NoChangeArrowheads = true };

            nonVisualShapeDrawingProperties1.Append(shapeLocks1);

            nonVisualShapeProperties1.Append(nonVisualDrawingProperties2);
            nonVisualShapeProperties1.Append(nonVisualShapeDrawingProperties1);

            Xdr.ShapeProperties shapeProperties1 = new Xdr.ShapeProperties() { BlackWhiteMode = A.BlackWhiteModeValues.Auto };

            A.Transform2D transform2D1 = new A.Transform2D();
            A.Offset offset2 = new A.Offset() { X = 87L, Y = 291L };
            A.Extents extents2 = new A.Extents() { Cx = 108L, Cy = 6L };

            transform2D1.Append(offset2);
            transform2D1.Append(extents2);

            A.PresetGeometry presetGeometry1 = new A.PresetGeometry() { Preset = A.ShapeTypeValues.Rectangle };
            A.AdjustValueList adjustValueList1 = new A.AdjustValueList();

            presetGeometry1.Append(adjustValueList1);

            A.SolidFill solidFill1 = new A.SolidFill();
            A.RgbColorModelHex rgbColorModelHex1 = new A.RgbColorModelHex() { Val = "FFFFFF" };

            solidFill1.Append(rgbColorModelHex1);

            A.Outline outline1 = new A.Outline() { Width = 12700 };

            A.SolidFill solidFill2 = new A.SolidFill();
            A.RgbColorModelHex rgbColorModelHex2 = new A.RgbColorModelHex() { Val = "000000" };

            solidFill2.Append(rgbColorModelHex2);
            A.Miter miter1 = new A.Miter() { Limit = 800000 };
            A.HeadEnd headEnd1 = new A.HeadEnd();
            A.TailEnd tailEnd1 = new A.TailEnd();

            outline1.Append(solidFill2);
            outline1.Append(miter1);
            outline1.Append(headEnd1);
            outline1.Append(tailEnd1);

            shapeProperties1.Append(transform2D1);
            shapeProperties1.Append(presetGeometry1);
            shapeProperties1.Append(solidFill1);
            shapeProperties1.Append(outline1);

            shape1.Append(nonVisualShapeProperties1);
            shape1.Append(shapeProperties1);

            Xdr.Shape shape2 = new Xdr.Shape() { Macro = "", TextLink = "" };

            Xdr.NonVisualShapeProperties nonVisualShapeProperties2 = new Xdr.NonVisualShapeProperties();
            Xdr.NonVisualDrawingProperties nonVisualDrawingProperties3 = new Xdr.NonVisualDrawingProperties() { Id = (UInt32Value)1035U, Name = "Rectangle 334" };

            Xdr.NonVisualShapeDrawingProperties nonVisualShapeDrawingProperties2 = new Xdr.NonVisualShapeDrawingProperties();
            A.ShapeLocks shapeLocks2 = new A.ShapeLocks() { NoChangeArrowheads = true };

            nonVisualShapeDrawingProperties2.Append(shapeLocks2);

            nonVisualShapeProperties2.Append(nonVisualDrawingProperties3);
            nonVisualShapeProperties2.Append(nonVisualShapeDrawingProperties2);

            Xdr.ShapeProperties shapeProperties2 = new Xdr.ShapeProperties() { BlackWhiteMode = A.BlackWhiteModeValues.Auto };

            A.Transform2D transform2D2 = new A.Transform2D();
            A.Offset offset3 = new A.Offset() { X = 195L, Y = 291L };
            A.Extents extents3 = new A.Extents() { Cx = 109L, Cy = 6L };

            transform2D2.Append(offset3);
            transform2D2.Append(extents3);

            A.PresetGeometry presetGeometry2 = new A.PresetGeometry() { Preset = A.ShapeTypeValues.Rectangle };
            A.AdjustValueList adjustValueList2 = new A.AdjustValueList();

            presetGeometry2.Append(adjustValueList2);

            A.SolidFill solidFill3 = new A.SolidFill();
            A.RgbColorModelHex rgbColorModelHex3 = new A.RgbColorModelHex() { Val = "000000" };

            solidFill3.Append(rgbColorModelHex3);

            A.Outline outline2 = new A.Outline() { Width = 12700 };

            A.SolidFill solidFill4 = new A.SolidFill();
            A.RgbColorModelHex rgbColorModelHex4 = new A.RgbColorModelHex() { Val = "000000" };

            solidFill4.Append(rgbColorModelHex4);
            A.Miter miter2 = new A.Miter() { Limit = 800000 };
            A.HeadEnd headEnd2 = new A.HeadEnd();
            A.TailEnd tailEnd2 = new A.TailEnd();

            outline2.Append(solidFill4);
            outline2.Append(miter2);
            outline2.Append(headEnd2);
            outline2.Append(tailEnd2);

            shapeProperties2.Append(transform2D2);
            shapeProperties2.Append(presetGeometry2);
            shapeProperties2.Append(solidFill3);
            shapeProperties2.Append(outline2);

            shape2.Append(nonVisualShapeProperties2);
            shape2.Append(shapeProperties2);

            Xdr.Shape shape3 = new Xdr.Shape() { Macro = "", TextLink = "" };

            Xdr.NonVisualShapeProperties nonVisualShapeProperties3 = new Xdr.NonVisualShapeProperties();
            Xdr.NonVisualDrawingProperties nonVisualDrawingProperties4 = new Xdr.NonVisualDrawingProperties() { Id = (UInt32Value)1027U, Name = "Text Box 335" };

            Xdr.NonVisualShapeDrawingProperties nonVisualShapeDrawingProperties3 = new Xdr.NonVisualShapeDrawingProperties() { TextBox = true };
            A.ShapeLocks shapeLocks3 = new A.ShapeLocks() { NoChangeArrowheads = true };

            nonVisualShapeDrawingProperties3.Append(shapeLocks3);

            nonVisualShapeProperties3.Append(nonVisualDrawingProperties4);
            nonVisualShapeProperties3.Append(nonVisualShapeDrawingProperties3);

            Xdr.ShapeProperties shapeProperties3 = new Xdr.ShapeProperties() { BlackWhiteMode = A.BlackWhiteModeValues.Auto };

            A.Transform2D transform2D3 = new A.Transform2D();
            A.Offset offset4 = new A.Offset() { X = 57L, Y = 304L };
            A.Extents extents4 = new A.Extents() { Cx = 60L, Cy = 21L };

            transform2D3.Append(offset4);
            transform2D3.Append(extents4);

            A.PresetGeometry presetGeometry3 = new A.PresetGeometry() { Preset = A.ShapeTypeValues.Rectangle };
            A.AdjustValueList adjustValueList3 = new A.AdjustValueList();

            presetGeometry3.Append(adjustValueList3);
            A.NoFill noFill1 = new A.NoFill();

            A.Outline outline3 = new A.Outline() { Width = 1 };
            A.NoFill noFill2 = new A.NoFill();
            A.Miter miter3 = new A.Miter() { Limit = 800000 };
            A.HeadEnd headEnd3 = new A.HeadEnd();
            A.TailEnd tailEnd3 = new A.TailEnd();

            outline3.Append(noFill2);
            outline3.Append(miter3);
            outline3.Append(headEnd3);
            outline3.Append(tailEnd3);

            shapeProperties3.Append(transform2D3);
            shapeProperties3.Append(presetGeometry3);
            shapeProperties3.Append(noFill1);
            shapeProperties3.Append(outline3);

            Xdr.TextBody textBody1 = new Xdr.TextBody();

            A.BodyProperties bodyProperties1 = new A.BodyProperties() { Wrap = A.TextWrappingValues.None, LeftInset = 27432, TopInset = 18288, RightInset = 0, BottomInset = 0, Anchor = A.TextAnchoringTypeValues.Top, UpRight = true };
            A.ShapeAutoFit shapeAutoFit1 = new A.ShapeAutoFit();

            bodyProperties1.Append(shapeAutoFit1);
            A.ListStyle listStyle1 = new A.ListStyle();

            A.Paragraph paragraph1 = new A.Paragraph();

            A.ParagraphProperties paragraphProperties1 = new A.ParagraphProperties() { Alignment = A.TextAlignmentTypeValues.Left, RightToLeft = false };
            A.DefaultRunProperties defaultRunProperties1 = new A.DefaultRunProperties() { FontSize = 1000 };

            paragraphProperties1.Append(defaultRunProperties1);

            A.Run run1 = new A.Run();

            A.RunProperties runProperties1 = new A.RunProperties() { Language = "en-US", AlternativeLanguage = "ko-KR", FontSize = 1200, Bold = true, Italic = false, Underline = A.TextUnderlineValues.None, Strike = A.TextStrikeValues.NoStrike, Baseline = 0 };

            A.SolidFill solidFill5 = new A.SolidFill();
            A.RgbColorModelHex rgbColorModelHex5 = new A.RgbColorModelHex() { Val = "000000" };

            solidFill5.Append(rgbColorModelHex5);
            A.LatinFont latinFont1 = new A.LatinFont() { Typeface = "돋움" };
            A.EastAsianFont eastAsianFont1 = new A.EastAsianFont() { Typeface = "돋움" };

            runProperties1.Append(solidFill5);
            runProperties1.Append(latinFont1);
            runProperties1.Append(eastAsianFont1);
            A.Text text1 = new A.Text();
            text1.Text = KL;

            run1.Append(runProperties1);
            run1.Append(text1);

            paragraph1.Append(paragraphProperties1);
            paragraph1.Append(run1);

            textBody1.Append(bodyProperties1);
            textBody1.Append(listStyle1);
            textBody1.Append(paragraph1);

            shape3.Append(nonVisualShapeProperties3);
            shape3.Append(shapeProperties3);
            shape3.Append(textBody1);

            Xdr.Shape shape4 = new Xdr.Shape() { Macro = "", TextLink = "" };

            Xdr.NonVisualShapeProperties nonVisualShapeProperties4 = new Xdr.NonVisualShapeProperties();
            Xdr.NonVisualDrawingProperties nonVisualDrawingProperties5 = new Xdr.NonVisualDrawingProperties() { Id = (UInt32Value)1029U, Name = "Text Box 337" };

            Xdr.NonVisualShapeDrawingProperties nonVisualShapeDrawingProperties4 = new Xdr.NonVisualShapeDrawingProperties() { TextBox = true };
            A.ShapeLocks shapeLocks4 = new A.ShapeLocks() { NoChangeArrowheads = true };

            nonVisualShapeDrawingProperties4.Append(shapeLocks4);

            nonVisualShapeProperties4.Append(nonVisualDrawingProperties5);
            nonVisualShapeProperties4.Append(nonVisualShapeDrawingProperties4);

            Xdr.ShapeProperties shapeProperties4 = new Xdr.ShapeProperties() { BlackWhiteMode = A.BlackWhiteModeValues.Auto };

            A.Transform2D transform2D4 = new A.Transform2D();
            A.Offset offset5 = new A.Offset() { X = 299L, Y = 305L };
            A.Extents extents5 = new A.Extents() { Cx = 60L, Cy = 21L };

            transform2D4.Append(offset5);
            transform2D4.Append(extents5);

            A.PresetGeometry presetGeometry4 = new A.PresetGeometry() { Preset = A.ShapeTypeValues.Rectangle };
            A.AdjustValueList adjustValueList4 = new A.AdjustValueList();

            presetGeometry4.Append(adjustValueList4);
            A.NoFill noFill3 = new A.NoFill();

            A.Outline outline4 = new A.Outline() { Width = 1 };
            A.NoFill noFill4 = new A.NoFill();
            A.Miter miter4 = new A.Miter() { Limit = 800000 };
            A.HeadEnd headEnd4 = new A.HeadEnd();
            A.TailEnd tailEnd4 = new A.TailEnd();

            outline4.Append(noFill4);
            outline4.Append(miter4);
            outline4.Append(headEnd4);
            outline4.Append(tailEnd4);

            shapeProperties4.Append(transform2D4);
            shapeProperties4.Append(presetGeometry4);
            shapeProperties4.Append(noFill3);
            shapeProperties4.Append(outline4);

            Xdr.TextBody textBody2 = new Xdr.TextBody();

            A.BodyProperties bodyProperties2 = new A.BodyProperties() { Wrap = A.TextWrappingValues.None, LeftInset = 27432, TopInset = 18288, RightInset = 0, BottomInset = 0, Anchor = A.TextAnchoringTypeValues.Top, UpRight = true };
            A.ShapeAutoFit shapeAutoFit2 = new A.ShapeAutoFit();

            bodyProperties2.Append(shapeAutoFit2);
            A.ListStyle listStyle2 = new A.ListStyle();

            A.Paragraph paragraph2 = new A.Paragraph();

            A.ParagraphProperties paragraphProperties2 = new A.ParagraphProperties() { Alignment = A.TextAlignmentTypeValues.Left, RightToLeft = false };
            A.DefaultRunProperties defaultRunProperties2 = new A.DefaultRunProperties() { FontSize = 1000 };

            paragraphProperties2.Append(defaultRunProperties2);

            A.Run run2 = new A.Run();

            A.RunProperties runProperties2 = new A.RunProperties() { Language = "en-US", AlternativeLanguage = "ko-KR", FontSize = 1200, Bold = true, Italic = false, Underline = A.TextUnderlineValues.None, Strike = A.TextStrikeValues.NoStrike, Baseline = 0 };

            A.SolidFill solidFill6 = new A.SolidFill();
            A.RgbColorModelHex rgbColorModelHex6 = new A.RgbColorModelHex() { Val = "000000" };

            solidFill6.Append(rgbColorModelHex6);
            A.LatinFont latinFont2 = new A.LatinFont() { Typeface = "돋움" };
            A.EastAsianFont eastAsianFont2 = new A.EastAsianFont() { Typeface = "돋움" };

            runProperties2.Append(solidFill6);
            runProperties2.Append(latinFont2);
            runProperties2.Append(eastAsianFont2);
            A.Text text2 = new A.Text();
            text2.Text = FT;

            run2.Append(runProperties2);
            run2.Append(text2);

            paragraph2.Append(paragraphProperties2);
            paragraph2.Append(run2);

            textBody2.Append(bodyProperties2);
            textBody2.Append(listStyle2);
            textBody2.Append(paragraph2);

            shape4.Append(nonVisualShapeProperties4);
            shape4.Append(shapeProperties4);
            shape4.Append(textBody2);

            Xdr.Shape shape5 = new Xdr.Shape() { Macro = "", TextLink = "" };

            Xdr.NonVisualShapeProperties nonVisualShapeProperties5 = new Xdr.NonVisualShapeProperties();
            Xdr.NonVisualDrawingProperties nonVisualDrawingProperties6 = new Xdr.NonVisualDrawingProperties() { Id = (UInt32Value)1030U, Name = "Text Box 338" };

            Xdr.NonVisualShapeDrawingProperties nonVisualShapeDrawingProperties5 = new Xdr.NonVisualShapeDrawingProperties() { TextBox = true };
            A.ShapeLocks shapeLocks5 = new A.ShapeLocks() { NoChangeArrowheads = true };

            nonVisualShapeDrawingProperties5.Append(shapeLocks5);

            nonVisualShapeProperties5.Append(nonVisualDrawingProperties6);
            nonVisualShapeProperties5.Append(nonVisualShapeDrawingProperties5);

            Xdr.ShapeProperties shapeProperties5 = new Xdr.ShapeProperties() { BlackWhiteMode = A.BlackWhiteModeValues.Auto };

            A.Transform2D transform2D5 = new A.Transform2D();
            A.Offset offset6 = new A.Offset() { X = 177L, Y = 305L };
            A.Extents extents6 = new A.Extents() { Cx = 36L, Cy = 21L };

            transform2D5.Append(offset6);
            transform2D5.Append(extents6);

            A.PresetGeometry presetGeometry5 = new A.PresetGeometry() { Preset = A.ShapeTypeValues.Rectangle };
            A.AdjustValueList adjustValueList5 = new A.AdjustValueList();

            presetGeometry5.Append(adjustValueList5);
            A.NoFill noFill5 = new A.NoFill();

            A.Outline outline5 = new A.Outline() { Width = 1 };
            A.NoFill noFill6 = new A.NoFill();
            A.Miter miter5 = new A.Miter() { Limit = 800000 };
            A.HeadEnd headEnd5 = new A.HeadEnd();
            A.TailEnd tailEnd5 = new A.TailEnd();

            outline5.Append(noFill6);
            outline5.Append(miter5);
            outline5.Append(headEnd5);
            outline5.Append(tailEnd5);

            shapeProperties5.Append(transform2D5);
            shapeProperties5.Append(presetGeometry5);
            shapeProperties5.Append(noFill5);
            shapeProperties5.Append(outline5);

            Xdr.TextBody textBody3 = new Xdr.TextBody();

            A.BodyProperties bodyProperties3 = new A.BodyProperties() { Wrap = A.TextWrappingValues.None, LeftInset = 27432, TopInset = 18288, RightInset = 0, BottomInset = 0, Anchor = A.TextAnchoringTypeValues.Top, UpRight = true };
            A.ShapeAutoFit shapeAutoFit3 = new A.ShapeAutoFit();

            bodyProperties3.Append(shapeAutoFit3);
            A.ListStyle listStyle3 = new A.ListStyle();

            A.Paragraph paragraph3 = new A.Paragraph();

            A.ParagraphProperties paragraphProperties3 = new A.ParagraphProperties() { Alignment = A.TextAlignmentTypeValues.Left, RightToLeft = false };
            A.DefaultRunProperties defaultRunProperties3 = new A.DefaultRunProperties() { FontSize = 1000 };

            paragraphProperties3.Append(defaultRunProperties3);

            A.Run run3 = new A.Run();

            A.RunProperties runProperties3 = new A.RunProperties() { Language = "en-US", AlternativeLanguage = "ko-KR", FontSize = 1200, Bold = true, Italic = false, Underline = A.TextUnderlineValues.None, Strike = A.TextStrikeValues.NoStrike, Baseline = 0 };

            A.SolidFill solidFill7 = new A.SolidFill();
            A.RgbColorModelHex rgbColorModelHex7 = new A.RgbColorModelHex() { Val = "000000" };

            solidFill7.Append(rgbColorModelHex7);
            A.LatinFont latinFont3 = new A.LatinFont() { Typeface = "돋움" };
            A.EastAsianFont eastAsianFont3 = new A.EastAsianFont() { Typeface = "돋움" };

            runProperties3.Append(solidFill7);
            runProperties3.Append(latinFont3);
            runProperties3.Append(eastAsianFont3);
            A.Text text3 = new A.Text();
            text3.Text = LC;

            run3.Append(runProperties3);
            run3.Append(text3);

            paragraph3.Append(paragraphProperties3);
            paragraph3.Append(run3);

            textBody3.Append(bodyProperties3);
            textBody3.Append(listStyle3);
            textBody3.Append(paragraph3);

            shape5.Append(nonVisualShapeProperties5);
            shape5.Append(shapeProperties5);
            shape5.Append(textBody3);

            Xdr.Shape shape6 = new Xdr.Shape() { Macro = "", TextLink = "" };

            Xdr.NonVisualShapeProperties nonVisualShapeProperties6 = new Xdr.NonVisualShapeProperties();
            Xdr.NonVisualDrawingProperties nonVisualDrawingProperties7 = new Xdr.NonVisualDrawingProperties() { Id = (UInt32Value)1032U, Name = "Rectangle 340" };

            Xdr.NonVisualShapeDrawingProperties nonVisualShapeDrawingProperties6 = new Xdr.NonVisualShapeDrawingProperties();
            A.ShapeLocks shapeLocks6 = new A.ShapeLocks() { NoChangeArrowheads = true };

            nonVisualShapeDrawingProperties6.Append(shapeLocks6);

            nonVisualShapeProperties6.Append(nonVisualDrawingProperties7);
            nonVisualShapeProperties6.Append(nonVisualShapeDrawingProperties6);

            Xdr.ShapeProperties shapeProperties6 = new Xdr.ShapeProperties() { BlackWhiteMode = A.BlackWhiteModeValues.Auto };

            A.Transform2D transform2D6 = new A.Transform2D();
            A.Offset offset7 = new A.Offset() { X = 106L, Y = 270L };
            A.Extents extents7 = new A.Extents() { Cx = 29L, Cy = 20L };

            transform2D6.Append(offset7);
            transform2D6.Append(extents7);

            A.PresetGeometry presetGeometry6 = new A.PresetGeometry() { Preset = A.ShapeTypeValues.Rectangle };
            A.AdjustValueList adjustValueList6 = new A.AdjustValueList();

            presetGeometry6.Append(adjustValueList6);
            A.NoFill noFill7 = new A.NoFill();

            A.Outline outline6 = new A.Outline() { Width = 9525 };
            A.NoFill noFill8 = new A.NoFill();
            A.Miter miter6 = new A.Miter() { Limit = 800000 };
            A.HeadEnd headEnd6 = new A.HeadEnd();
            A.TailEnd tailEnd6 = new A.TailEnd();

            outline6.Append(noFill8);
            outline6.Append(miter6);
            outline6.Append(headEnd6);
            outline6.Append(tailEnd6);

            shapeProperties6.Append(transform2D6);
            shapeProperties6.Append(presetGeometry6);
            shapeProperties6.Append(noFill7);
            shapeProperties6.Append(outline6);

            Xdr.TextBody textBody4 = new Xdr.TextBody();

            A.BodyProperties bodyProperties4 = new A.BodyProperties() { Wrap = A.TextWrappingValues.None, LeftInset = 27432, TopInset = 18288, RightInset = 0, BottomInset = 0, Anchor = A.TextAnchoringTypeValues.Top, UpRight = true };
            A.ShapeAutoFit shapeAutoFit4 = new A.ShapeAutoFit();

            bodyProperties4.Append(shapeAutoFit4);
            A.ListStyle listStyle4 = new A.ListStyle();

            A.Paragraph paragraph4 = new A.Paragraph();

            A.ParagraphProperties paragraphProperties4 = new A.ParagraphProperties() { Alignment = A.TextAlignmentTypeValues.Left, RightToLeft = false };
            A.DefaultRunProperties defaultRunProperties4 = new A.DefaultRunProperties() { FontSize = 1000 };

            paragraphProperties4.Append(defaultRunProperties4);

            A.Run run4 = new A.Run();

            A.RunProperties runProperties4 = new A.RunProperties() { Language = "en-US", AlternativeLanguage = "ko-KR", FontSize = 1100, Bold = true, Italic = false, Underline = A.TextUnderlineValues.None, Strike = A.TextStrikeValues.NoStrike, Baseline = 0 };

            A.SolidFill solidFill8 = new A.SolidFill();
            A.RgbColorModelHex rgbColorModelHex8 = new A.RgbColorModelHex() { Val = "000000" };

            solidFill8.Append(rgbColorModelHex8);
            A.LatinFont latinFont4 = new A.LatinFont() { Typeface = "돋움" };
            A.EastAsianFont eastAsianFont4 = new A.EastAsianFont() { Typeface = "돋움" };

            runProperties4.Append(solidFill8);
            runProperties4.Append(latinFont4);
            runProperties4.Append(eastAsianFont4);
            A.Text text4 = new A.Text();
            text4.Text = fweek1;

            run4.Append(runProperties4);
            run4.Append(text4);

            A.Run run5 = new A.Run();

            A.RunProperties runProperties5 = new A.RunProperties() { Language = "ko-KR", AlternativeLanguage = "en-US", FontSize = 1100, Bold = true, Italic = false, Underline = A.TextUnderlineValues.None, Strike = A.TextStrikeValues.NoStrike, Baseline = 0 };

            A.SolidFill solidFill9 = new A.SolidFill();
            A.RgbColorModelHex rgbColorModelHex9 = new A.RgbColorModelHex() { Val = "000000" };

            solidFill9.Append(rgbColorModelHex9);
            A.LatinFont latinFont5 = new A.LatinFont() { Typeface = "돋움" };
            A.EastAsianFont eastAsianFont5 = new A.EastAsianFont() { Typeface = "돋움" };

            runProperties5.Append(solidFill9);
            runProperties5.Append(latinFont5);
            runProperties5.Append(eastAsianFont5);
            A.Text text5 = new A.Text();
            if (fweek1 == "")
                text5.Text = "";
            else
                text5.Text = "주";

            run5.Append(runProperties5);
            run5.Append(text5);

            paragraph4.Append(paragraphProperties4);
            paragraph4.Append(run4);
            paragraph4.Append(run5);

            textBody4.Append(bodyProperties4);
            textBody4.Append(listStyle4);
            textBody4.Append(paragraph4);

            shape6.Append(nonVisualShapeProperties6);
            shape6.Append(shapeProperties6);
            shape6.Append(textBody4);

            Xdr.Shape shape7 = new Xdr.Shape() { Macro = "", TextLink = "" };

            Xdr.NonVisualShapeProperties nonVisualShapeProperties7 = new Xdr.NonVisualShapeProperties();
            Xdr.NonVisualDrawingProperties nonVisualDrawingProperties8 = new Xdr.NonVisualDrawingProperties() { Id = (UInt32Value)1033U, Name = "Rectangle 341" };

            Xdr.NonVisualShapeDrawingProperties nonVisualShapeDrawingProperties7 = new Xdr.NonVisualShapeDrawingProperties();
            A.ShapeLocks shapeLocks7 = new A.ShapeLocks() { NoChangeArrowheads = true };

            nonVisualShapeDrawingProperties7.Append(shapeLocks7);

            nonVisualShapeProperties7.Append(nonVisualDrawingProperties8);
            nonVisualShapeProperties7.Append(nonVisualShapeDrawingProperties7);

            Xdr.ShapeProperties shapeProperties7 = new Xdr.ShapeProperties() { BlackWhiteMode = A.BlackWhiteModeValues.Auto };

            A.Transform2D transform2D7 = new A.Transform2D();
            A.Offset offset8 = new A.Offset() { X = 182L, Y = 268L };
            A.Extents extents8 = new A.Extents() { Cx = 29L, Cy = 20L };

            transform2D7.Append(offset8);
            transform2D7.Append(extents8);

            A.PresetGeometry presetGeometry7 = new A.PresetGeometry() { Preset = A.ShapeTypeValues.Rectangle };
            A.AdjustValueList adjustValueList7 = new A.AdjustValueList();

            presetGeometry7.Append(adjustValueList7);
            A.NoFill noFill9 = new A.NoFill();

            A.Outline outline7 = new A.Outline() { Width = 9525 };
            A.NoFill noFill10 = new A.NoFill();
            A.Miter miter7 = new A.Miter() { Limit = 800000 };
            A.HeadEnd headEnd7 = new A.HeadEnd();
            A.TailEnd tailEnd7 = new A.TailEnd();

            outline7.Append(noFill10);
            outline7.Append(miter7);
            outline7.Append(headEnd7);
            outline7.Append(tailEnd7);

            shapeProperties7.Append(transform2D7);
            shapeProperties7.Append(presetGeometry7);
            shapeProperties7.Append(noFill9);
            shapeProperties7.Append(outline7);

            Xdr.TextBody textBody5 = new Xdr.TextBody();

            A.BodyProperties bodyProperties5 = new A.BodyProperties() { Wrap = A.TextWrappingValues.None, LeftInset = 27432, TopInset = 18288, RightInset = 0, BottomInset = 0, Anchor = A.TextAnchoringTypeValues.Top, UpRight = true };
            A.ShapeAutoFit shapeAutoFit5 = new A.ShapeAutoFit();

            bodyProperties5.Append(shapeAutoFit5);
            A.ListStyle listStyle5 = new A.ListStyle();

            A.Paragraph paragraph5 = new A.Paragraph();

            A.ParagraphProperties paragraphProperties5 = new A.ParagraphProperties() { Alignment = A.TextAlignmentTypeValues.Left, RightToLeft = false };
            A.DefaultRunProperties defaultRunProperties5 = new A.DefaultRunProperties() { FontSize = 1000 };

            paragraphProperties5.Append(defaultRunProperties5);

            A.Run run6 = new A.Run();

            A.RunProperties runProperties6 = new A.RunProperties() { Language = "en-US", AlternativeLanguage = "ko-KR", FontSize = 1100, Bold = true, Italic = false, Underline = A.TextUnderlineValues.None, Strike = A.TextStrikeValues.NoStrike, Baseline = 0 };

            A.SolidFill solidFill10 = new A.SolidFill();
            A.RgbColorModelHex rgbColorModelHex10 = new A.RgbColorModelHex() { Val = "000000" };

            solidFill10.Append(rgbColorModelHex10);
            A.LatinFont latinFont6 = new A.LatinFont() { Typeface = "돋움" };
            A.EastAsianFont eastAsianFont6 = new A.EastAsianFont() { Typeface = "돋움" };

            runProperties6.Append(solidFill10);
            runProperties6.Append(latinFont6);
            runProperties6.Append(eastAsianFont6);
            A.Text text6 = new A.Text();
            text6.Text = fweek2;

            run6.Append(runProperties6);
            run6.Append(text6);

            A.Run run7 = new A.Run();

            A.RunProperties runProperties7 = new A.RunProperties() { Language = "ko-KR", AlternativeLanguage = "en-US", FontSize = 1100, Bold = true, Italic = false, Underline = A.TextUnderlineValues.None, Strike = A.TextStrikeValues.NoStrike, Baseline = 0 };

            A.SolidFill solidFill11 = new A.SolidFill();
            A.RgbColorModelHex rgbColorModelHex11 = new A.RgbColorModelHex() { Val = "000000" };

            solidFill11.Append(rgbColorModelHex11);
            A.LatinFont latinFont7 = new A.LatinFont() { Typeface = "돋움" };
            A.EastAsianFont eastAsianFont7 = new A.EastAsianFont() { Typeface = "돋움" };

            runProperties7.Append(solidFill11);
            runProperties7.Append(latinFont7);
            runProperties7.Append(eastAsianFont7);
            A.Text text7 = new A.Text();
            if (fweek2 == "")
                text7.Text = "";
            else
                text7.Text = "주";

            run7.Append(runProperties7);
            run7.Append(text7);

            paragraph5.Append(paragraphProperties5);
            paragraph5.Append(run6);
            paragraph5.Append(run7);

            textBody5.Append(bodyProperties5);
            textBody5.Append(listStyle5);
            textBody5.Append(paragraph5);

            shape7.Append(nonVisualShapeProperties7);
            shape7.Append(shapeProperties7);
            shape7.Append(textBody5);

            Xdr.Shape shape8 = new Xdr.Shape() { Macro = "", TextLink = "" };

            Xdr.NonVisualShapeProperties nonVisualShapeProperties8 = new Xdr.NonVisualShapeProperties();
            Xdr.NonVisualDrawingProperties nonVisualDrawingProperties9 = new Xdr.NonVisualDrawingProperties() { Id = (UInt32Value)2U, Name = "Rectangle 342" };

            Xdr.NonVisualShapeDrawingProperties nonVisualShapeDrawingProperties8 = new Xdr.NonVisualShapeDrawingProperties();
            A.ShapeLocks shapeLocks8 = new A.ShapeLocks() { NoChangeArrowheads = true };

            nonVisualShapeDrawingProperties8.Append(shapeLocks8);

            nonVisualShapeProperties8.Append(nonVisualDrawingProperties9);
            nonVisualShapeProperties8.Append(nonVisualShapeDrawingProperties8);

            Xdr.ShapeProperties shapeProperties8 = new Xdr.ShapeProperties() { BlackWhiteMode = A.BlackWhiteModeValues.Auto };

            A.Transform2D transform2D8 = new A.Transform2D();
            A.Offset offset9 = new A.Offset() { X = 253L, Y = 268L };
            A.Extents extents9 = new A.Extents() { Cx = 29L, Cy = 20L };

            transform2D8.Append(offset9);
            transform2D8.Append(extents9);

            A.PresetGeometry presetGeometry8 = new A.PresetGeometry() { Preset = A.ShapeTypeValues.Rectangle };
            A.AdjustValueList adjustValueList8 = new A.AdjustValueList();

            presetGeometry8.Append(adjustValueList8);
            A.NoFill noFill11 = new A.NoFill();

            A.Outline outline8 = new A.Outline() { Width = 9525 };
            A.NoFill noFill12 = new A.NoFill();
            A.Miter miter8 = new A.Miter() { Limit = 800000 };
            A.HeadEnd headEnd8 = new A.HeadEnd();
            A.TailEnd tailEnd8 = new A.TailEnd();

            outline8.Append(noFill12);
            outline8.Append(miter8);
            outline8.Append(headEnd8);
            outline8.Append(tailEnd8);

            shapeProperties8.Append(transform2D8);
            shapeProperties8.Append(presetGeometry8);
            shapeProperties8.Append(noFill11);
            shapeProperties8.Append(outline8);

            Xdr.TextBody textBody6 = new Xdr.TextBody();

            A.BodyProperties bodyProperties6 = new A.BodyProperties() { Wrap = A.TextWrappingValues.None, LeftInset = 27432, TopInset = 18288, RightInset = 0, BottomInset = 0, Anchor = A.TextAnchoringTypeValues.Top, UpRight = true };
            A.ShapeAutoFit shapeAutoFit6 = new A.ShapeAutoFit();

            bodyProperties6.Append(shapeAutoFit6);
            A.ListStyle listStyle6 = new A.ListStyle();

            A.Paragraph paragraph6 = new A.Paragraph();

            A.ParagraphProperties paragraphProperties6 = new A.ParagraphProperties() { Alignment = A.TextAlignmentTypeValues.Left, RightToLeft = false };
            A.DefaultRunProperties defaultRunProperties6 = new A.DefaultRunProperties() { FontSize = 1000 };

            paragraphProperties6.Append(defaultRunProperties6);

            A.Run run8 = new A.Run();

            A.RunProperties runProperties8 = new A.RunProperties() { Language = "en-US", AlternativeLanguage = "ko-KR", FontSize = 1100, Bold = true, Italic = false, Underline = A.TextUnderlineValues.None, Strike = A.TextStrikeValues.NoStrike, Baseline = 0 };

            A.SolidFill solidFill12 = new A.SolidFill();
            A.RgbColorModelHex rgbColorModelHex12 = new A.RgbColorModelHex() { Val = "000000" };

            solidFill12.Append(rgbColorModelHex12);
            A.LatinFont latinFont8 = new A.LatinFont() { Typeface = "돋움" };
            A.EastAsianFont eastAsianFont8 = new A.EastAsianFont() { Typeface = "돋움" };

            runProperties8.Append(solidFill12);
            runProperties8.Append(latinFont8);
            runProperties8.Append(eastAsianFont8);
            A.Text text8 = new A.Text();
            text8.Text = fweek3;

            run8.Append(runProperties8);
            run8.Append(text8);

            A.Run run9 = new A.Run();

            A.RunProperties runProperties9 = new A.RunProperties() { Language = "ko-KR", AlternativeLanguage = "en-US", FontSize = 1100, Bold = true, Italic = false, Underline = A.TextUnderlineValues.None, Strike = A.TextStrikeValues.NoStrike, Baseline = 0 };

            A.SolidFill solidFill13 = new A.SolidFill();
            A.RgbColorModelHex rgbColorModelHex13 = new A.RgbColorModelHex() { Val = "000000" };

            solidFill13.Append(rgbColorModelHex13);
            A.LatinFont latinFont9 = new A.LatinFont() { Typeface = "돋움" };
            A.EastAsianFont eastAsianFont9 = new A.EastAsianFont() { Typeface = "돋움" };

            runProperties9.Append(solidFill13);
            runProperties9.Append(latinFont9);
            runProperties9.Append(eastAsianFont9);
            A.Text text9 = new A.Text();
            if (fweek3 == "")
                text9.Text = "";
            else
                text9.Text = "주";

            run9.Append(runProperties9);
            run9.Append(text9);

            paragraph6.Append(paragraphProperties6);
            paragraph6.Append(run8);
            paragraph6.Append(run9);

            textBody6.Append(bodyProperties6);
            textBody6.Append(listStyle6);
            textBody6.Append(paragraph6);

            shape8.Append(nonVisualShapeProperties8);
            shape8.Append(shapeProperties8);
            shape8.Append(textBody6);

            groupShape1.Append(nonVisualGroupShapeProperties1);
            groupShape1.Append(groupShapeProperties1);
            groupShape1.Append(shape1);
            groupShape1.Append(shape2);
            groupShape1.Append(shape3);
            groupShape1.Append(shape4);
            groupShape1.Append(shape5);
            groupShape1.Append(shape6);
            groupShape1.Append(shape7);
            groupShape1.Append(shape8);
            Xdr.ClientData clientData1 = new Xdr.ClientData();

            twoCellAnchor1.Append(fromMarker1);
            twoCellAnchor1.Append(toMarker1);
            twoCellAnchor1.Append(groupShape1);
            twoCellAnchor1.Append(clientData1);

            _Worksheet.WorksheetPart.DrawingsPart.WorksheetDrawing.Append(twoCellAnchor1);

            // 검증
            //CreateValidTwoCellAnchor(fromCol, fromColOffset, fromRow, fromRowOffset, toCol, toColOffset, toRow, toRowOffset);
        }

        /// <summary>
        /// 이벤트일의 calendar net
        /// </summary>
        /// <param name="row"></param>
        /// <param name="col"></param>
        /// <param name="text"></param>
        public void SetTextSPCalendar(int row, int col, string text)
        {
            if (_Worksheet == null) return;
            string colName = GetExcelColName(col);
            Cell cell = GetCell(_Worksheet, colName, row);

            if (cell == null) return;
            int index = InsertSharedStringItem(text, _StringTable);
            cell.CellValue = new CellValue(index.ToString());
            cell.DataType = new EnumValue<CellValues>(CellValues.SharedString);

            Font font1 = new Font();
            Bold bold1 = new Bold();
            FontSize fontSize1 = new FontSize() { Val = 11D };
            FontName fontName1 = new FontName() { Val = "돋움" };
            FontCharSet fontCharSet1 = new FontCharSet() { Val = 129 };
            font1.Append(bold1);
            font1.Append(fontSize1);
            font1.Append(fontName1);
            font1.Append(fontCharSet1);
            
            DocumentFormat.OpenXml.Spreadsheet.Fill fill3 = new DocumentFormat.OpenXml.Spreadsheet.Fill();

            DocumentFormat.OpenXml.Spreadsheet.PatternFill patternFill3 = new DocumentFormat.OpenXml.Spreadsheet.PatternFill() { PatternType = PatternValues.Solid };
            DocumentFormat.OpenXml.Spreadsheet.ForegroundColor foregroundColor1 = new DocumentFormat.OpenXml.Spreadsheet.ForegroundColor() { Indexed = (UInt32Value)13U };
            DocumentFormat.OpenXml.Spreadsheet.BackgroundColor backgroundColor1 = new DocumentFormat.OpenXml.Spreadsheet.BackgroundColor() { Indexed = (UInt32Value)64U };

            patternFill3.Append(foregroundColor1);
            patternFill3.Append(backgroundColor1);

            fill3.Append(patternFill3);

            _SheetDocument.WorkbookPart.WorkbookStylesPart.Stylesheet.Fills.Append(fill3);




            _SheetDocument.WorkbookPart.WorkbookStylesPart.Stylesheet.Fonts.Append(font1);
            UInt32Value formatID = _SheetDocument.WorkbookPart.WorkbookStylesPart.Stylesheet.CellFormats.Count;

            UInt32Value fontID = _SheetDocument.WorkbookPart.WorkbookStylesPart.Stylesheet.Fonts.Count;

            UInt32Value fillID = _SheetDocument.WorkbookPart.WorkbookStylesPart.Stylesheet.Fills.Count;



            Borders borders1 = new Borders() { Count = (UInt32Value)1U };

            Border border1 = new Border();
            DocumentFormat.OpenXml.Spreadsheet.LeftBorder leftBorder1 = new DocumentFormat.OpenXml.Spreadsheet.LeftBorder();
            DocumentFormat.OpenXml.Spreadsheet.RightBorder rightBorder1 = new DocumentFormat.OpenXml.Spreadsheet.RightBorder();
            DocumentFormat.OpenXml.Spreadsheet.TopBorder topBorder1 = new DocumentFormat.OpenXml.Spreadsheet.TopBorder();
            DocumentFormat.OpenXml.Spreadsheet.BottomBorder bottomBorder1 = new DocumentFormat.OpenXml.Spreadsheet.BottomBorder();
            DiagonalBorder diagonalBorder1 = new DiagonalBorder();

            border1.Append(leftBorder1);
            border1.Append(rightBorder1);
            border1.Append(topBorder1);
            border1.Append(bottomBorder1);
            border1.Append(diagonalBorder1);
            _SheetDocument.WorkbookPart.WorkbookStylesPart.Stylesheet.Borders.Append(border1);


            CellFormat cellFormat1 = new CellFormat() { NumberFormatId = (UInt32Value)0U, FontId = (UInt32Value)4U, FillId = (UInt32Value)3U, BorderId = (UInt32Value)2U };

            Alignment alignment1 = new Alignment() { Vertical = VerticalAlignmentValues.Center, Horizontal = HorizontalAlignmentValues.Center };
            cellFormat1.Alignment = alignment1;


            _SheetDocument.WorkbookPart.WorkbookStylesPart.Stylesheet.CellFormats.Append(cellFormat1);

            cell.StyleIndex = 106;



        }

        /// <summary>
        /// 이벤트일의 calendar day
        /// </summary>
        /// <param name="row"></param>
        /// <param name="col"></param>
        /// <param name="text"></param>
        public void SetTextSPDate(int row, int col, string text)
        {
            if (_Worksheet == null) return;
            string colName = GetExcelColName(col);
            Cell cell = GetCell(_Worksheet, colName, row);

            if (cell == null) return;
            int index = InsertSharedStringItem(text, _StringTable);
            cell.CellValue = new CellValue(index.ToString());
            cell.DataType = new EnumValue<CellValues>(CellValues.SharedString);

            Font font1 = new Font();
            Bold bold1 = new Bold();
            FontSize fontSize1 = new FontSize() { Val = 11D };
            FontName fontName1 = new FontName() { Val = "돋움" };
            FontCharSet fontCharSet1 = new FontCharSet() { Val = 129 };
            font1.Append(bold1);
            font1.Append(fontSize1);
            font1.Append(fontName1);
            font1.Append(fontCharSet1);




            DocumentFormat.OpenXml.Spreadsheet.Fill fill3 = new DocumentFormat.OpenXml.Spreadsheet.Fill();

            DocumentFormat.OpenXml.Spreadsheet.PatternFill patternFill3 = new DocumentFormat.OpenXml.Spreadsheet.PatternFill() { PatternType = PatternValues.Solid };
            DocumentFormat.OpenXml.Spreadsheet.ForegroundColor foregroundColor1 = new DocumentFormat.OpenXml.Spreadsheet.ForegroundColor() { Indexed = (UInt32Value)13U };
            DocumentFormat.OpenXml.Spreadsheet.BackgroundColor backgroundColor1 = new DocumentFormat.OpenXml.Spreadsheet.BackgroundColor() { Indexed = (UInt32Value)64U };

            patternFill3.Append(foregroundColor1);
            patternFill3.Append(backgroundColor1);

            fill3.Append(patternFill3);

            _SheetDocument.WorkbookPart.WorkbookStylesPart.Stylesheet.Fills.Append(fill3);




            _SheetDocument.WorkbookPart.WorkbookStylesPart.Stylesheet.Fonts.Append(font1);
            UInt32Value formatID = _SheetDocument.WorkbookPart.WorkbookStylesPart.Stylesheet.CellFormats.Count;

            UInt32Value fontID = _SheetDocument.WorkbookPart.WorkbookStylesPart.Stylesheet.Fonts.Count;

            UInt32Value fillID = _SheetDocument.WorkbookPart.WorkbookStylesPart.Stylesheet.Fills.Count;



            Borders borders1 = new Borders() { Count = (UInt32Value)1U };

            Border border1 = new Border();
            DocumentFormat.OpenXml.Spreadsheet.LeftBorder leftBorder1 = new DocumentFormat.OpenXml.Spreadsheet.LeftBorder();
            DocumentFormat.OpenXml.Spreadsheet.RightBorder rightBorder1 = new DocumentFormat.OpenXml.Spreadsheet.RightBorder();
            DocumentFormat.OpenXml.Spreadsheet.TopBorder topBorder1 = new DocumentFormat.OpenXml.Spreadsheet.TopBorder();
            DocumentFormat.OpenXml.Spreadsheet.BottomBorder bottomBorder1 = new DocumentFormat.OpenXml.Spreadsheet.BottomBorder();
            DiagonalBorder diagonalBorder1 = new DiagonalBorder();

            border1.Append(leftBorder1);
            border1.Append(rightBorder1);
            border1.Append(topBorder1);
            border1.Append(bottomBorder1);
            border1.Append(diagonalBorder1);
            _SheetDocument.WorkbookPart.WorkbookStylesPart.Stylesheet.Borders.Append(border1);


            CellFormat cellFormat1 = new CellFormat() { NumberFormatId = (UInt32Value)0U, FontId = (UInt32Value)4U, FillId = (UInt32Value)3U, BorderId = (UInt32Value)7U };

            Alignment alignment1 = new Alignment() { Vertical = VerticalAlignmentValues.Center, Horizontal = HorizontalAlignmentValues.Center };
            cellFormat1.Alignment = alignment1;


            _SheetDocument.WorkbookPart.WorkbookStylesPart.Stylesheet.CellFormats.Append(cellFormat1);

            cell.StyleIndex = 105;

        }

        #endregion


    }
}
