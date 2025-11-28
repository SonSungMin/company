using Amisys.Framework.Infrastructure.Interfaces;
using Amisys.Framework.Infrastructure.PrismSupps;
using Amisys.Framework.Infrastructure.RegionAdapters;
using DevExpress.Xpf.Core;
using Microsoft.Practices.Composite.Events;
using Microsoft.Practices.Composite.Presentation.Events;
using Microsoft.Practices.Prism.Logging;
using Microsoft.Practices.Prism.Regions;
using Microsoft.Practices.ServiceLocation;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;

namespace Amisys.Framework.Infrastructure.DataModels
{
    public class AmiPrismMefBase : INotifyPropertyChanged, IDisposable
    {
        protected string mainContentViewName;
        protected string regionName;
        protected string moduleFullName;
        protected string moduleName;
        protected List<string> attachedViewList = new List<string>();
        protected List<object> attachedViewList2 = new List<object>();
        protected List<SubscriptionToken> _DynamicSubsTokenList = new List<SubscriptionToken>();
        protected List<CompositePresentationEvent<object>> _DynamicEventList = new List<CompositePresentationEvent<object>>();
        protected List<SubscriptionToken> _EvtParamSubsTokenList = new List<SubscriptionToken>();
        protected List<CompositePresentationEvent<DynamicEventParam>> _EvtParamEventList = new List<CompositePresentationEvent<DynamicEventParam>>();
        private bool _IsDisposed = false;

        protected IEventAggregator eventAggregator
        {
            get => ServiceLocator.Current.GetInstance<IEventAggregator>();
        }
        protected IEntLibService entLibService => ServiceLocator.Current.GetInstance<IEntLibService>();

        protected IRegionManager regionManager => ServiceLocator.Current.GetInstance<IRegionManager>();

        protected ILogService logService => ServiceLocator.Current.GetInstance<ILogService>();

        protected ILayoutService layoutService => ServiceLocator.Current.GetInstance<ILayoutService>();

        protected void RegisterViewWithRegion(object view)
        {
            this.entLibService.Process((Action)(() =>
            {
                if (string.IsNullOrEmpty(this.regionName) || string.IsNullOrEmpty(this.mainContentViewName) || this.HasAttachedToRegion(view.GetType()))
                    return;
                RegionManagerExtensions.AddToRegion(this.regionManager, this.regionName, view);
                this.attachedViewList.Add(view.GetType().FullName);
            }));
        }
        protected void RegisterViewWithRegion(Type viewType)
        {
            this.entLibService.Process((Action)(() =>
            {
                if (string.IsNullOrEmpty(this.regionName) || string.IsNullOrEmpty(this.mainContentViewName) || this.HasAttachedToRegion(viewType))
                    return;
                RegionManagerExtensions.RegisterViewWithRegion(this.regionManager, this.regionName, viewType);
                this.attachedViewList.Add(viewType.FullName);
            }));
        }

        protected virtual bool HasAttachedToRegion()
        {
            return PrismSupporter.IsContainsViewInRegion(this.regionManager, this.regionName, this.mainContentViewName);
        }

        protected virtual bool HasAttachedToRegion(Type type)
        {
            return PrismSupporter.IsContainsViewInRegion(this.regionManager, this.regionName, type.FullName);
        }
        public void Write(string message)
        {
            this.entLibService.Process((Action)(() => this.logService.Write(message)));
        }

        public void Write(string message, Category category, Priority priority)
        {
            this.entLibService.Process((Action)(() => this.logService.Write(message, category, priority)));
        }

        public void WriteConstruced(string moduleName = "")
        {
            if (!string.IsNullOrEmpty(moduleName))
                this.Write($"{moduleName} Constructed.");
            else if (!string.IsNullOrEmpty(this.moduleFullName))
                this.Write($"{this.moduleFullName} Constructed.");
        }

        public void WriteInitialized(string moduleName = "")
        {
            if (!string.IsNullOrEmpty(moduleName))
                this.Write($"{moduleName} Initialized.");
            else if (!string.IsNullOrEmpty(this.moduleFullName))
                this.Write($"{this.moduleFullName} Initialized.");
        }

        public void SetThemeName(DependencyObject obj)
        {
            string devTheme = this.layoutService.GetDevTheme();
            ThemeManager.SetThemeName(obj, devTheme);
        }

        protected void EventSubscribe(
          DynamicEvent evt,
          Action<DynamicEventParam> action,
          ThreadOption option = ThreadOption.UIThread)
        {
            this._EvtParamEventList.Add((CompositePresentationEvent<DynamicEventParam>)evt);
            this._EvtParamSubsTokenList.Add(evt.Subscribe(action, option));
        }
        protected void EventSubscribe(
          CompositePresentationEvent<object> evt,
          Action<object> action,
          ThreadOption option = ThreadOption.UIThread)
        {
            this._DynamicEventList.Add(evt);
            this._DynamicSubsTokenList.Add(evt.Subscribe(action, option));
        }

