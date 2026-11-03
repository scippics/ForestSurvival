using Unity.Netcode;
using UnityEngine;

// Кузьмич: сетевое тело без игрока у дровяника. С ним можно поговорить, см. NpcTalk.
public static class DummyBotSpawner
{
    const float BotYaw = 40f;

    public static NetworkObject Spawn(GameObject playerPrefab)
    {
        GameObject bot = Object.Instantiate(playerPrefab, Kordon.BotSpot, Quaternion.identity);
        bot.name = "Kuzmich";
        bot.SetActive(true);

        var networkObject = bot.GetComponent<NetworkObject>();
        networkObject.Spawn();

        networkObject.GetComponent<PlayerAvatar>().SetBotPose(BotYaw);
        return networkObject;
    }
}
