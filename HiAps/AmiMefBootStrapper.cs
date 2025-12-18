using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Hosting;
using System.Linq;
using System.Text;
using System.Windows;
using Microsoft.Practices.Prism.MefExtensions;
using Microsoft.Practices.Prism.Modularity;
using Amisys.Framework.Presentation.AmiMainShell.Views;
using Amisys.Framework.Infrastructure.PrismSupps;
using Microsoft.Practices.Prism.Regions;
using DevExpress.Xpf.Docking;
using Amisys.Framework.Infrastructure.RegionAdapters;
using Amisys.Framework.Infrastructure;
using Microsoft.Practices.Composite.Events;
using System.ComponentModel.Composition;
using System.Reflection; 
using System.IO;
using System.Windows.Controls;
using Amisys.Service.HHIBasicPlanDataService;
using System.Windows.Controls.Primitives;
using Amisys.Infrastructure.HHIInfrastructure.Interfaces;

namespace Amisys.Framework.Presentation.AmiMainShell
{
    public class AmiMefBootStrapper : MefBootstrapper
    {
        public Window MainWindow { get; set; }
        
        public AggregateCatalog Catalog
        {
            get
            {
                return this.AggregateCatalog;
            }
        }

        protected override IModuleCatalog CreateModuleCatalog()
        {
            // When using MEF, the existing Prism ModuleCatalog is still the place to configure modules via configuration files.
            return new ConfigurationModuleCatalog();
        }

        protected override void ConfigureAggregateCatalog()
        {
            try
            {
                this.AggregateCatalog.Catalogs.Add(new AssemblyCatalog(typeof(AmiMefBootStrapper).Assembly));
                this.AggregateCatalog.Catalogs.Add(new AssemblyCatalog(typeof(MefRegionDefinition).Assembly));
                this.AggregateCatalog.Catalogs.Add(new AssemblyCatalog(typeof(EventAggregator).Assembly));

                this.AggregateCatalog.Catalogs.Add(new AssemblyCatalog("AmiModules\\AmiDataService.dll"));

                this.AggregateCatalog.Catalogs.Add(new DirectoryCatalog("HHIServices"));
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(ex.Message);
            }

        }

        public override void RegisterDefaultTypesIfMissing()
        {
            try
            {
                base.RegisterDefaultTypesIfMissing();
            }
            catch (ReflectionTypeLoadException ex)
            {
                StringBuilder sb = new StringBuilder();
                foreach (Exception exSub in ex.LoaderExceptions)
                {
                    sb.AppendLine(exSub.Message);
                    if (exSub is FileNotFoundException)
                    {
                        FileNotFoundException exFileNotFound = exSub as FileNotFoundException;
                        if (!string.IsNullOrEmpty(exFileNotFound.FusionLog))
                        {
                            sb.AppendLine("Fusion Log:");
                            sb.AppendLine(exFileNotFound.FusionLog);
                        }
                    }
                    sb.AppendLine();
                }
                string errorMessage = sb.ToString();
                //Console.WriteLine(errorMessage);

                MessageBox.Show(errorMessage);
                if (Application.Current != null)
                    Application.Current.Shutdown();
            } 
        }

        protected override RegionAdapterMappings ConfigureRegionAdapterMappings()
        {
            RegionAdapterMappings mappings = base.ConfigureRegionAdapterMappings();
            
            mappings.RegisterMapping(typeof(LayoutPanel), Container.GetExportedValue<LayoutPanelAdapter>());
            mappings.RegisterMapping(typeof(LayoutGroup), Container.GetExportedValue<LayoutGroupAdapter>());
            mappings.RegisterMapping(typeof(DocumentGroup), Container.GetExportedValue<DocumentGroupAdapter>());

            return mappings;
        }

        private T GetExportedValueSafely<T>() where T : class
        {
            try
            {
                return Container.GetExportedValue<T>();
            }
            catch (CompositionException)
            {
                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        protected override Microsoft.Practices.Prism.Regions.IRegionBehaviorFactory ConfigureDefaultRegionBehaviors()
        {
            var factory = base.ConfigureDefaultRegionBehaviors();
            //factory.AddIfMissing("AutoPopulateExportedViewsBehavior", typeof(AutoPopulateExportedViewsBehavior));
            return factory;
        }

        protected override DependencyObject CreateShell()
        {
            if (Application.Current == null) new Application();
            if (Application.Current.MainWindow == null)
            {
                if (this.MainWindow != null) Application.Current.MainWindow = this.MainWindow;
                else new Window();
            }

            return this.Container.GetExportedValue<Shell>();
        }

        protected override void InitializeShell()
        {
            base.InitializeShell();

            //Application.Current.MainWindow = (Shell)this.Shell;

            if (this.Shell != null)
                ((Shell)this.Shell).SetModuleOriginInfo(this.AggregateCatalog);
        }

        public UserControl GetShell()
        {
            return this.Shell as UserControl;
        }

    }
}













        
