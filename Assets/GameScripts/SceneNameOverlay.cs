using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneNameOverlay : MonoBehaviour
{
    [Header("Mode")]
    public bool showOnlySceneName = true;
    public bool allowToggle = true;
    public Key toggleKey = Key.F1;

    [Header("Text")]
    public TMP_FontAsset fontAsset;
    public int fontSize = 64;
    public Color textColor = Color.white;

    private static SceneNameOverlay instance;
    private Canvas canvas;
    private Image background;
    private TextMeshProUGUI sceneText;
    private string currentSceneName = string.Empty;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        BuildUi();
        UpdateSceneName(SceneManager.GetActiveScene());
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    void OnDestroy()
    {
        if (instance == this)
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }
    }

    void Update()
    {
        if (!allowToggle) return;

        if (Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame)
        {
            ToggleMode();
            return;
        }

        if (Touchscreen.current != null)
        {
            TouchControl touch0 = Touchscreen.current.touches[0];
            TouchControl touch1 = Touchscreen.current.touches[1];
            bool touch0Pressed = touch0.press.isPressed && touch0.press.wasPressedThisFrame;
            bool touch1Pressed = touch1.press.isPressed && touch1.press.wasPressedThisFrame;
            if (touch0Pressed && touch1Pressed)
            {
                ToggleMode();
            }
        }
    }

    void ToggleMode()
    {
        showOnlySceneName = !showOnlySceneName;
        ApplyMode();
    }

    void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        UpdateSceneName(scene);
    }

    void UpdateSceneName(Scene scene)
    {
        if (sceneText != null)
        {
            sceneText.text = scene.name;
        }

        currentSceneName = scene.name;

        ApplyMode();
    }

    void ApplyMode()
    {
        if (canvas == null) return;
        canvas.enabled = showOnlySceneName;
    }

    void BuildUi()
    {
        GameObject canvasObj = new GameObject("SceneNameCanvas");
        canvasObj.transform.SetParent(transform, false);
        canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        canvasObj.AddComponent<GraphicRaycaster>();

        GameObject bgObj = new GameObject("SceneNameBackground");
        bgObj.transform.SetParent(canvasObj.transform, false);
        background = bgObj.AddComponent<Image>();
        background.color = Color.black;
        RectTransform bgRect = background.rectTransform;
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        GameObject textObj = new GameObject("SceneNameText");
        textObj.transform.SetParent(canvasObj.transform, false);
        sceneText = textObj.AddComponent<TextMeshProUGUI>();
        sceneText.text = "";
        sceneText.alignment = TextAlignmentOptions.Center;
        sceneText.color = textColor;
        sceneText.fontSize = fontSize;
        TMP_FontAsset resolvedFont = fontAsset != null ? fontAsset : TMP_Settings.defaultFontAsset;
        if (resolvedFont != null)
        {
            sceneText.font = resolvedFont;
        }

        RectTransform textRect = sceneText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
    }
}
