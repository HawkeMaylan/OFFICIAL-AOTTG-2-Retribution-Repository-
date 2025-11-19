using UnityEngine;
using Characters;
using Settings;

public class SimpleInteractZone : MonoBehaviour
{
    public Collider triggerZone;

    private Human localHuman;
    private bool isInside = false;
    private Transform humanTrigger;

    private static string currentPrompt = "";
    private static string extraPrompt = "";

    private void Update()
    {
        if (!isInside)
        {
            Human checkHuman = FindLocalHumanInZone();
            if (checkHuman != null)
            {
                localHuman = checkHuman;
                humanTrigger = localHuman.transform.Find("HumanTrigger");
                isInside = true;
            }
        }
        else if (localHuman == null || !IsStillInZone())
        {
            ClearPrompt();
            isInside = false;
            localHuman = null;
            humanTrigger = null;
        }

        if (isInside && localHuman != null)
        {
            currentPrompt = $"Press {SettingsManager.InputSettings.Interaction.Interact2} to Interact";
            extraPrompt = ""; // Optional, you can display context

            if (SettingsManager.InputSettings.Interaction.Interact2.GetKeyDown())
            {
                // >>> Call your custom function here <<<
                Debug.Log("Interaction triggered!");

                // Example:
                // MyFunction();
            }
        }
    }

    private Human FindLocalHumanInZone()
    {
        foreach (Human h in FindObjectsOfType<Human>())
        {
            if (h.photonView != null && h.photonView.IsMine)
            {
                Transform trigger = h.transform.Find("HumanTrigger");
                if (trigger != null && triggerZone.bounds.Contains(trigger.position))
                    return h;
            }
        }
        return null;
    }

    private bool IsStillInZone()
    {
        return humanTrigger != null && triggerZone.bounds.Contains(humanTrigger.position);
    }

    private void ClearPrompt()
    {
        currentPrompt = "";
        extraPrompt = "";
    }

    private void OnGUI()
    {
        if (!string.IsNullOrEmpty(currentPrompt))
        {
            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                alignment = TextAnchor.UpperCenter,
                wordWrap = false,
                normal = { textColor = Color.white }
            };

            float labelWidth = 600f;
            float labelHeight = 30f;
            float labelX = Screen.width / 2 - labelWidth / 2;

            GUI.Label(new Rect(labelX, 50, labelWidth, labelHeight), currentPrompt, style);

            if (!string.IsNullOrEmpty(extraPrompt))
                GUI.Label(new Rect(labelX, 85, labelWidth, labelHeight), extraPrompt, style);
        }
    }
}
