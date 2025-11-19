using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;
using UI;
using Settings;

public class RadialMenuController : MonoBehaviour
{
    [Header("Configuration")]
    public int segmentsPerPage = 8;
    public float radius = 200f;
    public float iconSize = 50f;
    public float deadZone = 0.2f;
    public KeyCode toggleKey = KeyCode.Tab;
    public bool useMouseSelection = true;
    [Tooltip("When enabled, menu must be held open and selection is applied on release")]
    public bool holdToSelectMode = false;

    [Header("UI References")]
    public GameObject radialMenuBase;
    public RectTransform selectionIndicator;
    public Text selectionNameText;
    public Text pageNameText;
    public Text pageNumberText;

    [Header("Pages")]
    public List<RadialMenuPage> pages = new List<RadialMenuPage>();

    private int currentPage = 0;
    private bool menuActive = false;
    private int currentSelection = -1;
    private Vector2 inputDirection;
    private InGameMenu _inGameMenu;
    private bool _isHolding = false;
    private int _pendingSelection = -1;

    [Tooltip("How long to stay 'in menu' after selection (seconds)")]
    public float postSelectionMenuTime = 1f; // Inspector-configurable delay

    private float _postSelectionTimer = 0f;
    private bool _inPostSelectionState = false;

    // Memory leak protection
    private Dictionary<string, GameObject> prefabLookup = new Dictionary<string, GameObject>();
    private List<GameObject> _createdUIElements = new List<GameObject>();
    private bool _isInitialized = false;

    void Start()
    {
        try
        {
            _inGameMenu = (InGameMenu)UIManager.CurrentMenu;
        }
        catch
        {
            _inGameMenu = null;
        }

        if (radialMenuBase != null)
        {
            radialMenuBase.SetActive(false);
        }
    }

    void OnDestroy()
    {
        Cleanup();
    }

    void OnDisable()
    {
        // Clean up when object is disabled
        if (menuActive)
        {
            ForceCloseMenu();
        }
    }

    void Update()
    {
        // Early exit if critical components are missing
        if (radialMenuBase == null || selectionIndicator == null ||
            selectionNameText == null || pageNameText == null || pageNumberText == null)
            return;

        // Handle post-selection timer
        if (_inPostSelectionState)
        {
            _postSelectionTimer -= Time.deltaTime;
            if (_postSelectionTimer <= 0f)
            {
                _inPostSelectionState = false;
                if (_inGameMenu != null)
                    _inGameMenu.SetRadialMenuActive(false);
            }
            return; // Skip other input during post-selection
        }
        HandleMenuToggleInput();

        if (!menuActive) return;

        GetInputDirection();
        UpdateSelection();
        HandleSelectionInput();
    }

    void HandleMenuToggleInput()
    {
        if (holdToSelectMode)
        {
            // Hold mode behavior
            if (Input.GetKeyDown(toggleKey) && !IsAnyMenuOpen())
            {
                OpenMenu();
            }
            else if (Input.GetKeyUp(toggleKey) && menuActive)
            {
                if (_isHolding && _pendingSelection != -1)
                {
                    ExecuteSelection(_pendingSelection);
                }
                CloseMenu();
            }
        }
        else
        {
            // Original toggle behavior
            if (SettingsManager.InputSettings.General.BuildMenuRadial.GetKeyDown())
            {
                if (menuActive)
                {
                    CloseMenu();
                }
                else if (!IsAnyMenuOpen())
                {
                    OpenMenu();
                }
            }
        }
    }

    public void InitializeWithBuildables(List<GameObject> buildablePrefabs)
    {
        // Clear previous data first
        CleanupEvents();
        prefabLookup.Clear();
        pages.Clear();

        // First organize prefabs by category
        Dictionary<string, RadialMenuPage> categoryPages = new Dictionary<string, RadialMenuPage>();

        foreach (GameObject prefab in buildablePrefabs)
        {
            BuildableObjectHelper helper = prefab.GetComponent<BuildableObjectHelper>();
            if (helper == null) continue;

            string category = helper.category ?? "Uncategorized";

            // Get or create the page for this category
            if (!categoryPages.TryGetValue(category, out RadialMenuPage page))
            {
                page = new RadialMenuPage { pageName = category };
                categoryPages[category] = page;
            }

            // Add to lookup dictionary
            string displayName = helper.displayName ?? prefab.name;
            prefabLookup[displayName] = prefab;

            // Create menu option - ONLY use the icon from BuildableObjectHelper
            RadialMenuOption option = new RadialMenuOption
            {
                optionName = displayName,
                icon = helper.menuIcon // No fallback - assumes icon is required
            };

            option.onSelect = new UnityEngine.Events.UnityEvent();
            option.onSelect.AddListener(() => OnBuildableSelected(option.optionName));

            page.options.Add(option);
        }

        // Add all category pages to the main pages list
        pages.AddRange(categoryPages.Values);

        // Set default page if none selected
        if (currentPage >= pages.Count) currentPage = 0;

        _isInitialized = true;

        if (menuActive)
        {
            UpdateMenuDisplay();
        }
    }

