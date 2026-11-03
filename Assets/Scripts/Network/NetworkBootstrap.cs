using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using Debug = UnityEngine.Debug;

// Сеть без сцены и префабов в ассетах: менеджер и шаблон игрока создаются кодом при запуске.
// Игра сразу становится хостом на этом компьютере; с ключом запуска -join адрес подключается как клиент.
public static class NetworkBootstrap
{
    const string LocalAddress = "127.0.0.1";
    const string ListenAddress = "0.0.0.0";
    const string JoinArgument = "-join";
    const ushort FirstPort = 7777;
    const int PortAttempts = 10;

    // Хеш шаблона: обычно его считает редактор, а у созданного кодом он нулевой.
    // Значение одно на всех игроков, иначе клиент не поймёт, какой объект создаёт сервер.
    const uint PlayerPrefabHash = 0x46535431;
    const uint WorldPrefabHash = 0x46535432;

    public static GameObject PlayerPrefab { get; private set; }
    public static GameObject WorldPrefab { get; private set; }
    public static ushort HostPort { get; private set; } = FirstPort;

    sealed class PrefabInstanceHandler : INetworkPrefabInstanceHandler
    {
        readonly GameObject template;

        public PrefabInstanceHandler(GameObject template)
        {
            this.template = template;
        }

        // Шаблон выключен, поэтому копия включается вручную, а иначе в ней не выполнился бы Awake.
        public NetworkObject Instantiate(ulong ownerClientId, Vector3 position, Quaternion rotation)
        {
            GameObject instance = UnityEngine.Object.Instantiate(template, position, rotation);
            instance.SetActive(true);
            return instance.GetComponent<NetworkObject>();
        }

        public void Destroy(NetworkObject networkObject)
        {
            UnityEngine.Object.Destroy(networkObject.gameObject);
        }
    }

    public static void Start(GameObject playerPrefab)
    {
        PlayerPrefab = playerPrefab;
        SetPrefabHash(playerPrefab.GetComponent<NetworkObject>(), PlayerPrefabHash);

        WorldPrefab = new GameObject("WorldSync");
        WorldPrefab.SetActive(false);
        UnityEngine.Object.DontDestroyOnLoad(WorldPrefab);
        SetPrefabHash(WorldPrefab.AddComponent<NetworkObject>(), WorldPrefabHash);
        WorldPrefab.AddComponent<WorldSync>();
        WorldPrefab.AddComponent<RadioStreamNetworker>();

        var managerObject = new GameObject("NetworkManager");
        var manager = managerObject.AddComponent<NetworkManager>();
        var transport = managerObject.AddComponent<UnityTransport>();

        manager.NetworkConfig = new NetworkConfig
        {
            NetworkTransport = transport,
            EnableSceneManagement = false,
            ConnectionApproval = false,
            PlayerPrefab = playerPrefab
        };
        manager.AddNetworkPrefab(playerPrefab);
        manager.PrefabHandler.AddHandler(playerPrefab, new PrefabInstanceHandler(playerPrefab));
        manager.AddNetworkPrefab(WorldPrefab);
        manager.PrefabHandler.AddHandler(WorldPrefab, new PrefabInstanceHandler(WorldPrefab));

        string joinAddress = ReadJoinAddress();
        if (joinAddress != null)
        {
            ParseAddress(joinAddress, out string host, out ushort port);
            transport.SetConnectionData(host, port);
            manager.StartClient();

            // Хоста нет или он не пускает: вместо вечного ожидания игра запускается заново своим хостом.
            JoinScreen screen = new GameObject("JoinScreen").AddComponent<JoinScreen>();
            screen.Address = joinAddress;
            manager.OnClientDisconnectCallback += _ => { if (screen != null) screen.Fail(); };
            return;
        }

        StartHost(manager, transport);
    }

