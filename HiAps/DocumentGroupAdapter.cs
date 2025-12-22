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
using System.Windows.Media;

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
        private readonly HashSet<object> floatingViews = new HashSet<object>();
        private bool isProcessingCollectionChange = false;

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

            // DockLayoutManager의 FloatGroups 변경 감지
            var dockLayoutManager = LayoutItemsHelper.GetDockLayoutManager(regionTarget);
            if (dockLayoutManager != null)
            {
                dockLayoutManager.FloatGroups.CollectionChanged += (s, e) => OnFloatGroupsChanged(region, s, e);
            }

            // Views 컬렉션 변경 이벤트 구독
            region.Views.CollectionChanged += (sender, e) =>
                OnViewsCollectionChanged(region, regionTarget, sender, e);
        }

        /// <summary>
        /// FloatGroup 컬렉션 변경 처리
        /// </summary>
        private void OnFloatGroupsChanged(IRegion region, object sender, NotifyCollectionChangedEventArgs e)
        {
            // 탭을 떼어내어 Floating 윈도우가 생성될 때 기본 크기 설정
            if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null)
            {
                foreach (FloatGroup floatGroup in e.NewItems)
                {
                    floatGroup.FloatSize = new Size(1000, 700);

                    // 기본 사이즈 설정
                    if (floatGroup.FloatSize.Width < 200 || floatGroup.FloatSize.Height < 200)
                    {
                        floatGroup.FloatSize = new Size(1000, 700);
                    }
                }
            }

            // FloatGroup이 제거될 때 (패널이 다시 도킹될 때)
            if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems != null)
            {
                foreach (FloatGroup floatGroup in e.OldItems)
                {
                    // FloatGroup 내의 모든 패널 확인
                    foreach (var item in floatGroup.Items)
                    {
                        if (item is DocumentPanel panel)
                        {
                            var content = panel.Content;
                            if (content != null && floatingViews.Contains(content))
                            {
                                // Floating 상태에서 벗어났으므로 목록에서 제거
                                floatingViews.Remove(content);

                                // Region에 다시 추가 (이미 없다면)
                                if (!region.Views.Contains(content))
                                {
                                    isProcessingCollectionChange = true;
                                    region.Add(content);
                                    isProcessingCollectionChange = false;
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// DocumentPanel이 언로드될 때 리소스 정리
        /// </summary>
        private void DocumentPanel_Unloaded(object sender, RoutedEventArgs e)
        {
            if (sender is DocumentPanel documentPanel)
            {
                var content = documentPanel.Content;
                if (content != null && floatingViews.Contains(content))
                {
                    floatingViews.Remove(content);
                }

                if (content is IDisposable disposableContent && documentPanel.IsClosed)
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
                if (content != null && floatingViews.Contains(content))
                {
                    floatingViews.Remove(content);
                }

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
            // 프로그래매틱한 변경 중이면 무시
            if (isProcessingCollectionChange)
                return;

            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    HandleViewsAdded(region, regionTarget, e.NewItems);
                    break;

                case NotifyCollectionChangedAction.Remove:
                    HandleViewsRemoved(region, regionTarget, e.OldItems);
                    break;
            }
        }

        /// <summary>
        /// 새로운 View가 추가될 때 처리
        /// </summary>
        private void HandleViewsAdded(IRegion region, DocumentGroup regionTarget, IList newItems)
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

                    // 이미 floating 상태인 뷰가 다시 추가되는 경우 처리
                    if (floatingViews.Contains(newItem))
                    {
                        // 기존 floating 패널 찾기
                        var existingPanel = FindDocumentPanelForContent(dockLayoutManager, newItem);
                        if (existingPanel != null)
                        {
                            // 이미 존재하므로 활성화만 수행
                            dockLayoutManager.DockController.Activate(existingPanel);
                            continue;
                        }
                        else
                        {
                            // floating 목록에서 제거 (패널이 없으면)
                            floatingViews.Remove(newItem);
                        }
                    }

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
        /// DockLayoutManager 내의 모든 DocumentPanel 중에서 특정 content를 가진 패널 찾기
        /// </summary>
        private DocumentPanel FindDocumentPanelForContent(DockLayoutManager manager, object content)
        {
            // 모든 아이템 검색 (FloatGroup 포함)
            var allItems = manager.GetItems();
            foreach (var item in allItems)
            {
                if (item is DocumentPanel panel && panel.Content == content)
                {
                    return panel;
                }
            }
            return null;
        }

        /// <summary>
        /// 패널이 FloatGroup 내에 있는지 확인
        /// </summary>
        private bool IsFloating(DocumentPanel panel)
        {
            if (panel == null)
                return false;

            // Visual Tree와 Logical Tree를 모두 확인
            DependencyObject current = panel;
            while (current != null)
            {
                if (current is FloatGroup)
                    return true;

                // Visual Tree 먼저 확인
                DependencyObject visualParent = VisualTreeHelper.GetParent(current);
                if (visualParent != null)
                {
                    current = visualParent;
                    continue;
                }

                // Visual Tree에 없으면 Logical Tree 확인
                DependencyObject logicalParent = LogicalTreeHelper.GetParent(current);
                if (logicalParent != null)
                {
                    current = logicalParent;
                    continue;
                }

                break;
            }

            return false;
        }

        /// <summary>
        /// View가 제거될 때 처리
        /// </summary>
        private void HandleViewsRemoved(IRegion region, DocumentGroup regionTarget, IList oldItems)
        {
            if (oldItems == null)
                return;

            foreach (var oldItem in oldItems)
            {
                try
                {
                    var dockLayoutManager = LayoutItemsHelper.GetDockLayoutManager(regionTarget);
                    if (dockLayoutManager?.DockController == null)
                        continue;

                    // 전체 DockLayoutManager에서 패널 찾기 (floating 상태일 수도 있음)
                    var documentPanel = FindDocumentPanelForContent(dockLayoutManager, oldItem);

                    if (documentPanel != null)
                    {
                        // 패널의 상태 확인
                        bool isFloating = IsFloating(documentPanel);
                        bool isClosed = documentPanel.IsClosed;

                        // Floating 상태로 변경된 경우
                        if (isFloating && !isClosed)
                        {
                            // Region에 다시 추가하여 추적 유지
                            if (!floatingViews.Contains(oldItem))
                            {
                                floatingViews.Add(oldItem);
                            }

                            // 패널은 제거하지 않음 (floating 상태 유지)
                            continue;
                        }

                        // 실제로 닫힌 경우에만 제거
                        if (isClosed || !isFloating)
                        {
                            if (floatingViews.Contains(oldItem))
                            {
                                floatingViews.Remove(oldItem);
                            }

                            dockLayoutManager.DockController.RemovePanel(documentPanel);

                            // 리소스 정리
                            if (oldItem is IDisposable disposableItem)
                            {
                                disposableItem.Dispose();
                            }
                        }
                    }
                    else if (oldItem is IPanelInfo panelInfo)
                    {
                        // IPanelInfo 인터페이스를 통한 처리 (기존 로직)
                        var parentWindow = panelInfo.GetParentWnd();
                        if (parentWindow is DocumentPanel panel)
                        {
                            bool isFloating = IsFloating(panel);
                            bool isClosed = panel.IsClosed;

                            if (isFloating && !isClosed)
                            {
                                if (!floatingViews.Contains(oldItem))
                                {
                                    floatingViews.Add(oldItem);
                                }
                                continue;
                            }

                            if (floatingViews.Contains(oldItem))
                            {
                                floatingViews.Remove(oldItem);
                            }

                            dockLayoutManager.DockController.RemovePanel(panel);

                            if (oldItem is IDisposable disposableItem)
                            {
                                disposableItem.Dispose();
                            }
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
