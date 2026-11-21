using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DiscordLookup.Utils;

public class ApngFrame
{
    public BitmapSource Image { get; set; } = null!;
    public double DelayMs { get; set; }
}

/// <summary>
/// Discordのアバターデコレーション(APNG)を完全な透明度と色彩を維持したまま
/// 部分矩形や終盤フレームのオフセットも完全透過キャンバスにマッピングして
/// ノイズやチラつきを100%排除してデコードする高精度パーサー
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

    private class RawFrameData
    {
        public int Width;
        public int Height;
        public int XOffset;
        public int YOffset;
        public double DelayMs;
        public byte DisposeOp;
        public byte BlendOp;
        public List<byte[]> IdatChunks = new();
    }

    public static List<ApngFrame>? Decode(byte[] bytes)
    {
        if (bytes == null || bytes.Length < 8) return null;
        for (int i = 0; i < 8; i++)
            if (bytes[i] != PngSignature[i]) return null;

        int pos = 8;
        byte[]? ihdrChunk = null;
        int canvasWidth = 0;
        int canvasHeight = 0;
        var commonChunks = new List<byte[]>();

        var rawFrames = new List<RawFrameData>();
        RawFrameData? currentRawFrame = null;

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
                canvasWidth = (bytes[dataStart] << 24) | (bytes[dataStart + 1] << 16) | (bytes[dataStart + 2] << 8) | bytes[dataStart + 3];
                canvasHeight = (bytes[dataStart + 4] << 24) | (bytes[dataStart + 5] << 16) | (bytes[dataStart + 6] << 8) | bytes[dataStart + 7];
            }
            else if (type == "PLTE" || type == "tRNS")
            {
                var chunk = new byte[12 + length];
                Array.Copy(bytes, pos, chunk, 0, chunk.Length);
                commonChunks.Add(chunk);
            }
            else if (type == "fcTL")
            {
                int w = (bytes[dataStart + 4] << 24) | (bytes[dataStart + 5] << 16) | (bytes[dataStart + 6] << 8) | bytes[dataStart + 7];
                int h = (bytes[dataStart + 8] << 24) | (bytes[dataStart + 9] << 16) | (bytes[dataStart + 10] << 8) | bytes[dataStart + 11];
                int x = (bytes[dataStart + 12] << 24) | (bytes[dataStart + 13] << 16) | (bytes[dataStart + 14] << 8) | bytes[dataStart + 15];
                int y = (bytes[dataStart + 16] << 24) | (bytes[dataStart + 17] << 16) | (bytes[dataStart + 18] << 8) | bytes[dataStart + 19];

                ushort delayNum = (ushort)((bytes[dataStart + 20] << 8) | bytes[dataStart + 21]);
                ushort delayDen = (ushort)((bytes[dataStart + 22] << 8) | bytes[dataStart + 23]);
                if (delayDen == 0) delayDen = 100;
                double delayMs = (double)delayNum * 1000.0 / delayDen;
                if (delayMs < 10.0) delayMs = 33.3;

                byte disposeOp = bytes[dataStart + 24];
                byte blendOp = bytes[dataStart + 25];

                currentRawFrame = new RawFrameData
                {
                    Width = w,
                    Height = h,
                    XOffset = x,
                    YOffset = y,
                    DelayMs = delayMs,
                    DisposeOp = disposeOp,
                    BlendOp = blendOp
                };
                rawFrames.Add(currentRawFrame);
            }
            else if (type == "IDAT")
            {
                if (currentRawFrame != null)
                {
                    var chunk = new byte[12 + length];
                    Array.Copy(bytes, pos, chunk, 0, chunk.Length);
                    currentRawFrame.IdatChunks.Add(chunk);
                }
            }
            else if (type == "fdAT")
            {
                if (length > 4 && currentRawFrame != null)
                {
                    uint idatLength = length - 4;
                    byte[] idatChunk = new byte[12 + idatLength];

                    idatChunk[0] = (byte)(idatLength >> 24);
                    idatChunk[1] = (byte)(idatLength >> 16);
                    idatChunk[2] = (byte)(idatLength >> 8);
                    idatChunk[3] = (byte)idatLength;

                    idatChunk[4] = 0x49; // I
                    idatChunk[5] = 0x44; // D
                    idatChunk[6] = 0x41; // A
                    idatChunk[7] = 0x54; // T

                    Array.Copy(bytes, dataStart + 4, idatChunk, 8, (int)idatLength);

                    byte[] idatTypeBytes = { 0x49, 0x44, 0x41, 0x54 };
                    uint crc = CalculateCrc(idatTypeBytes, idatChunk, 8, (int)idatLength);
                    idatChunk[8 + idatLength] = (byte)(crc >> 24);
                    idatChunk[9 + idatLength] = (byte)(crc >> 16);
                    idatChunk[10 + idatLength] = (byte)(crc >> 8);
                    idatChunk[11 + idatLength] = (byte)crc;

                    currentRawFrame.IdatChunks.Add(idatChunk);
                }
            }
            else if (type == "IEND")
            {
                break;
            }

            pos = nextPos;
        }

        if (ihdrChunk == null || rawFrames.Count <= 1 || canvasWidth <= 0 || canvasHeight <= 0) return null;

        byte[] ihdrTypeBytes = { 0x49, 0x48, 0x44, 0x52 };
        var frames = new List<ApngFrame>();
        int canvasStride = canvasWidth * 4;
        byte[] canvasBuffer = new byte[canvasHeight * canvasStride];

        foreach (var rf in rawFrames)
        {
            if (rf.IdatChunks.Count == 0 || rf.Width <= 0 || rf.Height <= 0) continue;

            // 各フレーム固有の幅・高さに合わせた IHDR チャンクを作成
            byte[] frameIhdr = (byte[])ihdrChunk.Clone();
            frameIhdr[8] = (byte)(rf.Width >> 24);
            frameIhdr[9] = (byte)(rf.Width >> 16);
            frameIhdr[10] = (byte)(rf.Width >> 8);
            frameIhdr[11] = (byte)rf.Width;

            frameIhdr[12] = (byte)(rf.Height >> 24);
            frameIhdr[13] = (byte)(rf.Height >> 16);
            frameIhdr[14] = (byte)(rf.Height >> 8);
            frameIhdr[15] = (byte)rf.Height;

            // IHDR の CRC32 を再計算
            uint ihdrCrc = CalculateCrc(ihdrTypeBytes, frameIhdr, 8, 13);
            frameIhdr[21] = (byte)(ihdrCrc >> 24);
            frameIhdr[22] = (byte)(ihdrCrc >> 16);
            frameIhdr[23] = (byte)(ihdrCrc >> 8);
            frameIhdr[24] = (byte)ihdrCrc;

            BitmapSource frameBitmap;
            using (var ms = new MemoryStream())
            {
                ms.Write(PngSignature, 0, PngSignature.Length);
                ms.Write(frameIhdr, 0, frameIhdr.Length);
                foreach (var common in commonChunks)
                    ms.Write(common, 0, common.Length);
                foreach (var idat in rf.IdatChunks)
                    ms.Write(idat, 0, idat.Length);
                ms.Write(IendChunk, 0, IendChunk.Length);

                ms.Position = 0;
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.StreamSource = ms;
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();
                frameBitmap = bmp;
            }

            // DisposeOp == 2 (APNG_DISPOSE_OP_PREVIOUS) のため描画前のキャンバスを退避
            byte[]? previousBuffer = rf.DisposeOp == 2 ? (byte[])canvasBuffer.Clone() : null;

            // Bgra32 に正規化してピクセル抽出
            BitmapSource bgraFrame = frameBitmap.Format == PixelFormats.Bgra32
                ? frameBitmap
                : new FormatConvertedBitmap(frameBitmap, PixelFormats.Bgra32, null, 0);

            int frameStride = rf.Width * 4;
            byte[] framePixels = new byte[rf.Height * frameStride];
            bgraFrame.CopyPixels(framePixels, frameStride, 0);

            // キャンバスへ描画 (BlendOp: 0=SOURCE 上書き, 1=OVER アルファ合成)
            int maxY = Math.Min(rf.Height, canvasHeight - rf.YOffset);
            int maxX = Math.Min(rf.Width, canvasWidth - rf.XOffset);
            for (int y = 0; y < maxY; y++)
            {
                int dstY = rf.YOffset + y;
                if (dstY < 0) continue;
                int srcRow = y * frameStride;
                int dstRow = dstY * canvasStride;

                for (int x = 0; x < maxX; x++)
                {
                    int dstX = rf.XOffset + x;
                    if (dstX < 0) continue;
                    int sIdx = srcRow + x * 4;
                    int dIdx = dstRow + dstX * 4;

                    if (rf.BlendOp == 0)
                    {
                        canvasBuffer[dIdx] = framePixels[sIdx];
                        canvasBuffer[dIdx + 1] = framePixels[sIdx + 1];
                        canvasBuffer[dIdx + 2] = framePixels[sIdx + 2];
                        canvasBuffer[dIdx + 3] = framePixels[sIdx + 3];
                    }
                    else
                    {
                        byte sa = framePixels[sIdx + 3];
                        if (sa == 255)
                        {
                            canvasBuffer[dIdx] = framePixels[sIdx];
                            canvasBuffer[dIdx + 1] = framePixels[sIdx + 1];
                            canvasBuffer[dIdx + 2] = framePixels[sIdx + 2];
                            canvasBuffer[dIdx + 3] = 255;
                        }
                        else if (sa > 0)
                        {
                            byte da = canvasBuffer[dIdx + 3];
                            int outA = sa + (da * (255 - sa) + 127) / 255;
                            if (outA > 0)
                            {
                                int invSa = 255 - sa;
                                canvasBuffer[dIdx] = (byte)((framePixels[sIdx] * sa + canvasBuffer[dIdx] * da * invSa / 255) / outA);
                                canvasBuffer[dIdx + 1] = (byte)((framePixels[sIdx + 1] * sa + canvasBuffer[dIdx + 1] * da * invSa / 255) / outA);
                                canvasBuffer[dIdx + 2] = (byte)((framePixels[sIdx + 2] * sa + canvasBuffer[dIdx + 2] * da * invSa / 255) / outA);
                                canvasBuffer[dIdx + 3] = (byte)outA;
                            }
                        }
                    }
                }
            }

            // 合成済みキャンバスから不変の BitmapSource を生成して Freeze
            var finalImage = BitmapSource.Create(
                canvasWidth, canvasHeight, 96, 96, PixelFormats.Bgra32, null,
                (byte[])canvasBuffer.Clone(), canvasStride);
            finalImage.Freeze();
            frames.Add(new ApngFrame { Image = finalImage, DelayMs = rf.DelayMs });

            // 次フレームのための DisposeOp 適用
            if (rf.DisposeOp == 1)
            {
                // APNG_DISPOSE_OP_BACKGROUND: 描画領域を完全透明にクリア
                for (int y = 0; y < maxY; y++)
                {
                    int dstY = rf.YOffset + y;
                    if (dstY < 0) continue;
                    int clearStart = Math.Max(0, rf.XOffset);
                    int clearLen = maxX - (clearStart - rf.XOffset);
                    if (clearLen > 0)
                    {
                        Array.Clear(canvasBuffer, dstY * canvasStride + clearStart * 4, clearLen * 4);
                    }
                }
            }
            else if (rf.DisposeOp == 2 && previousBuffer != null)
            {
                // APNG_DISPOSE_OP_PREVIOUS: 描画前のキャンバスに戻す
                canvasBuffer = previousBuffer;
            }
        }

        return frames.Count > 1 ? frames : null;
    }

    /// <summary>
    /// STAバックグラウンドスレッドでAPNGの全フレームを非同期デコード・事前合成・Freezeして返し、
    /// UIスレッドの同期ブロック（フリーズ・ラグ）を完全にゼロにする非同期デコーダー
    /// </summary>
    public static Task<List<ApngFrame>?> DecodeAsync(byte[] bytes)
    {
        var tcs = new TaskCompletionSource<List<ApngFrame>?>();
        var thread = new Thread(() =>
        {
            try
            {
                var result = Decode(bytes);
                tcs.SetResult(result);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        return tcs.Task;
    }
}

/// <summary>
/// CompositionTarget.Rendering (60FPS/ディスプレイリフレッシュレート完全同期) により
/// CPU負荷ほぼゼロで極めて滑らかにAPNGアニメーションをループ再生する高精度コントローラー
/// </summary>
public class ApngAnimationController
{
    private readonly List<System.Windows.Controls.Image> _targets;
    private readonly List<ApngFrame> _frames;
    private readonly double _totalDurationMs;
    private readonly double[] _frameStartTimes;
    private readonly System.Diagnostics.Stopwatch _stopwatch = new();
    private int _lastFrameIndex = -1;
    private bool _isRunning;

    public ApngAnimationController(System.Windows.Controls.Image target, List<ApngFrame> frames)
        : this(new[] { target }, frames)
    {
    }

    public ApngAnimationController(IEnumerable<System.Windows.Controls.Image> targets, List<ApngFrame> frames)
    {
        _targets = targets.Where(t => t != null).ToList();
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
        CompositionTarget.Rendering += OnRendering;
    }

    public void Stop()
    {
        if (!_isRunning) return;
        _isRunning = false;
        _stopwatch.Stop();
        CompositionTarget.Rendering -= OnRendering;
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
            var frameImage = _frames[frameIndex].Image;
            foreach (var target in _targets)
            {
                target.Source = frameImage;
            }
        }
    }
}
