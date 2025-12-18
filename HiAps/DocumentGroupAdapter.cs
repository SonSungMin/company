using DevExpress.Xpf.Docking;
using DevExpress.Xpf.Docking.Base;
using Microsoft.Practices.Prism.Regions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel.Composition;
using System.Linq;
using System.Windows;

namespace Amisys.Framework.Infrastructure.RegionAdapters
{
    /// <summary>
    /// DocumentGroup을 위한 Region Adapter
    /// DevExpress Docking 컨트롤과 Prism Region을 연결
    /// </summary>
    [PartCreationPolicy(CreationPolicy.NonShared)]
    [Export(typeof(DocumentGroupAdapter))]
    public class DocumentGroupAdapter : RegionAdapterBase<DocumentGroup>
    {
        private readonly SortedList<string, int> viewSequenceList = new SortedList<string, int>();

        [ImportingConstructor]
        public DocumentGroupAdapter(IRegionBehaviorFactory behaviorFactory)
            : base(behaviorFactory)
        {
        }

        /// <summary>
        /// 새로운 Region 인스턴스 생성
        /// </summary>
        protected override IRegion CreateRegion()
        {
            return new AllActiveRegion();
        }

        /// <summary>
        /// Region과 DocumentGroup을 연결
        /// </summary>
        protected override void Adapt(IRegion region, DocumentGroup regionTarget)
        {
            if (region == null)
                throw new ArgumentNullException(nameof(region));
            if (regionTarget == null)
                throw new ArgumentNullException(nameof(regionTarget));

            // Views 컬렉션 변경 이벤트 구독
            region.Views.CollectionChanged += (sender, e) =>
                OnViewsCollectionChanged(region, regionTarget, sender, e);
        }

        /// <summary>
        /// DocumentPanel이 언로드될 때 리소스 정리
        /// </summary>
        private void DocumentPanel_Unloaded(object sender, RoutedEventArgs e)
        {
            if (sender is DocumentPanel documentPanel)
            {
                var content = documentPanel.Content;
                if (content is IDisposable disposableContent)
                {
                    disposableContent.Dispose();
                }
            }
        }

        /// <summary>
        /// DockItem이 닫힐 때 리소스 정리
        /// </summary>
        private void DockLayoutManager_DockItemClosed(object sender, DockItemClosedEventArgs e)
        {
            if (e.AffectedItems?.Any() != true)
                return;

            foreach (var affectedItem in e.AffectedItems.OfType<DocumentPanel>())
            {
                var content = affectedItem.Content;
                if (content is IDisposable disposableContent)
                {
                    disposableContent.Dispose();
                }
            }
        }
        /// <summary>
        /// View 이름에 대한 시퀀스 번호 가져오기
        /// </summary>
        private int GetViewSequence(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return 1;

            if (viewSequenceList.ContainsKey(name))
            {
                viewSequenceList[name]++;
                return viewSequenceList[name];
            }

            viewSequenceList.Add(name, 1);
            return 1;
        }

        /// <summary>
        /// 시퀀스 번호가 포함된 패널 이름 생성
        /// </summary>
        private string GetPanelNameWithSequence(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "New Page";

            int sequence = GetViewSequence(name);
            return sequence > 1 ? $"{name}_{sequence}" : name;
        }

        /// <summary>
        /// Views 컬렉션 변경 시 호출되는 메서드
        /// </summary>
        private void OnViewsCollectionChanged(
            IRegion region,
            DocumentGroup regionTarget,
            object sender,
            NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    HandleViewsAdded(regionTarget, e.NewItems);
                    break;

                case NotifyCollectionChangedAction.Remove:
                    HandleViewsRemoved(regionTarget, e.OldItems);
                    break;
            }
        }

        /// <summary>
        /// 새로운 View가 추가될 때 처리
        /// </summary>
        private void HandleViewsAdded(DocumentGroup regionTarget, IList newItems)
        {
            if (newItems == null)
                return;

            foreach (var newItem in newItems)
            {
                try
                {
                    var dockLayoutManager = LayoutItemsHelper.GetDockLayoutManager(regionTarget);
                    if (dockLayoutManager?.DockController == null)
                        continue;

                    // DockItem 닫기 이벤트 구독
                    dockLayoutManager.DockItemClosed -= DockLayoutManager_DockItemClosed;
                    dockLayoutManager.DockItemClosed += DockLayoutManager_DockItemClosed;

                    // 새 DocumentPanel 생성
                    var documentPanel = dockLayoutManager.DockController.AddDocumentPanel(regionTarget);
                    documentPanel.Content = newItem;

                    // Unloaded 이벤트 구독
                    documentPanel.Unloaded -= DocumentPanel_Unloaded;
                    documentPanel.Unloaded += DocumentPanel_Unloaded;

                    // Panel 정보 설정
                    SetupDocumentPanel(documentPanel, newItem);
                    // Panel 활성화
                    dockLayoutManager.DockController.Activate(documentPanel);
                }
                catch (Exception ex)
                {
                    // 로깅이 필요한 경우
                    // Logger.LogError($"View 추가 중 오류: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// View가 제거될 때 처리
        /// </summary>
        private void HandleViewsRemoved(DocumentGroup regionTarget, IList oldItems)
        {
            if (oldItems == null)
                return;

            foreach (var oldItem in oldItems.OfType<IPanelInfo>())
            {
                try
                {
                    var dockLayoutManager = LayoutItemsHelper.GetDockLayoutManager(regionTarget);
                    if (dockLayoutManager?.DockController == null)
                        continue;
                    var parentWindow = oldItem.GetParentWnd();
                    if (parentWindow is DocumentPanel documentPanel)
                    {
                        dockLayoutManager.DockController.RemovePanel(documentPanel);

                        // 리소스 정리
                        if (oldItem is IDisposable disposableItem)
                        {
                            disposableItem.Dispose();
                        }
                    }
                }
                catch (Exception ex)
                {
                    // 로깅이 필요한 경우
                    // Logger.LogError($"View 제거 중 오류: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// DocumentPanel의 이름과 캡션 설정
        /// </summary>
        private void SetupDocumentPanel(DocumentPanel documentPanel, object content)
        {
            if (content is IPanelInfo panelInfo)
            {
                var panelName = panelInfo.GetPanelName();
                var panelCaption = panelInfo.GetPanelCaption();

                documentPanel.Name = GetPanelNameWithSequence(panelName);
                documentPanel.Caption = panelCaption;

                // 부모 윈도우 설정
                panelInfo.SetParentWnd(documentPanel);
            }
            else
            {
                documentPanel.Name = GetPanelNameWithSequence("New Page");
                documentPanel.Caption = "New Page";
            }
        }
    }
}



























            
