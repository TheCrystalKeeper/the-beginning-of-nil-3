using UnityEngine;

public class MinimalKinematicController2D : MonoBehaviour
{
    [Header("Capsule Shape (2D)")]
    public float height = 1.8f;      // along Y
    public float radius = 0.35f;     // along X
    public float skin = 0.02f;

    [Header("Movement")]
    public float moveSpeed = 8f;
    [Tooltip("How fast you ramp up toward target horizontal speed (m/s^2).")]
    public float acceleration = 90f;
    [Tooltip("How fast you slow down when target speed is lower (m/s^2).")]
    public float deceleration = 120f;
    public float gravity = 25f;
    public float jumpSpeed = 9f;
    [Range(0, 89)] public float maxSlopeAngle = 50f;
    public float groundSnapDistance = 0.12f;
    public int maxSlideIterations = 3;

    [Header("Collision")]
    public LayerMask collisionMask = ~0;

    [Header("Jump Grounding Prevention")]
    [Tooltip("Time after a jump during which ground detection is suppressed to avoid re-sticking.")]
    public float jumpGroundingPreventionTime = 0.10f;

    [Header("Debug")]
    public bool drawGizmos = true;

    // State
    public Vector2 Velocity { get; private set; }
    public bool IsGrounded { get; private set; }
    public Vector2 GroundNormal { get; private set; } = Vector2.up;

    // input
    Vector2 _moveInput;
    bool _jumpRequested;

    // timers
    float _groundingPreventionTimer;

    void Awake()
    {
        // Small unstick so first cast doesn't start overlapped with ground
        transform.position += Vector3.up * (skin * 2f);
    }

