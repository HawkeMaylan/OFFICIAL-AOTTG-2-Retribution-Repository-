using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using Characters;
using Settings;
using GameManagers;
using ApplicationManagers;
using System.Collections;
using System.Collections.Generic;

public class BladePickup : MonoBehaviourPunCallbacks, IPunObservable
{
    [Header("Blade Pickup Settings")]
    public Collider triggerZone;
    public int bladePickup = 4;
    public float cooldownDuration = 10f;
    public int maxGrants = 3;

    [Header("Object Cleanup")]
    public bool destroyWhenEmpty = false;
    public float shrinkAndDestroyTime = 1.5f;

    private Human localHuman;
    private static string currentPrompt = "";
    private static string extraPrompt = "";

    [SerializeField]
    private float lastGrantTime = -999f;
    private int grantsUsed = 0;
    private bool isInside = false;
    private bool isShrinking = false;

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(grantsUsed);
            stream.SendNext(lastGrantTime);
            stream.SendNext(isShrinking);
        }
        else
        {
            grantsUsed = (int)stream.ReceiveNext();
            lastGrantTime = (float)stream.ReceiveNext();
            isShrinking = (bool)stream.ReceiveNext();
        }
    }

    private void Update()
    {
        if (ChatManager.IsChatActive() || isShrinking)
            return;

        // Only check if we lost track of the human
        if (isInside && (localHuman == null || !IsHumanValid(localHuman)))
        {
            ClearPrompt();
            isInside = false;
            localHuman = null;
            return;
        }

        if (isInside && localHuman != null)
        {
            UpdatePromptAndInput();
        }
    }

    private void UpdatePromptAndInput()
    {
        int remaining = maxGrants - grantsUsed;

        if (remaining <= 0)
        {
            currentPrompt = "No Blades remaining";
            extraPrompt = "";
            return;
        }

        int bladesNeeded = GetBladesNeeded(localHuman);
        if (bladesNeeded <= 0)
        {
            currentPrompt = "Blades at maximum capacity";
            extraPrompt = "";
            return;
        }

        int actualPickup = Mathf.Min(bladePickup, bladesNeeded);
        extraPrompt = $"Blade pickups left: {remaining}";

        float timeSinceLast = Time.time - lastGrantTime;
        if (timeSinceLast < cooldownDuration)
        {
            float timeLeft = Mathf.Ceil(cooldownDuration - timeSinceLast);
            currentPrompt = $"Pickup on cooldown ({timeLeft}s)";
        }
        else
        {
            currentPrompt = $"Press {SettingsManager.InputSettings.Interaction.Interact2} to Collect Blades: +{actualPickup}";

            if (SettingsManager.InputSettings.Interaction.Interact2.GetKeyDown())
            {
                TryGrantBlades();
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isShrinking) return;

        Human human = other.GetComponentInParent<Human>();
        if (human != null && human.photonView.IsMine)
        {
            localHuman = human;
            isInside = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        Human human = other.GetComponentInParent<Human>();
        if (human != null && human == localHuman)
        {
            ClearPrompt();
            isInside = false;
            localHuman = null;
        }
    }

    private void TryGrantBlades()
    {
        if (isShrinking) return;

        // Check local conditions first
        if (grantsUsed >= maxGrants || (Time.time - lastGrantTime) < cooldownDuration)
            return;

        if (localHuman == null)
            return;

        int bladesNeeded = GetBladesNeeded(localHuman);
        if (bladesNeeded <= 0)
        {
            Debug.Log("Player already at maximum blade capacity");
            return;
        }

        int actualPickup = Mathf.Min(bladePickup, bladesNeeded);

        // Apply blades locally to the interacting player
        AddBladesToHuman(localHuman, actualPickup);

        // Only consume a grant if we gave the full bladePickup amount
        if (actualPickup == bladePickup)
        {
            // Update the shared state via RPC
            photonView.RPC("RPC_UpdateGrants", RpcTarget.All, grantsUsed + 1, Time.time);

            // Handle destruction if empty
            if ((grantsUsed + 1) >= maxGrants && destroyWhenEmpty)
            {
                if (photonView.IsMine) // Only the owner handles destruction
                {
                    photonView.RPC("RPC_StartShrinkAndDestroy", RpcTarget.All);
                }
                else
                {
                    // Request master client to take over and destroy
                    photonView.RPC("RPC_RequestTakeoverAndDestroy", RpcTarget.MasterClient);
                }
            }
        }
        else
        {
            Debug.Log($"Partial pickup: {actualPickup}/{bladePickup} blades given (no grant consumed)");
        }
    }

    [PunRPC]
    private void RPC_UpdateGrants(int newGrantsUsed, float newLastGrantTime)
    {
        grantsUsed = newGrantsUsed;
        lastGrantTime = newLastGrantTime;
    }

    [PunRPC]
    private void RPC_RequestTakeoverAndDestroy()
    {
        // Master client takes ownership and starts destruction
        if (!photonView.IsMine)
            photonView.TransferOwnership(PhotonNetwork.LocalPlayer);

        // Start destruction process
        if (!isShrinking)
            StartCoroutine(TakeoverAndDestroy());
    }

    private IEnumerator TakeoverAndDestroy()
    {
        // Wait a frame for ownership transfer to complete
        yield return null;

        // Now we should own the object, start the destruction process
        photonView.RPC("RPC_StartShrinkAndDestroy", RpcTarget.All);
    }

    [PunRPC]
    private void RPC_StartShrinkAndDestroy()
    {
        if (!isShrinking)
            StartCoroutine(ShrinkAndDestroy());
    }

    private int GetBladesNeeded(Human human)
    {
        if (human.Weapon == null)
            return bladePickup;

        if (human.Weapon is BladeWeapon bladeWeapon)
        {
            return Mathf.Max(0, bladeWeapon.MaxBlades - bladeWeapon.BladesLeft);
        }

        // Reflection fallback (cached for performance)
        var weaponType = human.Weapon.GetType();
        string[] bladeCountNames = { "BladesLeft", "bladesLeft", "BladeCount", "bladeCount" };
        string[] maxBladeNames = { "MaxBlades", "maxBlades", "MaxBladeCount", "maxBladeCount" };

        foreach (var fieldName in bladeCountNames)
        {
            var field = weaponType.GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (field != null && field.FieldType == typeof(int))
            {
                int currentBlades = (int)field.GetValue(human.Weapon);
                foreach (var maxFieldName in maxBladeNames)
                {
                    var maxField = weaponType.GetField(maxFieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (maxField != null && maxField.FieldType == typeof(int))
                    {
                        int maxBlades = (int)maxField.GetValue(human.Weapon);
                        return Mathf.Max(0, maxBlades - currentBlades);
                    }
                }
                return bladePickup;
            }
        }
        return bladePickup;
    }

    private void AddBladesToHuman(Human human, int amount)
    {
        if (human.Weapon == null)
        {
            Debug.LogWarning("Human has no weapon equipped");
            return;
        }

        if (human.Weapon is BladeWeapon bladeWeapon)
        {
            bladeWeapon.BladesLeft += amount;
            Debug.Log($"Added {amount} blades. New total: {bladeWeapon.BladesLeft}/{bladeWeapon.MaxBlades}");
            return;
        }

        var weaponType = human.Weapon.GetType();
        string[] bladeCountNames = { "BladesLeft", "bladesLeft", "BladeCount", "bladeCount" };

        foreach (var fieldName in bladeCountNames)
        {
            var field = weaponType.GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (field != null && field.FieldType == typeof(int))
            {
                int currentBlades = (int)field.GetValue(human.Weapon);
                field.SetValue(human.Weapon, currentBlades + amount);
                Debug.Log($"Added {amount} blades via field '{fieldName}'. New total: {currentBlades + amount}");
                return;
            }

            var property = weaponType.GetProperty(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (property != null && property.PropertyType == typeof(int) && property.CanWrite)
            {
                int currentBlades = (int)property.GetValue(human.Weapon);
                property.SetValue(human.Weapon, currentBlades + amount);
                Debug.Log($"Added {amount} blades via property '{fieldName}'. New total: {currentBlades + amount}");
                return;
            }
        }

        string[] methodNames = { "AddBlades", "AddAmmo", "RefillBlades" };
        foreach (var methodName in methodNames)
        {
            var method = weaponType.GetMethod(methodName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (method != null)
            {
                var parameters = method.GetParameters();
                if (parameters.Length == 1 && parameters[0].ParameterType == typeof(int))
                {
                    method.Invoke(human.Weapon, new object[] { amount });
                    Debug.Log($"Called method '{methodName}' to add {amount} blades");
                    return;
                }
            }
        }

        Debug.LogWarning($"Could not add blades to weapon of type: {weaponType}");
    }

    private IEnumerator ShrinkAndDestroy()
    {
        isShrinking = true;

        if (localHuman != null && localHuman.photonView.IsMine)
        {
            ClearPrompt();
            isInside = false;
            localHuman = null;
        }

        Vector3 originalScale = transform.localScale;
        float timer = 0f;

        while (timer < shrinkAndDestroyTime)
        {
            float t = timer / shrinkAndDestroyTime;
            transform.localScale = Vector3.Lerp(originalScale, Vector3.zero, t);
            timer += Time.deltaTime;
            yield return null;
        }

        if (photonView != null && photonView.IsMine)
            PhotonNetwork.Destroy(gameObject);
    }

    private bool IsHumanValid(Human human)
    {
        return human != null && human.gameObject != null && human.photonView != null;
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
                normal = { textColor = currentPrompt.Contains("cooldown") ? Color.red : Color.white }
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