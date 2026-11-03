using UnityEngine;

public enum GameState
{
    Menu,
    Playing,
    Paused,
    Cutscene
}

// Общее состояние игры: от него зависят ввод, курсор и подсказки.
public static class GameFlow
{
    public static GameState State { get; private set; } = GameState.Menu;

    public static event System.Action<GameState> StateChanged;

    public static void SetState(GameState state)
    {
        State = state;
        bool playing = state == GameState.Playing || state == GameState.Cutscene;
        Cursor.lockState = playing ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !playing;

        // В кооперативе ставить мир на паузу будет нельзя: у остальных игроков он идёт дальше.
        Time.timeScale = state == GameState.Paused ? 0f : 1f;
        StateChanged?.Invoke(state);
    }

    public static void TogglePause()
    {
        if (State == GameState.Playing)
            SetState(GameState.Paused);
        else if (State == GameState.Paused)
            SetState(GameState.Playing);
    }
}
