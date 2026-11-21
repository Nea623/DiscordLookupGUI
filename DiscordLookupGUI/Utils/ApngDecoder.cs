using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media.Imaging;

namespace DiscordLookup.Utils;

public class ApngFrame
{
    public BitmapSource Image { get; set; } = null!;
    public double DelayMs { get; set; }
}

/// <summary>
/// Discordのアバターデコレーション(APNG)を完全な透明度と色彩を維持したまま
/// 各フレーム独立したPNGとして超高速デコードする高精度パーサー
/// </summary>
public static class ApngDecoder
{
    private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
    private static readonly byte[] IendChunk = { 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82 };
    private static readonly uint[] CrcTable = InitializeCrcTable();

    private static uint[] InitializeCrcTable()
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint c = i;
            for (int j = 0; j < 8; j++)
            {
                if ((c & 1) != 0)
                    c = 0xedb88320u ^ (c >> 1);
                else
                    c >>= 1;
            }
            table[i] = c;
        }
        return table;
    }

    private static uint CalculateCrc(byte[] type, byte[] data, int dataOffset, int dataLength)
    {
        uint c = 0xffffffffu;
        for (int i = 0; i < 4; i++)
            c = CrcTable[(c ^ type[i]) & 0xff] ^ (c >> 8);
        for (int i = 0; i < dataLength; i++)
            c = CrcTable[(c ^ data[dataOffset + i]) & 0xff] ^ (c >> 8);
        return c ^ 0xffffffffu;
    }

    public static List<ApngFrame>? Decode(byte[] bytes)
    {
        if (bytes == null || bytes.Length < 8) return null;
        for (int i = 0; i < 8; i++)
            if (bytes[i] != PngSignature[i]) return null;

        int pos = 8;
        byte[]? ihdrChunk = null;
        var commonChunks = new List<byte[]>(); // PLTE, tRNS など

        var frameDataList = new List<(double delayMs, List<byte[]> idatChunks)>();
        double currentDelay = 40.0;
        List<byte[]>? currentIdatChunks = null;

        while (pos + 12 <= bytes.Length)
        {
            uint length = ((uint)bytes[pos] << 24) | ((uint)bytes[pos + 1] << 16) | ((uint)bytes[pos + 2] << 8) | bytes[pos + 3];
            string type = System.Text.Encoding.ASCII.GetString(bytes, pos + 4, 4);
            int dataStart = pos + 8;
            int nextPos = pos + 12 + (int)length;

            if (nextPos > bytes.Length) break;

            if (type == "IHDR")
            {
                ihdrChunk = new byte[12 + length];
                Array.Copy(bytes, pos, ihdrChunk, 0, ihdrChunk.Length);
            }
            else if (type == "PLTE" || type == "tRNS")
            {
                var chunk = new byte[12 + length];
                Array.Copy(bytes, pos, chunk, 0, chunk.Length);
                commonChunks.Add(chunk);
            }
            else if (type == "fcTL")
            {
                // 新しいフレームの開始
                if (dataStart + 24 <= bytes.Length)
                {
                    ushort delayNum = (ushort)((bytes[dataStart + 20] << 8) | bytes[dataStart + 21]);
                    ushort delayDen = (ushort)((bytes[dataStart + 22] << 8) | bytes[dataStart + 23]);
                    if (delayDen == 0) delayDen = 100;
                    currentDelay = (double)delayNum * 1000.0 / delayDen;
                    if (currentDelay < 10.0) currentDelay = 33.3; // 30FPS ~ 60FPS
                }

                currentIdatChunks = new List<byte[]>();
                frameDataList.Add((currentDelay, currentIdatChunks));
            }
            else if (type == "IDAT")
            {
                if (currentIdatChunks == null)
                {
                    currentIdatChunks = new List<byte[]>();
                    frameDataList.Add((currentDelay, currentIdatChunks));
                }
                var chunk = new byte[12 + length];
                Array.Copy(bytes, pos, chunk, 0, chunk.Length);
                currentIdatChunks.Add(chunk);
            }
            else if (type == "fdAT")
            {
                // 先頭4バイト(シーケンス番号)を除去して IDAT チャンクとして再構成
                if (length > 4 && currentIdatChunks != null)
                {
                    uint idatLength = length - 4;
                    byte[] idatChunk = new byte[12 + idatLength];

                    // Length
                    idatChunk[0] = (byte)(idatLength >> 24);
                    idatChunk[1] = (byte)(idatLength >> 16);
                    idatChunk[2] = (byte)(idatLength >> 8);
                    idatChunk[3] = (byte)idatLength;

                    // Type "IDAT"
                    idatChunk[4] = 0x49; // I
                    idatChunk[5] = 0x44; // D
                    idatChunk[6] = 0x41; // A
                    idatChunk[7] = 0x54; // T

                    // Data
                    Array.Copy(bytes, dataStart + 4, idatChunk, 8, (int)idatLength);

                    // CRC
                    byte[] idatTypeBytes = { 0x49, 0x44, 0x41, 0x54 };
                    uint crc = CalculateCrc(idatTypeBytes, idatChunk, 8, (int)idatLength);
                    idatChunk[8 + idatLength] = (byte)(crc >> 24);
                    idatChunk[9 + idatLength] = (byte)(crc >> 16);
                    idatChunk[10 + idatLength] = (byte)(crc >> 8);
                    idatChunk[11 + idatLength] = (byte)crc;

                    currentIdatChunks.Add(idatChunk);
                }
            }
            else if (type == "IEND")
            {
                break;
            }

            pos = nextPos;
        }

        if (ihdrChunk == null || frameDataList.Count <= 1) return null;

        var frames = new List<ApngFrame>();
        foreach (var (delay, idats) in frameDataList)
        {
            if (idats.Count == 0) continue;

            using var ms = new MemoryStream();
            ms.Write(PngSignature, 0, PngSignature.Length);
            ms.Write(ihdrChunk, 0, ihdrChunk.Length);
            foreach (var common in commonChunks)
                ms.Write(common, 0, common.Length);
            foreach (var idat in idats)
                ms.Write(idat, 0, idat.Length);
            ms.Write(IendChunk, 0, IendChunk.Length);

            ms.Position = 0;
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = ms;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();

            frames.Add(new ApngFrame { Image = bitmap, DelayMs = delay });
        }

        return frames.Count > 1 ? frames : null;
    }
}

