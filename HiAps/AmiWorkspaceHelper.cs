using Amisys.Framework.Infrastructure.DataModels;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Amisys.Framework.Infrastructure.Utility
{
    public class AmiWorkspaceHelper
    {
        public static string xmlPath = AmiFileUtility.GetAppBasedAbsolutePath(ConfigurationManager.AppSettings["AppViewListFilename"]);
        public static string root = "WorkspaceViewList";
        public static string viewElement = "View";
        public static string moduleAttrName = "ModuleName";
        public static string viewAttrName = "ViewName";
        public static string nodeAttrName = "NodeName";
        public static string idAttrName = "Id";
        public AmiWorkspaceHelper() => AmiWorkspaceHelper.xmlPath = this.InitializeXmlPath();

        private string InitializeXmlPath()
        {
            return AmiFileUtility.GetAppBasedAbsolutePath(ConfigurationManager.AppSettings["AppViewListFilename"]);
        }

        private static XDocument LoadXmlDocument(string xmlpath, bool createNew)
        {
            try
            {
                XDocument xdocument;
                try
                {
                    xdocument = XDocument.Load(xmlpath);
                }
                catch (FileNotFoundException ex)
                {
                    xdocument = new XDocument();
                    XElement content = new XElement((XName)AmiWorkspaceHelper.root);
                    xdocument.Add((object)content);
                }
                return xdocument;
            }
            catch (Exception ex)
            {
            }
            return (XDocument)null;
        }
        public static void Save(string workspaceName, List<ModuleViewId> moduleViewList)
        {
            try
            {
                XDocument xdocument = AmiWorkspaceHelper.LoadXmlDocument(AmiWorkspaceHelper.xmlPath, true);
                XElement xelement = xdocument.Element((XName)AmiWorkspaceHelper.root);
                string safeElementName = AmiWorkspaceHelper.GetSafeElementName(workspaceName);
                xelement.Element((XName)safeElementName)?.Remove();
                XElement content1 = new XElement((XName)safeElementName);
                xelement.Add((object)content1);

                for (int index = 0; index < moduleViewList.Count; ++index)
                {
                    string moduleName = moduleViewList[index].ModuleName;
                    string viewName = moduleViewList[index].ViewName;
                    string nodeName = moduleViewList[index].NodeName;
                    int id = moduleViewList[index].Id;
                    XElement content2 = new XElement((XName)AmiWorkspaceHelper.viewElement);
                    content2.Add((object)new XAttribute((XName)AmiWorkspaceHelper.moduleAttrName, (object)moduleName));
                    content2.Add((object)new XAttribute((XName)AmiWorkspaceHelper.viewAttrName, (object)viewName));
                    content2.Add((object)new XAttribute((XName)AmiWorkspaceHelper.nodeAttrName, (object)nodeName));
                    content2.Add((object)new XAttribute((XName)AmiWorkspaceHelper.idAttrName, (object)id));
                    content1.Add((object)content2);
                }
                xdocument.Save(AmiWorkspaceHelper.xmlPath);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public static void Load(string workspaceName, List<ModuleViewId> moduleViewList)
        {
            try
            {
                XDocument xdocument = AmiWorkspaceHelper.LoadXmlDocument(AmiWorkspaceHelper.xmlPath, false);
                if (xdocument == null)
                    return;

                XElement xelement = xdocument.Element((XName)AmiWorkspaceHelper.root).Element((XName)AmiWorkspaceHelper.GetSafeElementName(workspaceName));
                if (xelement == null)
                    return;

                foreach (XElement element in xelement.Elements())
                {
                    if (element.Name == (XName)AmiWorkspaceHelper.viewElement)
                    {
                        XAttribute xattribute1 = element.Attribute((XName)AmiWorkspaceHelper.moduleAttrName);
                        XAttribute xattribute2 = element.Attribute((XName)AmiWorkspaceHelper.viewAttrName);
                        XAttribute xattribute3 = element.Attribute((XName)AmiWorkspaceHelper.nodeAttrName);
                        XAttribute xattribute4 = element.Attribute((XName)AmiWorkspaceHelper.idAttrName);

                        int result = 0;
                        int.TryParse(xattribute4.Value, out result);

                        string rawViewName = xattribute2 != null ? xattribute2.Value : string.Empty;
                        string resolvedViewName = rawViewName;

                        if (!string.IsNullOrEmpty(rawViewName) && !rawViewName.Contains(","))
                        {
                            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                            {
                                var type = assembly.GetTypes().FirstOrDefault(t => t.Name == rawViewName);
                                if (type != null)
                                {
                                    resolvedViewName = type.AssemblyQualifiedName;
                                    break;
                                }
                            }
                        }

                        ModuleViewId moduleViewId = new ModuleViewId()
                        {
                            ModuleName = xattribute1 != null ? xattribute1.Value : string.Empty,
                            ViewName = resolvedViewName,
                            NodeName = xattribute3 != null ? xattribute3.Value : string.Empty,
                            Id = xattribute4 != null ? result : -1
                        };

                        moduleViewList.Add(moduleViewId);
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public static void Delete(string workspace)
        {
            try
            {
                XDocument xdocument = AmiWorkspaceHelper.LoadXmlDocument(AmiWorkspaceHelper.xmlPath, true);
                XElement xelement = xdocument.Element((XName)AmiWorkspaceHelper.root).Element((XName)AmiWorkspaceHelper.GetSafeElementName(workspace));
                if (xelement == null)
                    return;
                xelement.Remove();
                xdocument.Save(AmiWorkspaceHelper.xmlPath);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private static string GetSafeElementName(string name)
        {
            return Regex.Replace(Regex.Replace(name, "[#{}&()]+(?=[^<>]*>)", ""), "\\s*", "");
        }
    }
}


