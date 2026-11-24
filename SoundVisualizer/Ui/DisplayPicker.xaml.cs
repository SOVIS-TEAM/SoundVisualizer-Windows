// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Threading;
using SoundVisualizer.Display;
using SoundVisualizer.Localization;

namespace SoundVisualizer.Ui
{
    /// <summary>모니터 선택 칸 하나에 보이는 것. 칸의 서식(DisplayPicker.xaml 의 DisplayCard)이 읽는다.</summary>
    public sealed class DisplayChoice
    {
        public DisplayChoice(DisplayInfo display, IReadOnlyList<DisplayInfo> all, string size, string primaryText)
        {
            Display = display;
            All = all;
            Size = size;
            PrimaryText = primaryText;
        }

        /// <summary>이 칸의 모니터.</summary>
        public DisplayInfo Display { get; }

        /// <summary>연결된 모니터 모두(칸의 차례). 배치 그림이 쓴다.</summary>
        public IReadOnlyList<DisplayInfo> All { get; }

        /// <summary>해상도 글자.</summary>
        public string Size { get; }

        /// <summary>주 모니터면 그 표시, 아니면 빈 문자열.</summary>
        public string PrimaryText { get; }
    }

    /// <summary>
    /// 오버레이를 표시할 모니터를 선택하는 칸(#29). 모니터를 두 대 쓰는 사용자가 "어떻게 설정해야 할지 모르겠다"고 알려 와 만들었다.
    /// 예전에는 오버레이가 뜨는 모니터를 정할 수 없어, 게임을 하는 모니터에 띄우려면 Windows 에서 주 모니터를 바꿔야 했다.
    ///
    /// - 연결된 모니터마다 칸 하나. 그림은 모니터가 놓인 모양이고(DisplayLayoutIcon), 칸의 차례도 놓인 차례다.
    /// - 선택한 적이 없으면 주 모니터의 칸이 선택되어 보인다. 선택해 둔 모니터가 연결되어 있지 않으면 어느 칸도 선택해 두지 않고
    ///   그 까닭과 할 일을 한 줄로 적는다(쉬운 설정의 크기 칸 #425 와 같다). 그래야 주 모니터 칸을 눌러 '여기에 두기'를 저장할 수 있다.
    ///   저장된 선택은 그대로 두어, 아무것도 누르지 않고 다시 연결하면 그 모니터로 돌아간다.
    /// - 칸을 누르면 그 모니터를 AppSettings 에 쓰고 저장을 예약한 뒤 <see cref="DisplayChanged"/> 로 알린다. 알림은 창까지 올라가므로
    ///   런처가 한 곳에서 받아 다른 화면의 칸과 최대 프레임을 맞추고 떠 있는 오버레이를 옮긴다.
    ///
    /// 어느 모니터에 띄울지, 칸의 차례와 번호는 Core 의 <see cref="OverlayDisplays"/> 가 정한다.
    /// </summary>
    public partial class DisplayPicker : UserControl
    {
        public static readonly RoutedEvent DisplayChangedEvent = EventManager.RegisterRoutedEvent(
            nameof(DisplayChanged), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(DisplayPicker));

        private bool _loading;
        private string _signature = "";
        private bool _missingAnnounced;

        public DisplayPicker()
        {
            InitializeComponent();
            // 설정 탭에 들어오거나 쉬운 설정·자세한 설정을 오가 이 칸이 보이게 되면, 그동안 나타난 안내를 읽게 한다.
            IsVisibleChanged += (s, e) =>
            {
                if (IsVisible) Dispatcher.InvokeAsync(AnnounceMissing, DispatcherPriority.Loaded);
            };
        }

        /// <summary>칸에서 모니터를 바꿨다. 값은 이미 AppSettings 에 썼다.</summary>
        public event RoutedEventHandler DisplayChanged
        {
            add => AddHandler(DisplayChangedEvent, value);
            remove => RemoveHandler(DisplayChangedEvent, value);
        }

        /// <summary>선택할 것이 있는지(모니터가 두 대 이상). 한 대뿐이면 이 칸을 놓은 쪽이 구역을 통째로 접는다.</summary>
        public bool HasChoices { get; private set; }

