using System;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager I;

    public void Awake()
    {
        if (I == null)
        {
            I = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
}