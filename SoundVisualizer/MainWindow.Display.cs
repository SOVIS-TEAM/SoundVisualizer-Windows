// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using SoundVisualizer.Display;
using SoundVisualizer.Ui;

namespace SoundVisualizer
{
    /// <summary>
    /// 오버레이를 표시할 모니터(#29). 설정에서 선택한 모니터에 오버레이 창을 놓는다. 선택한 적이 없거나 그 모니터가 연결되어 있지 않으면
    /// 주 모니터다(Core 의 OverlayDisplays). 모니터가 한 대뿐이면 아무것도 옮기지 않아 예전과 똑같이 뜬다.
    /// </summary>
    public partial class MainWindow
    {
        /// <summary>
        /// 창을 처음 띄울 때 선택한 모니터에 놓는다. 창 핸들이 생긴 뒤, 보이기 전이다. 클릭 관통 설정(ApplyClickThroughMagic) 다음에 부른다.
        /// </summary>
        private void InitOverlayDisplay()
        {
            SourceInitialized += (s, e) => MoveToSelectedDisplay();
        }

        /// <summary>
        /// 지금 선택과 지금 연결된 모니터에 맞춰 창을 옮긴다. 런처가 설정에서 모니터를 바꿨을 때와 모니터 구성이 바뀌었을 때 부른다.
        /// 이미 그 모니터에 있으면 아무 일도 하지 않는다.
        /// </summary>
        public void MoveToSelectedDisplay()
        {
            try
            {
                var display = OverlayDisplays.Resolve(DisplayEnumerator.Current(), AppSettings.OverlayDisplay);
                if (display != null) OverlayPlacement.MoveTo(this, display);
            }
            catch (Exception ex)
            {
                AppLog.Warn("display", "오버레이를 선택한 모니터로 옮기지 못했습니다.", ex);
            }
        }
    }
}
