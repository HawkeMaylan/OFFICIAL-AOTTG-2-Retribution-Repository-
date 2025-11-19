using System.Collections.Generic;
using UnityEngine;
using Weather;
using UI;
using Utility;
using CustomSkins;
using ApplicationManagers;
using System.Diagnostics;
using Photon;
using Map;
using CustomLogic;
using System.Collections;
using Characters;
using Settings;
using System.IO;

public class MemoryTestTimeController : MonoBehaviour
{
    [SerializeField] private float testTimeScale = 100f;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
        {
            Time.timeScale = testTimeScale;
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            Time.timeScale = 1f;
        }
    }
}