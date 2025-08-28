using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections;

[RequireComponent(typeof(Camera))]
public class CameraPostFXStates : MonoBehaviour
{
    // -------- Public API (call these) --------
    public void SetStateA(float blendTime = 0f) => BlendToState(StateId.A, blendTime);
    public void SetStateB(float blendTime = 0f) => BlendToState(StateId.B, blendTime);
    public void SetStateC(float blendTime = 0f) => BlendToState(StateId.C, blendTime);

    // Optionally expose a numeric setter if you prefer: 0=A,1=B,2=C
    public void SetStateIndex(int index, float blendTime = 0f)
    {
        if (index <= 0) SetStateA(blendTime);
        else if (index == 1) SetStateB(blendTime);
        else SetStateC(blendTime);
    }

    // -------- State definitions (tune later) --------
    public enum StateId { A, B, C }

    [System.Serializable]
    public struct PostFXState
    {
        [Header("Depth Of Field (Gaussian)")]
        public bool dofEnabled;
        [Range(0f, 2f)] public float dofMaxRadius; // blur strength
        public float dofStart;   // usually 0
        public float dofEnd;     // tiny number (~0.0001) for full-frame blur
        public bool dofHighQuality;

        [Header("Vignette")]
        public bool vignetteEnabled;
        [Range(0f, 1f)] public float vignetteIntensity;
        [ColorUsage(false, true)] public Color vignetteColor;
        [Range(0f, 1f)] public float vignetteSmoothness;

        [Header("Bloom")]
        public bool bloomEnabled;
        [Range(0f, 100f)] public float bloomIntensity;
        [Range(0f, 1f)] public float bloomThreshold;
        [ColorUsage(false, true)] public Color bloomTint;


        [Header("Color Adjustments")]
        public bool colorEnabled;
        [Range(-5f, 5f)] public float postExposure;
        [Range(-100f, 100f)] public float contrast;
        [ColorUsage(false, true)] public Color colorFilter;
        [Range(-180f, 180f)] public float hueShift;
        [Range(-100f, 100f)] public float saturation;
    }

    [Header("States (fill these later)")]
    public PostFXState stateA = DefaultStateA();
    public PostFXState stateB = DefaultStateB();
    public PostFXState stateC = DefaultStateC();

    [Header("Volume Source (optional)")]
    [Tooltip("If assigned, this profile is cloned at runtime so project assets aren’t modified.")]
    public VolumeProfile baseProfile;

    [Header("Blend")]
    [Tooltip("Default blend time if not specified in calls.")]
    public float defaultBlendTime = 0.25f;

    // -------- Internals --------
    Volume _volume;
    VolumeProfile _runtimeProfile;

    DepthOfField _dof;
    Vignette _vignette;
    Bloom _bloom;
    ColorAdjustments _color;

    Coroutine _blend;

