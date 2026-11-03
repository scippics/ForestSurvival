using Unity.Netcode;
using UnityEngine;

// Эфир магнитолы: водитель с включённой настройкой передаёт звук своего компьютера, а остальные слышат его из машины.
// Чанки идут к серверу и только от него ко всем: сервер пропускает лишь того, кто сейчас в эфире и сидит за рулём.
// Звук не надёжный: потерянный кусок лучше пропустить, чем ждать и копить задержку.
public class RadioStreamNetworker : NetworkBehaviour
{
    public const ulong NoBroadcaster = ulong.MaxValue;

    // 50 мс на чанк: 20 пакетов в секунду примерно по 550 байт, это около 90 кбит/с на слушателя.
    const int ChunkSamples = RadioCapture.OutputRate / 20;
    const int MaxChunkBytes = RadioCodec.HeaderBytes + ChunkSamples;
    const float SilenceLevel = 0.0003f;
    const float SilenceSeconds = 1f;

    public static RadioStreamNetworker Instance { get; private set; }

    public bool IsOnAir => IsSpawned && broadcaster.Value != NoBroadcaster;
    public bool IsLocalBroadcaster => IsSpawned && broadcaster.Value == NetworkManager.LocalClientId;
    public RadioPlayback Playback => playback;
    public RadioCapture Capture => capture;
    public int ChunksSent { get; private set; }
    public int ChunksReceived { get; private set; }

    readonly NetworkVariable<ulong> broadcaster = new NetworkVariable<ulong>(NoBroadcaster);
    readonly short[] chunk = new short[ChunkSamples];

    VehicleController car;
    RadioPlayback playback;
    RadioCapture capture;
    int predictor;
    int stepIndex;
    float silentFor;

    public override void OnNetworkSpawn()
    {
        Instance = this;
        car = FindAnyObjectByType<VehicleController>();
        playback = car.gameObject.AddComponent<RadioPlayback>();
    }

    public override void OnNetworkDespawn()
    {
        StopCapture();
        if (Instance == this)
            Instance = null;
    }

    // Удержание E на магнитоле. Без согласия в настройках трансляция не начнётся ни при каких условиях.
    public bool Toggle()
    {
        if (!IsSpawned)
            return false;
        if (IsLocalBroadcaster)
        {
            RequestBroadcastRpc(false);
            return true;
        }
        if (!GameSettings.AllowExternalAudioStreaming || !RadioCapture.IsSupported)
            return false;
        RequestBroadcastRpc(true);
        return true;
    }

    [Rpc(SendTo.Server)]
    void RequestBroadcastRpc(bool on, RpcParams rpc = default)
    {
        ulong sender = rpc.Receive.SenderClientId;
        if (!on)
        {
            if (broadcaster.Value == sender)
                broadcaster.Value = NoBroadcaster;
        }
        else if (WorldSync.Instance != null && WorldSync.Instance.DriverId == sender)
        {
            broadcaster.Value = sender;
        }
    }

    void Update()
    {
        if (!IsSpawned)
            return;

        // Водитель вышел или отключился: эфир заканчивается сам, чужой компьютер слушать незачем.
        if (IsServer && broadcaster.Value != NoBroadcaster && (WorldSync.Instance == null || WorldSync.Instance.DriverId != broadcaster.Value))
            broadcaster.Value = NoBroadcaster;

        car.RadioOnAir = IsOnAir;
        playback.SetActive(IsOnAir && !IsLocalBroadcaster);

        bool wanted = IsLocalBroadcaster && GameSettings.AllowExternalAudioStreaming;
        if (IsLocalBroadcaster && !GameSettings.AllowExternalAudioStreaming)
            RequestBroadcastRpc(false);

        if (wanted && capture == null)
        {
            capture = new RadioCapture();
            predictor = 0;
            stepIndex = 0;
        }
        else if (!wanted && capture != null)
        {
            StopCapture();
        }

        if (capture != null)
            SendCaptured();
    }

    void StopCapture()
    {
        if (capture != null)
            capture.Dispose();
        capture = null;
    }

    void SendCaptured()
    {
        while (capture.Available >= ChunkSamples)
        {
            int count = capture.Read(chunk);

            // Тишину не передаём: на паузе в плеере сеть не занята ничем.
            if (IsSilent(count))
                continue;

            byte[] data = RadioCodec.Encode(chunk, count, ref predictor, ref stepIndex);
            ChunksSent++;
            if (IsServer)
                RelayChunkRpc(data, RpcTarget.Not(NetworkManager.LocalClientId, RpcTargetUse.Temp));
            else
                ChunkToServerRpc(data);
        }
    }

