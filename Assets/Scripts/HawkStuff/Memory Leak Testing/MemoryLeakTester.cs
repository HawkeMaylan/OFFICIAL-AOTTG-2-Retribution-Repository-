using UnityEngine;
using Photon.Pun;
using System.Collections.Generic;
using Characters;
using System.Collections;
using GameManagers;
using UI;
using System;

public class MemoryLeakTester : MonoBehaviour
{
    public KeyCode testKey = KeyCode.T;
    public float simulatedMinutesPerPress = 20f;
    public int targetFPS = 60;

    private bool testInProgress = false;
    private List<object> testObjects = new List<object>();

    void Update()
    {
        if (Input.GetKeyDown(testKey) && !testInProgress)
        {
            StartCoroutine(SimulateGameTime());
        }
    }

    IEnumerator SimulateGameTime()
    {
        testInProgress = true;

        // Calculate how many frames represent 20 minutes of gameplay
        int totalFrames = Mathf.RoundToInt(simulatedMinutesPerPress * 60 * targetFPS);
        Debug.Log($"Simulating {simulatedMinutesPerPress} minutes ({totalFrames} frames at {targetFPS} FPS)");

        long startMemory = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();

        for (int frame = 0; frame < totalFrames; frame++)
        {
            // Simulate one frame of your game's normal operations
            SimulateOneFrame();

            // Show progress
            if (frame % (totalFrames / 10) == 0)
            {
                float progress = (float)frame / totalFrames * 100f;
                Debug.Log($"Progress: {progress:F1}%");
            }

            yield return null; // Wait one real frame
        }

        // Check memory after simulation
        GC.Collect();
        long endMemory = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
        long memoryUsed = endMemory - startMemory;

        Debug.Log($"After {simulatedMinutesPerPress} simulated minutes: {memoryUsed / 1024 / 1024} MB memory increase");

        testInProgress = false;
    }

    void SimulateOneFrame()
    {
        // PUT YOUR ACTUAL GAME CODE HERE that would normally run each frame
        // This is where you'd call your suspect systems that might leak

        // Example: Simulate some object creation
        for (int i = 0; i < 10; i++)
        {
            var testObject = new TestData();
            testObjects.Add(testObject); // Potential leak if this list never clears
        }

        // Call your actual game systems that you suspect might leak:
        // - Your enemy spawn system
        // - Your UI management system  
        // - Your event system
        // - Your particle system
        // etc.
    }

    // Example test class
    class TestData
    {
        public int id;
        public string name;
        public float[] data = new float[50];
    }

    [ContextMenu("Clear Test")]
    public void ClearTest()
    {
        testObjects.Clear();
        GC.Collect();
    }
}