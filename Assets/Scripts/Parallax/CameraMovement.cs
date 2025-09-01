using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraMovement : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    public Vector2 followOffset = Vector2.zero;
    public float cameraZ = -10f;

    [Header("Smoothing")]
    [Tooltip("Smooth time used by SmoothDamp (seconds).")]
    public float smoothTime = 0.18f;
    [Tooltip("Max speed for SmoothDamp (Infinity = unlimited).")]
    public float maxSpeed = Mathf.Infinity;
    [Tooltip("Snap if we get teleported farther than this distance.")]
    public float snapDistance = 25f;

    [Header("World Bounds (Camera center is constrained within these)")]
    public bool useBounds = true;
    public float minX = -30f;
    public float maxX = 30f;
    public float minY = -10f;
    public float maxY = 20f;
    public float easeAmount = 0.6f; // adjust in Inspector

    [Tooltip("Width (in world units) of the soft fringe where the camera eases into the edge.")]
    public float softEdge = 6f;

    [Header("Optional: Curved X mapping (matches your earlier trig curve)")]
    public bool useCurvedX = false;
    [Tooltip("Equivalent to your 'areaX' * chunkWidth center shifting.")]
    public float areaX = 0f;
    public float chunkWidth = 50f;
    [Tooltip("Your original curve params.")]
    public float xCurveScale = 14.4f;
    public float xCurveAmplitude = 4.9f;
    public float yScale = 0.25f;
    public float yOffset = 0.2f;

    Camera _cam;
    Vector3 _vel; // SmoothDamp cache

    void Awake()
    {
        _cam = GetComponent<Camera>();
    }

    void Start()
    {
        if (!target) return;
        transform.position = ComputeTargetPosition(); // snap on start
    }

    void LateUpdate()
    {
        if (GameState.IsPaused()) return;

        if (!target) return;

        Vector3 desired = ComputeTargetPosition();

        // Snap after long teleports to avoid long easing
        if ((transform.position - desired).sqrMagnitude > snapDistance * snapDistance)
        {
            transform.position = desired;
            _vel = Vector3.zero;
            return;
        }

        transform.position = Vector3.SmoothDamp(
            transform.position, desired, ref _vel, smoothTime, maxSpeed, Time.deltaTime
        );
    }

    Vector3 ComputeTargetPosition()
    {
        // Unclamped desired (follow)
        Vector2 desired2D;
        if (useCurvedX)
        {
            // Reproduce your X curve and Y mapping
            float currentX = target.position.x - Mathf.Repeat(areaX, 1000f) * chunkWidth;
            float s = Mathf.Sin(0.5f * Mathf.PI * (currentX / xCurveScale));
            float s3 = Mathf.Sin(1.5f * Mathf.PI * (currentX / xCurveScale));
            float mappedX = (s - (1f / 9f) * s3) * xCurveAmplitude + (areaX * chunkWidth);
            float mappedY = target.position.y * yScale + yOffset;
            desired2D = new Vector2(mappedX, mappedY) + followOffset;
        }
        else
        {
            desired2D = (Vector2)target.position + followOffset;
        }

        // Apply soft border limiting
        if (useBounds)
            desired2D = SoftClampToBounds(desired2D, GetInnerBounds(), softEdge);

        return new Vector3(desired2D.x, desired2D.y, cameraZ);
    }

    // Camera center cannot pass beyond world bounds minus half the camera view.
    Rect GetInnerBounds()
    {
        // minX/maxX/minY/maxY are the world positions where the *edges* of the camera should stop.
        float xMin = minX;
        float xMax = maxX;
        float yMin = minY;
        float yMax = maxY;

        // If bounds are inverted, collapse to midpoint
        if (xMin > xMax) { float cx = 0.5f * (minX + maxX); xMin = xMax = cx; }
        if (yMin > yMax) { float cy = 0.5f * (minY + maxY); yMin = yMax = cy; }

        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }

    static Rect ExpandRect(Rect r, float m)
    {
        return new Rect(r.xMin - m, r.yMin - m, r.width + 2f * m, r.height + 2f * m);
    }

    // Softly clamp 'p' inside 'hard'. Within 'soft' units outside 'hard', blend toward the edge.
    Vector2 SoftClampToBounds(Vector2 p, Rect hard, float soft)
    {
        float x = EaseToEdge(p.x, hard.xMin, hard.xMax, soft);
        float y = EaseToEdge(p.y, hard.yMin, hard.yMax, soft);
        return new Vector2(x, y);
    }

    float EaseToEdge(float v, float min, float max, float soft)
    {
        if (soft <= 0f) return Mathf.Clamp(v, min, max);

        float tMin = Mathf.Clamp01(Mathf.InverseLerp(min + soft, min, v));
        float tMax = Mathf.Clamp01(Mathf.InverseLerp(max - soft, max, v));

        // Blend between linear and smoothstep using easeAmount
        tMin = Mathf.Lerp(tMin, tMin * tMin * (3f - 2f * tMin), easeAmount);
        tMax = Mathf.Lerp(tMax, tMax * tMax * (3f - 2f * tMax), easeAmount);

        float vMin = Mathf.Lerp(v, min, tMin);
        float vMax = Mathf.Lerp(vMin, max, tMax);
        return Mathf.Clamp(vMax, min, max);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!_cam) _cam = GetComponent<Camera>();
        if (!useBounds) return;

        Rect inner = GetInnerBounds();

        // Hard (inner) bounds the camera center is clamped to
        Gizmos.color = Color.yellow;
        DrawRect(inner);

        // Soft fringe (visualize easing band)
        Gizmos.color = new Color(1f, 1f, 0f, 0.25f);
        DrawRect(ExpandRect(inner, softEdge));
    }

    static void DrawRect(Rect r)
    {
        // 19.2f for x, 10.8 for y

        float xcam_size = 19.2f;
        float ycam_size = 10.8f;
        Vector3 a = new Vector3(r.xMin - xcam_size, r.yMin - ycam_size, 0f);
        Vector3 b = new Vector3(r.xMax + xcam_size, r.yMin - ycam_size, 0f);
        Vector3 c = new Vector3(r.xMax + xcam_size, r.yMax + ycam_size, 0f);
        Vector3 d = new Vector3(r.xMin - xcam_size, r.yMax + ycam_size, 0f);
        Gizmos.DrawLine(a, b); Gizmos.DrawLine(b, c);
        Gizmos.DrawLine(c, d); Gizmos.DrawLine(d, a);
    }
#endif
}
