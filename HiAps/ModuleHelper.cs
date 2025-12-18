using Amisys.Framework.Infrastructure;
using Amisys.Framework.Infrastructure.DataModels;
using Amisys.Framework.Infrastructure.RegionAdapters;
using Amisys.Framework.Infrastructure.Utility;
using Microsoft.Practices.Prism.Regions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Amisys.Framework.Presentation.Utility
{
    public static class ModuleHelper
    {
        public static IRegionManager RegionManager { get; set; }

        /// <summary>
        /// 현재 Workspace에 활성화된 Module:View 목록
        /// </summary>
        /// <param name="moduleNames"></param>
        /// <param name="viewNames"></param>
        public static void GetActiveModuleAndViewList(out List<string> moduleNames, out List<string> viewNames)
        {
            moduleNames = new List<string>();
            viewNames = new List<string>();

            if (RegionManager != null)
            {
                foreach (IPanelInfo view in RegionManager.Regions[MefRegionDefinition.MainRegion].ActiveViews)
                {
                    string moduleName = view.GetModuleName();
                    string viewName = AmiStringUtility.ExtractClassName(view.GetViewName());

                    moduleNames.Add(moduleName);
                    viewNames.Add(viewName);
                }
            }
        }


        /// <summary>
        /// 현재 Workspace에 활성화된 Module:View 목록
        /// </summary>
        public static void GetActiveModuleAndViewList(out List<ModuleViewId> moduleViews)
        {
            moduleViews = new List<ModuleViewId>();

            if (RegionManager != null)
            {
                foreach (IPanelInfo view in RegionManager.Regions[MefRegionDefinition.MainRegion].ActiveViews)
                {
                    var moduleView = new ModuleViewId()
                    {
                        ModuleName = view.GetModuleName(),
                        ViewName = view.GetViewName(),
                        NodeName = view.GetPanelName(),
                        Id = view.GetId()
                    };
                    moduleViews.Add(moduleView);
                }
            }
        }

        /// <summary>
        /// 지정한 모듈/뷰이름의 활성화된 뷰를 찾아서 리턴
        /// 동일 이름이 여러개일 경우 첫번째 뷰 리턴
        /// </summary>
        /// <param name="moduleName"></param>
        /// <param name="viewName"></param>
        /// <returns></returns>
        public static object GetActiveModuleAndView(string moduleName, string viewName)
        {
            if (RegionManager != null)
            {
                foreach (IPanelInfo view in RegionManager.Regions[MefRegionDefinition.MainRegion].ActiveViews)
                {
                    string actModuleName = view.GetModuleName();
                    string actViewName = AmiStringUtility.ExtractClassName(view.GetViewName());

                    if (moduleName == actModuleName && viewName == actViewName)
                        return view;
                }
            }
            return null;
        }

        /// <summary>
        /// 현재 Workspace에서 활성화된 모듈 목록
        /// </summary>
        /// <returns></returns>
        public static List<string> GetActiveModuleList()
        {
            List<string> modules = new List<string>();

            if (RegionManager != null)
            {
                foreach (IPanelInfo view in RegionManager.Regions[MefRegionDefinition.MainRegion].ActiveViews)
                {
                    modules.Add(view.GetPanelName());
                }
            }

            return modules;
        }

        /// <summary>
        /// 현재 Workspace에서 지정한 Module:View의 개수
        /// </summary>
        /// <param name="moduleName"></param>
        /// <param name="viewName"></param>
        /// <returns></returns>
        public static int GetActiveViewCount(string moduleName, string viewName)
        {
            int cnt = 0;

            if (RegionManager != null)
            {
                foreach (IPanelInfo view in RegionManager.Regions[MefRegionDefinition.MainRegion].ActiveViews)
                {
                    if (view.GetModuleName() == moduleName && view.GetViewName() == viewName)
                    {
                        cnt++;
                    }
                }
            }

            return cnt;
        }
    }
}


















              
