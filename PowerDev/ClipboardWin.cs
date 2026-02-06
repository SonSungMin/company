using DevExpress.CodeParser;
using DevExpress.XtraBars.Alerter;
using DevExpress.XtraRichEdit.Model;
using DevExpress.XtraSplashScreen;
using DevTools.UI;
using DevTools.UI.Control;
using DevTools.UI.PopUp;
using DevTools.Util;
using DevTools.Util.DataBase;
using Microsoft.Office.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Point = System.Drawing.Point;

namespace DevTools
{
    public partial class ClipboardWin : Form
    {
        private static SynchronizationContext _uiContext;
        private static AlertControl _alert;
        private GlobalKeyboardHook keyboardHook;
        private string copiedText = "";
        private List<string> chunks = new List<string>();
        private bool isProcessing = false;
        private const int CHUNK_SIZE = 1000;

        [DllImport("user32.dll")]
        static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        const byte VK_CONTROL = 0x11;
        const byte VK_V = 0x56;
        const uint KEYEVENTF_KEYUP = 0x0002;

        public ClipboardWin()
        {
            InitializeComponent();

            if (LicenseManager.UsageMode == LicenseUsageMode.Designtime) return;

            DevExpress.Skins.SkinManager.EnableFormSkins();
            DevExpress.Utils.AppearanceObject.DefaultFont = new System.Drawing.Font("굴림체", 9f);

            this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
            this.UpdateStyles();

            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = Color.Black;
            this.TransparencyKey = Color.Black;

            keyboardHook = new GlobalKeyboardHook();
            keyboardHook.KeyDown += OnGlobalKeyDown;
            keyboardHook.Hook();


            _uiContext = SynchronizationContext.Current;
            _alert = new AlertControl
            {
                AutoFormDelay = 3000
            };

            Location = new Point(1, 1);

            if (SystemInfoContext.Current == null)
            {
                SystemInfoContext systemInfoContext = new SystemInfoContext();
                systemInfoContext.SetCallContext();
                systemInfoContext.SetThreadPrincipal();
            }

            SystemInfoContext.Current["CLIPBOARD_TOUPPER"] = togUpper.Checked ? "1" : "0";
            SystemInfoContext.Current["CLIPBOARD_SEQUENCE"] = togSeq.Checked ? "1" : "0";
            SystemInfoContext.Current["CLIPBOARD_MANY_TEXT"] = togManyText.Checked ? "1" : "0";

            SetStatus();
        }


        bool isMove = false;
        Point fPt;
        private void windowMover_MouseDown(object sender, MouseEventArgs e)
        {
            isMove = true;
            fPt = new Point(e.X, e.Y);
        }

        private void windowMover_MouseUp(object sender, MouseEventArgs e)
        {
            isMove = false;
        }

        private void windowMover_MouseMove(object sender, MouseEventArgs e)
        {
            if (isMove && (e.Button & MouseButtons.Left) == MouseButtons.Left)
                Location = new Point(this.Left - (fPt.X - e.X), this.Top - (fPt.Y - e.Y));
        }


        public static void Show(string message)
        {
            // 1. 메인 폼이 존재하는지 안전하게 확인
            Form mainForm = (Application.OpenForms.Count > 0) ? Application.OpenForms[0] : null;

            // 폼이 없거나 닫히는 중이면 실행하지 않음
            if (mainForm == null || mainForm.IsDisposed || mainForm.Disposing) return;

            // 2. 현재 스레드가 UI 스레드가 아니라면 Invoke를 통해 UI 스레드로 넘김
            if (mainForm.InvokeRequired)
            {
                // BeginInvoke는 비동기로 UI 스레드에 작업을 던집니다 (호출한 스레드는 대기하지 않음)
                mainForm.BeginInvoke(new Action(() => Show(message)));
                return;
            }

            // --- UI 스레드 진입 성공 ---

            // 3. (옵션) 알림이 자동으로 사라지지 않게 설정 (라이브러리에 따라 다름)
            // 예: DevExpress AlertControl인 경우
            // _alert.AutoFormDelay = 0; // 0이면 닫기 버튼 누를 때까지 유지됨

            // 알림 표시
            _alert.Show(mainForm, "알림", message);
        }


