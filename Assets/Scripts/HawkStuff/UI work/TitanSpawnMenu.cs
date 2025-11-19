using UnityEngine;
using Photon.Pun;
using ApplicationManagers;
using GameManagers;
using Characters;
using System.Collections;
using System.IO;
using System;
using Settings;

public class TitanSpawnMenu : MonoBehaviourPun
{
    private bool menuOpen = false;

    private string[] titanTypes = new string[] { "Normal", "Abnormal", "Jumper", "Crawler", "Thrower", "Punk", "Aberrant", "Strider", "Twin", "Twitcher", "Sedentary" };
    private int selectedTypeIndex = 0;

    private string inputX = "0", inputY = "0", inputZ = "0", inputCount = "1";

    private bool useRandomArea = false;
    private string cornerAX = "0", cornerAY = "0", cornerAZ = "0";
    private string cornerBX = "0", cornerBY = "0", cornerBZ = "0";


    private bool useRandomWeights = false;
    private string[] weightInputs = new string[] { "10", "10", "10", "10", "10", "10", "10", "10", "10", "10", "10" };

    private bool overrideSize = false, overrideHP = false, overrideSpeed = false, overrideAnimSpeed = false;
    private string minSize = "1", maxSize = "1";
    private string minHP = "1000", maxHP = "2000";
    private string minSpeed = "10", maxSpeed = "20";
    private string minAnimSpeed = "1", maxAnimSpeed = "1.5";

    private bool overrideWalkSpeed = false, overrideTurnSpeed = false, overrideActionPause = false;
    private bool overrideTurnPause = false, overrideJumpForce = false, overrideRotateSpeed = false;
    private string minWalkSpeed = "5", maxWalkSpeed = "10";
    private string minTurnSpeed = "1", maxTurnSpeed = "3";
    private string minActionPause = "0.5", maxActionPause = "1";
    private string minTurnPause = "0.5", maxTurnPause = "1.2";
    private string minJumpForce = "100", maxJumpForce = "300";
    private string minRotateSpeed = "1.5", maxRotateSpeed = "4.0";

    private string presetName = "";

    private Vector2 presetScroll;
    private string[] savedPresetNames = new string[0];
    private int selectedPresetIndex = -1;

   



    [Serializable]
    private class TitanPreset
    {
        public bool useRandomWeights;
        public string[] weightInputs;
        public int selectedTypeIndex;
        public bool overrideSize, overrideHP, overrideSpeed, overrideAnimSpeed;
        public string minSize, maxSize, minHP, maxHP, minSpeed, maxSpeed, minAnimSpeed, maxAnimSpeed;
        public bool overrideWalkSpeed, overrideTurnSpeed, overrideActionPause, overrideTurnPause, overrideJumpForce, overrideRotateSpeed;
        public string minWalkSpeed, maxWalkSpeed, minTurnSpeed, maxTurnSpeed, minActionPause, maxActionPause;
        public string minTurnPause, maxTurnPause, minJumpForce, maxJumpForce, minRotateSpeed, maxRotateSpeed;
        public string inputX, inputY, inputZ;
        public bool useRandomArea;
        public string cornerAX, cornerAY, cornerAZ;
        public string cornerBX, cornerBY, cornerBZ;
        public string inputCount;
    }

    void Update()
    {
        if (SettingsManager.InputSettings.General.TitanSpawnMenu.GetKeyDown() && PhotonNetwork.IsMasterClient)
        {
            menuOpen = !menuOpen;
            Cursor.visible = menuOpen;
            Cursor.lockState = menuOpen ? CursorLockMode.None : CursorLockMode.Locked;
        }
    }

