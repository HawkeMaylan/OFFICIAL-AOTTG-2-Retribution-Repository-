using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using ApplicationManagers;
using Settings;

namespace UI
{
    public class ItemPopupManager : MonoBehaviour
    {
        public static ItemPopupManager Instance;

        private const float Duration = 4f;
        private const float FadeDuration = 2f;
        private const int MaxPopups = 5;
        private const float Spacing = 35f;

        private readonly Queue<GameObject> _popupQueue = new Queue<GameObject>();

        private Transform _popupParent;
        private GameObject _popupPrefab;

        private void Awake()
        {
            Instance = this;

            GameObject menu = GameObject.Find("DefaultMenu(Clone)");
            if (menu == null)
                return;

            _popupParent = menu.transform.Find("BottomRightPopups");
            if (_popupParent == null)
            {
                _popupParent = new GameObject("BottomRightPopups", typeof(RectTransform)).transform;
                _popupParent.SetParent(menu.transform);
                RectTransform rt = _popupParent.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
                rt.pivot = new Vector2(1f, 0f);
                rt.anchoredPosition = new Vector2(-20f, 20f);
                rt.sizeDelta = new Vector2(300f, 400f);
            }

            _popupPrefab = (GameObject)ResourceManager.LoadAsset("UI", "ItemNotificationPopup", false);
        }

        public void ShowPopup(string message)
        {
            if (_popupPrefab == null || _popupParent == null)
                return;

            GameObject popup = Instantiate(_popupPrefab, _popupParent);
            popup.transform.SetAsLastSibling();

            Text text = popup.GetComponentInChildren<Text>();
            if (text != null)
                text.text = message;

            _popupQueue.Enqueue(popup);
            UpdatePopupPositions();

            StartCoroutine(FadeAndDestroy(popup, Duration, FadeDuration));
        }

        private IEnumerator FadeAndDestroy(GameObject popup, float totalDuration, float fadeDuration)
        {
            yield return new WaitForSeconds(totalDuration - fadeDuration);

            // Check if popup still exists
            if (popup == null)
                yield break;

            CanvasGroup cg = popup.GetComponent<CanvasGroup>();
            if (cg == null)
                cg = popup.AddComponent<CanvasGroup>();

            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                // Check if popup was destroyed during fade
                if (popup == null)
                    yield break;

                elapsed += Time.deltaTime;
                cg.alpha = 1f - (elapsed / fadeDuration);
                yield return null;
            }

            if (popup != null)
            {
                cg.alpha = 0f;

                // Remove from queue and destroy
                if (_popupQueue.Contains(popup))
                {
                    // Create a new queue without the destroyed popup
                    var newQueue = new Queue<GameObject>();
                    foreach (var item in _popupQueue)
                    {
                        if (item != popup && item != null)
                            newQueue.Enqueue(item);
                    }
                    _popupQueue.Clear();
                    foreach (var item in newQueue)
                        _popupQueue.Enqueue(item);

                    Destroy(popup);
                    UpdatePopupPositions();
                }
            }
        }

        private void UpdatePopupPositions()
        {
            int index = 0;

            // First clean up any null references in the queue
            var validPopups = new List<GameObject>();
            foreach (GameObject popup in _popupQueue)
            {
                if (popup != null)
                    validPopups.Add(popup);
            }

            _popupQueue.Clear();
            foreach (var popup in validPopups)
                _popupQueue.Enqueue(popup);

            // Update positions
            foreach (GameObject popup in _popupQueue)
            {
                if (popup != null)
                {
                    RectTransform rt = popup.GetComponent<RectTransform>();
                    if (rt != null)
                        rt.anchoredPosition = new Vector2(0f, index * Spacing);
                }
                index++;
            }

            // Remove oldest popups if exceeding max count
            while (_popupQueue.Count > MaxPopups)
            {
                GameObject oldest = _popupQueue.Dequeue();
                if (oldest != null)
                    Destroy(oldest);
            }
        }
    }
}