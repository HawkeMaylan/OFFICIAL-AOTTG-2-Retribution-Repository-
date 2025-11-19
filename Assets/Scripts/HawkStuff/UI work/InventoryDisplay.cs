using UnityEngine;
using Characters;
using System.Collections.Generic;
using Settings;
using GameManagers;
public class InventoryDisplay : MonoBehaviour
{
    private bool _showInventory = false;
    private Human _localHuman;
    private HumanInventory _inventory;
    private HumanStats _stats;

    private void Update()
    {
        
        if (SettingsManager.InputSettings.Human.Inventory.GetKeyDown())

        {
            ToggleInventoryDisplay();
        }
        else if (_showInventory && Input.anyKeyDown)
        {
            _showInventory = false;
        }
    }

    private void ToggleInventoryDisplay()
    {
        _localHuman = FindLocalHuman();
        _inventory = _localHuman != null ? _localHuman.GetComponent<HumanInventory>() : null;
        _stats = _localHuman != null ? _localHuman.Stats : null;
        _showInventory = !_showInventory;
    }

    private void OnGUI()
    {
        if (!_showInventory || _inventory == null || _stats == null)
            return;

        // First, draw the stats panel (top fixed)
        float topX = 20f;
        float topY = 20f;
        float boxWidth = 220f;
        float statsBoxHeight = 130f;

        GUI.Box(new Rect(topX, topY, boxWidth, statsBoxHeight), "Stats");
        GUI.Label(new Rect(topX + 10, topY + 30, 200, 20), $"Speed: {_stats.Speed}");
        GUI.Label(new Rect(topX + 10, topY + 50, 200, 20), $"Gas: {_stats.Gas}");
        GUI.Label(new Rect(topX + 10, topY + 70, 200, 20), $"Ammo: {_stats.Ammunition}");
        GUI.Label(new Rect(topX + 10, topY + 90, 200, 20), $"Accel: {_stats.Acceleration}");
        GUI.Label(new Rect(topX + 10, topY + 110, 200, 20), $"Expertise: {_stats.Expertise}");
        GUI.Label(new Rect(topX + 10, topY + 130, 200, 20), $"HorseSpeed: {_stats.HorseSpeed}");

        // Then, draw the inventory panel below it
        List<string> items = _inventory.GetItemTypes();
        int itemCount = items.Count;
        int inventoryHeight = 30 + itemCount * 20;

        float inventoryY = topY + statsBoxHeight + 20;
        GUI.Box(new Rect(topX, inventoryY, boxWidth, inventoryHeight), "Inventory");

        for (int i = 0; i < itemCount; i++)
        {
            string item = items[i];
            int count = _inventory.GetItemCount(item);
            GUI.Label(new Rect(topX + 10, inventoryY + 20 + i * 20, 200, 20), $"{item}: {count}");
        }
    }

    private Human FindLocalHuman()
    {
        foreach (var human in FindObjectsOfType<Human>())
        {
            if (human != null && human.IsMine())
                return human;
        }
        return null;
    }
}