    private void OnBuildableSelected(string optionName)
    {
        if (prefabLookup.TryGetValue(optionName, out GameObject prefab))
        {
            BuildSystem buildSystem = FindObjectOfType<BuildSystem>();
            if (buildSystem != null)
            {
                buildSystem.HandleBuildableSelection(prefab);
            }
        }
    }

    void OpenMenu()
    {
        if (!_isInitialized) return;

        menuActive = true;
        radialMenuBase.SetActive(true);
        _isHolding = true;
        _pendingSelection = -1;

        if (_inGameMenu != null)
        {
            _inGameMenu.SetRadialMenuActive(true);
        }
        _inPostSelectionState = false;
        UpdateMenuDisplay();
    }

    void CloseMenu()
    {
        menuActive = false;
        _isHolding = false;
        radialMenuBase.SetActive(false);

        // Clean up UI elements immediately
        CleanupUIElements();

        // Start post-selection delay
        _postSelectionTimer = postSelectionMenuTime;
        _inPostSelectionState = true;

        if (UIManager.CurrentMenu is InGameMenu inGameMenu)
        {
            inGameMenu.SkipAHSSInput = true;
        }
    }

    void ForceCloseMenu()
    {
        menuActive = false;
        _isHolding = false;
        if (radialMenuBase != null)
            radialMenuBase.SetActive(false);

        CleanupUIElements();

        if (_inGameMenu != null)
        {
            _inGameMenu.SetRadialMenuActive(false);
        }
    }

    void ExecuteSelection(int selectionIndex)
    {
        if (currentPage < pages.Count && selectionIndex < pages[currentPage].options.Count)
        {
            pages[currentPage].options[selectionIndex].onSelect.Invoke();
            if (UIManager.CurrentMenu is InGameMenu inGameMenu)
            {
                inGameMenu.SkipAHSSInput = true;
            }
        }
    }

    private bool IsAnyMenuOpen()
    {
        return InGameMenu.InMenu() || _inPostSelectionState;
    }

