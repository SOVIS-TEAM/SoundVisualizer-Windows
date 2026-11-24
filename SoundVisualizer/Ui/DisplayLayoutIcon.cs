// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Media;
using SoundVisualizer.Display;

namespace SoundVisualizer.Ui
{
    /// <summary>
    /// 모니터 선택 칸(#29)의 그림. 모니터들이 책상에 놓인 모양을 그대로 줄여 그리고, 이 칸의 모니터만 채워서 보인다.
    /// 번호를 몰라도 "왼쪽 것", "위의 것"처럼 자리로 알아볼 수 있다. Windows 디스플레이 설정의 배치 그림과 같은 생각이다.
    ///
    /// 실제 놓인 방향을 그린 것이라 오른쪽에서 왼쪽으로 쓰는 언어에서도 뒤집지 않는다(모양 카드의 그림과 같다).
    /// 옆의 글자가 같은 것을 말하므로 화면 읽어주기에서는 건너뛴다.
    /// </summary>
    public sealed class DisplayLayoutIcon : FrameworkElement
    {
        public static readonly DependencyProperty DisplaysProperty = DependencyProperty.Register(
            nameof(Displays), typeof(IReadOnlyList<DisplayInfo>), typeof(DisplayLayoutIcon),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty DisplayProperty = DependencyProperty.Register(
            nameof(Display), typeof(DisplayInfo), typeof(DisplayLayoutIcon),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ForegroundProperty = DependencyProperty.Register(
            nameof(Foreground), typeof(Brush), typeof(DisplayLayoutIcon),
            new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

        // 다른 모니터는 테두리만, 조금 옅게 그린다. 이 칸의 모니터가 먼저 눈에 들어온다. 선택한 칸(강조 바탕에 흰 그림)에서도 그림 요소
        // 대비 3:1 을 넘도록 0.7 이다(0.6 이면 2.85:1, 0.7 이면 3.34:1, 선택하지 않은 칸은 6.03:1).
        private const double OtherOpacity = 0.7;
        private const double Stroke = 1.5;
        private const double Corner = 2.5;
        // 맞닿은 모니터 사이를 띄운다. 붙여 그리면 한 덩어리로 보인다.
        private const double Gap = 1.5;

        public DisplayLayoutIcon()
        {
            Width = 96;
            Height = 44;
            SnapsToDevicePixels = true;
            FlowDirection = FlowDirection.LeftToRight;
        }

        /// <summary>연결된 모니터 모두(칸의 차례).</summary>
        public IReadOnlyList<DisplayInfo>? Displays
        {
            get => (IReadOnlyList<DisplayInfo>?)GetValue(DisplaysProperty);
            set => SetValue(DisplaysProperty, value);
        }

        /// <summary>이 칸의 모니터. 채워서 그린다.</summary>
        public DisplayInfo? Display
        {
            get => (DisplayInfo?)GetValue(DisplayProperty);
            set => SetValue(DisplayProperty, value);
        }

        /// <summary>그림의 색. 칸의 글자색을 넘긴다(선택한 칸은 흰색).</summary>
        public Brush Foreground
        {
            get => (Brush)GetValue(ForegroundProperty);
            set => SetValue(ForegroundProperty, value);
        }

        protected override AutomationPeer OnCreateAutomationPeer() => new EasyIcon.HiddenPeer(this);

        protected override void OnRender(DrawingContext dc)
        {
            var displays = Displays;
            if (displays == null || displays.Count == 0) return;

            var boxes = OverlayDisplays.Layout(displays, ActualWidth, ActualHeight);
            var pen = new Pen(Foreground, Stroke);
            pen.Freeze();
            var own = Display;

            for (int i = 0; i < boxes.Count; i++)
            {
                var box = boxes[i];
                double width = box.Width - Gap * 2;
                double height = box.Height - Gap * 2;
                if (width <= 0 || height <= 0) continue;
                var rect = new Rect(box.X + Gap, box.Y + Gap, width, height);

                if (own != null && ReferenceEquals(displays[i], own))
                {
                    dc.DrawRoundedRectangle(Foreground, null, rect, Corner, Corner);
                }
                else
                {
                    // 선의 가운데가 네모의 가장자리에 오므로 선 두께의 절반만큼 안으로 들인다. 채운 네모와 바깥 크기가 같아진다.
                    rect.Inflate(-Stroke / 2, -Stroke / 2);
                    if (rect.Width <= 0 || rect.Height <= 0) continue;
                    dc.PushOpacity(OtherOpacity);
                    dc.DrawRoundedRectangle(null, pen, rect, Corner, Corner);
                    dc.Pop();
                }
            }
        }
    }
}
