using UnityEngine;

[DisallowMultipleComponent]
public class ParallaxPositionScale : MonoBehaviour
{
    [Header("Camera")]
    [Tooltip("Camera to track. If left empty, uses Camera.main.")]
    public Camera cam;

    [Header("Parallax Settings")]
    [Tooltip("Extra Y offset applied after parallax.")]
    public float offsetY = 0f;

    [Tooltip("Higher = weaker parallax. Matches your old 'scale' (default 18).")]
    public float parallaxScale = 40f;
    [Tooltip("Invert only the camera-driven Y motion (optional).")]
    public bool invertY = false;

    [Tooltip("Enable small horizontal wobble by Z (keeps your original look).")]
    public bool useWobble = true;

    [Tooltip("Horizontal wobble amplitude per Z unit (original: 0.01 * z).")]
    public float wobbleAmplitudePerZ = 0.01f;

    [Tooltip("Horizontal wobble frequency (original used 0.2).")]
    public float wobbleFrequency = 0.2f;

    [Header("Activation")]
    [Tooltip("Only update when camera is near the layer’s start point (perf saver). Set 0 to always update.")]
    public float activationDistance = 20f;

    // Optional metadata
    public int section;

    // --- minimal addition: auto re-anchor on big camera jump/teleport ---
    [Tooltip("If the camera moves farther than this in one frame, re-anchor to avoid stretched parallax. Set 0 to disable.")]
    public float reanchorJumpThreshold = 6f;
    Vector3 _prevCamPos;
    // --------------------------------------------------------------------

    // Internals
    Transform _camT;
    float _startX, _startY, _z;
    float _camStartX, _camStartY;

    void Awake()
    {
        if (!cam) cam = Camera.main;
        if (cam) _camT = cam.transform;
    }

    void Start()
    {
        // Anchor to where you placed the object in the editor
        _startX = transform.position.x;
        _startY = transform.position.y;
        _z = transform.position.z;

        // Also anchor to the camera's position at play start
        if (_camT)
        {
            _camStartX = _camT.position.x;
            _camStartY = _camT.position.y;
            _prevCamPos = _camT.position; // minimal addition
        }

        ApplyParallax(); // initial placement, no pop
    }

    void LateUpdate() // run after camera moves
    {
        if (!_camT) return;

        // minimal addition: detect large camera jump and re-anchor
        if (reanchorJumpThreshold > 0f)
        {
            float step = Vector3.Distance(_prevCamPos, _camT.position);
            if (step > reanchorJumpThreshold)
                ForceReanchor();
            _prevCamPos = _camT.position;
        }

        ApplyParallax();
    }

    void ApplyParallax()
    {
        Vector3 camPos = _camT.position;

        if (activationDistance > 0f)
        {
            // Same idea as before: only update when camera is near this layer's start
            Vector3 startAtCamZ = new Vector3(_startX, _startY, camPos.z);
            if (Vector3.Distance(camPos, startAtCamZ) >= activationDistance)
                return;
        }

        // Camera delta since play started (so object stays where you placed it when cam hasn't moved)
        float camDX = camPos.x - _camStartX;
        float camDY = camPos.y - _camStartY;

        float denom = Mathf.Max(0.0001f, parallaxScale);
        float factor = _z / denom;

        float x = _startX + camDX * factor;
        float y = _startY + (invertY ? -camDY : camDY) * factor;

        if (useWobble)
        {
            float wobble = Mathf.Sin(wobbleFrequency * (Time.time + _startX * 105.3f)) * (wobbleAmplitudePerZ * _z);
            x += wobble;
        }

        y += offsetY;

        transform.position = new Vector3(x, y, _z);
    }

    // minimal helper: re-anchor to current camera, preserving visual position (no pop)
    void ForceReanchor()
    {
        Vector3 camPos = _camT.position;

        // current displayed position
        Vector3 current = transform.position;

        // remove wobble/offset so re-anchoring keeps the same on-screen position
        float wobble = 0f;
        if (useWobble)
            wobble = Mathf.Sin(wobbleFrequency * (Time.time + _startX * 105.3f)) * (wobbleAmplitudePerZ * _z);

        _startX = current.x - wobble;
        _startY = current.y - offsetY;

        _camStartX = camPos.x;
        _camStartY = camPos.y;
    }
}
