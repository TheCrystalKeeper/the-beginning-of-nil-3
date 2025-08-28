using UnityEngine;
using System.Collections;

[RequireComponent(typeof(SpriteRenderer))]
public class SpritePauseFade : MonoBehaviour
{
    [Header("Fade Settings")]
    [Range(0f, 1f)] public float targetAlpha = 0.5f;
    public float fadeInTime = 0.25f;
    public float fadeOutTime = 0.25f;
    public bool startHidden = true;

    SpriteRenderer _sr;
    Coroutine _routine;

    void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        float initialAlpha = (startHidden || GameState.State == GameState.Playing) ? 0f : targetAlpha;
        SetAlpha(initialAlpha);
    }

    void OnEnable()
    {
        GameState.OnStateChanged += HandleStateChange;
        HandleStateChange(GameState.State); // sync immediately
    }

    void OnDisable()
    {
        GameState.OnStateChanged -= HandleStateChange;
        if (_routine != null) StopCoroutine(_routine);
    }

    void HandleStateChange(int newState)
    {
        bool paused = (newState == GameState.PausedNoUI || newState == GameState.PausedWithUI);
        float to = paused ? targetAlpha : 0f;
        float time = paused ? fadeInTime : fadeOutTime;

        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(FadeTo(to, time));
    }

    IEnumerator FadeTo(float target, float time)
    {
        float from = _sr.color.a;
        float t = 0f;

        while (t < time)
        {
            t += Time.unscaledDeltaTime; // unaffected by Time.timeScale
            float a = Mathf.Lerp(from, target, Mathf.Clamp01(t / time));
            SetAlpha(a);
            yield return null;
        }

        SetAlpha(target);
        _routine = null;
    }

    void SetAlpha(float a)
    {
        var c = _sr.color;
        c.a = a;
        _sr.color = c;
    }
}
