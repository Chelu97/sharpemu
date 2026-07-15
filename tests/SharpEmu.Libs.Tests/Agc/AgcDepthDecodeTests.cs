// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using SharpEmu.Libs.Agc;
using Xunit;

namespace SharpEmu.Libs.Tests.Agc;

// libSceAgc never writes depth-buffer registers through explicit context
// offsets. Depth state arrives as a positional "DepthRenderTarget" block of
// (0xFFFFFFFF, value) pairs inside the cx indirect register tables. These runs
// were captured from Quake (PPSA01880) traces: the 8192x8192 shadow atlas bind
// and the 1920x1080 main depth bind.
public sealed class AgcDepthDecodeTests
{
    [Fact]
    public void TryDecodeDepthRenderTargetRun_ShadowAtlasBind_DecodesBaseSizeAndNoClear()
    {
        // Captured run: shadow atlas 0x95200000, no stencil, HTILE 0xA5200000,
        // 8192x8192, DEPTH_CLEAR slot carries 0xFFFFFFFF (bind without clear).
        uint[] run =
        [
            0xFFF0FFF3, 0xFFFFFFFE,
            0x00952000, 0x00000000, 0x00952000, 0x00000000,
            0xFFFFFF00, 0xFFFFFF00, 0xFFFFFF00, 0xFFFFFF00, 0xFFFFFF00,
            0x3F001FFF, 0x00A52000, 0xDFFFDFFF, 0xFFFFFFFF, 0xFFFFFFFF,
        ];

        var matched = AgcExports.TryDecodeDepthRenderTargetRun(run, out var block, out var unbind);

        Assert.True(matched);
        Assert.False(unbind);
        Assert.Equal(0x00952000u, block.ZBase);
        Assert.Equal(0u, block.StencilBase);
        Assert.Equal(0x00A52000u, block.HtileBase);
        Assert.Equal(8192u, block.Width);
        Assert.Equal(8192u, block.Height);
        Assert.Equal(uint.MaxValue, block.ClearBits);
    }

    [Fact]
    public void TryDecodeDepthRenderTargetRun_MainDepthBind_DecodesStencilAndClear()
    {
        // Captured run: main depth 0x25B200000 with stencil 0xFFA00000, HTILE
        // 0x90780000, 1920x1080, DEPTH_CLEAR = 1.0f.
        uint[] run =
        [
            0xFFF0FFF3, 0xFFFFFFFF,
            0x025B2000, 0x00FFA000, 0x025B2000, 0x00FFA000,
            0xFFFFFF00, 0xFFFFFF00, 0xFFFFFF00, 0xFFFFFF00, 0xFFFFFF00,
            0x3F001FFF, 0x00907800, 0xC437C77F, 0x3F800000, 0xFFFFFF00,
        ];

        var matched = AgcExports.TryDecodeDepthRenderTargetRun(run, out var block, out var unbind);

        Assert.True(matched);
        Assert.False(unbind);
        Assert.Equal(0x025B2000u, block.ZBase);
        Assert.Equal(0x00FFA000u, block.StencilBase);
        Assert.Equal(0x00907800u, block.HtileBase);
        Assert.Equal(1920u, block.Width);
        Assert.Equal(1080u, block.Height);
        Assert.Equal(0x3F800000u, block.ClearBits);
    }

    [Fact]
    public void TryDecodeDepthRenderTargetRun_AllUnsetRun_ReportsUnbind()
    {
        var run = new uint[16];
        System.Array.Fill(run, uint.MaxValue);

        var matched = AgcExports.TryDecodeDepthRenderTargetRun(run, out _, out var unbind);

        Assert.False(matched);
        Assert.True(unbind);
    }

    [Fact]
    public void TryDecodeDepthRenderTargetRun_GuardBandRun_MatchesNothing()
    {
        // Captured guard-band block: two repeated floats plus a packed s16
        // pair. Must neither bind nor unbind the depth target.
        uint[] run =
        [
            0xFFFFFFFF, 0x477FFDFE, 0xFFFFFFFF, 0x477FFDFE, 0xFFFFFFFF, 0xFE00FE00,
        ];

        var matched = AgcExports.TryDecodeDepthRenderTargetRun(run, out _, out var unbind);

        Assert.False(matched);
        Assert.False(unbind);
    }

    [Fact]
    public void TryDecodeDepthRenderTargetRun_SizeSlotWithoutFlagBits_MatchesNothing()
    {
        // Same shape as a bind but the size slot lacks the 0xC000 flag bits in
        // both halves, so it cannot be a DepthRenderTarget block.
        uint[] run =
        [
            0xFFF0FFF3, 0xFFFFFFFE,
            0x00952000, 0x00000000, 0x00952000, 0x00000000,
            0xFFFFFF00, 0xFFFFFF00, 0xFFFFFF00, 0xFFFFFF00, 0xFFFFFF00,
            0x3F001FFF, 0x00A52000, 0x04370780, 0xFFFFFFFF, 0xFFFFFFFF,
        ];

        var matched = AgcExports.TryDecodeDepthRenderTargetRun(run, out _, out var unbind);

        Assert.False(matched);
        Assert.False(unbind);
    }
}