    void Awake()
    {
        // Ensure a Volume on this Camera (global overlay)
        _volume = GetComponent<Volume>();
        if (!_volume)
        {
            _volume = gameObject.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 100f;
        }

        // Clone base profile or create a fresh one
        _runtimeProfile = baseProfile ? Instantiate(baseProfile) : ScriptableObject.CreateInstance<VolumeProfile>();
        _volume.profile = _runtimeProfile;

        // Ensure overrides exist
        if (!_runtimeProfile.TryGet(out _dof)) _dof = _runtimeProfile.Add<DepthOfField>(true);
        if (!_runtimeProfile.TryGet(out _vignette)) _vignette = _runtimeProfile.Add<Vignette>(true);
        if (!_runtimeProfile.TryGet(out _bloom)) _bloom = _runtimeProfile.Add<Bloom>(true);
        if (!_runtimeProfile.TryGet(out _color)) _color = _runtimeProfile.Add<ColorAdjustments>(true);

        // Lock-in override flags once (we’ll just change .value later)
        _dof.mode.overrideState = true;
        _dof.gaussianStart.overrideState = true;
        _dof.gaussianEnd.overrideState = true;
        _dof.gaussianMaxRadius.overrideState = true;
        _dof.highQualitySampling.overrideState = true;

        _vignette.intensity.overrideState = true;
        _vignette.color.overrideState = true;
        _vignette.smoothness.overrideState = true;

        _bloom.intensity.overrideState = true;
        _bloom.threshold.overrideState = true;
        _bloom.tint.overrideState = true;

        _color.postExposure.overrideState = true;
        _color.contrast.overrideState = true;
        _color.colorFilter.overrideState = true;
        _color.hueShift.overrideState = true;
        _color.saturation.overrideState = true;

        // Force Gaussian for our DOF workflow
        _dof.mode.value = DepthOfFieldMode.Gaussian;

        // Start in A by default
        ApplyImmediate(stateA);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            SetStateA(0.25f); // blend time = 0.25s, change if you like
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            SetStateB(0.25f);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            SetStateC(0.25f);
        }
    }

    // ---------- Public blend helpers ----------
    void BlendToState(StateId id, float blendTime)
    {
        var target = id == StateId.A ? stateA : id == StateId.B ? stateB : stateC;
        if (_blend != null) StopCoroutine(_blend);
        _blend = StartCoroutine(BlendTo(target, blendTime <= 0f ? defaultBlendTime : blendTime));
    }

    // ---------- Immediate apply (no blend) ----------
    void ApplyImmediate(PostFXState s)
    {
        // DOF
        _dof.active = s.dofEnabled;
        _dof.gaussianStart.value = s.dofStart;
        _dof.gaussianEnd.value = s.dofEnd;
        _dof.gaussianMaxRadius.value = s.dofMaxRadius;
        _dof.highQualitySampling.value = s.dofHighQuality;

        // Vignette
        _vignette.active = s.vignetteEnabled;
        _vignette.intensity.value = s.vignetteIntensity;
        _vignette.color.value = s.vignetteColor;
        _vignette.smoothness.value = s.vignetteSmoothness;

        // Bloom
        _bloom.active = s.bloomEnabled;
        _bloom.intensity.value = s.bloomIntensity;
        _bloom.threshold.value = s.bloomThreshold;
        _bloom.tint.value = s.bloomTint;

        // Color
        _color.active = s.colorEnabled;
        _color.postExposure.value = s.postExposure;
        _color.contrast.value = s.contrast;
        _color.colorFilter.value = s.colorFilter;
        _color.hueShift.value = s.hueShift;
        _color.saturation.value = s.saturation;
    }

    IEnumerator BlendTo(PostFXState target, float time)
    {
        // Snapshot current values
        var from = CaptureCurrent();

        if (Mathf.Approximately(time, 0f))
        {
            ApplyImmediate(target);
            _blend = null;
            yield break;
        }

        // Make sure active flags are enabled for blending paths
        _dof.active = (from.dofEnabled || target.dofEnabled);
        _vignette.active = (from.vignetteEnabled || target.vignetteEnabled);
        _bloom.active = (from.bloomEnabled || target.bloomEnabled);
        _color.active = (from.colorEnabled || target.colorEnabled);

        float t = 0f;
        while (t < time)
        {
            t += Time.unscaledDeltaTime; // unaffected by Time.timeScale
            float a = Mathf.Clamp01(t / time);

            // DOF
            LerpActive(_dof, from.dofEnabled, target.dofEnabled, a);
            _dof.gaussianStart.value = Mathf.Lerp(from.dofStart, target.dofStart, a);
            _dof.gaussianEnd.value = Mathf.Lerp(from.dofEnd, target.dofEnd, a);
            _dof.gaussianMaxRadius.value = Mathf.Lerp(from.dofMaxRadius, target.dofMaxRadius, a);
            _dof.highQualitySampling.value = a < 0.5f ? from.dofHighQuality : target.dofHighQuality;

            // Vignette
            LerpActive(_vignette, from.vignetteEnabled, target.vignetteEnabled, a);
            _vignette.intensity.value = Mathf.Lerp(from.vignetteIntensity, target.vignetteIntensity, a);
            _vignette.color.value = Color.Lerp(from.vignetteColor, target.vignetteColor, a);
            _vignette.smoothness.value = Mathf.Lerp(from.vignetteSmoothness, target.vignetteSmoothness, a);

            // Bloom
            LerpActive(_bloom, from.bloomEnabled, target.bloomEnabled, a);
            _bloom.intensity.value = Mathf.Lerp(from.bloomIntensity, target.bloomIntensity, a);
            _bloom.threshold.value = Mathf.Lerp(from.bloomThreshold, target.bloomThreshold, a);
            _bloom.tint.value = Color.Lerp(from.bloomTint, target.bloomTint, a);

            // Color
            LerpActive(_color, from.colorEnabled, target.colorEnabled, a);
            _color.postExposure.value = Mathf.Lerp(from.postExposure, target.postExposure, a);
            _color.contrast.value = Mathf.Lerp(from.contrast, target.contrast, a);
            _color.colorFilter.value = Color.Lerp(from.colorFilter, target.colorFilter, a);
            _color.hueShift.value = Mathf.Lerp(from.hueShift, target.hueShift, a);
            _color.saturation.value = Mathf.Lerp(from.saturation, target.saturation, a);

            yield return null;
        }

        // Finalize to exact target (and disable inactive)
        ApplyImmediate(target);
        _blend = null;
    }

    PostFXState CaptureCurrent()
    {
        return new PostFXState
        {
            // DOF
            dofEnabled = _dof.active,
            dofMaxRadius = _dof.gaussianMaxRadius.value,
            dofStart = _dof.gaussianStart.value,
            dofEnd = _dof.gaussianEnd.value,
            dofHighQuality = _dof.highQualitySampling.value,

            // Vignette
            vignetteEnabled = _vignette.active,
            vignetteIntensity = _vignette.intensity.value,
            vignetteColor = _vignette.color.value,
            vignetteSmoothness = _vignette.smoothness.value,

            // Bloom
            bloomEnabled = _bloom.active,
            bloomIntensity = _bloom.intensity.value,
            bloomThreshold = _bloom.threshold.value,
            bloomTint = _bloom.tint.value,

            // Color
            colorEnabled = _color.active,
            postExposure = _color.postExposure.value,
            contrast = _color.contrast.value,
            colorFilter = _color.colorFilter.value,
            hueShift = _color.hueShift.value,
            saturation = _color.saturation.value
        };
    }

    static void LerpActive(VolumeComponent comp, bool from, bool to, float a)
    {
        // keep enabled while blending; disable at extremes
        if (a <= 0.001f) comp.active = from;
        else if (a >= 0.999f) comp.active = to;
        else comp.active = (from || to);
    }

    // ---------- Reasonable starter presets (edit later) ----------
    static PostFXState DefaultStateA() => new PostFXState
    {
        dofEnabled = false,
        dofMaxRadius = 0f,
        dofStart = 0f,
        dofEnd = 0.0001f,
        dofHighQuality = true,

        vignetteEnabled = true,
        vignetteIntensity = 0.387f,
        vignetteColor = Color.black,
        vignetteSmoothness = 0.6f,

        bloomEnabled = true,
        bloomIntensity = 1.8f,
        bloomThreshold = 0.4f,
        bloomTint = Color.white,

        colorEnabled = false,
        postExposure = 0f,
        contrast = 0f,
        colorFilter = Color.white,
        hueShift = 0f,
        saturation = 0f
    };

    static PostFXState DefaultStateB() => new PostFXState
    {
        dofEnabled = false,
        dofMaxRadius = 0f,
        dofStart = 0f,
        dofEnd = 0.0001f,
        dofHighQuality = true,

        vignetteEnabled = true,
        vignetteIntensity = 0.387f,
        vignetteColor = Color.black,
        vignetteSmoothness = 0.6f,

        bloomEnabled = true,
        bloomIntensity = 1f,
        bloomThreshold = 0f,
        bloomTint = new Color(0, 184, 255),


        colorEnabled = false,
        postExposure = 0f,
        contrast = 0f,
        colorFilter = Color.white,
        hueShift = 59f,
        saturation = -40f
    };

    static PostFXState DefaultStateC() => new PostFXState
    {
        dofEnabled = false,
        dofMaxRadius = 0f,
        dofStart = 0f,
        dofEnd = 0.0001f,
        dofHighQuality = true,
        vignetteEnabled = true,
        vignetteIntensity = 0.5f,
        vignetteColor = new Color(0f, 0f, 0f, 1f),
        vignetteSmoothness = 0.9f,

        bloomEnabled = true,
        bloomIntensity = 0.5f,
        bloomThreshold = 0.9f,
        bloomTint = Color.white,
        colorEnabled = true,
        postExposure = 0f,
        contrast = 10f,
        colorFilter = Color.white,
        hueShift = 0f,
        saturation = 0f
    };
}