    // Порт занят, если игра уже открыта на этом компьютере: тогда берётся следующий.
    static void StartHost(NetworkManager manager, UnityTransport transport)
    {
        for (int attempt = 0; attempt < PortAttempts; attempt++)
        {
            HostPort = (ushort)(FirstPort + attempt);

            // Слушаем все сетевые карты: друзья подключаются по адресу этого компьютера.
            transport.SetConnectionData(LocalAddress, HostPort, ListenAddress);
            if (manager.StartHost())
                return;
            manager.Shutdown();
        }
        Debug.LogError("Не удалось запустить сервер: все порты заняты.");
    }

    static void ParseAddress(string address, out string host, out ushort port)
    {
        host = address;
        port = FirstPort;
        int colon = address.LastIndexOf(':');
        if (colon > 0 && ushort.TryParse(address.Substring(colon + 1), out ushort parsed))
        {
            host = address.Substring(0, colon);
            port = parsed;
        }
    }

    // В адресе только то, что бывает в имени хоста: строка уходит в аргументы запуска.
    public static bool IsValidAddress(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return false;
        ParseAddress(address.Trim(), out string host, out _);
        return Uri.CheckHostName(host) != UriHostNameType.Unknown;
    }

    // Сеть поднимается один раз при старте, поэтому к другу игра переходит перезапуском.
    public static void RelaunchAsClient(string address)
    {
        Relaunch(JoinArgument + " " + address.Trim());
    }

    public static void Relaunch(string arguments)
    {
        Process.Start(Process.GetCurrentProcess().MainModule.FileName, arguments);
        Application.Quit();
    }

    public static List<string> LocalAddresses()
    {
        var addresses = new List<string>();
        foreach (NetworkInterface card in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (card.OperationalStatus != OperationalStatus.Up || card.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                continue;
            foreach (UnicastIPAddressInformation entry in card.GetIPProperties().UnicastAddresses)
            {
                if (entry.Address.AddressFamily == AddressFamily.InterNetwork)
                    addresses.Add(entry.Address.ToString());
            }
        }
        return addresses;
    }

    static string ReadJoinAddress()
    {
        string[] arguments = Environment.GetCommandLineArgs();
        for (int i = 0; i + 1 < arguments.Length; i++)
        {
            if (arguments[i] == JoinArgument)
                return arguments[i + 1];
        }
        return null;
    }

    public static void SpawnWorld()
    {
        GameObject world = UnityEngine.Object.Instantiate(WorldPrefab);
        world.SetActive(true);
        world.GetComponent<NetworkObject>().Spawn();
    }

    static void SetPrefabHash(NetworkObject networkObject, uint hash)
    {
        FieldInfo field = typeof(NetworkObject).GetField("GlobalObjectIdHash", BindingFlags.NonPublic | BindingFlags.Instance);
        if (field == null)
            throw new InvalidOperationException("В NetworkObject нет поля GlobalObjectIdHash: версия Netcode изменилась.");
        field.SetValue(networkObject, hash);
    }
}

// Чёрный экран на время подключения: мир клиента появится только вместе с его игроком.
public class JoinScreen : MonoBehaviour
{
    const float FailSeconds = 4f;
    const float TimeoutSeconds = 25f;

    public string Address;

    float failedAt = -1f;
    Texture2D black;

    void Awake()
    {
        black = Texture2D.blackTexture;
    }

    public void Fail()
    {
        if (failedAt < 0f)
            failedAt = Time.unscaledTime;
    }

    void Update()
    {
        if (FindAnyObjectByType<MainMenu>() != null)
        {
            Destroy(gameObject);
            return;
        }

        if (Time.unscaledTime > TimeoutSeconds)
            Fail();

        if (failedAt >= 0f && Time.unscaledTime > failedAt + FailSeconds)
        {
            failedAt = float.MaxValue;
            NetworkBootstrap.Relaunch("");
        }
    }

    void OnGUI()
    {
        GUI.depth = -10;
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), black);
        var style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = Mathf.RoundToInt(Screen.height / 36f)
        };
        string text = failedAt < 0f ? "Подключение к " + Address + "..." : "Не удалось подключиться к " + Address + ". Возвращаюсь...";
        GUI.Label(new Rect(0f, 0f, Screen.width, Screen.height), text, style);
    }
}
