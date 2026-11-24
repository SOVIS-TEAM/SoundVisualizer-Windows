// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using SoundVisualizer.Display;

namespace SoundVisualizer.Tests.Display;

/// <summary>
/// 연결된 모니터 목록을 잠깐 읽어 두는 규칙(#29). 모니터를 한 번 읽는 데 0.5~1ms 가 드는데, 런처는 설정 화면을 다시 그릴 때마다 세 번 묻고
/// 오버레이 편집 패널에서 슬라이더를 끄는 동안에는 그 일이 값이 바뀔 때마다 일어난다.
/// </summary>
public class BriefCacheTests
{
    private sealed class Source
    {
        public long Now;
        public int Reads;
        public bool Fail;

        public string Read()
        {
            if (Fail) throw new InvalidOperationException("읽지 못했습니다.");
            Reads++;
            return "값 " + Reads;
        }

        public BriefCache<string> Cache(long freshMilliseconds = 1000) => new(Read, freshMilliseconds, () => Now);
    }

    [Fact]
    public void 처음_물으면_읽고_잠깐_동안은_읽어_둔_것을_돌려준다()
    {
        var source = new Source { Now = 5000 };
        var cache = source.Cache();

        string first = cache.Get();
        Assert.Equal("값 1", first);

        source.Now = 5999;
        Assert.Same(first, cache.Get());
        Assert.Same(first, cache.Get());
        Assert.Equal(1, source.Reads);
    }

    [Fact]
    public void 정해_둔_시간이_지나면_다시_읽고_그때부터_다시_센다()
    {
        var source = new Source { Now = 5000 };
        var cache = source.Cache();
        cache.Get();

        source.Now = 6000;
        Assert.Equal("값 2", cache.Get());

        // 두 번째로 읽은 때(6000)부터 센다. 처음 읽은 때부터 세면 여기서 또 읽는다.
        source.Now = 6999;
        Assert.Equal("값 2", cache.Get());
        source.Now = 7000;
        Assert.Equal("값 3", cache.Get());
        Assert.Equal(3, source.Reads);
    }

    [Fact]
    public void 켜진_지_얼마_안_돼_시계가_0_근처여도_처음에는_읽는다()
    {
        // 읽은 적이 없는데 "방금 읽었다"고 여기면 안 된다.
        var source = new Source { Now = 0 };
        var cache = source.Cache();

        Assert.Equal("값 1", cache.Get());
        Assert.Equal(1, source.Reads);
    }

    [Fact]
    public void 다시_읽으라고_하면_바로_읽고_그_값을_잠깐_돌려준다()
    {
        var source = new Source { Now = 5000 };
        var cache = source.Cache();
        cache.Get();

        // 모니터 구성이 바뀌었다는 알림을 받았다. 읽은 지 얼마 안 됐어도 다시 읽는다.
        source.Now = 5010;
        string refreshed = cache.Refresh();
        Assert.Equal("값 2", refreshed);

        source.Now = 6009;
        Assert.Same(refreshed, cache.Get());
        source.Now = 6010;
        Assert.Equal("값 3", cache.Get());
    }

    [Fact]
    public void 읽은_적이_없어도_다시_읽으라고_할_수_있다()
    {
        var source = new Source { Now = 5000 };
        var cache = source.Cache();

        string refreshed = cache.Refresh();
        Assert.Equal("값 1", refreshed);
        Assert.Same(refreshed, cache.Get());
        Assert.Equal(1, source.Reads);
    }

    [Fact]
    public void 시계가_뒤로_가면_다시_읽는다()
    {
        var source = new Source { Now = 5000 };
        var cache = source.Cache();
        cache.Get();

        source.Now = 4000;
        Assert.Equal("값 2", cache.Get());
        Assert.Equal(2, source.Reads);
    }

    [Fact]
    public void 읽다가_실패하면_읽어_둔_것은_그대로이고_다음에_다시_읽는다()
    {
        var source = new Source { Now = 5000 };
        var cache = source.Cache();
        string first = cache.Get();

        source.Fail = true;
        source.Now = 5100;
        Assert.Throws<InvalidOperationException>(() => cache.Refresh());
        // 실패한 읽기는 읽어 둔 값도, 읽은 때도 바꾸지 않는다.
        Assert.Same(first, cache.Get());

        source.Fail = false;
        source.Now = 6000;
        Assert.Equal("값 2", cache.Get());
    }

    [Fact]
    public void 여러_스레드가_함께_물어도_한_번만_읽는다()
    {
        // 읽기가 오래 걸려야 여러 스레드가 '아직 읽은 값 없음'을 함께 본다. 그래서 읽기를 20ms 걸리게 하고 스레드를 한꺼번에 출발시킨다.
        // 잠금이 없거나 읽기를 잠금 밖에서 하면 여러 번 읽어 이 시험이 실패한다(바로 끝나는 읽기로는 잠금을 빼도 99% 넘게 통과했다).
        int reads = 0;
        var cache = new BriefCache<string>(() =>
        {
            Thread.Sleep(20);
            return "값 " + Interlocked.Increment(ref reads);
        }, 1000, () => 5000);

        const int Threads = 8;
        var results = new string[Threads];
        using var start = new Barrier(Threads);
        var threads = Enumerable.Range(0, Threads).Select(i => new Thread(() =>
        {
            start.SignalAndWait();
            results[i] = cache.Get();
        })).ToList();
        threads.ForEach(thread => thread.Start());
        Assert.All(threads, thread => Assert.True(thread.Join(TimeSpan.FromSeconds(10))));

        Assert.Equal(1, Volatile.Read(ref reads));
        Assert.All(results, result => Assert.Same(results[0], result));
    }
}