    void OnGUI()
    {
        if (!menuOpen) return;

        GUI.Box(new Rect(20, 20, 800, 1150), "Titan Spawn Menu");

        GUI.Label(new Rect(30, 60, 80, 20), "Position X:"); inputX = GUI.TextField(new Rect(120, 60, 100, 20), inputX);
        GUI.Label(new Rect(30, 90, 80, 20), "Position Y:"); inputY = GUI.TextField(new Rect(120, 90, 100, 20), inputY);
        GUI.Label(new Rect(30, 120, 80, 20), "Position Z:"); inputZ = GUI.TextField(new Rect(120, 120, 100, 20), inputZ);
        GUI.Label(new Rect(30, 150, 80, 20), "Count:"); inputCount = GUI.TextField(new Rect(120, 150, 100, 20), inputCount);

        useRandomArea = GUI.Toggle(new Rect(360, 60, 250, 20), useRandomArea, " Randomize Spawn Within Area");


        if (useRandomArea)
        {
            GUI.Label(new Rect(360, 90, 150, 20), "Corner A (X Y Z):");
            cornerAX = GUI.TextField(new Rect(510, 90, 50, 20), cornerAX);
            cornerAY = GUI.TextField(new Rect(570, 90, 50, 20), cornerAY);
            cornerAZ = GUI.TextField(new Rect(630, 90, 50, 20), cornerAZ);

            GUI.Label(new Rect(360, 120, 150, 20), "Corner B (X Y Z):");
            cornerBX = GUI.TextField(new Rect(510, 120, 50, 20), cornerBX);
            cornerBY = GUI.TextField(new Rect(570, 120, 50, 20), cornerBY);
            cornerBZ = GUI.TextField(new Rect(630, 120, 50, 20), cornerBZ);
        }



        useRandomWeights = GUI.Toggle(new Rect(30, 180, 400, 20), useRandomWeights, " Use Weighted Random");

        if (useRandomWeights)
        {
            GUI.Label(new Rect(300, 210, 400, 20), "Titan Type Weights (%):");
            for (int i = 0; i < titanTypes.Length; i++)
            {
                GUI.Label(new Rect(300, 240 + i * 25, 80, 20), titanTypes[i]);
                weightInputs[i] = GUI.TextField(new Rect(350, 240 + i * 25, 60, 20), weightInputs[i]);
            }
        }
        else
        {
            GUI.Label(new Rect(30, 210, 400, 20), "Titan Type:");
            selectedTypeIndex = GUI.SelectionGrid(new Rect(60, 240, 340, 60), selectedTypeIndex, titanTypes,4);
        }

        int baseY = 440;
        DrawOverride("Size", ref overrideSize, ref minSize, ref maxSize, baseY);
        DrawOverride("HP", ref overrideHP, ref minHP, ref maxHP, baseY += 55);
        DrawOverride("Run Speed", ref overrideSpeed, ref minSpeed, ref maxSpeed, baseY += 55);
        DrawOverride("Animation Speed", ref overrideAnimSpeed, ref minAnimSpeed, ref maxAnimSpeed, baseY += 55);
        DrawOverride("Walk Speed", ref overrideWalkSpeed, ref minWalkSpeed, ref maxWalkSpeed, baseY += 55);
        DrawOverride("Turn Speed", ref overrideTurnSpeed, ref minTurnSpeed, ref maxTurnSpeed, baseY += 55);
        DrawOverride("Action Pause", ref overrideActionPause, ref minActionPause, ref maxActionPause, baseY += 55);
        DrawOverride("Turn Pause", ref overrideTurnPause, ref minTurnPause, ref maxTurnPause, baseY += 55);
        DrawOverride("Jump Force", ref overrideJumpForce, ref minJumpForce, ref maxJumpForce, baseY += 55);
        DrawOverride("Rotate Speed", ref overrideRotateSpeed, ref minRotateSpeed, ref maxRotateSpeed, baseY += 55);

        if (GUI.Button(new Rect(240, 60, 100, 30), "Spawn"))
            TrySpawnTitans();

        // Preset name + save/load
        GUI.Label(new Rect(850, 60, 100, 20), "Preset Name:");
        presetName = GUI.TextField(new Rect(950, 60, 120, 20), presetName);

        if (GUI.Button(new Rect(850, 90, 90, 25), "Save Preset"))
        {
            SavePreset();
        }
        if (GUI.Button(new Rect(950, 90, 90, 25), "Load Preset"))
        {
            LoadPreset();
        }
        if (GUI.Button(new Rect(1050, 90, 90, 25), "Refresh List"))
        {
            RefreshPresetList();
        }


        GUI.Label(new Rect(850, 120, 150, 20), "Saved Presets:");
        presetScroll = GUI.BeginScrollView(
            new Rect(850, 145, 230, 200),
            presetScroll,
            new Rect(0, 0, 210, savedPresetNames.Length * 30)
        );



        for (int i = 0; i < savedPresetNames.Length; i++)
        {
            if (GUI.Button(new Rect(0, i * 30, 160, 25), savedPresetNames[i]))
            {
                presetName = savedPresetNames[i];
                selectedPresetIndex = i;
            }

            if (GUI.Button(new Rect(170, i * 30, 25, 25), "X"))
            {
                string path = Path.Combine(Application.persistentDataPath, savedPresetNames[i] + ".json");
                if (File.Exists(path)) File.Delete(path);
                RefreshPresetList();
                break;
            }
        }

        GUI.EndScrollView();



    }