    bool IsSilent(int count)
    {
        float peak = 0f;
        for (int i = 0; i < count; i++)
            peak = Mathf.Max(peak, Mathf.Abs(chunk[i] / 32768f));

        silentFor = peak < SilenceLevel ? silentFor + count / (float)RadioCapture.OutputRate : 0f;
        return silentFor > SilenceSeconds;
    }

    [Rpc(SendTo.Server, Delivery = RpcDelivery.Unreliable)]
    void ChunkToServerRpc(byte[] data, RpcParams rpc = default)
    {
        ulong sender = rpc.Receive.SenderClientId;
        if (sender != broadcaster.Value || data == null || data.Length > MaxChunkBytes)
            return;
        RelayChunkRpc(data, RpcTarget.Not(sender, RpcTargetUse.Temp));
    }

    [Rpc(SendTo.SpecifiedInParams, Delivery = RpcDelivery.Unreliable)]
    void RelayChunkRpc(byte[] data, RpcParams rpc)
    {
        ChunksReceived++;
        playback.Feed(data);
    }
}

// Голос магнитолы: настоящий 3D-источник на панели, поэтому снаружи машины звук слабеет с расстоянием.
// Чанки складываются в кольцевой буфер, а звуковой поток забирает их сам: небольшой запас скрывает неровности сети.
public class RadioPlayback : MonoBehaviour
{
    const int Rate = RadioCapture.OutputRate;
    const int RingSamples = Rate * 2;
    const int StartSamples = Rate * 2 / 5;
    const int MaxBufferedSamples = Rate;
    const float MinDistance = 2f;
    const float MaxDistance = 15f;
    static readonly Vector3 RadioPoint = new Vector3(0f, 0.222f, 0.72f);

    public int Underruns { get; private set; }
    public float Level { get; private set; }
    public AudioSource Source => source;

    readonly float[] ring = new float[RingSamples];
    readonly float[] decoded = new float[Rate / 10];
    readonly object ringLock = new object();
    AudioSource source;
    int readAt;
    int stored;
    bool buffering = true;

    void Awake()
    {
        var holder = new GameObject("RadioStream");
        holder.transform.SetParent(transform, false);
        holder.transform.localPosition = RadioPoint;

        source = holder.AddComponent<AudioSource>();
        source.clip = AudioClip.Create("RadioStream", Rate, 1, Rate, true, OnRead);
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = MinDistance;
        source.maxDistance = MaxDistance;
        source.dopplerLevel = 0f;
    }

    public void SetActive(bool active)
    {
        if (active && !source.isPlaying)
            source.Play();
        else if (!active && source.isPlaying)
            source.Stop();

        if (!active)
        {
            lock (ringLock)
            {
                stored = 0;
                buffering = true;
            }
        }
    }

    public void Feed(byte[] data)
    {
        int count = RadioCodec.Decode(data, decoded);
        lock (ringLock)
        {
            for (int i = 0; i < count; i++)
            {
                ring[(readAt + stored) % RingSamples] = decoded[i];
                if (stored < RingSamples)
                    stored++;
                else
                    readAt = (readAt + 1) % RingSamples;
            }

            // Часы двух компьютеров расходятся: лишний запас сбрасывается, иначе задержка росла бы.
            if (stored > MaxBufferedSamples)
            {
                int drop = stored - StartSamples;
                readAt = (readAt + drop) % RingSamples;
                stored -= drop;
            }
            if (buffering && stored >= StartSamples)
                buffering = false;
        }
    }

    // Вызывается из потока звука.
    void OnRead(float[] output)
    {
        float sum = 0f;
        lock (ringLock)
        {
            for (int i = 0; i < output.Length; i++)
            {
                if (buffering || stored == 0)
                {
                    if (!buffering)
                    {
                        buffering = true;
                        Underruns++;
                    }
                    output[i] = 0f;
                    continue;
                }
                float sample = ring[readAt];
                readAt = (readAt + 1) % RingSamples;
                stored--;
                output[i] = sample;
                sum += sample * sample;
            }
        }
        Level = output.Length > 0 ? Mathf.Sqrt(sum / output.Length) : 0f;
    }
}
