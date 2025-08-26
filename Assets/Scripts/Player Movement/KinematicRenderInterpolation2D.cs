using UnityEngine;

// ========================================================================
// Script to move the child character (Actual rendered player)
// towards the calculated position at the proper time.
// ========================================================================

public class KinematicRenderInterpolation2D : MonoBehaviour
{
    [Header("Render")]
    public Transform renderTransform;
    public SpriteRenderer spriteRenderer;

    [Header("Facing")]
    [Tooltip("Face last non-zero input. If no input, keep last facing.")]
    public bool keepLastFacingWhenIdle = true;
    [Tooltip("If true, use SpriteRenderer.flipX; otherwise flip localScale.x.")]
    public bool useSpriteRendererFlipX = true;

    Vector3 _prevPos, _currPos;
    float _prevRotZ, _currRotZ;
    float _accum; // time since last fixed step

    Vector3 _baseLocalScale = Vector3.one;
    int _facing = 1; // +1 = right, -1 = left

    void OnEnable()
    {
        _prevPos = _currPos = transform.position;
        _prevRotZ = _currRotZ = transform.eulerAngles.z;
        _accum = 0f;

        if (renderTransform != null)
            _baseLocalScale = renderTransform.localScale;
    }

    void FixedUpdate()
    {
        _prevPos = _currPos;
        _prevRotZ = _currRotZ;
        _currPos = transform.position;
        _currRotZ = transform.eulerAngles.z;
        _accum = 0f;
    }

    void Update()
    {
        // --- Instant facing based on keys (independent of physics/velocity) ---
        bool left = Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A);
        bool right = Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D);

        int inputDir = 0;
        if (left && !right) inputDir = -1;
        else if (right && !left) inputDir = 1;

        if (inputDir != 0)
        {
            _facing = inputDir; // flip instantly when a directional key is pressed
        }
        else if (!keepLastFacingWhenIdle)
        {
            // Optional: face right by default when idle (set keepLastFacingWhenIdle=false to use this)
            _facing = 1;
        }

        // --- Interpolation for smooth visual movement/rotation ---
        _accum += Time.deltaTime;
        float t = Mathf.Clamp01(Time.fixedDeltaTime > 0f ? _accum / Time.fixedDeltaTime : 1f);
        if (renderTransform)
        {
            renderTransform.position = Vector3.Lerp(_prevPos, _currPos, t);
            float z = Mathf.LerpAngle(_prevRotZ, _currRotZ, t);
            renderTransform.rotation = Quaternion.Euler(0, 0, z);
        }

        // --- Apply facing flip after interpolation ---
        ApplyFacing();
    }

    void ApplyFacing()
    {
        if (renderTransform == null) return;

        if (useSpriteRendererFlipX && spriteRenderer != null)
        {
            // Assumes art faces RIGHT by default
            spriteRenderer.flipX = (_facing < 0);
        }
        else
        {
            float sx = Mathf.Abs(_baseLocalScale.x) * (_facing < 0 ? -1f : 1f);
            if (Mathf.Abs(renderTransform.localScale.x - sx) > 0.0001f)
            {
                renderTransform.localScale = new Vector3(sx, _baseLocalScale.y, _baseLocalScale.z);
            }
        }
    }
}