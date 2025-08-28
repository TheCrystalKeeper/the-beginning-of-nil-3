using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections;

[RequireComponent(typeof(Camera))]
public class CameraLensBlurController : MonoBehaviour
{
    [Header("Source Profile (optional)")]
    [Tooltip("Assign your Renderer’s Default Volume Profile asset here (optional). " +
             "If left empty, an empty runtime profile will be created.")]
    public VolumeProfile sourceProfile; // your Default Volume Profile asset

    [Header("Blur Settings")]
    [Range(0f, 2f)] public float targetMaxRadius = 1.5f; // blur strength
    public float tweenTime = 0.25f;                      // seconds

    Volume _volume;
    VolumeProfile _runtimeProfile;
    DepthOfField _dof;
    Coroutine _tween;

    void Awake()
    {
        // Ensure a Volume on this camera
        _volume = GetComponent<Volume>();
        if (!_volume)
        {
            _volume = gameObject.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 100f;
        }

        // Clone the source profile so we don't modify the project asset
        if (sourceProfile)
            _runtimeProfile = Instantiate(sourceProfile);
        else
            _runtimeProfile = ScriptableObject.CreateInstance<VolumeProfile>();

        _volume.profile = _runtimeProfile;

        // Ensure Depth Of Field (Gaussian)
        if (!_runtimeProfile.TryGet(out _dof))
            _dof = _runtimeProfile.Add<DepthOfField>(true);

        _dof.active = true;
        _dof.mode.overrideState = true;
        _dof.mode.value = DepthOfFieldMode.Gaussian;

        // Configure for full-frame blur controlled via gaussianMaxRadius
        _dof.gaussianStart.overrideState = true;
        _dof.gaussianEnd.overrideState = true;
        _dof.gaussianMaxRadius.overrideState = true;
        _dof.highQualitySampling.overrideState = true;

        _dof.gaussianStart.value = 0f;
        _dof.gaussianEnd.value = 0.0001f;  // tiny focus range -> screen-wide blur
        _dof.gaussianMaxRadius.value = 0f; // start clear
        _dof.highQualitySampling.value = true;
    }

    public void BlurOut() => StartTween(targetMaxRadius, tweenTime);
    public void ClearBlur() => StartTween(0f, tweenTime);

    void StartTween(float toRadius, float time)
    {
        if (_tween != null) StopCoroutine(_tween);
        _tween = StartCoroutine(TweenRadius(toRadius, Mathf.Max(0f, time)));
    }

    IEnumerator TweenRadius(float to, float time)
    {
        float from = _dof.gaussianMaxRadius.value;
        if (Mathf.Approximately(time, 0f))
        {
            _dof.gaussianMaxRadius.value = to;
            _tween = null;
            yield break;
        }

        float t = 0f;
        while (t < time)
        {
            t += Time.unscaledDeltaTime; // works while paused
            float a = Mathf.Clamp01(t / time);
            _dof.gaussianMaxRadius.value = Mathf.Lerp(from, to, a);
            yield return null;
        }
        _dof.gaussianMaxRadius.value = to;
        _tween = null;
    }

    // Optional quick test
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.B)) BlurOut();
        if (Input.GetKeyDown(KeyCode.N)) ClearBlur();
    }
}
