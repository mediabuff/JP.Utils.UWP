﻿using JP.Utils.UI;
using System;
using System.Collections;
using System.Numerics;
using Windows.Foundation;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;

namespace JP.Utils.InheritingControl
{
    public class ListViewEx : ListView
    {
        public string StickyHeaderName
        {
            get { return (string)GetValue(StickyHeaderNameProperty); }
            set { SetValue(StickyHeaderNameProperty, value); }
        }

        public static readonly DependencyProperty StickyHeaderNameProperty =
            DependencyProperty.Register("StickyHeaderName", typeof(string), typeof(ListViewEx), new PropertyMetadata(null));

        public string UIElementToMove
        {
            get { return (string)GetValue(UIElementToMoveProperty); }
            set { SetValue(UIElementToMoveProperty, value); }
        }

        public static readonly DependencyProperty UIElementToMoveProperty =
            DependencyProperty.Register("UIElementToMove", typeof(string), typeof(ListViewEx), new PropertyMetadata(null));

        public bool EnableWaggingAnimation
        {
            get { return (bool)GetValue(EnableWaggingAnimationProperty); }
            set { SetValue(EnableWaggingAnimationProperty, value); }
        }

        public static readonly DependencyProperty EnableWaggingAnimationProperty =
            DependencyProperty.Register("EnableWaggingAnimation", typeof(bool), typeof(ListViewEx), new PropertyMetadata(false));

        public event Action OnReorderStopped;

        private ScrollViewer _scrollViewer;
        private Compositor _compositor;
        private Visual _movingVisual;
        private int _movingItemIndex;
        private int _zindex;
        private bool[] _isItemsAnimated;
        private double _distanceToTopBeforeMoving = 0f;
        private double _distanceToTopAfterMoving = 0f;
        private FrameworkElement _movingItem;

        public ListViewEx()
        {
            this.ContainerContentChanging += this_ContainerContentChanging;
            this.RegisterPropertyChangedCallback(HeaderProperty, (sender, e) =>
             {
                 ((FrameworkElement)Header).SizeChanged += header_SizeChanged;
             });
        }

        #region Header
        private void header_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            _scrollViewer = this.GetScrollViewer();
            InitialStickyHeader();
        }

        private void InitialStickyHeader()
        {
            if (_scrollViewer != null && StickyHeaderName != null)
            {
                var header = FindHeader();

                //找到 Header 的父容器，设置它的 ZIndex 才能让其在 ListView 的 Items 之上
                var headerContainer = header.Parent as ContentControl;

                Canvas.SetZIndex(headerContainer, 1);

                var stickyHeader = FindStickyContent();

                if (stickyHeader == null) throw new ArgumentNullException("Make sure you have define x:Name of the UIElement to be sticky.");

                var stickyVisual = ElementCompositionPreview.GetElementVisual(stickyHeader);
                var compositor = stickyVisual.Compositor;

                //计算 StickyContent 距离 ScrollViewer Content 顶部的纵向距离
                var transform = stickyHeader.TransformToVisual((UIElement)_scrollViewer.Content);
                var offsetY = (float)transform.TransformPoint(new Point(0, 0)).Y;

                var scrollProperties = ElementCompositionPreview.GetScrollViewerManipulationPropertySet(_scrollViewer);

                //当往下滚动的时候 ScrollingProperties.Translation.Y 不断负向增加，(ScrollingProperties.Translation.Y +OffsetY)
                //表明 StickyContent 距离顶部还有多少距离，当要滚出屏幕顶部的时候，(-OffsetY - ScrollingProperties.Translation.Y) 
                //计算还要把 Offset.Y 增加多少才让其 “Sticky“
                var scrollingAnimation = compositor.CreateExpressionAnimation(
                    "ScrollingProperties.Translation.Y +OffsetY> 0 ? 0 : -OffsetY - ScrollingProperties.Translation.Y");
                scrollingAnimation.SetReferenceParameter("ScrollingProperties", scrollProperties);
                scrollingAnimation.SetScalarParameter("OffsetY", offsetY);

                stickyVisual.StartAnimation("Offset.Y", scrollingAnimation);
            }
        }

        private FrameworkElement FindHeader()
        {
            return this.Header as FrameworkElement;
        }

        private FrameworkElement FindStickyContent()
        {
            var header = FindHeader();
            var stickyContent = header.FindName(StickyHeaderName) as FrameworkElement;
            return stickyContent;
        }

        #endregion

