using Unity.Netcode;
using UnityEngine;

// Тестовый бот: неподвижный сетевой манекен рядом со стартом. Показывает, что сервер работает,
// а модель игрока видна со стороны.
public static class DummyBotSpawner
{
    static readonly Vector3 BotPosition = new Vector3(-2.5f, 0.9f, -7f);
    const float BotYaw = 40f;

    public static NetworkObject Spawn(GameObject playerPrefab)
    {
        GameObject bot = Object.Instantiate(playerPrefab, BotPosition, Quaternion.identity);
        bot.name = "DummyBot";
        bot.SetActive(true);

        var networkObject = bot.GetComponent<NetworkObject>();
        networkObject.Spawn();

        networkObject.GetComponent<PlayerAvatar>().SetBotPose(BotYaw);
        return networkObject;
    }
}