        #region 키보드 후킹 ####################################################


        static ArrayList arrayList;
        static int _ctlVCnt = 0; // 붙여 넣기를 누른 횟수

        static TableListPop TABLE_LIST_POP = null;
        static Keys Pre_Keys;

        private async void OnGlobalKeyDown(object sender, KeyEventArgs e)
        {
            Console.WriteLine($"### pre:{Pre_Keys}, cur:{e.KeyCode}");
            
            // 복사
            if (e.Control && e.KeyCode == Keys.C)
            {
                await Task.Delay(300);

                string text = Clipboard.GetText();

                if (!string.IsNullOrEmpty(text))
                {
                    if (SystemInfoContext.Current["CLIPBOARD_SEQUENCE"].Equals("1"))
                    {
                        arrayList.Add(SystemInfoContext.Current["CLIPBOARD_TOUPPER"].Equals("1") ? text.ToUpper() : text);
                    }
                    else if (SystemInfoContext.Current["CLIPBOARD_MANY_TEXT"].Equals("1"))
                    {
                        if (Clipboard.GetText().Length >= CHUNK_SIZE)
                        {
                            //Task.Delay(300).ContinueWith(_ =>
                            //{
                            //    this.Invoke(new Action(() => ProcessClipboard()));
                            //});
                            if (this.IsDisposed || !this.IsHandleCreated) return;

                            ProcessClipboard();
                        }
                        else
                        {
                            Show($"{text.Length}자를 복사했습니다.");
                        }
                    }
                }
            }
            // 붙여넣기
            else if (e.Control && e.KeyCode == Keys.V)
            {
                if (SystemInfoContext.Current["CLIPBOARD_TOUPPER"].Equals("1"))
                {
                    e.Handled = true;

                    string text = Clipboard.GetText();
                    // 텍스트인 경우만
                    if (IsClipboardText)
                    {
                        Clipboard.SetData(DataFormats.Text, text.ToUpper());
                    }
                }
                // 순차 붙여넣기
                else if (SystemInfoContext.Current["CLIPBOARD_SEQUENCE"].Equals("1"))
                {
                    e.Handled = true;
                    Clipboard.SetData(DataFormats.Text, arrayList[_ctlVCnt++].ToString());

                    // 붙여넣기가 복사한 갯수보다 많으면 index 초기화(처음부터 다시 붙여넣는다.)
                    if (_ctlVCnt >= arrayList.Count)
                    {
                        MessageBox.Show("마지막 붙여넣기.\n다시 붙여 넣으면 처음부터 시작.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        _ctlVCnt = 0;
                    }
                }
                else if (SystemInfoContext.Current["CLIPBOARD_MANY_TEXT"].Equals("1"))
                {
                    if(e.KeyCode == Keys.Escape)
                    {
                        // 작업 중이라면 취소 요청
                        if (isProcessing)
                        {
                            Show("작업이 취소되었습니다.");
                            e.Handled = true;
                            return;
                        }
                    }

                    if(isProcessing)
                    {
                        e.Handled = false;
                        //Show("아직 복사 작업이 진행중입니다.");
                        return;
                    }
                    if (chunks.Count > 0)
                    {
                        e.Handled = true;

                        //Task.Run(() => StartPasting());
                        await Task.Delay(300);
                        StartPasting();
                    }
                    else
                    {
                        Clipboard.SetData(DataFormats.Text, Clipboard.GetText());
                    }
                }
            }
            else if ((Pre_Keys == Keys.LWin || Pre_Keys == Keys.RWin) && e.KeyCode == Keys.F4)
            {
                string dbms, db, tableName;

                dbms = SystemInfoContext.Current["DBMS"];
                db = SystemInfoContext.Current["DB"];

                if (string.IsNullOrEmpty(dbms))
                {
                    MessageBox.Show("DB Connection 정보가 설정되지 않았습니다.\n메인화면에서 설정하세요.");

                    return;
                }

                tableName = Clipboard.GetText().ToUpper().Trim();

                string[] tableNames = GetTableArray(tableName);
                string msg = "";

                SplashFormProperties splash = new SplashFormProperties();
                SplashScreenManager WaitProgress = new SplashScreenManager(typeof(global::DevTools.WaitProgress), splash);
                WaitProgress.ShowWaitForm();
                WaitProgress.SetWaitFormDescription($"{tableName} 테이블 정보를 조회중입니다.");
                
                try
                {
                    foreach (string tableNm in tableNames)
                    {
                        DataSet dsTableInfo = DBUtil.GetTableInfo(dbms, db, tableNm);

                        if (dsTableInfo == null || dsTableInfo.Tables.Count == 0 || dsTableInfo.Tables[0].Rows.Count == 0)
                            goto EndProcess;

                        if (dsTableInfo.Tables.Count == 1)
                        {
                            //MessageBox.Show($"{tableName} 테이블은 {SystemInfoContext.Current["USER"]}@{db} DB에 없는 테이블입니다.\n현재 접속 정보를 확인하세요.", "존재하지 않는 테이블", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            //goto EndProcess;
                            msg += $"{tableNm} 테이블은 {SystemInfoContext.Current["USER"]}@{db} DB에 없는 테이블입니다.\n현재 접속 정보를 확인하세요.\n";
                            continue;
                        }

                        string txtTableSpace = "";
                        var tbInfo = dsTableInfo.Tables[1].Rows[0]["TABLESPACE_NAME"];

                        if (tbInfo != null)
                            txtTableSpace = dsTableInfo.Tables[1].Rows[0]["TABLESPACE_NAME"].ToString();

                        string txtTableDesc = dsTableInfo.Tables[1].Rows[0]["COMMENTS"].ToString();

                        if (dsTableInfo == null || dsTableInfo.Tables == null || dsTableInfo.Tables.Count == 0)
                        {
                            msg += $"{tableNm} 테이블이 존재하지 않습니다.\n";

                            continue;
                        }

                        if (TABLE_LIST_POP == null)
                        {
                            TABLE_LIST_POP = new TableListPop();
                            TABLE_LIST_POP.Show();
                        }

                        if (TABLE_LIST_POP.IsDockPanel(tableNm))
                        {
                            //TABLE_LIST_POP.SetActivePanel(tableName);
                            //TABLE_LIST_POP.Activate();
                            //return;

                            continue;
                        }

                        TableInfoMng pop = new TableInfoMng(dsTableInfo.Tables[0], txtTableSpace, tableNm, txtTableDesc);

                        if (TABLE_LIST_POP.AddTable(pop, tableNm) == false)
                        {
                            TABLE_LIST_POP = new TableListPop();
                            TABLE_LIST_POP.Show();

                            TABLE_LIST_POP.AddTable(pop, tableNm);
                        }
                    }

                    if(!string.IsNullOrEmpty(msg))
                        MessageBox.Show(msg, "알림", MessageBoxButtons.OK, MessageBoxIcon.Error);

                    //pop.ShowPopup(pop, false, $"{tableName} 테이블 정보", true, false);
                }
                finally
                {
                    if(TABLE_LIST_POP != null)
                        TABLE_LIST_POP.Activate();

                    WaitProgress.CloseWaitForm();
                }
            }
        EndProcess:

            Pre_Keys = e.KeyCode;
        }

        static string[] GetTableArray(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return new string[0];

            return input.Split(',') // 1. 쉼표로 먼저 나눔
                        .Select(item => item.Trim()) // 앞뒤 공백 제거
                        .Where(item => !string.IsNullOrEmpty(item)) // 빈 항목 제거
                        .Select(item =>
                        {
                            // 2. 공백을 기준으로 다시 잘라서 '첫 번째 덩어리'만 가져옴
                            // 예: "TB_USER a" -> ["TB_USER", "a"] -> "TB_USER"
                            // 예: "TB_DEPT AS b" -> ["TB_DEPT", "AS", "b"] -> "TB_DEPT"
                            var parts = item.Split(new char[] { ' ', '\t', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                            return parts.Length > 0 ? parts[0] : null;
                        })
                        .Where(t => t != null)
                        .ToArray();
        }

        private void ProcessClipboard()
        {
            if (!Clipboard.ContainsText()) return;

            string text = Clipboard.GetText();

            copiedText = text;
            chunks.Clear();

            string txt = "";

            for (int i = 0; i < text.Length; i += CHUNK_SIZE)
            {
                int length = Math.Min(CHUNK_SIZE, text.Length - i);
                txt = text.Substring(i, length);
                //Debug.WriteLine($"$$$$$$$$ {txt}");
                chunks.Add(txt);
            }

            Show($"{text.Length}자를 {chunks.Count}개로 분할했습니다. Ctrl+V로 붙여넣기를 시작하세요.");
        }

        private void StartPasting()
        {
            if (isProcessing || chunks.Count == 0) return;

            isProcessing = true;

            Show($"{chunks.Count}개 청크를 순차적으로 붙여넣습니다.");

            for (int i = 0; i < chunks.Count; i++)
            {
                try
                {
                    // 1. 클립보드 완전히 비우기
                    this.Invoke(new Action(() =>
                    {
                        Clipboard.Clear();
                    }));
                    Thread.Sleep(100);

                    // 2. 새 청크 복사 (재시도 로직 포함)
                    bool success = false;
                    for (int retry = 0; retry < 5; retry++)
                    {
                        this.Invoke(new Action(() =>
                        {
                            try
                            {
                                Clipboard.SetText(chunks[i]);
                                success = true;
                            }
                            catch { }
                        }));

                        if (success)
                        {
                            Thread.Sleep(100);

                            // 3. 실제로 클립보드에 올바르게 들어갔는지 확인
                            string verify = "";
                            this.Invoke(new Action(() =>
                            {
                                verify = Clipboard.GetText();
                            }));

                            if (verify == chunks[i])
                            {
                                //System.Diagnostics.Debug.WriteLine($"[{i + 1}] 클립보드 검증 성공: {chunks[i].Substring(0, Math.Min(50, chunks[i].Length))}...");
                                break;
                            }
                        }

                        Thread.Sleep(100);
                    }

                    if (!success)
                    {
                        throw new Exception($"청크 {i + 1} 클립보드 복사 실패");
                    }

                    Thread.Sleep(300);

                    // 4. Ctrl+V 입력
                    SendCtrlV();
                    //Thread.Sleep(800); // 대기 시간 증가
                    Thread.Sleep(500); // 대기 시간 증가

                    if ((i + 1) % 10 == 0)
                    {
                        int index = i;
                        Show($"진행 중: {index + 1}/{chunks.Count}");
                    }
                }
                catch (Exception ex)
                {
                    Show($"붙여넣기 중 오류 ({i + 1}번째): {ex.Message}");
                    break;
                }
            }

            Show("모든 텍스트를 붙여넣었습니다.");

            isProcessing = false;
            chunks.Clear();
        }

        private void SetClipboardText(string text)
        {
            for (int retry = 0; retry < 3; retry++)
            {
                try
                {
                    Clipboard.SetText(text);
                    return;
                }
                catch
                {
                    Thread.Sleep(100);
                }
            }
        }

        private void SendCtrlV()
        {
            keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
            Thread.Sleep(50);

            keybd_event(VK_V, 0, 0, UIntPtr.Zero);
            Thread.Sleep(50);

            keybd_event(VK_V, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            Thread.Sleep(50);

            keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            keyboardHook.Unhook();
            base.OnFormClosing(e);
        }

        /// <summary>
        /// 클립보드에 있는 내용이 텍스트인지 확인
        /// </summary>
        static bool IsClipboardText
        {
            get
            {
                return !Clipboard.ContainsAudio() && !Clipboard.ContainsFileDropList() && !Clipboard.ContainsImage();
            }
        }

        static bool IsChangeClipboardText = false;



        #endregion 키보드 후킹 ####################################################

        private void pictureEdit2_Click(object sender, EventArgs e)
        {
            popupMenu1.ShowPopup(this.Location);
        }

        /// <summary>
        /// 클립보드 대문자 설정
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void togUpper_CheckedChanged(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            if (togUpper.Checked)
            {
                SystemInfoContext.Current["CLIPBOARD_TOUPPER"] = "1";
            }
            else
            {
                SystemInfoContext.Current["CLIPBOARD_TOUPPER"] = "0";
            }

            SetStatus();
        }

        /// <summary>
        /// 클립보드 순차 실행
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void togSeq_CheckedChanged(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            if (togSeq.Checked)
            {
                togManyText.Checked = false;
                SystemInfoContext.Current["CLIPBOARD_SEQUENCE"] = "1";
            }
            else
            {
                SystemInfoContext.Current["CLIPBOARD_SEQUENCE"] = "0";
            }

            SetStatus();
        }

        private void togManyText_CheckedChanged(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            if (togManyText.Checked)
            {
                togSeq.Checked = false;
                SystemInfoContext.Current["CLIPBOARD_MANY_TEXT"] = "1";
            }
            else
            {
                SystemInfoContext.Current["CLIPBOARD_MANY_TEXT"] = "0";
            }

            SetStatus();
        }

        /// <summary>
        /// 환경설정 상태 텍스트 표시
        /// </summary>
        void SetStatus()
        {
            string status = "";

            if (togUpper.Checked)
                status = "대문자";

            if (togSeq.Checked)
                status = string.IsNullOrEmpty(status) ? "순차" : status + ", 순차";

            if (togManyText.Checked)
                status = "대용량 순차 붙여넣기";

            if (string.IsNullOrEmpty(status))
                status = "활성 옵션 없음.";

            lblStatus.Text = status;
        }

        /// <summary>
        /// 메뉴 닫기
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void barClose_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            popupMenu1.HidePopup();
        }


        #region 화면 캡쳐

        Form FORM_CAP;
        Form form1;
        Point MD = Point.Empty;
        Rectangle RECT1 = Rectangle.Empty;
        Rectangle RECT2 = Rectangle.Empty;
        private const int PEN_WIDTH = 1;
        private const LineCap START_CAP = LineCap.ArrowAnchor;
        private const LineCap END_CAP = LineCap.ArrowAnchor;
        Point mAnchorPoint = new Point(10, 10);
        Point mPreviousPoint = Point.Empty;
        Point mPreviousPoint_rec = Point.Empty;
        /// <summary>
        /// 스크린 캡쳐
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void barBtnCapture_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            CaptureToClipboard();
        }

        /// <summary>
        /// 선택 영역을 클립보드로 캡쳐하기
        /// </summary>
        void CaptureToClipboard()
        {
            this.Hide();

            // 캡쳐가 진행중이라는 의미로 흰 투명 배경의 폼을 깔아준다.
            form1 = new Form();
            form1.BackColor = Color.White;
            form1.Opacity = 0.3;
            form1.ControlBox = false;
            form1.MaximizeBox = false;
            form1.MinimizeBox = false;
            form1.FormBorderStyle = FormBorderStyle.None;

            // 폼이 최대화 상태라면 일반 상태로 변경
            if (form1.WindowState == FormWindowState.Maximized)
            {
                form1.WindowState = FormWindowState.Normal;
            }
            // FormBorderStyle을 None으로 설정해야 경계선 없이 확장 가능
            form1.FormBorderStyle = FormBorderStyle.None;

            // 모든 화면 영역 계산
            Rectangle allScreens = GetAllScreensBounds();

            // 폼 위치와 크기 설정
            form1.Location = new Point(allScreens.X, allScreens.Y);
            form1.Size = new System.Drawing.Size(allScreens.Width, allScreens.Height);

            form1.Show();

            // 캡쳐 선택 영역을 지정하고 실제 캡쳐를 진행하는 폼
            FORM_CAP = new Form();
            FORM_CAP.BackColor = Color.Wheat;
            FORM_CAP.TransparencyKey = FORM_CAP.BackColor;
            FORM_CAP.ControlBox = false;
            FORM_CAP.MaximizeBox = false;
            FORM_CAP.MinimizeBox = false;
            FORM_CAP.FormBorderStyle = FormBorderStyle.None;
            //FORM_CAP.WindowState = FormWindowState.Maximized;
            FORM_CAP.KeyDown += Form2_KeyDown;
            FORM_CAP.MouseDown += form2_MouseDown;
            FORM_CAP.MouseMove += form2_MouseMove;
            FORM_CAP.MouseUp += form2_MouseUp;
            FORM_CAP.Cursor = Cursors.Cross;


            // 폼이 최대화 상태라면 일반 상태로 변경
            if (FORM_CAP.WindowState == FormWindowState.Maximized)
            {
                FORM_CAP.WindowState = FormWindowState.Normal;
            }
            // FormBorderStyle을 None으로 설정해야 경계선 없이 확장 가능
            FORM_CAP.FormBorderStyle = FormBorderStyle.None;
            
            // 폼 위치와 크기 설정
            FORM_CAP.Location = new Point(allScreens.X, allScreens.Y);
            FORM_CAP.Size = new System.Drawing.Size(allScreens.Width, allScreens.Height);
            // 최상위로 표시
            FORM_CAP.TopMost = true;

            FORM_CAP.Show();
        }

        // 모든 모니터를 포함하는 전체 영역 계산
        private Rectangle GetAllScreensBounds()
        {
            // 시스템의 가상 화면 전체를 가져옵니다 (모든 모니터 포함)
            Rectangle virtualScreen = SystemInformation.VirtualScreen;

            //// 디버깅을 위한 로그
            //Debug.WriteLine($"Virtual Screen: X={virtualScreen.X}, Y={virtualScreen.Y}, Width={virtualScreen.Width}, Height={virtualScreen.Height}");

            //// 모든 개별 스크린에 대한 정보도 출력
            //foreach (Screen screen in Screen.AllScreens)
            //{
            //    Debug.WriteLine($"Screen {screen.DeviceName}: Bounds={screen.Bounds}, Primary={screen.Primary}");
            //}

            return virtualScreen;
        }

        private void Form2_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                form1.Close();

                FORM_CAP.Close();
                FORM_CAP.Cursor = Cursors.Default;
                this.Show();
            }
        }