        /// <summary>
        /// 연결된 모니터와 저장된 선택을 다시 읽어 칸을 맞춘다. 런처가 설정을 다시 그릴 때마다, 모니터 구성이 바뀔 때마다 부른다.
        /// 모니터 구성과 화면 언어가 그대로면 칸은 두고 선택만 맞춘다(키보드 초점이 남는다).
        /// </summary>
        public void Reload()
        {
            _loading = true;
            try
            {
                var ordered = OverlayDisplays.Ordered(DisplayEnumerator.Current());
                string signature = DisplayChoiceText.Signature(ordered, Loc.Instance.Code);
                bool rebuilt = false;
                bool hadFocus = Cards.IsKeyboardFocusWithin;
                if (signature != _signature)
                {
                    _signature = signature;
                    FillCards(ordered);
                    rebuilt = true;
                }

                // 선택해 둔 모니터가 연결되어 있지 않으면 어느 칸도 선택해 두지 않는다(쉬운 설정의 크기 칸 #425 와 같다). 오버레이가 지금
                // 쓰는 주 모니터의 칸을 선택해 두면, 그 칸을 눌러도 선택이 바뀌지 않아 '주 모니터에 두기'를 저장할 수 없다.
                string saved = AppSettings.OverlayDisplay;
                bool missing = OverlayDisplays.IsMissing(ordered, saved);
                var target = missing ? null : OverlayDisplays.Resolve(ordered, saved);
                int index = -1;
                for (int i = 0; i < ordered.Count; i++)
                {
                    if (ReferenceEquals(ordered[i], target)) index = i;
                }
                // 칸은 ordered 와 같은 차례로 만들었다. 표식이 같아 칸을 그대로 둔 때에도 차례는 같다.
                if (index >= 0 && index < Cards.Items.Count) Cards.SelectedIndex = index;
                else Cards.ClearSelection();

                HasChoices = ordered.Count >= 2;
                bool showMissing = HasChoices && missing;
                if (!showMissing) _missingAnnounced = false;
                TxtMissing.Visibility = showMissing ? Visibility.Visible : Visibility.Collapsed;
                // 안내 읽기와 초점은 배치가 끝난 뒤에 한다. 이 칸은 쉬운 설정이 선택 상태를 맞출 때(보이게 된 바로 그때)에도 다시 읽힌다.
                if (showMissing) Dispatcher.InvokeAsync(AnnounceMissing, DispatcherPriority.Loaded);
                if (rebuilt && hadFocus) Dispatcher.InvokeAsync(RestoreFocus, DispatcherPriority.Loaded);
            }
            finally
            {
                _loading = false;
            }
        }

        /// <summary>
        /// 선택한 모니터가 연결되어 있지 않다는 안내를 화면 읽어주기가 읽게 한다. 안내가 나타난 뒤 이 칸이 보일 때 한 번만 읽는다.
        /// WPF 는 LiveSetting 만으로는 알리지 않아 직접 올린다(쉬운 설정의 크기 안내와 같다).
        /// </summary>
        private void AnnounceMissing()
        {
            if (_missingAnnounced || !TxtMissing.IsVisible) return;
            _missingAnnounced = true;
            if (AutomationPeer.ListenerExists(AutomationEvents.LiveRegionChanged))
            {
                UIElementAutomationPeer.CreatePeerForElement(TxtMissing)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
            }
        }

        /// <summary>
        /// 모니터 구성이 바뀌어 칸을 다시 만들었고 그 전에 키보드 초점이 칸에 있었다. 빠진 칸과 함께 사라진 초점을 선택된 칸(없으면 첫 칸)에
        /// 돌려준다. 창이 활성일 때만 한다(다른 창에 있던 초점을 가져오지 않는다).
        /// </summary>
        private void RestoreFocus()
        {
            if (!IsVisible || Window.GetWindow(this) is not { IsActive: true }) return;
            var item = (Cards.SelectedItem ?? (Cards.Items.Count > 0 ? Cards.Items[0] : null)) as ListBoxItem;
            item?.Focus();
        }

        private void FillCards(IReadOnlyList<DisplayInfo> ordered)
        {
            var numbers = OverlayDisplays.Numbers(ordered);
            string itemPattern = Loc.Get("settings.display.item");
            string sizePattern = Loc.Get("settings.display.size");
            string primary = Loc.Get("settings.display.primary");
            string pair = Loc.Get("easy.name_pair");

            Cards.Items.Clear();
            for (int i = 0; i < ordered.Count; i++)
            {
                var display = ordered[i];
                string title = DisplayChoiceText.Title(itemPattern, numbers[i]);
                string size = DisplayChoiceText.Size(sizePattern, display);
                string primaryText = display.IsPrimary ? primary : "";
                var item = new ListBoxItem
                {
                    Content = title,
                    Tag = new DisplayChoice(display, ordered, size, primaryText),
                };
                AutomationProperties.SetName(item, DisplayChoiceText.Name(pair, title, size, primaryText));
                Cards.Items.Add(item);
            }
            // 묶음의 이름은 설정의 이름이다. 화면 읽어주기가 "표시할 모니터, 2개 중 1번째"처럼 읽는다.
            AutomationProperties.SetName(Cards, Loc.Get("settings.display.title"));
        }

        private void Cards_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading) return;
            if (Cards.SelectedItem is not ListBoxItem { Tag: DisplayChoice choice }) return;

            string key = choice.Display.Key;
            if (string.Equals(AppSettings.OverlayDisplay, key, StringComparison.OrdinalIgnoreCase)) return;

            AppSettings.OverlayDisplay = key;
            AppSettings.Save();
            TxtMissing.Visibility = Visibility.Collapsed;
            RaiseEvent(new RoutedEventArgs(DisplayChangedEvent, this));
        }
    }
}