/// <summary>
/// CompositionTarget.Rendering (60FPS/ディスプレイリフレッシュレート完全同期) により
/// CPU負荷ほぼゼロで極めて滑らかにAPNGアニメーションをループ再生する高精度コントローラー
/// </summary>
public class ApngAnimationController
{
    private readonly System.Windows.Controls.Image _target;
    private readonly List<ApngFrame> _frames;
    private readonly double _totalDurationMs;
    private readonly double[] _frameStartTimes;
    private readonly System.Diagnostics.Stopwatch _stopwatch = new();
    private int _lastFrameIndex = -1;
    private bool _isRunning;

    public ApngAnimationController(System.Windows.Controls.Image target, List<ApngFrame> frames)
    {
        _target = target;
        _frames = frames;
        _frameStartTimes = new double[frames.Count];
        double current = 0;
        for (int i = 0; i < frames.Count; i++)
        {
            _frameStartTimes[i] = current;
            current += Math.Max(16.0, frames[i].DelayMs);
        }
        _totalDurationMs = Math.Max(current, 16.0);
    }

    public void Start()
    {
        if (_isRunning || _frames.Count <= 1) return;
        _isRunning = true;
        _stopwatch.Restart();
        _lastFrameIndex = -1;
        System.Windows.Media.CompositionTarget.Rendering += OnRendering;
    }

    public void Stop()
    {
        if (!_isRunning) return;
        _isRunning = false;
        _stopwatch.Stop();
        System.Windows.Media.CompositionTarget.Rendering -= OnRendering;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        if (!_isRunning || _frames.Count == 0) return;

        double elapsed = _stopwatch.Elapsed.TotalMilliseconds % _totalDurationMs;
        int frameIndex = 0;
        for (int i = _frames.Count - 1; i >= 0; i--)
        {
            if (elapsed >= _frameStartTimes[i])
            {
                frameIndex = i;
                break;
            }
        }

        if (frameIndex != _lastFrameIndex)
        {
            _lastFrameIndex = frameIndex;
            _target.Source = _frames[frameIndex].Image;
        }
    }
}
