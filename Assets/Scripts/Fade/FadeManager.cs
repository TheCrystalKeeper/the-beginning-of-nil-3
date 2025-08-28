using UnityEngine;
using System.Collections;

[RequireComponent(typeof(SpriteRenderer))]
public class FadeManager : MonoBehaviour
{
    [Header("Camera Lock")]
    public Camera targetCamera;
    public bool lockPosition = true;
    public bool lockRotation = true;
    public bool lockScaleToView = true;  // autosize to camera view (orthographic only)
    public float edgeMargin = 0.05f;

    [Header("Fade Defaults")]
    public float defaultFadeTime = 0.3f;
    public Color defaultColor = Color.black;

    [Header("Auto Start")]
    public bool fadeOnStart = true;          // fade from opaque -> transparent at boot
    public float startDelay = 0f;            // seconds before starting the fade
    public float startDuration = 0.5f;       // how long the initial fade takes

    [Header("Pause Dimming")]
    public bool dimOnPause = true;
    [Range(0f, 1f)] public float pauseDimAlpha = 0.4f;  // target alpha while paused
    public float pauseFadeTime = 0.2f;                  // how quickly to dim/undim
    public Color pauseDimColor = Color.black;           // color for the dim overlay

    public static bool IsFading { get; private set; } = false;
    public static bool FadeComplete => !IsFading;

    SpriteRenderer sr;
    Coroutine routine;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        transform.localScale = new Vector3(100f, 100f, 100f);
        if (!targetCamera) targetCamera = Camera.main;

        // Start fully opaque in the chosen defaultColor
        sr.color = new Color(defaultColor.r, defaultColor.g, defaultColor.b, 1f);
    }

    void OnEnable()
    {
        GameState.OnStateChanged += HandleGameStateChange;
    }

    void OnDisable()
    {
        GameState.OnStateChanged -= HandleGameStateChange;
    }

    void Start()
    {
        if (fadeOnStart) StartCoroutine(StartFadeRoutine());
    }

    IEnumerator StartFadeRoutine()
    {
        // Ensure the overlay is locked/scaled to the camera before fading
        yield return new WaitForEndOfFrame();

        if (startDelay > 0f) yield return new WaitForSecondsRealtime(startDelay);

        // Fade to transparent from the current color
        FadeOut(startDuration, defaultColor);
    }

    void LateUpdate()
    {
        // You can keep this pause guard if you like; fades use unscaled time so they still run.
        if (GameState.IsPaused()) return;

        if (!targetCamera) return;

        if (lockPosition)
            transform.position = new Vector3(targetCamera.transform.position.x,
                                             targetCamera.transform.position.y,
                                             transform.position.z);

        if (lockRotation)
            transform.rotation = Quaternion.identity;

        if (lockScaleToView && targetCamera.orthographic && sr.sprite)
        {
            float worldHeight = targetCamera.orthographicSize * 2f + edgeMargin * 2f;
            float worldWidth = worldHeight * targetCamera.aspect + edgeMargin * 2f;

            Vector2 spriteSize = sr.sprite.bounds.size;
            float sx = worldWidth / Mathf.Max(0.0001f, spriteSize.x);
            float sy = worldHeight / Mathf.Max(0.0001f, spriteSize.y);
            transform.localScale = new Vector3(sx, sy, 1f);
        }
    }

    public void FadeIn(float time = -1f, Color? color = null)
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(FadeTo(1f, time, color));
    }

    public void FadeOut(float time = -1f, Color? color = null)
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(FadeTo(0f, time, color));
    }

    IEnumerator FadeTo(float targetAlpha, float time, Color? color)
    {
        IsFading = true;

        float duration = (time > 0f) ? time : defaultFadeTime;
        Color baseColor = color ?? defaultColor;

        Color start = sr.color;
        Color end = new Color(baseColor.r, baseColor.g, baseColor.b, targetAlpha);

        // preserve current alpha for start, but match RGB to baseColor
        start.r = baseColor.r; start.g = baseColor.g; start.b = baseColor.b;

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            sr.color = Color.Lerp(start, end, Mathf.Clamp01(t / duration));
            yield return null;
        }
        sr.color = end;

        IsFading = false;
        routine = null;
    }

    // === GameState integration ===
    void HandleGameStateChange(int newState)
    {
        if (!dimOnPause) return;

        // When paused (either mode), fade to dim alpha; when playing, fade to clear.
        if (newState == GameState.Playing)
        {
            // back to clear
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(FadeTo(0f, pauseFadeTime, defaultColor));
        }
        else
        {
            // dim on pause (use pauseDimColor)
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(FadeTo(pauseDimAlpha, pauseFadeTime, pauseDimColor));
        }
    }
}
