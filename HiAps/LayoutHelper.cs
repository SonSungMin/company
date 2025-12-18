using Amisys.Framework.Infrastructure.DataModels;
using Amisys.Framework.Infrastructure.Interfaces;
using Amisys.Framework.Infrastructure.Utility;
using DevExpress.Xpf.Docking;
using Microsoft.Practices.Prism.Regions;
using Microsoft.Practices.ServiceLocation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Amisys.Framework.Presentation.Utility
{
    public class LayoutHelper
    {
        public static IRegionManager RegionManager { get; set; }
        public static DockLayoutManager DockManager { get; set; }

        private static string moduleName = "AmiMainShell";

        private static ILogService logger
        {
            get
            {
                return ServiceLocator.Current.GetInstance<ILogService>();
            }
        }

        public static bool SaveDockLayoutAndViewList(AmiViewList workspace)
        {
            if (workspace != null && DockManager != null && RegionManager != null)
            {
                string tmpWorkspaceName = EnsureProperWorkspaceName(workspace);
                return (SaveDockLayout(tmpWorkspaceName) && SaveViewList(tmpWorkspaceName));
            }
            return false;
        }

        private static bool SaveDockLayout(string workspaceName)
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
                SetLayoutViewList(DockManager);

                DockManager.SaveLayoutToXml(filename);
            }
            catch (Exception ex)
            {
                string msg = string.Format("[{0}]Layout을 저장할 수 없습니다.", workspaceName);
                logger.Write(msg);
                logger.Write(ex.ToString());

                return false;
            }

            return true;
        }

        private static bool DeleteDockLayout(string workspaceName)
        {
            string path;
            string filename;
            GetSafeWorkspaceLayoutFilename(workspaceName, out path, out filename);

            try
            {
                AmiFileUtility.ForceDeleteFile(filename);

                return true;
            }
            catch (Exception ex)
            {
                string msg = string.Format("[{0}]Layout을 저장할 수 없습니다.", workspaceName);
                logger.Write(msg);
                logger.Write(ex.ToString());
            }
            return false;
        }


        public static void SetLayoutViewList(DockLayoutManager dockManager)
        {
            foreach (var item in dockManager.LayoutRoot.Items)
            {

            }
        }

        public static void GetSafeWorkspaceLayoutFilename(string workspaceName, out string path, out string filename)
        {
            path = string.Format("{0}\\Layout\\", AmiFileUtility.GetApplicationDirectory());
            filename = string.Format("{0}{1}.xml", path, AmiFileUtility.MakeSafeFilename(workspaceName));
        }


        public static bool SaveViewList(string workspaceName)
        {
            List<ModuleViewId> viewList;
            ModuleHelper.GetActiveModuleAndViewList(out viewList);

            try
            {
                AmiWorkspaceHelper.Save(workspaceName, viewList);
            }
            catch (Exception ex)
            {
                string msg = string.Format("[{0}]Layout을 저장할 수 없습니다.", workspaceName);
                logger.Write(msg);
                logger.Write(ex.ToString());
                return false;
            }

            return true;
        }

        public static bool DeleteViewList(string workspaceName)
        {
            try
            {
                AmiWorkspaceHelper.Delete(workspaceName);

                return true;
            }
            catch (Exception ex)
            {
                string msg = string.Format("[{0}]View 목록을 삭제할 수 없습니다.", workspaceName);
                logger.Write(msg);
                logger.Write(ex.ToString());
            }
            return false;
        }

        public static bool LoadViewList(string workspaceName, out List<ModuleViewId> moduleViews)
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

        public static bool LoadDockLayout(AmiViewList workspace)
        {
            if (workspace != null)
            {
                string tmpWorkspaceName = EnsureProperWorkspaceName(workspace);
                string path;
                string filename;
                GetSafeWorkspaceLayoutFilename(tmpWorkspaceName, out path, out filename);

                if (DockManager != null)
                {
                    try
                    {
                        if (AmiFileUtility.IsExistFile(filename))
                        {
                            // restore layout
                            AmiFileUtility.ReleseLockedFile(filename);
                            DockManager.RestoreLayoutFromXml(filename);

                            return true;
                        }
                    }
                    catch (Exception)
                    {
                    }
                }
            }

            return false;
        }

        private static string EnsureProperWorkspaceName(AmiViewList workspace)
        {
            if (workspace == null)
            {
                return moduleName;
            }
            else
            {
                return string.Format("{0}",  workspace.GetWorkSpaceName());
            }
        }

        public static bool DeleteDockLayoutAndViewList(AmiViewList workspace)
        {
            if (workspace != null)
            {
                string workspaceName = EnsureProperWorkspaceName(workspace);
                return (DeleteDockLayout(workspaceName) && DeleteViewList(workspaceName));
            }
            return false;
        }
    }
}          