    private void DrawOverride(string label, ref bool toggle, ref string min, ref string max, int y)
    {
        toggle = GUI.Toggle(new Rect(30, y, 200, 20), toggle, $" Override {label}");
        GUI.Label(new Rect(50, y + 25, 80, 20), "Min:"); min = GUI.TextField(new Rect(90, y + 25, 50, 20), min);
        GUI.Label(new Rect(150, y + 25, 80, 20), "Max:"); max = GUI.TextField(new Rect(190, y + 25, 50, 20), max);
    }

    private void TrySpawnTitans()
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            Debug.Log("Only Master Client can spawn titans.");
            return;
        }

        float x = 0f, y = 0f, z = 0f;
        int count = 1;

        float.TryParse(inputX, out x);
        float.TryParse(inputY, out y);
        float.TryParse(inputZ, out z);
        int.TryParse(inputCount, out count);

        Vector3 basePos = Vector3.zero;

        if (useRandomArea)
        {
            float.TryParse(cornerAX, out float x1); float.TryParse(cornerBX, out float x2);
            float.TryParse(cornerAY, out float y1); float.TryParse(cornerBY, out float y2);
            float.TryParse(cornerAZ, out float z1); float.TryParse(cornerBZ, out float z2);

            basePos = new Vector3(
                UnityEngine.Random.Range(Mathf.Min(x1, x2), Mathf.Max(x1, x2)),
                UnityEngine.Random.Range(Mathf.Min(y1, y2), Mathf.Max(y1, y2)),
                UnityEngine.Random.Range(Mathf.Min(z1, z2), Mathf.Max(z1, z2))
            );
        }
        else
        {
            basePos = new Vector3(x, y, z);
        }


        InGameManager manager = SceneLoader.CurrentGameManager as InGameManager;
        if (manager == null)
        {
            Debug.LogError("InGameManager not found.");
            return;
        }

        string typeToUse = useRandomWeights ? GetWeightedRandomType() : titanTypes[selectedTypeIndex];
        manager.StartCoroutine(SpawnAndOverrideRoutine(manager, typeToUse, count, basePos, 0f));
    }

    private IEnumerator SpawnAndOverrideRoutine(InGameManager manager, string fixedType, int count, Vector3 basePos, float rotationY)

    {
        for (int i = 0; i < count; i++)
        {
            Vector3 spawnPos = basePos;

            if (useRandomArea)
            {
                float.TryParse(cornerAX, out float x1); float.TryParse(cornerBX, out float x2);
                float.TryParse(cornerAY, out float y1); float.TryParse(cornerBY, out float y2);
                float.TryParse(cornerAZ, out float z1); float.TryParse(cornerBZ, out float z2);

                spawnPos = new Vector3(
                    UnityEngine.Random.Range(Mathf.Min(x1, x2), Mathf.Max(x1, x2)),
                    UnityEngine.Random.Range(Mathf.Min(y1, y2), Mathf.Max(y1, y2)),
                    UnityEngine.Random.Range(Mathf.Min(z1, z2), Mathf.Max(z1, z2))
                );
            }
            string type = useRandomWeights ? GetWeightedRandomType() : fixedType;

            BaseTitan titan = manager.SpawnAITitanAt(type, spawnPos, rotationY);

            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();

            if (titan == null)
            {
                Debug.LogError($"[TitanSpawnMenu] Failed to spawn titan of type: {type}");
                continue;
            }

            if (overrideSize && float.TryParse(minSize, out float minS) && float.TryParse(maxSize, out float maxS))
                titan.SetSize(UnityEngine.Random.Range(minS, maxS));
            if (overrideHP && int.TryParse(minHP, out int minHp) && int.TryParse(maxHP, out int maxHp))
                titan.SetHealth(UnityEngine.Random.Range(minHp, maxHp + 1));
            if (overrideSpeed && float.TryParse(minSpeed, out float minSpd) && float.TryParse(maxSpeed, out float maxSpd))
                titan.RunSpeedBase = UnityEngine.Random.Range(minSpd, maxSpd);
            if (overrideAnimSpeed && float.TryParse(minAnimSpeed, out float minAnim) && float.TryParse(maxAnimSpeed, out float maxAnim))
                titan.AttackSpeedMultiplier = UnityEngine.Random.Range(minAnim, maxAnim);
            if (overrideWalkSpeed && float.TryParse(minWalkSpeed, out float minWalk) && float.TryParse(maxWalkSpeed, out float maxWalk))
                titan.WalkSpeedBase = UnityEngine.Random.Range(minWalk, maxWalk);
            if (overrideTurnSpeed && float.TryParse(minTurnSpeed, out float minTurn) && float.TryParse(maxTurnSpeed, out float maxTurn))
                titan.TurnSpeed = UnityEngine.Random.Range(minTurn, maxTurn);
            if (overrideActionPause && float.TryParse(minActionPause, out float minActPause) && float.TryParse(maxActionPause, out float maxActPause))
                titan.ActionPause = UnityEngine.Random.Range(minActPause, maxActPause);
            if (overrideTurnPause && float.TryParse(minTurnPause, out float minTPause) && float.TryParse(maxTurnPause, out float maxTPause))
                titan.TurnPause = UnityEngine.Random.Range(minTPause, maxTPause);
            if (overrideJumpForce && float.TryParse(minJumpForce, out float minJump) && float.TryParse(maxJumpForce, out float maxJump))
                titan.JumpForce = UnityEngine.Random.Range(minJump, maxJump);
            if (overrideRotateSpeed && float.TryParse(minRotateSpeed, out float minRot) && float.TryParse(maxRotateSpeed, out float maxRot))
                titan.RotateSpeed = UnityEngine.Random.Range(minRot, maxRot);
        }
    }

    private string GetWeightedRandomType()
    {
        float[] weights = new float[titanTypes.Length];
        float totalWeight = 0f;

        for (int i = 0; i < titanTypes.Length; i++)
        {
            if (!float.TryParse(weightInputs[i], out float w)) w = 0f;
            weights[i] = Mathf.Max(0, w);
            totalWeight += weights[i];
        }

        if (totalWeight <= 0f)
            return "Normal";

        float rand = UnityEngine.Random.Range(0f, totalWeight);
        float cumulative = 0f;

        for (int i = 0; i < weights.Length; i++)
        {
            cumulative += weights[i];
            if (rand <= cumulative)
                return titanTypes[i];
        }

        return titanTypes[0];
    }

    private void SavePreset()
    {
        if (string.IsNullOrWhiteSpace(presetName)) return;

        TitanPreset preset = new TitanPreset
        {
            useRandomWeights = useRandomWeights,
            weightInputs = (string[])weightInputs.Clone(),
            selectedTypeIndex = selectedTypeIndex,
            overrideSize = overrideSize,
            minSize = minSize,
            maxSize = maxSize,
            overrideHP = overrideHP,
            minHP = minHP,
            maxHP = maxHP,
            overrideSpeed = overrideSpeed,
            minSpeed = minSpeed,
            maxSpeed = maxSpeed,
            overrideAnimSpeed = overrideAnimSpeed,
            minAnimSpeed = minAnimSpeed,
            maxAnimSpeed = maxAnimSpeed,
            overrideWalkSpeed = overrideWalkSpeed,
            minWalkSpeed = minWalkSpeed,
            maxWalkSpeed = maxWalkSpeed,
            overrideTurnSpeed = overrideTurnSpeed,
            minTurnSpeed = minTurnSpeed,
            maxTurnSpeed = maxTurnSpeed,
            overrideActionPause = overrideActionPause,
            minActionPause = minActionPause,
            maxActionPause = maxActionPause,
            overrideTurnPause = overrideTurnPause,
            minTurnPause = minTurnPause,
            maxTurnPause = maxTurnPause,
            overrideJumpForce = overrideJumpForce,
            minJumpForce = minJumpForce,
            maxJumpForce = maxJumpForce,
            overrideRotateSpeed = overrideRotateSpeed,
            minRotateSpeed = minRotateSpeed,
            maxRotateSpeed = maxRotateSpeed,
            inputX = this.inputX,
            inputY = this.inputY,
            inputZ = this.inputZ,
            useRandomArea = this.useRandomArea,
            cornerAX = this.cornerAX,
            cornerAY = this.cornerAY,
            cornerAZ = this.cornerAZ,
            cornerBX = this.cornerBX,
            cornerBY = this.cornerBY,
            cornerBZ = this.cornerBZ,
            inputCount = this.inputCount,


        };

        string json = JsonUtility.ToJson(preset, true);
        File.WriteAllText(Path.Combine(Application.persistentDataPath, $"{presetName}.json"), json);
        RefreshPresetList();

        Debug.Log("Preset saved: " + presetName);
    }

    private void LoadPreset()
    {
        string path = Path.Combine(Application.persistentDataPath, $"{presetName}.json");
        if (!File.Exists(path)) return;

        string json = File.ReadAllText(path);
        TitanPreset preset = JsonUtility.FromJson<TitanPreset>(json);

        useRandomWeights = preset.useRandomWeights;
        weightInputs = (string[])preset.weightInputs.Clone();
        selectedTypeIndex = preset.selectedTypeIndex;
        overrideSize = preset.overrideSize; minSize = preset.minSize; maxSize = preset.maxSize;
        overrideHP = preset.overrideHP; minHP = preset.minHP; maxHP = preset.maxHP;
        overrideSpeed = preset.overrideSpeed; minSpeed = preset.minSpeed; maxSpeed = preset.maxSpeed;
        overrideAnimSpeed = preset.overrideAnimSpeed; minAnimSpeed = preset.minAnimSpeed; maxAnimSpeed = preset.maxAnimSpeed;
        overrideWalkSpeed = preset.overrideWalkSpeed; minWalkSpeed = preset.minWalkSpeed; maxWalkSpeed = preset.maxWalkSpeed;
        overrideTurnSpeed = preset.overrideTurnSpeed; minTurnSpeed = preset.minTurnSpeed; maxTurnSpeed = preset.maxTurnSpeed;
        overrideActionPause = preset.overrideActionPause; minActionPause = preset.minActionPause; maxActionPause = preset.maxActionPause;
        overrideTurnPause = preset.overrideTurnPause; minTurnPause = preset.minTurnPause; maxTurnPause = preset.maxTurnPause;
        overrideJumpForce = preset.overrideJumpForce; minJumpForce = preset.minJumpForce; maxJumpForce = preset.maxJumpForce;
        overrideRotateSpeed = preset.overrideRotateSpeed; minRotateSpeed = preset.minRotateSpeed; maxRotateSpeed = preset.maxRotateSpeed;
        inputX = preset.inputX;
        inputY = preset.inputY;
        inputZ = preset.inputZ;
        useRandomArea = preset.useRandomArea;
        cornerAX = preset.cornerAX;
        cornerAY = preset.cornerAY;
        cornerAZ = preset.cornerAZ;
        cornerBX = preset.cornerBX;
        cornerBY = preset.cornerBY;
        cornerBZ = preset.cornerBZ;
        inputCount = preset.inputCount;




        Debug.Log("Preset loaded: " + presetName);
    }

    private void RefreshPresetList()
    {
        string folder = Application.persistentDataPath;
        string[] files = Directory.GetFiles(folder, "*.json");
        savedPresetNames = new string[files.Length];

        for (int i = 0; i < files.Length; i++)
            savedPresetNames[i] = Path.GetFileNameWithoutExtension(files[i]);
    }

}
