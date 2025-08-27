using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class GrassAnimation : MonoBehaviour
{
    [Header("Sway Settings")]
    [Tooltip("How far the grass bends left and right (in degrees).")]
    public float swayAmount = 5f;
    [Tooltip("How fast the grass sways back and forth.")]
    public float swaySpeed = 2f;
    [Tooltip("Random time offset so multiple tufts do not sway in sync.")]
    public float randomPhaseOffset = 0f;
    [Tooltip("Multiplier to reduce idle sway while the player is inside the box.")]
    [Range(0f, 1f)] public float swayInsideMultiplier = 0.3f;

    [Header("Attract (while player inside the box)")]
    [Tooltip("Local-space box half-size around this tuft that counts as 'inside'.")]
    public Vector2 rustleBoxHalfSize = new Vector2(3f, 3f);
    [Tooltip("Local-space offset of the box center from the tuft position.")]
    public Vector2 rustleBoxOffset = Vector2.zero;
    [Tooltip("Degrees of bend per world-unit of player horizontal offset.")]
    public float attractGain = 8f;
    [Tooltip("Clamp for the attract bend (degrees).")]
    public float attractMaxDeflection = 15f;

    [Header("Return Spring")]
    [Tooltip("How strongly the tuft tries to reach the target deflection.")]
    public float springStiffness = 40f;
    [Tooltip("How much it resists motion (deg/s damping). ~critically damped near 2*sqrt(k).")]
    public float damping = 12f;
    [Tooltip("Hard clamp for total deflection to avoid extremes (degrees).")]
    public float maxDeflectionClamp = 25f;

    private float _baseRotation;
    private Transform _player;
    private MinimalKinematicController2D _playerCtrl; // not required; here for future use
    private Vector3 _prevPlayerPos;

    // spring state: deflection angle (deg) and its angular velocity (deg/s)
    private float _deflection;
    private float _deflectionVel;

    // contact state
    private bool _wasInside;

    void Start()
    {
        _baseRotation = transform.eulerAngles.z;

        if (Mathf.Approximately(randomPhaseOffset, 0f))
            randomPhaseOffset = Random.Range(0f, Mathf.PI * 2f);

        var tagged = GameObject.FindGameObjectWithTag("Player");
        if (tagged)
        {
            _player = tagged.transform;
            _playerCtrl = _player.GetComponent<MinimalKinematicController2D>();
            _prevPlayerPos = _player.position;
        }
    }

    void Update()
    {
        float dt = Time.deltaTime;

        // idle sway (reduced when inside)
        bool inside = false;
        float desired = 0f;

        if (_player)
        {
            Vector2 center = (Vector2)transform.position + rustleBoxOffset;
            Vector2 half = rustleBoxHalfSize;
            Vector2 p = _player.position;

            inside = Mathf.Abs(p.x - center.x) <= half.x &&
                     Mathf.Abs(p.y - center.y) <= half.y;

            if (inside)
            {
                // Pull toward the player's horizontal offset
                float dx = p.x - center.x; // horizontal difference from box center
                desired = Mathf.Clamp(dx * attractGain, -attractMaxDeflection, attractMaxDeflection);

                // Direction-dependent bias based on player's horizontal velocity
                float dtSafe = Mathf.Max(0.0001f, Time.deltaTime);
                float vx = (_player.position.x - _prevPlayerPos.x) / dtSafe;
                float speed01 = Mathf.Clamp01(Mathf.Abs(vx) / 5f);     // soft cap speed
                float dir = Mathf.Sign(vx);                             // -1 left, +1 right
                float bias = dir * (attractMaxDeflection * 0.25f) * speed01; // up to 25% of max
                desired = Mathf.Clamp(desired + bias, -attractMaxDeflection, attractMaxDeflection);
            }
        }

        float swayMul = inside ? swayInsideMultiplier : 1f;
        float angle = _baseRotation + Mathf.Cos(Time.time * swaySpeed + randomPhaseOffset) * (swayAmount * swayMul);

        // spring toward desired (desired=0 when outside)
        float k = Mathf.Max(0f, springStiffness);
        float c = Mathf.Max(0f, damping);
        float acc = -k * (_deflection - desired) - c * _deflectionVel;

        _deflectionVel += acc * dt;
        _deflection += _deflectionVel * dt;

        // clamp total deflection
        _deflection = Mathf.Clamp(_deflection, -maxDeflectionClamp, maxDeflectionClamp);

        angle += _deflection;

        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        _wasInside = inside;
        if (_player) _prevPlayerPos = _player.position;
    }


    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.9f, 0.3f, 0.35f);
        Vector3 c = transform.position + (Vector3)rustleBoxOffset;
        Vector3 size = new Vector3(rustleBoxHalfSize.x * 2f, rustleBoxHalfSize.y * 2f, 0.01f);
        Gizmos.DrawCube(c, size);
        Gizmos.color = new Color(0.2f, 0.9f, 0.3f, 0.8f);
        Gizmos.DrawWireCube(c, size);
    }
}
