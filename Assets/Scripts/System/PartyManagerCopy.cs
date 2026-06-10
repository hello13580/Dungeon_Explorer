using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public class PartyManagerCopy : MonoBehaviour
{
    public static PartyManagerCopy Instance { get; private set; }

    private List<Unit> partyUnits = new List<Unit>();

    private void Awake()
    {
        if (Instance != this && Instance != null)
        {
            Debug.Log("중복된 Party Manager 발견");
            Destroy(this.gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }


}