        void form2_MouseDown(object sender, MouseEventArgs e)
        {
            MD = e.Location;
        }


        void form2_MouseMove(object sender, MouseEventArgs e)
        {
            Point MM = e.Location;

            using (Graphics g = FORM_CAP.CreateGraphics())
            {
                // 이전 선 지우기
                using (Pen clear_pen = new Pen(FORM_CAP.BackColor, PEN_WIDTH))
                {
                    clear_pen.StartCap = START_CAP;
                    clear_pen.EndCap = END_CAP;
                    
                    // 수직선
                    g.DrawLine(clear_pen, mPreviousPoint.X, 0, mPreviousPoint.X, Screen.PrimaryScreen.WorkingArea.Height);
                    // 수평선
                    g.DrawLine(clear_pen, 0, mPreviousPoint.Y, Screen.PrimaryScreen.WorkingArea.Width, mPreviousPoint.Y);
                }

                // 이전 위치 저장
                mPreviousPoint = e.Location;

                // 선 그리기
                using (Pen draw_pen = new Pen(Color.Red, PEN_WIDTH))
                {
                    draw_pen.StartCap = START_CAP;
                    draw_pen.EndCap = END_CAP;
                    // 수직선
                    g.DrawLine(draw_pen, MM.X, 0, MM.X, Screen.PrimaryScreen.WorkingArea.Height);
                    // 수평선
                    g.DrawLine(draw_pen, 0, MM.Y, Screen.PrimaryScreen.WorkingArea.Width, MM.Y);
                }                
            }

            if (e.Button != MouseButtons.Left)
                return;

            // 선택 사격형 그리기
            using (Graphics g = FORM_CAP.CreateGraphics())
            {
                using (Pen clear_pen = new Pen(FORM_CAP.BackColor, PEN_WIDTH))
                {
                    RECT1 = new Rectangle(Math.Min(MD.X, MM.X), Math.Min(MD.Y, MM.Y), Math.Abs(MD.X - MM.X), Math.Abs(MD.Y - MM.Y));
                    g.DrawRectangle(clear_pen, RECT1);
                }

                mPreviousPoint_rec = e.Location;

                using (Pen draw_pen = new Pen(Color.Red, PEN_WIDTH))
                {
                    RECT2 = new Rectangle(Math.Min(MD.X, MM.X), Math.Min(MD.Y, MM.Y), Math.Abs(MD.X - MM.X), Math.Abs(MD.Y - MM.Y));
                    g.DrawRectangle(draw_pen, RECT2);
                }
            }

            //Region rgn = new Region(new Rectangle(0, 0, Screen.PrimaryScreen.WorkingArea.Width, Screen.PrimaryScreen.WorkingArea.Height));
            //GraphicsPath path = new GraphicsPath();
            //path.AddRectangle(RECT1);
            //rgn.Exclude(path);
            //Graphics graphics = FORM_CAP.CreateGraphics();
            //graphics.FillRegion(Brushes.Transparent, rgn);
        }
        
