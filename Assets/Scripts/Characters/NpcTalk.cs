using System.Collections.Generic;
using UnityEngine;

// Разговор с Кузьмичом: идти и смотреть по сторонам можно и во время беседы, а отошёл — разговор окончен.
// Ответы зависят от стадии пролога, поэтому он всегда говорит про то, чем игрок сейчас занят.
public class NpcTalk : MonoBehaviour
{
    const float FaceDistance = 6f;
    const float TalkDistance = 4.5f;

    public static NpcTalk Current { get; private set; }

    PlayerAvatar avatar;
    PlayerInputReader input;
    Transform listener;
    readonly List<StoryText.Topic> topics = new List<StoryText.Topic>();

    void Awake()
    {
        avatar = GetComponent<PlayerAvatar>();
    }

    public void Open(PlayerInputReader reader, Transform player)
    {
        input = reader;
        listener = player;
        Current = this;

        ForestAudio.Play(ForestSound.Click, 0.2f);
        Show(StoryText.NpcGreeting(StoryDirector.Instance.SharedStage));
    }

    public void Close()
    {
        if (Current == this)
            Current = null;
        StoryUI.Instance.HideDialogue();
    }

    void Show(string line)
    {
        StoryText.NpcTopics(StoryDirector.Instance.SharedStage, topics);
        var options = new List<string>();
        foreach (StoryText.Topic topic in topics)
            options.Add(topic.Question);
        options.Add(StoryText.NpcLeave);
        StoryUI.Instance.ShowDialogue(StoryText.NpcName, line, options);
    }

    void Update()
    {
        Camera view = Camera.main;
        bool near = view != null && (view.transform.position - transform.position).sqrMagnitude < FaceDistance * FaceDistance;
        avatar.FaceTarget = near ? view.transform : null;

        if (Current != this)
            return;

        if (GameFlow.State != GameState.Playing || (listener.position - transform.position).sqrMagnitude > TalkDistance * TalkDistance)
        {
            Close();
            return;
        }

        int choice = input.ChoicePressed;
        if (choice <= 0 || choice > topics.Count + 1)
            return;

        if (choice == topics.Count + 1)
        {
            Close();
            StoryUI.Instance.Say(StoryText.NpcName + ": " + StoryText.NpcBye);
            return;
        }
        Show(topics[choice - 1].Answer);
    }
}
