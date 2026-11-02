using System.Collections.Generic;
using UnityEngine;

public class BlueprintNotebook : MonoBehaviour
{
    public List<string> unlockedRecipes = new List<string>();

    PlayerInputReader input;

    void Awake()
    {
        input = GetComponent<PlayerInputReader>();
    }

    void Update()
    {
        if (input.ConsumeNotebook())
            PrintRecipes();
    }

    public void AddRecipe(string recipeID)
    {
        if (!unlockedRecipes.Contains(recipeID))
            unlockedRecipes.Add(recipeID);
    }

    void PrintRecipes()
    {
        Debug.Log(unlockedRecipes.Count == 0
            ? "Блокнот пуст"
            : "Блокнот: " + string.Join(", ", unlockedRecipes));
    }
}
