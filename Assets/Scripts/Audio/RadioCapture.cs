using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Debug = UnityEngine.Debug;

// Захват звука компьютера для магнитолы: Spotify, Яндекс.Музыка, браузер. Unity так не умеет, поэтому напрямую WASAPI.
// Режим «всё, кроме этой игры»: иначе в эфир ушли бы наш мотор и лес. Игра рассчитана на Windows 11 и Mac,
// поэтому запасного захвата для старых систем нет. На Mac захвата нет, там только приём.
public sealed class RadioCapture : IDisposable
{
    public const int OutputRate = 22050;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    public static bool IsSupported => true;

    const int DeviceRate = 44100;
    const int Channels = 2;
    const int BitsPerSample = 16;
    const long BufferDuration = 2000000;
    const int WaitMilliseconds = 100;
    const int ActivateTimeoutMilliseconds = 3000;
    const int MaxQueuedSamples = OutputRate * 2;
    const uint SilentFlag = 2;

    const uint StreamLoopback = 0x00020000;
    const uint StreamEventCallback = 0x00040000;
    const uint StreamAutoConvert = 0x80000000;
    const uint StreamDefaultQuality = 0x08000000;

    readonly object queueLock = new object();
    readonly Queue<short> queue = new Queue<short>();
    readonly Thread thread;
    volatile bool running = true;
    bool hasHalfSample;
    int halfSample;

    public string Error { get; private set; }

    public RadioCapture()
    {
        thread = new Thread(Run) { IsBackground = true, Name = "RadioCapture" };
        thread.SetApartmentState(ApartmentState.MTA);
        thread.Start();
    }

    public void Dispose()
    {
        running = false;
    }

    // Забирает накопленные отсчёты, не больше чем вмещает buffer.
    public int Read(short[] buffer)
    {
        lock (queueLock)
        {
            int count = Math.Min(buffer.Length, queue.Count);
            for (int i = 0; i < count; i++)
                buffer[i] = queue.Dequeue();
            return count;
        }
    }

    public int Available
    {
        get
        {
            lock (queueLock)
                return queue.Count;
        }
    }

    void Run()
    {
        IAudioClient client = null;
        IAudioCaptureClient capture = null;
        try
        {
            client = ActivateExcludingGame();
            if (client == null)
                throw new InvalidOperationException("система не дала захват без звука игры");

            var format = new WaveFormat
            {
                Tag = 1,
                Channels = Channels,
                SamplesPerSec = DeviceRate,
                AvgBytesPerSec = DeviceRate * Channels * BitsPerSample / 8,
                BlockAlign = Channels * BitsPerSample / 8,
                BitsPerSample = BitsPerSample,
                ExtraSize = 0
            };
            int result = client.Initialize(0, StreamLoopback | StreamEventCallback | StreamAutoConvert | StreamDefaultQuality, BufferDuration, 0, ref format, IntPtr.Zero);
            if (result < 0)
                Marshal.ThrowExceptionForHR(result);

            using (var wake = new AutoResetEvent(false))
            {
                client.SetEventHandle(wake.SafeWaitHandle.DangerousGetHandle());
                Guid service = typeof(IAudioCaptureClient).GUID;
                client.GetService(ref service, out object created);
                capture = (IAudioCaptureClient)created;
                client.Start();

                while (running)
                {
                    wake.WaitOne(WaitMilliseconds);
                    Drain(capture);
                }
                client.Stop();
            }
        }
        catch (Exception exception)
        {
            Error = exception.Message;
            Debug.LogWarning("Магнитола: захват звука не запустился: " + exception.Message);
        }
        finally
        {
            if (capture != null)
                Marshal.ReleaseComObject(capture);
            if (client != null)
                Marshal.ReleaseComObject(client);
        }
    }

    void Drain(IAudioCaptureClient capture)
    {
        capture.GetNextPacketSize(out uint frames);
        while (frames > 0 && running)
        {
            capture.GetBuffer(out IntPtr data, out uint read, out uint flags, out _, out _);
            Enqueue(data, read, (flags & SilentFlag) != 0);
            capture.ReleaseBuffer(read);
            capture.GetNextPacketSize(out frames);
        }
    }

