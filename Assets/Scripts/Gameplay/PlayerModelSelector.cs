using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerModelSelector : MonoBehaviour
{
    [Header("Visual models by player")]
    [SerializeField] private GameObject[] playerModels;

    public void SetModel(int playerIndex)
    {
        for (int i = 0; i < playerModels.Length; i++)
        {
            playerModels[i].SetActive(i == playerIndex);
        }
    }
}