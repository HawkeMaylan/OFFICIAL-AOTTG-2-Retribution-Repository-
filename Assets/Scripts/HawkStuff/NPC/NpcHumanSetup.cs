
using UnityEngine;
using SimpleJSONFixed;
using System;
using System.Collections.Generic;

[ExecuteInEditMode]
public class NpcHumanSetup : MonoBehaviour
{
    [Header("References")]
    public Renderer chestRenderer;
    public Renderer legRenderer;
    public Renderer headRenderer;
    public Renderer hairRenderer;
    public GameObject hairObjectRoot;

    [Header("Editor Tools")]
    public bool randomizeOnStart = false;

    private JSONNode costumeInfo;
    private JSONNode hairInfo;

    private void Start()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying && randomizeOnStart)
        {
            ApplyRandomPresetToAssignedParts();
        }
#endif
    }

    [ContextMenu("Apply Random Costume To Body Parts")]
    public void ApplyRandomPresetToAssignedParts()
    {
        LoadCostumeInfo();
        if (costumeInfo == null || hairInfo == null)
        {
            Debug.LogWarning("Costume JSON not loaded.");
            return;
        }

        System.Random rand = new System.Random();
        bool male = rand.Next(0, 2) == 0;
        var costumeArray = costumeInfo[male ? "Male" : "Female"].AsArray;
        int costumeIndex = rand.Next(0, costumeArray.Count);
        var costume = costumeArray[costumeIndex];

        var hairArray = hairInfo[male ? "Male" : "Female"].AsArray;
        int hairIndex = rand.Next(0, hairArray.Count);
        var hair = hairArray[hairIndex];

        // Apply chest textures
        ApplyTexture(chestRenderer, costume["_main_tex"], "_MainTex");
        ApplyTexture(chestRenderer, costume["_main_tex_mask"], "_MaskTex");
        ApplyTexture(chestRenderer, costume["_color_tex"], "_ColorTex");

        // Apply leg texture
        ApplyTexture(legRenderer, costume["_pants_tex"], "_MainTex");

        // Apply skin to head
        ApplyTexture(headRenderer, "skin", "_MainTex");

        // Apply hair mesh (assumes hair mesh prefab already in hairObjectRoot)
        if (hairObjectRoot != null && hairRenderer != null)
        {
            string hairTex = hair["Texture"];
            ApplyTexture(hairRenderer, hairTex, "_MainTex");
        }
    }

    private void ApplyTexture(Renderer renderer, string textureName, string propName)
    {
        if (renderer == null || string.IsNullOrEmpty(textureName)) return;
        Texture tex = Resources.Load<Texture>("Textures/" + textureName);
        if (tex != null)
        {
            if (renderer.sharedMaterial == null)
                renderer.sharedMaterial = new Material(Shader.Find("Standard"));

            renderer.sharedMaterial.shader = Shader.Find("Standard");
            renderer.sharedMaterial.SetTexture(propName, tex);
        }
        else
        {
            Debug.LogWarning("Missing texture: " + textureName);
        }
    }

    private void LoadCostumeInfo()
    {
        TextAsset jsonAsset = Resources.Load<TextAsset>("Data/Info/CostumeInfo");
        if (jsonAsset != null)
        {
            var root = JSON.Parse(jsonAsset.text);
            costumeInfo = root["Costume"];
            hairInfo = root["Hair"];
        }
        else
        {
            Debug.LogError("CostumeInfo.json not found! Expected at Resources/Data/Info/CostumeInfo.json");
        }
    }
}
