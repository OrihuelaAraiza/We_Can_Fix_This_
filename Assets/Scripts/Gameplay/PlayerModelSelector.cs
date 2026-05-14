using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerModelSelector : MonoBehaviour
{
    [Header("Modelos visuales por jugador")]
    [SerializeField] private GameObject[] playerModels;

    public void SetModel(int playerIndex)
    {
        for (int i = 0; i < playerModels.Length; i++)
        {
            playerModels[i].SetActive(i == playerIndex);
        }
    }
}