        protected void EventSubscribe(Action<DynamicEventParam> action, ThreadOption option = ThreadOption.UIThread)
        {
            this.EventSubscribe(this.eventAggregator.GetEvent<DynamicEvent>(), action, option);
        }

        protected void EventSubscribeWithPublisherThread(
          DynamicEvent evt,
          Action<DynamicEventParam> action,
          ThreadOption option = ThreadOption.PublisherThread)
        {
            this._EvtParamEventList.Add((CompositePresentationEvent<DynamicEventParam>)evt);
            this._EvtParamSubsTokenList.Add(evt.Subscribe(action, option));
        }
        protected void EventSubscribeWithPublisherThread(
          CompositePresentationEvent<object> evt,
          Action<object> action,
          ThreadOption option = ThreadOption.PublisherThread)
        {
            this._DynamicEventList.Add(evt);
            this._DynamicSubsTokenList.Add(evt.Subscribe(action, option));
        }

        protected void EventSubscribeWithUIThread(Action<DynamicEventParam> action)
        {
            ThreadOption option = ThreadOption.UIThread;
            this.EventSubscribe(this.eventAggregator.GetEvent<DynamicEvent>(), action, option);
        }

        protected void EventSubscribeWithUIThread(DynamicEvent evt, Action<DynamicEventParam> action)
        {
            ThreadOption threadOption = ThreadOption.UIThread;
            this._EvtParamEventList.Add((CompositePresentationEvent<DynamicEventParam>)evt);
            this._EvtParamSubsTokenList.Add(evt.Subscribe(action, threadOption));
        }
        protected void EventSubscribeWithUIThread(
          CompositePresentationEvent<object> evt,
          Action<object> action)
        {
            ThreadOption threadOption = ThreadOption.UIThread;
            this._DynamicEventList.Add(evt);
            this._DynamicSubsTokenList.Add(evt.Subscribe(action, threadOption));
        }

        protected void EventSubscribeWithPublisherThread(
          Action<DynamicEventParam> action,
          ThreadOption option = ThreadOption.PublisherThread)
        {
            this.EventSubscribe(this.eventAggregator.GetEvent<DynamicEvent>(), action, option);
        }

        protected void EventUnsubscribe()
        {
            for (int index = 0; index < this._DynamicEventList.Count; ++index)
            {
                if (this._DynamicEventList[index].Contains(this._DynamicSubsTokenList[index]))
                    this._DynamicEventList[index].Unsubscribe(this._DynamicSubsTokenList[index]);
            }
            for (int index = 0; index < this._EvtParamEventList.Count; ++index)
            {
                if (this._EvtParamEventList[index].Contains(this._EvtParamSubsTokenList[index]))
                    this._EvtParamEventList[index].Unsubscribe(this._EvtParamSubsTokenList[index]);
            }
        }

        public virtual void Dispose()
        {
            if (this._IsDisposed)
                return;
            this.OnDispose();
            this._IsDisposed = true;
            GC.SuppressFinalize((object)this);
        }

        protected virtual void OnDispose()
        {
            this.DetatchAllViewFromRegion();
            this.EventUnsubscribe();
        }
        protected void DetatchAllViewFromRegion()
        {
            if (string.IsNullOrEmpty(this.regionName))
                return;
            foreach (string attachedView in this.attachedViewList)
                PrismSupporter.DetatchViewFromRegion(this.regionManager, this.regionName, attachedView);
            this.attachedViewList.Clear();
        }

        protected void DetatchViewFromRegion()
        {
            if (string.IsNullOrEmpty(this.regionName))
                return;
            PrismSupporter.DetatchViewFromRegion(this.regionManager, this.regionName, this.mainContentViewName);
            this.attachedViewList.Remove(this.mainContentViewName);
        }

        protected void DetatchViewFromRegion(Type view)
        {
            if (string.IsNullOrEmpty(this.regionName))
                return;
            PrismSupporter.DetatchViewFromRegion(this.regionManager, this.regionName, view.FullName);
            this.attachedViewList.Remove(view.FullName);
        }

        protected void DetatchViewFromRegion(Type viewType, int id)
        {
            if (string.IsNullOrEmpty(this.regionName))
                return;
            object view = this.FindView(viewType, id);
            if (view != null)
            {
                PrismSupporter.DetatchViewFromRegion(this.regionManager, this.regionName, viewType.FullName, id);
                this.attachedViewList2.Remove(view);
            }
        }

        protected object FindView(Type viewType, int id)
        {
            if (this.attachedViewList2 == null)
                return (object)null;
            foreach (object view in this.attachedViewList2)
            {
                if (view.GetType() == viewType && (view as IPanelInfo).GetId() == id)
                    return view;
            }
            return (object)null;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string key)
        {
            PropertyChangedEventHandler propertyChanged = this.PropertyChanged;
            if (propertyChanged == null)
                return;
            propertyChanged((object)this, new PropertyChangedEventArgs(key));
        }
    }
}


