        #region Reorder
        private void this_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            _scrollViewer = this.GetScrollViewer();
            var listViewVisual = ElementCompositionPreview.GetElementVisual(sender);
            _compositor = listViewVisual.Compositor;

            if (!args.InRecycleQueue)
            {
                args.ItemContainer.Loaded -= ItemContainer_Loaded;
                args.ItemContainer.Loaded += ItemContainer_Loaded;
            }
        }

        private void ItemContainer_Loaded(object sender, RoutedEventArgs e)
        {
            var itemsPanel = (ItemsStackPanel)this.ItemsPanelRoot;
            var itemContainer = (ListViewItem)sender;
            var itemIndex = this.IndexFromContainer(itemContainer);

            var item = itemContainer.ContentTemplateRoot as FrameworkElement;
            var uielementToReorder = item.FindName(UIElementToMove) as UIElement;

            uielementToReorder.ManipulationMode = ManipulationModes.TranslateY;

            uielementToReorder.ManipulationStarted -= UIElementToReorder_ManipulationStarted;
            uielementToReorder.ManipulationDelta -= UIElementToReorder_ManipulationDelta;
            uielementToReorder.ManipulationCompleted -= UIElementToReorder_ManipulationCompleted;

            uielementToReorder.ManipulationStarted += UIElementToReorder_ManipulationStarted;
            uielementToReorder.ManipulationDelta += UIElementToReorder_ManipulationDelta;
            uielementToReorder.ManipulationCompleted += UIElementToReorder_ManipulationCompleted;
        }

        private void UIElementToReorder_ManipulationStarted(object sender, ManipulationStartedRoutedEventArgs e)
        {
            var uiElement = sender as FrameworkElement;
            var rootElement = uiElement.Parent as FrameworkElement;

            _movingItem = this.ContainerFromItem(rootElement.DataContext) as FrameworkElement;
            _movingVisual = ElementCompositionPreview.GetElementVisual(_movingItem);
            _movingItemIndex = this.IndexFromContainer(_movingItem);

            //Disable transition animation to make the ending smooth.
            DisableTransition();

            //The starting position
            _distanceToTopBeforeMoving = _movingItem.TransformToVisual(_scrollViewer.Content as UIElement).TransformPoint(new Point(0, 0)).Y;

            var items = this.Items;

            //Fade out all items
            for (int i = 0; i < items.Count; i++)
            {
                if (_movingItemIndex == i) continue;

                var item = items[i];
                var itemContainer = this.ContainerFromItem(item) as ListViewItem;
                var containerVisual = ElementCompositionPreview.GetElementVisual(itemContainer);

                var fadeAnimation = _compositor.CreateScalarKeyFrameAnimation();
                fadeAnimation.InsertKeyFrame(1f, 0.3f);
                fadeAnimation.Duration = TimeSpan.FromMilliseconds(500);

                containerVisual.StartAnimation("Opacity", fadeAnimation);

                if (EnableWaggingAnimation)
                    StartWaggingAnimation(i);
            }

            _isItemsAnimated = new bool[this.Items.Count];
        }

        private void UIElementToReorder_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
        {
            var uiElement = sender as FrameworkElement;
            var grid = uiElement.Parent as FrameworkElement;

            //Make sure it's on top.
            var currentItem = this.ContainerFromItem(grid.DataContext) as FrameworkElement;
            Canvas.SetZIndex(currentItem, _zindex++);

            var currentIndex = this.IndexFromContainer(currentItem);

            _distanceToTopAfterMoving = currentItem.TransformToVisual(_scrollViewer.Content as UIElement).TransformPoint(new Point(0, 0)).Y;

            //Move element by dy.
            _movingVisual.Offset = new Vector3(0f, (float)(_movingVisual.Offset.Y + e.Delta.Translation.Y), 0f);

            for (int i = 0; i < this.Items.Count; i++)
            {
                if (i == currentIndex) continue;

                //Caluclate the position betwee moving item and others and do some animations.
                AnimateItemOffset(i);
            }

            //TODO: Scroll the ScrollViewer while the moving item is about to exit the visible area.

            //var pointToSV = container.TransformToVisual(_scrollViewer.Content as UIElement).TransformPoint(new Point(0, 0));
            //if (pointToSV.Y + visual.Offset.Y + 200 > _scrollViewer.ActualHeight)
            //{
            //    var scrollViewerY = _scrollViewer.VerticalOffset;
            //    _scrollViewer.ChangeView(null, 10 + scrollViewerY, null);
            //}
        }

        private void UIElementToReorder_ManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
        {
            foreach (var item in this.Items)
            {
                var container = this.ContainerFromItem(item) as ListViewItem;
                var containerVisual = ElementCompositionPreview.GetElementVisual(container);

                var fadeAnimation = _compositor.CreateScalarKeyFrameAnimation();
                fadeAnimation.InsertKeyFrame(1f, 1f);
                fadeAnimation.Duration = TimeSpan.FromMilliseconds(500);

                containerVisual.StartAnimation("Opacity", fadeAnimation);
                containerVisual.Offset = new Vector3(0f, 0f, 0f);
                containerVisual.StopAnimation("Offset.Y");
            }

            var indexToInsert = (int)Math.Floor(_distanceToTopAfterMoving / _movingItem.ActualHeight);
            if (indexToInsert < 0) indexToInsert = 0;
            if (indexToInsert >= this.Items.Count) indexToInsert = this.Items.Count - 1;

            //Now move item from ItemsSource 
            //Moving from Items property will cause an exception.
            var collection = this.ItemsSource as IList;
            if (collection == null) throw new ArgumentNullException("The ItemsSource should inherit from IList.");

            var movingItem = collection[_movingItemIndex];
            collection.Remove(movingItem);
            collection.Insert(indexToInsert, movingItem);

            OnReorderStopped?.Invoke();
        }

        private void AnimateItemOffset(int itemIndex)
        {
            var item = this.ContainerFromIndex(itemIndex) as FrameworkElement;
            var itemVisual = ElementCompositionPreview.GetElementVisual(item);
            var offsetYToTop = (float)(item.TransformToVisual(_scrollViewer.Content as UIElement).TransformPoint(new Point(0, 0))).Y;

            var targetOffsetY = 0f;
            if (_distanceToTopAfterMoving > _distanceToTopBeforeMoving)
            {
                if (offsetYToTop > _distanceToTopBeforeMoving && offsetYToTop < _distanceToTopAfterMoving)
                {
                    targetOffsetY = -50f;
                }
                else targetOffsetY = 0f;
            }
            else
            {
                if (offsetYToTop > _distanceToTopAfterMoving && offsetYToTop < _distanceToTopBeforeMoving)
                {
                    targetOffsetY = 50f;
                }
                else targetOffsetY = 0f;
            }

            //The one are being animated should not be animated at this time.
            //Note that mass number of animations being triggered wil slow down the app, specifically in Mobile device.
            if (!_isItemsAnimated[itemIndex])
            {
                if (itemVisual.Offset.Y == targetOffsetY) return;

                var movingAnim = _compositor.CreateScalarKeyFrameAnimation();
                movingAnim.InsertKeyFrame(1f, targetOffsetY);
                movingAnim.Duration = TimeSpan.FromMilliseconds(200);

                //Create a batch to know when the animation would end.
                var batch = _compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
                itemVisual.StartAnimation("Offset.Y", movingAnim);
                _isItemsAnimated[itemIndex] = true;
                batch.Completed += (sender, e) =>
                {
                    _isItemsAnimated[itemIndex] = false;
                    if (EnableWaggingAnimation)
                        StartWaggingAnimation(itemIndex);
                };
                batch.End();
            }
        }

        private void StartWaggingAnimation(int i)
        {
            var item = this.Items[i];
            var itemContainer = this.ContainerFromItem(item) as ListViewItem;
            var containerVisual = ElementCompositionPreview.GetElementVisual(itemContainer);

            var random = new Random((int)DateTime.Now.Ticks).Next();
            var offsetAnimation = _compositor.CreateScalarKeyFrameAnimation();
            var offsetY = containerVisual.Offset.Y;
            offsetAnimation.InsertKeyFrame(0f, offsetY + (i % 2 == 0 ? 0f : 5f));
            offsetAnimation.InsertKeyFrame(0.5f, offsetY + (i % 2 == 0 ? 5f : 0f));
            offsetAnimation.InsertKeyFrame(1f, offsetY + (i % 2 == 0 ? 0f : 5f));
            offsetAnimation.DelayTime = TimeSpan.FromMilliseconds(i % 2 == 0 ? i * 100 : 0);
            offsetAnimation.Duration = TimeSpan.FromMilliseconds(5000);
            offsetAnimation.IterationBehavior = AnimationIterationBehavior.Forever;

            containerVisual.StartAnimation("Offset.Y", offsetAnimation);
        }

        private void DisableTransition()
        {
            this.ItemContainerTransitions = null;
        }
        #endregion
    }
}
