// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
namespace SoundVisualizer.Display
{
    /// <summary>
    /// 읽는 데 시간이 드는 값을 잠깐 동안만 그대로 돌려준다(#29). 정해 둔 시간이 지나면 다음에 물을 때 다시 읽고,
    /// 값이 바뀌었다는 것을 아는 쪽은 <see cref="Refresh"/> 로 바로 다시 읽는다.
    ///
    /// 연결된 모니터 목록(<see cref="DisplayEnumerator"/>)에 쓴다. 여러 스레드에서 불러도 된다.
    /// </summary>
    internal sealed class BriefCache<T> where T : class
    {
        private readonly Func<T> _read;
        private readonly Func<long> _clock;
        private readonly long _freshMilliseconds;
        private readonly object _gate = new();
        private T? _value;
        private long _readAt;

        /// <param name="read">값을 읽는 일.</param>
        /// <param name="freshMilliseconds">읽은 값을 그대로 돌려주는 시간(밀리초).</param>
        /// <param name="clock">밀리초 시계. 주지 않으면 켜진 뒤 흐른 시간(<see cref="Environment.TickCount64"/>)이다.</param>
        public BriefCache(Func<T> read, long freshMilliseconds, Func<long>? clock = null)
        {
            _read = read;
            _freshMilliseconds = freshMilliseconds;
            _clock = clock ?? (() => Environment.TickCount64);
        }

        /// <summary>읽어 둔 값. 읽은 적이 없거나 정해 둔 시간이 지났으면 다시 읽는다.</summary>
        public T Get()
        {
            lock (_gate)
            {
                long now = _clock();
                long age = now - _readAt;
                // 시계가 뒤로 갔으면(age < 0) 읽어 둔 값이 얼마나 묵었는지 알 수 없다. 다시 읽는다.
                if (_value != null && age >= 0 && age < _freshMilliseconds) return _value;
                return Read(now);
            }
        }

        /// <summary>읽어 둔 값을 버리고 지금 다시 읽는다.</summary>
        public T Refresh()
        {
            lock (_gate)
            {
                return Read(_clock());
            }
        }

        private T Read(long now)
        {
            // 읽다가 실패하면(예외) 읽어 둔 값과 시각은 그대로다.
            T value = _read();
            _value = value;
            _readAt = now;
            return value;
        }
    }
}