    // Стерео в моно и вдвое меньшая частота: для магнитолы в кабине этого хватает, а в сеть уходит вчетверо меньше.
    void Enqueue(IntPtr data, uint frames, bool silent)
    {
        lock (queueLock)
        {
            for (int i = 0; i < frames; i++)
            {
                int mono = 0;
                if (!silent)
                    mono = (Marshal.ReadInt16(data, i * 4) + Marshal.ReadInt16(data, i * 4 + 2)) / 2;

                if (!hasHalfSample)
                {
                    halfSample = mono;
                    hasHalfSample = true;
                    continue;
                }
                hasHalfSample = false;
                queue.Enqueue((short)((halfSample + mono) / 2));
            }
            while (queue.Count > MaxQueuedSamples)
                queue.Dequeue();
        }
    }

    static IAudioClient ActivateExcludingGame()
    {
        IntPtr parameters = IntPtr.Zero, variant = IntPtr.Zero;
        try
        {
            // AUDIOCLIENT_ACTIVATION_PARAMS: тип, номер процесса, режим исключения.
            parameters = Marshal.AllocHGlobal(12);
            Marshal.WriteInt32(parameters, 0, 1);
            Marshal.WriteInt32(parameters, 4, Process.GetCurrentProcess().Id);
            Marshal.WriteInt32(parameters, 8, 1);

            // PROPVARIANT с типом BLOB: размер по смещению 8, указатель по смещению 16.
            variant = Marshal.AllocHGlobal(24);
            for (int i = 0; i < 24; i++)
                Marshal.WriteByte(variant, i, 0);
            Marshal.WriteInt16(variant, 0, 65);
            Marshal.WriteInt32(variant, 8, 12);
            Marshal.WriteIntPtr(variant, 16, parameters);

            var handler = new ActivationHandler();
            Guid clientId = typeof(IAudioClient).GUID;
            ActivateAudioInterfaceAsync("VAD\\Process_Loopback", ref clientId, variant, handler, out IActivateAudioInterfaceAsyncOperation operation);
            if (!handler.Done.WaitOne(ActivateTimeoutMilliseconds))
                return null;

            operation.GetActivateResult(out int result, out object created);
            return result >= 0 ? (IAudioClient)created : null;
        }
        catch (Exception exception)
        {
            Debug.Log("Магнитола: режим без звука игры недоступен: " + exception.Message);
            return null;
        }
        finally
        {
            if (variant != IntPtr.Zero)
                Marshal.FreeHGlobal(variant);
            if (parameters != IntPtr.Zero)
                Marshal.FreeHGlobal(parameters);
        }
    }

    [DllImport("Mmdevapi.dll", ExactSpelling = true, PreserveSig = false)]
    static extern void ActivateAudioInterfaceAsync(
        [MarshalAs(UnmanagedType.LPWStr)] string path,
        ref Guid iid,
        IntPtr parameters,
        IActivateAudioInterfaceCompletionHandler handler,
        out IActivateAudioInterfaceAsyncOperation operation);

    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    struct WaveFormat
    {
        public short Tag;
        public short Channels;
        public int SamplesPerSec;
        public int AvgBytesPerSec;
        public short BlockAlign;
        public short BitsPerSample;
        public short ExtraSize;
    }

    sealed class ActivationHandler : IActivateAudioInterfaceCompletionHandler, IAgileObject
    {
        public readonly ManualResetEvent Done = new ManualResetEvent(false);

        public void ActivateCompleted(IActivateAudioInterfaceAsyncOperation operation)
        {
            Done.Set();
        }
    }

