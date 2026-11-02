using System;
using System.Reflection;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

// Сеть без сцены и префабов в ассетах: менеджер и шаблон игрока создаются кодом при запуске.
// Игра сразу становится хостом на этом компьютере; с ключом запуска -join адрес подключается как клиент.
public static class NetworkBootstrap
{
    const string LocalAddress = "127.0.0.1";
    const string JoinArgument = "-join";
    const ushort FirstPort = 7777;
    const int PortAttempts = 10;

    // Хеш шаблона: обычно его считает редактор, а у созданного кодом он нулевой.
    // Значение одно на всех игроков, иначе клиент не поймёт, какой объект создаёт сервер.
    const uint PlayerPrefabHash = 0x46535431;

    public static GameObject PlayerPrefab { get; private set; }

    sealed class PrefabInstanceHandler : INetworkPrefabInstanceHandler
    {
        // Шаблон выключен, поэтому копия включается вручную, а иначе в ней не выполнился бы Awake.
        public NetworkObject Instantiate(ulong ownerClientId, Vector3 position, Quaternion rotation)
        {
            GameObject instance = UnityEngine.Object.Instantiate(PlayerPrefab, position, rotation);
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
        SetPrefabHash(playerPrefab.GetComponent<NetworkObject>());

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
        manager.PrefabHandler.AddHandler(playerPrefab, new PrefabInstanceHandler());

        string joinAddress = ReadJoinAddress();
        if (joinAddress != null)
        {
            transport.SetConnectionData(joinAddress, FirstPort);
            manager.StartClient();
            return;
        }

        StartHost(manager, transport);
    }

    // Порт занят, если игра уже открыта на этом компьютере: тогда берётся следующий.
    static void StartHost(NetworkManager manager, UnityTransport transport)
    {
        for (int attempt = 0; attempt < PortAttempts; attempt++)
        {
            transport.SetConnectionData(LocalAddress, (ushort)(FirstPort + attempt));
            if (manager.StartHost())
                return;
            manager.Shutdown();
        }
        Debug.LogError("Не удалось запустить сервер: все порты заняты.");
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

    static void SetPrefabHash(NetworkObject networkObject)
    {
        FieldInfo field = typeof(NetworkObject).GetField("GlobalObjectIdHash", BindingFlags.NonPublic | BindingFlags.Instance);
        if (field == null)
            throw new InvalidOperationException("В NetworkObject нет поля GlobalObjectIdHash: версия Netcode изменилась.");
        field.SetValue(networkObject, PlayerPrefabHash);
    }
}
