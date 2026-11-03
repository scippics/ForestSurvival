using Unity.Netcode;
using UnityEngine;

// Тестовый бот: неподвижный сетевой манекен рядом со стартом. Показывает, что сервер работает,
// а модель игрока видна со стороны.
public static class DummyBotSpawner
{
    const float BotYaw = 40f;

    public static NetworkObject Spawn(GameObject playerPrefab)
    {
        GameObject bot = Object.Instantiate(playerPrefab, Kordon.BotSpot, Quaternion.identity);
        bot.name = "DummyBot";
        bot.SetActive(true);

        var networkObject = bot.GetComponent<NetworkObject>();
        networkObject.Spawn();

        networkObject.GetComponent<PlayerAvatar>().SetBotPose(BotYaw);
        return networkObject;
    }
}
