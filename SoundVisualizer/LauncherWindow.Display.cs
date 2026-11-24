// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Windows;
using Microsoft.Win32;
using SoundVisualizer.Display;
using SoundVisualizer.Ui;

namespace SoundVisualizer
{
    /// <summary>
    /// 런처의 '표시할 모니터'(#29). 모니터를 두 대 이상 쓰는 사람이 오버레이를 띄울 모니터를 선택한다.
    ///
    /// 칸은 쉬운 설정과 자세한 설정에 하나씩 있고(Ui/DisplayPicker), 칸이 값을 쓰고 알리면 여기서 나머지를 맞춘다:
    /// 다른 화면의 칸, 최대 프레임의 끝값(모니터마다 주사율이 다르다), 열려 있는 오버레이 편집 패널, 그리고 떠 있는 오버레이의 자리.
    /// 모니터를 꽂거나 뽑거나 해상도·주 모니터를 바꿨을 때도 같은 일을 한다.
    /// </summary>
    public partial class LauncherWindow
    {
        private void WatchDisplays()
        {
            // 칸의 알림은 창까지 올라온다. 어느 화면의 칸에서 바꿨든 여기 한 곳에서 받는다.
            AddHandler(DisplayPicker.DisplayChangedEvent, new RoutedEventHandler(OnOverlayDisplayChanged));
            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        }

        private void UnwatchDisplays()
        {
            // 프로세스가 끝날 때까지 살아 있는 알림이라 창을 닫을 때 뗀다.
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        }

        /// <summary>모니터 선택 칸에서 모니터를 바꿨다(칸이 이미 AppSettings 에 쓰고 저장을 예약했다).</summary>
        private void OnOverlayDisplayChanged(object sender, RoutedEventArgs e) => ApplyOverlayDisplay();

        /// <summary>
        /// Windows 가 모니터 구성이 바뀌었다고 알렸다(꽂거나 뽑음, 해상도·주사율·주 모니터·배치 변경). 알림은 다른 스레드에서 올 수 있어
        /// UI 스레드로 넘긴다.
        /// </summary>
        private void OnDisplaySettingsChanged(object? sender, EventArgs e) => Dispatcher.InvokeAsync(OnDisplaysChanged);

        /// <summary>
        /// 모니터 구성이 바뀌었다. 잠깐 읽어 둔 구성(DisplayEnumerator)을 버리고 새로 읽은 것으로 맞춘다. 선택해 둔 모니터가 뽑혔으면
        /// 주 모니터로, 다시 꽂혔으면 그 모니터로 오버레이가 옮겨 간다.
        /// </summary>
        private void OnDisplaysChanged()
        {
            DisplayEnumerator.Refresh();
            ApplyOverlayDisplay();
        }

        /// <summary>
        /// 자세한 설정의 모니터 칸을 맞춘다: 연결된 모니터를 다시 읽어 칸과 선택을 맞추고, 모니터가 한 대뿐이면 구역을 통째로 접는다.
        /// 쉬운 설정의 칸은 쉬운 설정이 스스로 맞춘다(EasySettingsView.ApplyCodeTexts).
        /// </summary>
        private void ReloadDisplayCards()
        {
            DisplayCardsDetailed.Reload();
            Show(PanelOverlayDisplay, DisplayCardsDetailed.HasChoices);
        }

        /// <summary>
        /// 지금 선택과 지금 연결된 모니터에 맞춰 설정 화면과 오버레이를 맞춘다.
        ///
        /// 최대 프레임의 저장값은 건드리지 않는다. 슬라이더의 끝값만 그 모니터의 주사율로 바뀐다(LoadSettingsToUI). 주사율이 낮은 모니터로
        /// 옮길 때 값을 깎아 저장하면, 화살표로 칸을 지나가거나 모니터가 절전으로 잠깐 빠지기만 해도 사용자가 정한 값이 낮아진 채 돌아오지
        /// 않는다. 그리기는 어차피 화면 주사율보다 자주 돌지 않으므로 깎을 까닭이 없다.
        /// </summary>
        private void ApplyOverlayDisplay()
        {
            bool wasInitializing = _isInitializing;
            _isInitializing = true; // 칸을 맞추며 생기는 바뀜 알림이 다시 저장하지 않게
            LoadSettingsToUI();
            _isInitializing = wasInitializing;

            SyncOverlayEditPanel();
            _overlayWindow?.MoveToSelectedDisplay();
        }
    }
}
