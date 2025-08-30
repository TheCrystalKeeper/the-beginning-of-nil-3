using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PauseMenuUI : MonoBehaviour
{
    [Header("UI")]
    public CanvasGroup canvasGroup;     // put this on the root panel
    public Button btnContinue;
    public Button btnSettings;
    public Button btnExit;
    public Selectable firstSelected;    // which control gets focus when opening

    [Header("Fade")]
    [Range(0f, 1f)] public float targetAlpha = 1f;
    public float fadeInTime = 0.2f;
    public float fadeOutTime = 0.2f;
    public bool startHidden = true;

    [Header("Hotkeys (optional)")]
    public bool allowEscapeToggle = true;
    public KeyCode toggleKey = KeyCode.Escape;

    [Header("Events")]
    public UnityEvent onContinue;   // optional; defaults provided below
    public UnityEvent onSettings;
    public UnityEvent onExit;

    [Header("Selection")]
    public bool deselectOnShow = true;

    Coroutine fadeRoutine;

    void Awake()
    {
        if (!canvasGroup) canvasGroup = GetComponent<CanvasGroup>();
        if (!canvasGroup) canvasGroup = gameObject.AddComponent<CanvasGroup>();

        // initialize visibility
        float a = (startHidden || GameState.State == GameState.Playing) ? 0f : targetAlpha;
        SetAlpha(a);
        SetInteractable(a > 0.999f);

        // default button hooks if you don’t wire them
        if (btnContinue && onContinue.GetPersistentEventCount() == 0)
            btnContinue.onClick.AddListener(() => GameState.Resume());
        if (btnSettings && onSettings.GetPersistentEventCount() == 0)
            btnSettings.onClick.AddListener(() => Debug.Log("Settings clicked (wire up your menu)."));
        if (btnExit && onExit.GetPersistentEventCount() == 0)
            btnExit.onClick.AddListener(() => Application.Quit());

        // also forward to UnityEvents so you can add more in Inspector
        if (btnContinue) btnContinue.onClick.AddListener(() => onContinue.Invoke());
        if (btnSettings) btnSettings.onClick.AddListener(() => onSettings.Invoke());
        if (btnExit) btnExit.onClick.AddListener(() => onExit.Invoke());
    }

    void OnEnable()
    {
        GameState.OnStateChanged += HandleStateChange;
        HandleStateChange(GameState.State); // sync if enabled mid-game
    }

    void OnDisable()
    {
        GameState.OnStateChanged -= HandleStateChange;
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = null;
    }

    void Update()
    {
        /*if (!allowEscapeToggle) return;

        if (Input.GetKeyDown(toggleKey))
        {
            if (GameState.IsPaused()) GameState.Resume();
            else GameState.PauseWithUI(); // or Pause()
        }*/
    }

    void HandleStateChange(int newState)
    {
        bool paused = (newState == GameState.PausedNoUI || newState == GameState.PausedWithUI);
        float to = paused ? targetAlpha : 0f;
        float t = paused ? fadeInTime : fadeOutTime;

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeTo(to, t, paused));
    }

    IEnumerator FadeTo(float target, float time, bool becomingVisible)
    {
        float from = canvasGroup.alpha;

        if (becomingVisible)
        {
            SetInteractable(true);
            if (deselectOnShow) DeselectAll();             // clear immediately
        }

        if (Mathf.Approximately(time, 0f))
        {
            SetAlpha(target);
            if (!becomingVisible) SetInteractable(false);
            else if (deselectOnShow) StartCoroutine(DeselectNextFrame()); // belt & suspenders
            fadeRoutine = null;
            yield break;
        }

        float tElapsed = 0f;
        while (tElapsed < time)
        {
            tElapsed += Time.unscaledDeltaTime;
            SetAlpha(Mathf.Lerp(from, target, Mathf.Clamp01(tElapsed / time)));
            yield return null;
        }

        SetAlpha(target);

        if (!becomingVisible)
        {
            SetInteractable(false);
        }
        else
        {
            if (deselectOnShow) StartCoroutine(DeselectNextFrame()); // ensure nothing is reselected
        }

        fadeRoutine = null;
    }

    void SetAlpha(float a)
    {
        canvasGroup.alpha = a;
    }

    void SetInteractable(bool on)
    {
        canvasGroup.interactable = on;
        canvasGroup.blocksRaycasts = on;
    }

    void FocusFirst() { if (deselectOnShow) DeselectAll(); }

    void DeselectAll()
    {
        var es = EventSystem.current;
        if (es != null) es.SetSelectedGameObject(null);
    }

    IEnumerator DeselectNextFrame()
    {
        yield return null; // wait one frame in case Unity auto-selects something on enable
        DeselectAll();
    }
}