    void Update()
    {
        // Direct key polling so it works with any input system
        float x = 0f;
        if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) x -= 1f;
        if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) x += 1f;
        _moveInput = new Vector2(Mathf.Clamp(x, -1f, 1f), 0f);

        // Jump on Z
        if (Input.GetKeyDown(KeyCode.Z))
            _jumpRequested = true;
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        _groundingPreventionTimer = Mathf.Max(0f, _groundingPreventionTimer - dt);

        // 1) Ground check (skipped briefly after jumping)
        if (_groundingPreventionTimer <= 0f)
            DoGroundCheck();
        else
            IsGrounded = false;

        // 2) Build desired horizontal speed from input (same logic ground/air)
        float targetX = _moveInput.x * moveSpeed;

        // --- Removed slope projection so slopes do NOT affect horizontal speed ---
        // (We keep control consistent on flats and slopes.)

        // --- Accel/Decel toward targetX (applies in air AND on ground) ---
        float rate = (Mathf.Abs(targetX) > Mathf.Abs(Velocity.x)) ? acceleration : deceleration;
        Velocity = new Vector2(Mathf.MoveTowards(Velocity.x, targetX, rate * dt), Velocity.y);

        // 3) Jump & gravity (jump doesn't touch X)
        if (IsGrounded)
        {
            // Keep glued to ground when not jumping
            if (Velocity.y < 0f) Velocity = new Vector2(Velocity.x, 0f);

            if (_jumpRequested)
            {
                Velocity = new Vector2(Velocity.x, jumpSpeed); // <- don't modify X on jump
                IsGrounded = false;
                _groundingPreventionTimer = jumpGroundingPreventionTime;
            }
            else
            {
                // Standing: ensure no upward velocity accumulates
                Velocity = new Vector2(Velocity.x, 0f);
            }
        }
        else
        {
            // Airborne: same horizontal control; apply gravity only to Y
            Velocity = new Vector2(Velocity.x, Velocity.y - gravity * dt);
        }
        _jumpRequested = false;

        // 4) Move with capsule casts & sliding
        Vector2 displacement = Velocity * dt;
        MoveWithCasts(displacement);

        // 4.5) While grounded and not rising, do a tiny downward follow so we hug descending slopes
        if (IsGrounded && Velocity.y <= 0f)
            FollowGroundWhileGrounded();

        // 5) Optional ground snap (skip while in prevention window)
        if (_groundingPreventionTimer <= 0f && !IsGrounded)
            TryGroundSnap();
    }

    // ---------- Physics2D helpers ----------

    void DoGroundCheck()
    {
        Vector2 origin = transform.position;
        Vector2 size = new Vector2(radius * 2f, Mathf.Max(0.01f, height));

        float checkDist = groundSnapDistance + skin + 0.05f;
        RaycastHit2D hit = Physics2D.CapsuleCast(
            origin + Vector2.up * 0.01f,
            size,
            CapsuleDirection2D.Vertical,
            0f,
            Vector2.down,
            checkDist,
            collisionMask
        );

        if (hit)
        {
            GroundNormal = hit.normal;
            IsGrounded = Vector2.Angle(hit.normal, Vector2.up) <= maxSlopeAngle &&
                         hit.distance <= groundSnapDistance + 0.02f;
        }
        else
        {
            GroundNormal = Vector2.up;
            IsGrounded = false;
        }
    }

    void TryGroundSnap()
    {
        if (Velocity.y > 0f) return;

        Vector2 origin = transform.position;
        Vector2 size = new Vector2(radius * 2f, Mathf.Max(0.01f, height));

        RaycastHit2D hit = Physics2D.CapsuleCast(
            origin + Vector2.up * 0.05f,
            size,
            CapsuleDirection2D.Vertical,
            0f,
            Vector2.down,
            groundSnapDistance + 0.05f,
            collisionMask
        );

        if (hit && Vector2.Angle(hit.normal, Vector2.up) <= maxSlopeAngle)
        {
            float snap = Mathf.Max(0f, hit.distance - skin);
            transform.position += (Vector3)(Vector2.down * snap);
            IsGrounded = true;
            GroundNormal = hit.normal;
            if (Velocity.y < 0f) Velocity = new Vector2(Velocity.x, 0f);
        }
    }

    // New: keep hugging ground even while grounded and moving onto a descending slope
    void FollowGroundWhileGrounded()
    {
        Vector2 origin = (Vector2)transform.position + Vector2.up * 0.02f;
        Vector2 size = new Vector2(radius * 2f, Mathf.Max(0.01f, height));

        RaycastHit2D hit = Physics2D.CapsuleCast(
            origin,
            size,
            CapsuleDirection2D.Vertical,
            0f,
            Vector2.down,
            groundSnapDistance + 0.05f,
            collisionMask
        );

        if (hit && Vector2.Angle(hit.normal, Vector2.up) <= maxSlopeAngle)
        {
            float snap = Mathf.Max(0f, hit.distance - skin);
            if (snap > 0f)
            {
                transform.position += (Vector3)(Vector2.down * snap);
                GroundNormal = hit.normal;
                // Stay grounded; don't change Velocity.x so speed remains constant on slopes
                if (Velocity.y < 0f) Velocity = new Vector2(Velocity.x, 0f);
            }
        }
    }

    void MoveWithCasts(Vector2 displacement)
    {
        Vector2 remaining = displacement;
        Vector2 pos = transform.position;
        Vector2 size = new Vector2(radius * 2f, Mathf.Max(0.01f, height));

        for (int i = 0; i < maxSlideIterations && remaining.sqrMagnitude > 1e-8f; i++)
        {
            Vector2 dir = remaining.normalized;
            float dist = remaining.magnitude;

            // Lift cast when grounded & moving sideways (prevents ground self-hit).
            // Also lift while rising during prevention window after jump.
            Vector2 castPos = pos;
            bool horizontalish = Mathf.Abs(dir.y) < 0.001f;
            bool goingUp = dir.y > 0.001f;

            if ((IsGrounded && horizontalish) || (_groundingPreventionTimer > 0f && goingUp))
                castPos += Vector2.up * (skin * 2f);

            RaycastHit2D hit = Physics2D.CapsuleCast(
                castPos,
                size,
                CapsuleDirection2D.Vertical,
                0f,
                dir,
                dist + skin,
                collisionMask
            );

            if (hit)
            {
                bool hitIsGroundish = Vector2.Angle(hit.normal, Vector2.up) < 5f;

                // Ignore ground self-hit in two cases:
                if ((IsGrounded && horizontalish && hitIsGroundish && hit.distance <= skin * 1.5f) ||
                    (_groundingPreventionTimer > 0f && goingUp && hitIsGroundish && hit.distance <= skin * 2f))
                {
                    pos += remaining;
                    remaining = Vector2.zero;
                    break;
                }

                float travel = Mathf.Max(0f, hit.distance - skin);
                pos += dir * travel;

                // Slide along the surface (projection onto tangent)
                Vector2 n = hit.normal;
                Vector2 tangent = new Vector2(n.y, -n.x);
                float along = Vector2.Dot(remaining - dir * travel, tangent);
                remaining = tangent * along;

                if (IsGrounded && Vector2.Angle(n, Vector2.up) > maxSlopeAngle)
                    IsGrounded = false;
            }
            else
            {
                pos += remaining;
                remaining = Vector2.zero;
                break;
            }
        }

        transform.position = pos;
    }

    void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;
        Gizmos.color = Color.cyan;

        // Visualize capsule as a box stand-in (good enough for sizing)
        Vector2 size = new Vector2(radius * 2f, Mathf.Max(0.01f, height));
        Gizmos.DrawWireCube(transform.position, size);
    }
}
