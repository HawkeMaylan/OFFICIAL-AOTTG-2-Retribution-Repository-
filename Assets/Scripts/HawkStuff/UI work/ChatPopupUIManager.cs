using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using Characters;
using GameManagers;
using ApplicationManagers;
using Settings;
using System.Collections;
using System.Collections.Generic;

public class ChatPopupUIManager : MonoBehaviourPun
{
    private GameObject panel;
    private InputField chatInput;
    private Button sendButton;
    private InGameManager gameManager;

    private static readonly float EmoteDuration = 15f;
    private static readonly int MaxMessages = 5;

    private static Dictionary<Transform, List<GameObject>> activePopups = new Dictionary<Transform, List<GameObject>>();

    private void Awake()
    {
        gameManager = SceneLoader.CurrentGameManager as InGameManager;
        if (gameManager == null)
        {
            Debug.LogError("ChatPopupUIManager: Not in InGameManager scene.");
            enabled = false;
            return;
        }

        if (!photonView.IsMine)
        {
            enabled = false;
            return;
        }

        CreateChatUI();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Slash))
            ToggleChatPanel();

        if (panel.activeSelf && Input.GetKeyDown(KeyCode.Return))
            SendChatPopup();
    }

    private void ToggleChatPanel()
    {
        panel.SetActive(!panel.activeSelf);
        if (panel.activeSelf)
        {
            chatInput.text = "";
            chatInput.Select();
            chatInput.ActivateInputField();
        }
    }

    private void SendChatPopup()
    {
        string msg = chatInput.text.Trim();
        if (string.IsNullOrEmpty(msg)) return;

        BaseCharacter character = gameManager.CurrentCharacter;
        if (character != null)
        {
            PhotonView pv = character.Cache.PhotonView;
            photonView.RPC("EmoteTextRPC", RpcTarget.All, pv.ViewID, msg);
        }

        chatInput.text = "";
        panel.SetActive(false);
    }

    [PunRPC]
    public void EmoteTextRPC(int viewId, string message)
    {
        if (!SettingsManager.UISettings.ShowEmotes.Value)
            return;

        PhotonView view = PhotonView.Find(viewId);
        if (view == null) return;

        BaseCharacter character = view.GetComponent<BaseCharacter>();
        if (character == null) return;

        Transform target = character.Cache.Transform;

        if (!activePopups.ContainsKey(target))
            activePopups[target] = new List<GameObject>();

        StartCoroutine(SpawnFloatingText(target, message));
    }

    private IEnumerator SpawnFloatingText(Transform target, string message)
    {
        GameObject canvasGO = GameObject.Find("DefaultMenu(Clone)");
        if (canvasGO == null) yield break;

        GameObject wrapper = new GameObject("FloatingTextWrapper", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        wrapper.transform.SetParent(canvasGO.transform, false);
        wrapper.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.85f); // black background

        GameObject textGO = new GameObject("FloatingText", typeof(RectTransform), typeof(Text));
        textGO.transform.SetParent(wrapper.transform, false);

        RectTransform wrapperRect = wrapper.GetComponent<RectTransform>();
        wrapperRect.sizeDelta = new Vector2(250, 30);

        RectTransform textRect = textGO.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Text textComp = textGO.GetComponent<Text>();
        textComp.text = message;
        textComp.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        textComp.fontSize = 18;
        textComp.alignment = TextAnchor.MiddleCenter;
        textComp.color = Color.white;

        // Add message to the list
        var list = activePopups[target];
        list.Add(wrapper);
        if (list.Count > MaxMessages)
        {
            Destroy(list[0]);
            list.RemoveAt(0);
        }

        float elapsed = 0f;
        while (elapsed < EmoteDuration)
        {
            elapsed += Time.deltaTime;

            if (target == null)
                break;

            Vector3 basePos = target.position + Vector3.up * 2.5f;
            for (int i = 0; i < list.Count; i++)
            {
                GameObject go = list[i];
                if (go == null) continue;
                Vector3 pos = Camera.main.WorldToScreenPoint(basePos + Vector3.up * (list.Count - 1 - i) * 0.4f);
                go.GetComponent<RectTransform>().position = pos;
            }

            yield return null;
        }

        if (activePopups.ContainsKey(target))
        {
            activePopups[target].Remove(wrapper);
        }
        Destroy(wrapper);
    }

    private void CreateChatUI()
    {
        GameObject canvasGO = GameObject.Find("DefaultMenu(Clone)");
        if (canvasGO == null) return;

        Canvas canvas = canvasGO.GetComponent<Canvas>();
        if (canvas == null) return;

        panel = new GameObject("ChatPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvas.transform, false);
        panel.GetComponent<Image>().color = new Color(0, 0, 0, 0.5f);

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.3f, 0.1f);
        panelRect.anchorMax = new Vector2(0.7f, 0.2f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        GameObject inputGO = new GameObject("ChatInput", typeof(RectTransform), typeof(Image), typeof(InputField));
        inputGO.transform.SetParent(panel.transform, false);
        RectTransform inputRect = inputGO.GetComponent<RectTransform>();
        inputRect.anchorMin = new Vector2(0f, 0f);
        inputRect.anchorMax = new Vector2(0.8f, 1f);
        inputRect.offsetMin = new Vector2(10, 10);
        inputRect.offsetMax = new Vector2(-10, -10);

        Image inputImage = inputGO.GetComponent<Image>();
        inputImage.color = Color.black;

        chatInput = inputGO.GetComponent<InputField>();
        chatInput.textComponent = CreateUIText(chatInput.transform, "InputText", TextAnchor.MiddleLeft, 14);
        chatInput.placeholder = CreateUIText(chatInput.transform, "Placeholder", TextAnchor.MiddleLeft, 14, "<Type Message>");
        chatInput.lineType = InputField.LineType.SingleLine;

        GameObject buttonGO = new GameObject("SendButton", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonGO.transform.SetParent(panel.transform, false);
        RectTransform buttonRect = buttonGO.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.8f, 0f);
        buttonRect.anchorMax = new Vector2(1f, 1f);
        buttonRect.offsetMin = new Vector2(5, 10);
        buttonRect.offsetMax = new Vector2(-10, -10);

        Image buttonImage = buttonGO.GetComponent<Image>();
        buttonImage.color = Color.gray;

        sendButton = buttonGO.GetComponent<Button>();
        sendButton.onClick.AddListener(SendChatPopup);
        CreateUIText(sendButton.transform, "SendText", TextAnchor.MiddleCenter, 14, "Send");

        panel.SetActive(false);
    }

    private Text CreateUIText(Transform parent, string name, TextAnchor alignment, int fontSize, string text = "")
    {
        GameObject textGO = new GameObject(name, typeof(RectTransform), typeof(Text));
        textGO.transform.SetParent(parent, false);
        RectTransform textRect = textGO.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Text uiText = textGO.GetComponent<Text>();
        uiText.text = text;
        uiText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        uiText.fontSize = fontSize;
        uiText.alignment = alignment;
        uiText.color = Color.white;

        return uiText;
    }
}