    [ComImport, Guid("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioClient
    {
        [PreserveSig]
        int Initialize(int shareMode, uint flags, long bufferDuration, long periodicity, ref WaveFormat format, IntPtr session);
        void GetBufferSize(out uint frames);
        void GetStreamLatency(out long latency);
        void GetCurrentPadding(out uint frames);
        void IsFormatSupported(int shareMode, IntPtr format, out IntPtr closest);
        void GetMixFormat(out IntPtr format);
        void GetDevicePeriod(out long defaultPeriod, out long minimumPeriod);
        void Start();
        void Stop();
        void Reset();
        void SetEventHandle(IntPtr handle);
        void GetService(ref Guid iid, [MarshalAs(UnmanagedType.IUnknown)] out object service);
    }

    [ComImport, Guid("C8ADBD64-E71E-48A0-A4DE-185C395CD317"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioCaptureClient
    {
        void GetBuffer(out IntPtr data, out uint frames, out uint flags, out ulong position, out ulong timestamp);
        void ReleaseBuffer(uint frames);
        void GetNextPacketSize(out uint frames);
    }

    [ComImport, Guid("72A22D78-CDE4-431D-B8CC-843A71199B6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IActivateAudioInterfaceAsyncOperation
    {
        void GetActivateResult(out int result, [MarshalAs(UnmanagedType.IUnknown)] out object activated);
    }

    [ComImport, Guid("41D949AB-9862-444A-80F6-C261334DA5EB"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IActivateAudioInterfaceCompletionHandler
    {
        void ActivateCompleted(IActivateAudioInterfaceAsyncOperation operation);
    }

    // Пустой интерфейс-метка: без него система вызывает нас не из своего потока и активация зависает.
    [ComImport, Guid("94EA2B94-E9CC-49E0-C0FF-EE64CA8F5B90"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAgileObject
    {
    }
#else
    public static bool IsSupported => false;
    public string Error => "Захват доступен только на Windows";
    public int Available => 0;

    public int Read(short[] buffer)
    {
        return 0;
    }

    public void Dispose()
    {
    }
#endif
}

// IMA ADPCM: четыре бита на отсчёт вместо шестнадцати. В начале каждого чанка своё состояние декодера,
// поэтому потерянный по сети чанк даёт короткий пропуск, а не шум до конца песни.
public static class RadioCodec
{
    public const int HeaderBytes = 3;

    static readonly int[] IndexTable = { -1, -1, -1, -1, 2, 4, 6, 8, -1, -1, -1, -1, 2, 4, 6, 8 };
    static readonly int[] StepTable =
    {
        7, 8, 9, 10, 11, 12, 13, 14, 16, 17, 19, 21, 23, 25, 28, 31, 34, 37, 41, 45, 50, 55, 60, 66, 73, 80, 88, 97, 107, 118, 130, 143,
        157, 173, 190, 209, 230, 253, 279, 307, 337, 371, 408, 449, 494, 544, 598, 658, 724, 796, 876, 963, 1060, 1166, 1282, 1411, 1552,
        1707, 1878, 2066, 2272, 2499, 2749, 3024, 3327, 3660, 4026, 4428, 4871, 5358, 5894, 6484, 7132, 7845, 8630, 9493, 10442, 11487,
        12635, 13899, 15289, 16818, 18500, 20350, 22385, 24623, 27086, 29794, 32767
    };

    public static byte[] Encode(short[] samples, int count, ref int predictor, ref int index)
    {
        var data = new byte[HeaderBytes + (count + 1) / 2];
        data[0] = (byte)(predictor & 0xFF);
        data[1] = (byte)((predictor >> 8) & 0xFF);
        data[2] = (byte)index;

        for (int i = 0; i < count; i++)
        {
            int step = StepTable[index];
            int difference = samples[i] - predictor;
            int code = 0;
            if (difference < 0)
            {
                code = 8;
                difference = -difference;
            }

            int change = step >> 3;
            if (difference >= step)
            {
                code |= 4;
                difference -= step;
                change += step;
            }
            if (difference >= step >> 1)
            {
                code |= 2;
                difference -= step >> 1;
                change += step >> 1;
            }
            if (difference >= step >> 2)
            {
                code |= 1;
                change += step >> 2;
            }

            predictor = Clamp(predictor + ((code & 8) != 0 ? -change : change), short.MinValue, short.MaxValue);
            index = Clamp(index + IndexTable[code], 0, StepTable.Length - 1);
            data[HeaderBytes + i / 2] |= (byte)(i % 2 == 0 ? code : code << 4);
        }
        return data;
    }

    // Возвращает, сколько отсчётов записано.
    public static int Decode(byte[] data, float[] output)
    {
        if (data == null || data.Length <= HeaderBytes)
            return 0;

        int predictor = (short)(data[0] | data[1] << 8);
        int index = Clamp(data[2], 0, StepTable.Length - 1);
        int count = Math.Min((data.Length - HeaderBytes) * 2, output.Length);
        for (int i = 0; i < count; i++)
        {
            int packed = data[HeaderBytes + i / 2];
            int code = i % 2 == 0 ? packed & 15 : packed >> 4;
            int step = StepTable[index];

            int change = step >> 3;
            if ((code & 4) != 0)
                change += step;
            if ((code & 2) != 0)
                change += step >> 1;
            if ((code & 1) != 0)
                change += step >> 2;

            predictor = Clamp(predictor + ((code & 8) != 0 ? -change : change), short.MinValue, short.MaxValue);
            index = Clamp(index + IndexTable[code], 0, StepTable.Length - 1);
            output[i] = predictor / 32768f;
        }
        return count;
    }

    static int Clamp(int value, int min, int max)
    {
        return value < min ? min : value > max ? max : value;
    }
}