    void GetInputDirection()
    {
        inputDirection = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));

        if (useMouseSelection && inputDirection.magnitude < deadZone)
        {
            Vector2 mousePos = Input.mousePosition;
            Vector2 center = new Vector2(Screen.width / 2, Screen.height / 2);
            inputDirection = (mousePos - center).normalized;
        }
    }

    void UpdateSelection()
    {
        if (currentPage >= pages.Count) return;

        int optionsOnPage = Mathf.Min(pages[currentPage].options.Count, segmentsPerPage);

        if (inputDirection.magnitude < deadZone)
        {
            if (currentSelection != -1)
            {
                currentSelection = -1;
                selectionIndicator.gameObject.SetActive(false);
                selectionNameText.text = "";
            }
            return;
        }

        float segmentAngle = 360f / optionsOnPage;
        float angle = Mathf.Atan2(inputDirection.y, inputDirection.x) * Mathf.Rad2Deg;
        if (angle < 0) angle += 360f;

        int newSelection = Mathf.FloorToInt(angle / segmentAngle);

        if (newSelection != currentSelection)
        {
            currentSelection = newSelection;
            UpdateSelectionVisual();

            if (holdToSelectMode && _isHolding)
            {
                _pendingSelection = currentSelection;
            }
        }
    }

    void UpdateSelectionVisual()
    {
        if (currentPage >= pages.Count) return;

        int optionsOnPage = Mathf.Min(pages[currentPage].options.Count, segmentsPerPage);

        if (currentSelection < 0 || currentSelection >= optionsOnPage) return;

        float segmentAngle = 360f / optionsOnPage;
        float angle = (currentSelection * segmentAngle + (segmentAngle / 2)) * Mathf.Deg2Rad;
        Vector2 pos = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        selectionIndicator.anchoredPosition = pos;
        selectionIndicator.gameObject.SetActive(true);

        if (currentPage < pages.Count && currentSelection < pages[currentPage].options.Count)
        {
            selectionNameText.text = pages[currentPage].options[currentSelection].optionName;
        }
    }

    void HandleSelectionInput()
    {
        if (UIManager.CurrentMenu is InGameMenu inGameMenu)
        {
            inGameMenu.SkipAHSSInput = true;
        }

        if (currentSelection == -1) return;

        if (!holdToSelectMode)
        {
            if (Input.GetButtonDown("Submit") || Input.GetMouseButtonDown(0))
            {
                if (EventSystem.current.currentSelectedGameObject != null ||
                    EventSystem.current.IsPointerOverGameObject())
                {
                    ExecuteSelection(currentSelection);
                    CloseMenu();
                }
            }
        }

        if (Input.GetKeyDown(KeyCode.Q))
        {
            PreviousPage();
        }
        else if (Input.GetKeyDown(KeyCode.E))
        {
            NextPage();
        }
    }

    void NextPage()
    {
        currentPage = (currentPage + 1) % pages.Count;
        ResetSelection();
        UpdateMenuDisplay();
    }

    void PreviousPage()
    {
        currentPage--;
        if (currentPage < 0) currentPage = pages.Count - 1;
        ResetSelection();
        UpdateMenuDisplay();
    }

    void ResetSelection()
    {
        currentSelection = -1;
        selectionIndicator.gameObject.SetActive(false);
        selectionNameText.text = "";
    }

    void UpdateMenuDisplay()
    {
        // Clean up previous UI first
        CleanupUIElements();

        if (currentPage >= pages.Count) return;

        pageNameText.text = pages[currentPage].pageName;
        pageNumberText.text = $"Page {currentPage + 1} of {pages.Count}";

        int optionsOnPage = Mathf.Min(pages[currentPage].options.Count, segmentsPerPage);
        float segmentAngle = 360f / optionsOnPage;

        for (int i = 0; i < optionsOnPage; i++)
        {
            float angle = (i * segmentAngle + (segmentAngle / 2)) * Mathf.Deg2Rad;
            Vector2 pos = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

            GameObject icon = new GameObject($"Option_{i}");
            RectTransform rt = icon.AddComponent<RectTransform>();
            rt.SetParent(radialMenuBase.transform);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(iconSize, iconSize);

            Image img = icon.AddComponent<Image>();
            img.sprite = pages[currentPage].options[i].icon;

            GameObject label = new GameObject($"Label_{i}");
            RectTransform labelRt = label.AddComponent<RectTransform>();
            labelRt.SetParent(icon.transform);
            labelRt.anchoredPosition = new Vector2(0, -iconSize);
            labelRt.sizeDelta = new Vector2(100, 30);

            Text labelText = label.AddComponent<Text>();
            labelText.text = pages[currentPage].options[i].optionName;
            labelText.alignment = TextAnchor.UpperCenter;
            labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            labelText.fontSize = 14;

            // Track for cleanup
            _createdUIElements.Add(icon);
            _createdUIElements.Add(label);
        }
    }

    // Memory leak protection methods
    private void CleanupUIElements()
    {
        foreach (GameObject uiElement in _createdUIElements)
        {
            if (uiElement != null)
            {
                if (Application.isPlaying)
                    Destroy(uiElement);
                else
                    DestroyImmediate(uiElement);
            }
        }
        _createdUIElements.Clear();

        // Additional safety cleanup of any remaining children
        if (radialMenuBase != null)
        {
            foreach (Transform child in radialMenuBase.transform)
            {
                if (child != selectionIndicator &&
                    child.gameObject != selectionNameText.gameObject &&
                    child.gameObject != pageNameText.gameObject &&
                    child.gameObject != pageNumberText.gameObject)
                {
                    if (Application.isPlaying)
                        Destroy(child.gameObject);
                    else
                        DestroyImmediate(child.gameObject);
                }
            }
        }
    }

    private void CleanupEvents()
    {
        foreach (var page in pages)
        {
            foreach (var option in page.options)
            {
                if (option.onSelect != null)
                {
                    option.onSelect.RemoveAllListeners();
                }
            }
        }
    }

    private void Cleanup()
    {
        // Comprehensive cleanup
        if (menuActive && _inGameMenu != null)
        {
            _inGameMenu.SetRadialMenuActive(false);
        }

        CleanupEvents();
        CleanupUIElements();
        prefabLookup.Clear();

        menuActive = false;
        _isHolding = false;
    }

    // Public method for external cleanup if needed
    public void ForceCleanup()
    {
        Cleanup();
    }
}

[System.Serializable]
public class RadialMenuPage
{
    public string pageName;
    public List<RadialMenuOption> options = new List<RadialMenuOption>();
}

[System.Serializable]
public class RadialMenuOption
{
    public string optionName;
    public Sprite icon;
    public UnityEngine.Events.UnityEvent onSelect;
}