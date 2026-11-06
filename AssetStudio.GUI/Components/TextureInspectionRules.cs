using System;
using System.Collections.Generic;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("AssetStudio.GUI.Tests")]

namespace AssetStudio.GUI
{
    internal enum TextureFilterRule
    {
        All,
        NonBlockCompressed,
        Large,
        ReadWriteEnabled,
        MipmapsEnabled,
        SuspectedNpotCompressionFallback
    }

    internal static class TextureInspectionRules
    {
        internal static readonly int[] LargeTextureSizePresets = { 1024, 2048, 4096 };

        private static readonly HashSet<TextureFormat> NonBlockCompressedFormats = new()
        {
            TextureFormat.RGBA32,
            TextureFormat.ARGB32,
            TextureFormat.RGB24,
            TextureFormat.RGBA4444,
            TextureFormat.ARGB4444,
            TextureFormat.RGB565,
            TextureFormat.R16,
            TextureFormat.RGBAHalf,
            TextureFormat.RGBAFloat,
            TextureFormat.BGRA32,
            TextureFormat.RG16,
            TextureFormat.R8
        };

        internal static bool Matches(
            TextureFilterRule rule,
            TextureFormat format,
            int width,
            int height,
            bool isReadable,
            bool mipMap,
            int mipCount,
            int largeTextureThreshold)
        {
            var hasMipmaps = mipMap || mipCount > 1;
            return rule switch
            {
                TextureFilterRule.All => true,
                TextureFilterRule.NonBlockCompressed => NonBlockCompressedFormats.Contains(format),
                TextureFilterRule.Large => width >= largeTextureThreshold || height >= largeTextureThreshold,
                TextureFilterRule.ReadWriteEnabled => isReadable,
                TextureFilterRule.MipmapsEnabled => hasMipmaps,
                TextureFilterRule.SuspectedNpotCompressionFallback =>
                    format == TextureFormat.RGBA32 &&
                    hasMipmaps &&
                    (!IsPowerOfTwo(width) || !IsPowerOfTwo(height)),
                _ => true
            };
        }

        internal static string GetDisplayName(TextureFilterRule rule)
        {
            return rule switch
            {
                TextureFilterRule.All => "全部纹理",
                TextureFilterRule.NonBlockCompressed => "非块压缩格式",
                TextureFilterRule.Large => "大尺寸",
                TextureFilterRule.ReadWriteEnabled => "Read/Write 已开启",
                TextureFilterRule.MipmapsEnabled => "Mipmaps 已开启",
                TextureFilterRule.SuspectedNpotCompressionFallback => "疑似 NPOT 压缩退化",
                _ => throw new ArgumentOutOfRangeException(nameof(rule), rule, null)
            };
        }

        internal static string GetDescription(TextureFilterRule rule, int largeTextureThreshold)
        {
            return rule switch
            {
                TextureFilterRule.All => "显示全部符合其他筛选条件的纹理",
                TextureFilterRule.NonBlockCompressed => "使用原始像素格式，可能增加包体或内存占用",
                TextureFilterRule.Large => $"宽或高任一边 ≥ {largeTextureThreshold}",
                TextureFilterRule.ReadWriteEnabled => "Unity Read/Write Enabled 已开启，可能保留 CPU 端副本",
                TextureFilterRule.MipmapsEnabled => "已启用或包含多个 Mipmap 层级",
                TextureFilterRule.SuspectedNpotCompressionFallback =>
                    "RGBA32、包含 Mipmaps，且宽或高不是 2 的幂",
                _ => throw new ArgumentOutOfRangeException(nameof(rule), rule, null)
            };
        }

        internal static bool IsPowerOfTwo(int value)
        {
            return value > 0 && (value & (value - 1)) == 0;
        }
    }
}
