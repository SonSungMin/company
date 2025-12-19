using Amisys.Infrastructure.HHIInfrastructure.Defintions;
using Amisys.Framework.Infrastructure.Utility;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Amisys.Framework.Presentation.DataModel;
using Microsoft.Practices.EnterpriseLibrary.Logging.Configuration;
using Microsoft.Practices.EnterpriseLibrary.ExceptionHandling;
using Microsoft.Practices.EnterpriseLibrary.Common.Configuration;
using System.Windows.Threading;
using System.Reflection;
using System.IO;
using FirstFloor.ModernUI.Windows.Controls;

namespace NewMainShell
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        Mutex mutex = null;
        public static ArgList mArgs = new ArgList();

        private static bool IsLoginSuccess = true;

        protected override void OnStartup(StartupEventArgs e)
        {
            Assembly ass = this.GetType().Assembly;
            string path = System.IO.Path.GetDirectoryName(ass.Location);
            Directory.SetCurrentDirectory(path);


            if (e.Args.Count() == 13) //Hyspics에서 로그인
            {
                mArgs.AddItemFromHyspics(e.Args);
            }
            if (e.Args.Count() == 12) //Hyspics에서 로그인
            {
                // mArgs.AddItemFromHyspics(e.Args);
                if (e.Args[0] == "test")
                    mArgs.AddItemFromHyspics(e.Args);
                else
                    ModernDialog.ShowMessage("개발버전이 추가 되었습니다. 프로그램 삭제후 재설치 하세요", "Error", MessageBoxButton.OK);
            }
            else if (e.Args.Count() < 13)
            {
                mArgs.AddItemTemp(e.Args);

                if (mArgs.Count() < 6)
                {
                    MessageBox.Show("Input the Login Info: DomainCategory, UserId, UserName, UserGroupId, UserGroup, Authority(W,R).", "Error", MessageBoxButton.OK, MessageBoxImage.Information);
                    Application.Current.Shutdown();
                    return;
                }
            }

            if (IsLoginSuccess == false)
            {
                ModernDialog.ShowMessage("사용자 로그인에 실패했습니다.", "Error", MessageBoxButton.OK);
                Application.Current.Shutdown();
                return;
            }

            // startup application
            base.OnStartup(e);
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);

            if (e.Args.Length != 12)
                ShowLoginWindow();

            if (IsLoginSuccess == false)
            {
                MessageBox.Show("사용자 로그인에 실패했습니다.", "Error", MessageBoxButton.OK, MessageBoxImage.Stop);

                if (Application.Current != null)
                    Application.Current.Shutdown();
                
                return;
            }

            if (MainWindow != null && MainWindow.Visibility != Visibility.Visible)
            {
                MainWindow.Visibility = Visibility.Visible;
            }
            // check log folder
            CheckLoggingFolder();
        }

        [Conditional("RELEASE")]
        public void ShowLoginWindow()
        {
            //2013.06.11 도성민 hyspics로만 로그인 하도록...
            //시작
            MessageBox.Show("(신)조선통합 혹은 포탈로 로그인 하시기 바랍니다.", "Error", MessageBoxButton.OK, MessageBoxImage.Stop);

            if (Application.Current != null)
                Application.Current.Shutdown();

            return;
        }

        private void CheckLoggingFolder()
        {
            // App config file path.  
            string appPath = AppDomain.CurrentDomain.SetupInformation.ConfigurationFile;

            Configuration entLibConfig = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

            LoggingSettings loggingSettings = (LoggingSettings)entLibConfig.GetSection(LoggingSettings.SectionName);

            TraceListenerData traceListenerData = loggingSettings.TraceListeners.Get("Rolling Flat File Trace Listener");
            RollingFlatFileTraceListenerData objFlatFileTraceListenerData = traceListenerData as RollingFlatFileTraceListenerData;

            string filename = objFlatFileTraceListenerData.FileName;
            string path = AmiFileUtility.ExtraceDirectoryFromFilename(filename);
            AmiFileUtility.ForceCreateDirectory(path);

            traceListenerData = loggingSettings.TraceListeners.Get("Algorithm Log Listener");
            objFlatFileTraceListenerData = traceListenerData as RollingFlatFileTraceListenerData;

            filename = objFlatFileTraceListenerData.FileName;
            path = AmiFileUtility.ExtraceDirectoryFromFilename(filename);
            AmiFileUtility.ForceCreateDirectory(path);
        }

        /// <summary>
        /// 처리되지 않은 예외
        /// 특히, SEHException 의 경우
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception)
            {
                HandleException((Exception)e.ExceptionObject, "Unhandled");
            }
        }

        public static void HandleException(Exception ex, string policy)
        {
            Boolean rethrow = false;
            try
            {
                var exManager = EnterpriseLibraryContainer.Current.GetInstance<ExceptionManager>();
                rethrow = exManager.HandleException(ex, policy);
            }
            catch (Exception innerEx)
            {
                string errorMsg = "An unexpected exception occured while " +
                    "calling HandleException with policy '" + policy + "'. ";
                errorMsg += Environment.NewLine + innerEx.ToString();

                MessageBox.Show(errorMsg, "Application Error",
                    MessageBoxButton.OK, MessageBoxImage.Stop);

                throw ex;
            }

            if (rethrow)
            {
                // WARNING: This will truncate the stack of the exception
                //throw ex;
                MessageBox.Show(ex.ToString(), "알림", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
            {
                MessageBox.Show("An unhandled exception occurred and has been logged. Please contact support.");
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);
        }
    }
}