        void form2_MouseUp(object sender, MouseEventArgs e)
        {
            form1.Hide();
            FORM_CAP.Hide();
            Screen scr = Screen.AllScreens[0];
            Bitmap bmp = new Bitmap(RECT1.Width, RECT1.Height);
            using (Graphics G = Graphics.FromImage(bmp))
            {
                G.CopyFromScreen(RECT1.Location, Point.Empty, RECT1.Size, CopyPixelOperation.SourceCopy);
                //pictureBox1.Image = bmp;
                Clipboard.SetImage(bmp);
            }
            form1.Close();
            FORM_CAP.Close();
            FORM_CAP.Cursor = Cursors.Default;
            RECT1 = Rectangle.Empty;
            this.Show();

            if(IsActivityCapture)
            {
                Image clipboardImage = Clipboard.GetImage();
                if (clipboardImage == null)
                {
                    return;
                }

                // Bitmap으로 변환
                Bitmap bitmap = new Bitmap(clipboardImage);

                string datapath = @"d:\tessdata\".Trim();
                if (datapath.EndsWith("\\", StringComparison.Ordinal) || datapath.EndsWith("/", StringComparison.Ordinal))
                {
                    datapath = datapath.Substring(0, datapath.Length - 1);
                }

                //using (var engine = new TesseractEngine("./tessdata", "eng", EngineMode.Default))

                //ShowActivity();
            }

            IsActivityCapture = false;
        }

