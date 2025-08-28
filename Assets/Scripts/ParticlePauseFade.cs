using UnityEngine;
using UnityEngine.Rendering;
using System.Collections;

[DisallowMultipleComponent]
[RequireComponent(typeof(ParticleSystem))]
public class ParticlePauseFade : MonoBehaviour
{
    [Header("Scope")]
    [Tooltip("Apply to all child ParticleSystems too.")]
    public bool includeChildren = true;

    [Header("Fade Settings")]
    [Range(0f, 1f)] public float targetAlpha = 1f;   // visible when paused
    public float fadeInTime = 0.25f;                 // pause -> visible
    public float fadeOutTime = 0.25f;                // unpause -> clear

    [Header("What to fade")]
    [Tooltip("Multiply material color alpha (affects already spawned particles).")]
    public bool fadeMaterialAlpha = true;
    [Tooltip("Scale emission rates to match alpha (affects newly spawned).")]
    public bool fadeEmission = true;

    // Internals
    ParticleSystem[] _systems;
    ParticleSystemRenderer[] _renderers;
    float[] _baseRateTimeMult;
    float[] _baseRateDistMult;
    Color[] _baseMaterialColor;
    Coroutine _routine;

    void Awake()
    {
        // Collect systems/renderers
        _systems = includeChildren ? GetComponentsInChildren<ParticleSystem>(true)
                                   : new[] { GetComponent<ParticleSystem>() };
        _renderers = new ParticleSystemRenderer[_systems.Length];

        _baseRateTimeMult = new float[_systems.Length];
        _baseRateDistMult = new float[_systems.Length];
        _baseMaterialColor = new Color[_systems.Length];

        for (int i = 0; i < _systems.Length; i++)
        {
            var ps = _systems[i];

            // Ensure unscaled time simulation (keeps running when paused)
            var main = ps.main;
            main.useUnscaledTime = true;

            // Cache emission multipliers
            var em = ps.emission;
            _baseRateTimeMult[i] = em.rateOverTimeMultiplier;
            _baseRateDistMult[i] = em.rateOverDistanceMultiplier;

            // Cache renderer/material + base color
            var r = ps.GetComponent<ParticleSystemRenderer>();
            _renderers[i] = r;

            // .material instantiates a unique instance for safe runtime edits
            if (r != null && r.sharedMaterial != null)
            {
                var mat = r.material;
                _baseMaterialColor[i] = mat.HasProperty("_Color") ? mat.color : Color.white;
            }
            else
            {
                _baseMaterialColor[i] = Color.white;
            }
        }
    }

    void OnEnable()
    {
        GameState.OnStateChanged += HandleStateChange;
        // Sync with current state on enable
        HandleStateChange(GameState.State);
    }

    void OnDisable()
    {
        GameState.OnStateChanged -= HandleStateChange;
        if (_routine != null) StopCoroutine(_routine);
        _routine = null;
    }

    // Public API if you want to call manually
    public void FadeIn() => StartFade(targetAlpha, fadeInTime);
    public void FadeOut() => StartFade(0f, fadeOutTime);

    void HandleStateChange(int newState)
    {
        bool paused = (newState == GameState.PausedNoUI || newState == GameState.PausedWithUI);
        StartFade(paused ? targetAlpha : 0f, paused ? fadeInTime : fadeOutTime);
    }

    void StartFade(float target, float time)
    {
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(FadeTo(target, Mathf.Max(0f, time)));
    }

    IEnumerator FadeTo(float target, float time)
    {
        // Capture starting values
        float[] fromRateTime = null, fromRateDist = null;
        Color[] fromColor = null;

        if (fadeEmission)
        {
            fromRateTime = new float[_systems.Length];
            fromRateDist = new float[_systems.Length];
            for (int i = 0; i < _systems.Length; i++)
            {
                var em = _systems[i].emission;
                fromRateTime[i] = em.rateOverTimeMultiplier;
                fromRateDist[i] = em.rateOverDistanceMultiplier;
            }
        }

        if (fadeMaterialAlpha)
        {
            fromColor = new Color[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
            {
                var r = _renderers[i];
                if (r != null && r.sharedMaterial != null && r.material.HasProperty("_Color"))
                    fromColor[i] = r.material.color;
                else
                    fromColor[i] = _baseMaterialColor[i];
            }
        }

        if (Mathf.Approximately(time, 0f))
        {
            ApplyAlpha(target, fromRateTime, fromRateDist, fromColor, 1f);
            _routine = null;
            yield break;
        }

        float t = 0f;
        while (t < time)
        {
            t += Time.unscaledDeltaTime; // unaffected by Time.timeScale
            float a = Mathf.Clamp01(t / time);
            ApplyAlpha(target, fromRateTime, fromRateDist, fromColor, a);
            yield return null;
        }
        ApplyAlpha(target, fromRateTime, fromRateDist, fromColor, 1f);
        _routine = null;
    }

    void ApplyAlpha(float target, float[] fromRateTime, float[] fromRateDist, Color[] fromColor, float lerp01)
    {
        // Emission scaling (lerp multipliers)
        if (fadeEmission)
        {
            for (int i = 0; i < _systems.Length; i++)
            {
                float toMult = Mathf.Lerp(fromRateTime[i], _baseRateTimeMult[i] * target, lerp01);
                float toDist = Mathf.Lerp(fromRateDist[i], _baseRateDistMult[i] * target, lerp01);
                var em = _systems[i].emission;
                em.rateOverTimeMultiplier = toMult;
                em.rateOverDistanceMultiplier = toDist;
            }
        }

        // Material alpha (multiplies current particle colors)
        if (fadeMaterialAlpha)
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                var r = _renderers[i];
                if (r == null || r.sharedMaterial == null) continue;
                var mat = r.material;
                if (!mat.HasProperty("_Color")) continue;

                Color baseCol = _baseMaterialColor[i];
                float newA = Mathf.Lerp(fromColor[i].a, baseCol.a * target, lerp01);
                Color outCol = baseCol; outCol.a = newA;
                mat.color = outCol;
            }
        }
    }
}
