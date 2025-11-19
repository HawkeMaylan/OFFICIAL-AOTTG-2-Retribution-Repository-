using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace UI
{
    public class ModularWheelPopup : MonoBehaviour
    {
        [SerializeField] private List<Button> buttons = new List<Button>();

        private int selectedItem = 0;
        private UnityAction callback;

        // Public property to expose selectedItem
        public int SelectedItem => selectedItem;

        public void Setup()
        {
            if (buttons == null || buttons.Count == 0)
            {
                Debug.LogError("Buttons list is not assigned or empty in ModularWheelPopup.");
                return;
            }

            // Initialize buttons and set up their positions
            for (int i = 0; i < buttons.Count; i++)
            {
                int index = i;
                buttons[index].onClick.AddListener(() => OnButtonClick(index));
            }

            // Set button positions (similar to the old WheelPopup)
            SetButtonPositions();
        }

        public void Show(List<string> options, UnityAction callback)
        {
            if (buttons == null || buttons.Count == 0)
            {
                Debug.LogError("Buttons list is not assigned or empty in ModularWheelPopup.");
                return;
            }

            if (gameObject.activeSelf)
            {
                StopAllCoroutines();
                SetTransformAlpha(1f); // Assuming MaxFadeAlpha is 1
            }

            this.callback = callback;

            for (int i = 0; i < options.Count; i++)
            {
                if (i >= buttons.Count)
                {
                    Debug.LogWarning($"Not enough buttons to display all options. Only displaying the first {buttons.Count} options.");
                    break;
                }

                buttons[i].gameObject.SetActive(true);

                // Ensure the button has a child named "Text" with a Text component
                Text buttonText = buttons[i].transform.Find("Text")?.GetComponent<Text>();
                if (buttonText == null)
                {
                    Debug.LogError($"Button at index {i} does not have a child named 'Text' with a Text component.");
                    continue;
                }

                buttonText.text = options[i];
            }

            for (int i = options.Count; i < buttons.Count; i++)
            {
                buttons[i].gameObject.SetActive(false);
            }

            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void OnButtonClick(int index)
        {
            selectedItem = index;
            callback.Invoke();
            Hide();
        }

        private void SetButtonPositions()
        {
            if (buttons == null || buttons.Count == 0)
            {
                Debug.LogError("Buttons list is not assigned or empty in ModularWheelPopup.");
                return;
            }

            // Set button positions in a radial layout
            float angleStep = 360f / buttons.Count;
            float radius = 180f; // Radius of the wheel

            for (int i = 0; i < buttons.Count; i++)
            {
                float angle = i * angleStep;
                float x = Mathf.Sin(Mathf.Deg2Rad * angle) * radius;
                float y = Mathf.Cos(Mathf.Deg2Rad * angle) * radius;

                RectTransform buttonRect = buttons[i].GetComponent<RectTransform>();
                buttonRect.anchoredPosition = new Vector2(x, y);
            }
        }

        private void SetTransformAlpha(float alpha)
        {
            // Set the alpha value for all UI elements in the popup
            CanvasGroup canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
            canvasGroup.alpha = alpha;
        }

        private void Update()
        {
            // Handle keybinds if needed
            // Example: Check for key presses to select items
            for (int i = 0; i < buttons.Count; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i)) // Assuming keys 1-8 are used
                {
                    OnButtonClick(i);
                    break;
                }
            }
        }
    }
}