        #endregion 화면 캡쳐


        #region Activity, Block 정보 보여주기

        bool IsActivityCapture = false;

        /// <summary>
        /// 캡쳐를 통해 정보 보여주기
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void barActCapcure_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            IsActivityCapture = true;

            CaptureToClipboard();
        }

        /// <summary>
        /// 직접 입력을 통해 정보 보여주기
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void barActRun_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            ShowActivity();
        }

        void ShowActivity()
        {
            ActivityInfo actInfo = new ActivityInfo();

            Form form = new Form();
            form.StartPosition = FormStartPosition.CenterScreen;
            form.Width = actInfo.Width;
            form.Height = actInfo.Height;
            form.Controls.Add(actInfo);
            form.Show();
        }
        #endregion
    }





    public class GlobalKeyboardHook
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private LowLevelKeyboardProc proc;
        private IntPtr hookID = IntPtr.Zero;

        public event EventHandler<KeyEventArgs> KeyDown;

        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll")]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        public void Hook()
        {
            proc = HookCallback;
            using (var curProcess = System.Diagnostics.Process.GetCurrentProcess())
            using (var curModule = curProcess.MainModule)
            {
                hookID = SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        public void Unhook()
        {
            UnhookWindowsHookEx(hookID);
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                bool ctrlPressed = (GetAsyncKeyState(0x11) & 0x8000) != 0;

                var e = new KeyEventArgs((Keys)vkCode | (ctrlPressed ? Keys.Control : Keys.None));
                KeyDown?.Invoke(this, e);

                if (e.Handled)
                    return (IntPtr)1;
            }

            return CallNextHookEx(hookID, nCode, wParam, lParam);
        }
    }
}
