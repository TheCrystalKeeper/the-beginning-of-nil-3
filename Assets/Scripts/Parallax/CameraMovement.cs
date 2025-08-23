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
    public Rect worldBounds = new Rect(-30, -10, 60, 30);  // x,y,width,height
    [Tooltip("How wide the 'soft fringe' is at the edges where the camera eases out.")]
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
        float halfH = _cam.orthographicSize;
        float halfW = halfH * _cam.aspect;

        float xMin = worldBounds.xMin + halfW;
        float xMax = worldBounds.xMax - halfW;
        float yMin = worldBounds.yMin + halfH;
        float yMax = worldBounds.yMax - halfH;

        // If bounds are smaller than the view, collapse to center
        if (xMin > xMax) { float cx = worldBounds.center.x; xMin = xMax = cx; }
        if (yMin > yMax) { float cy = worldBounds.center.y; yMin = yMax = cy; }

        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }

    // Softly clamp 'p' inside 'hard'. Within 'soft' units outside 'hard', blend toward the edge.
    static Vector2 SoftClampToBounds(Vector2 p, Rect hard, float soft)
    {
        if (soft <= 0f)
            return new Vector2(Mathf.Clamp(p.x, hard.xMin, hard.xMax),
                               Mathf.Clamp(p.y, hard.yMin, hard.yMax));

        float xHard = Mathf.Clamp(p.x, hard.xMin, hard.xMax);
        float yHard = Mathf.Clamp(p.y, hard.yMin, hard.yMax);

        float tx = Outside01(p.x, hard.xMin, hard.xMax, soft);
        float ty = Outside01(p.y, hard.yMin, hard.yMax, soft);

        // ease = smoothstep(0..1)
        tx = Mathf.SmoothStep(0f, 1f, tx);
        ty = Mathf.SmoothStep(0f, 1f, ty);

        float x = Mathf.Lerp(p.x, xHard, tx);
        float y = Mathf.Lerp(p.y, yHard, ty);

        // Prevent wandering too far: clamp to hard expanded by 'soft'
        Rect expanded = ExpandRect(hard, soft);
        x = Mathf.Clamp(x, expanded.xMin, expanded.xMax);
        y = Mathf.Clamp(y, expanded.yMin, expanded.yMax);

        return new Vector2(x, y);
    }

    static float Outside01(float v, float min, float max, float soft)
    {
        if (v < min) return Mathf.Clamp01((min - v) / soft);
        if (v > max) return Mathf.Clamp01((v - max) / soft);
        return 0f; // inside
    }

    static Rect ExpandRect(Rect r, float m)
    {
        return new Rect(r.xMin - m, r.yMin - m, r.width + 2f * m, r.height + 2f * m);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!_cam) _cam = GetComponent<Camera>();
        if (!useBounds) return;

        Gizmos.color = Color.yellow; // hard (inner) bounds
        Rect inner = GetInnerBounds();
        DrawRect(inner);

        Gizmos.color = new Color(1f, 1f, 0f, 0.25f); // soft fringe
        DrawRect(ExpandRect(inner, softEdge));
    }

    static void DrawRect(Rect r)
    {
        Vector3 a = new Vector3(r.xMin, r.yMin, 0f);
        Vector3 b = new Vector3(r.xMax, r.yMin, 0f);
        Vector3 c = new Vector3(r.xMax, r.yMax, 0f);
        Vector3 d = new Vector3(r.xMin, r.yMax, 0f);
        Gizmos.DrawLine(a, b); Gizmos.DrawLine(b, c);
        Gizmos.DrawLine(c, d); Gizmos.DrawLine(d, a);
    }
#endif
}
