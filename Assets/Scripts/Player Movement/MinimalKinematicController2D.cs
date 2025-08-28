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

    [Header("Step Assist (for walking up slopes/joins)")]
    [Tooltip("Max vertical nudge to take when a sideways move is blocked. Think 'few pixels' in world units.")]
    public float stepUpHeight = 0.15f;

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

    // states
    bool _frozen;
    public bool IsFrozen => _frozen;

    void Awake()
    {
        // Small unstick so first cast doesn't start overlapped with ground
        transform.position += Vector3.up * (skin * 2f);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        { if (GameState.IsPaused())
                GameState.Resume();
            else
                GameState.Pause();
        }

        if (GameState.IsPaused()) return;

        if (_frozen) return;
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
        if (GameState.IsPaused()) return;
        if (_frozen) return;
        float dt = Time.fixedDeltaTime;
        _groundingPreventionTimer = Mathf.Max(0f, _groundingPreventionTimer - dt);

        // 1) Ground check (skipped briefly after jumping)
        if (_groundingPreventionTimer <= 0f)
            DoGroundCheck();
        else
            IsGrounded = false;

        // 2) Build desired horizontal speed from input (same logic ground/air)
        float targetX = _moveInput.x * moveSpeed;

        // Slopes do NOT change horizontal speed; we keep control consistent

        // Accel/Decel toward targetX (applies in air AND on ground)
        float rate = (Mathf.Abs(targetX) > Mathf.Abs(Velocity.x)) ? acceleration : deceleration;
        Velocity = new Vector2(Mathf.MoveTowards(Velocity.x, targetX, rate * dt), Velocity.y);

        // 3) Jump & gravity (jump doesn't touch X)
        if (IsGrounded)
        {
            if (Velocity.y < 0f) Velocity = new Vector2(Velocity.x, 0f);

            if (_jumpRequested)
            {
                Velocity = new Vector2(Velocity.x, jumpSpeed);
                IsGrounded = false;
                _groundingPreventionTimer = jumpGroundingPreventionTime;
            }
            else
            {
                Velocity = new Vector2(Velocity.x, 0f);
            }
        }
        else
        {
            Velocity = new Vector2(Velocity.x, Velocity.y - gravity * dt);
        }
        _jumpRequested = false;

        // 4) Move with capsule casts & sliding (+ simple step-up assist)
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

    // Keep hugging ground even while grounded and moving onto a descending slope
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

            // Inset casts to avoid re-hitting current ground
            Vector2 castPos = pos;
            bool horizontalish = Mathf.Abs(dir.y) < 0.001f;
            bool goingUp = dir.y > 0.001f;
            bool rising = Velocity.y > 0f;
            bool canGroundNow = (_groundingPreventionTimer <= 0f) && (Velocity.y <= 0f);

            if (IsGrounded)
                castPos += GroundNormal * (skin * 2f);
            else if (_groundingPreventionTimer > 0f && goingUp)
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
                float angleFromUp = Vector2.Angle(hit.normal, Vector2.up);
                bool walkable = angleFromUp <= maxSlopeAngle;
                bool movingIntoSurface = Vector2.Dot(dir, -hit.normal) > 0f;
                bool floorLike = hit.normal.y > 0.5f; // tweak (0.4–0.7) if desired

                // --- AIRBORNE CORNER SUPPRESSION ---
                // While rising or during prevention, treat floor-like hits as blockers:
                // don't slide along the floor tangent and don't ground.
                if ((rising || _groundingPreventionTimer > 0f) && floorLike && movingIntoSurface)
                {
                    float travel = Mathf.Max(0f, hit.distance - skin);
                    pos += dir * travel;

                    // Keep only vertical leftover (prevents "ride-up" onto top)
                    Vector2 after = remaining - dir * travel;
                    remaining = Project2D(after, Vector2.up);

                    // Optional: if we were pushing into a side, zero X to stop grinding
                    if (Mathf.Abs(hit.normal.x) > 0.25f && Mathf.Sign(Velocity.x) == -Mathf.Sign(hit.normal.x))
                        Velocity = new Vector2(0f, Velocity.y);

                    IsGrounded = false;
                    // Tiny clearance to avoid re-hit
                    pos += hit.normal * (skin * 0.25f);
                    continue;
                }

                // --- TOO-STEEP SLOPE AS WALL when moving sideways ---
                if (!walkable && horizontalish && movingIntoSurface)
                {
                    float travel = Mathf.Max(0f, hit.distance - skin);
                    pos += dir * travel;

                    Velocity = new Vector2(0f, Velocity.y);          // stop horizontal
                    remaining = new Vector2(0f, remaining.y);        // cancel remaining X
                    pos += hit.normal * (skin * 0.5f);               // nudge off wall
                    IsGrounded = false;
                    continue;
                }

                // --- Walkable slope while moving sideways: slide along tangent ---
                if (walkable && horizontalish)
                {
                    float travel = Mathf.Max(0f, hit.distance - skin);
                    pos += dir * travel;

                    float leftover = Mathf.Max(0f, dist - travel);
                    Vector2 n = hit.normal;
                    Vector2 tangent = new Vector2(n.y, -n.x).normalized;
                    if (Vector2.Dot(tangent, dir) < 0f) tangent = -tangent;

                    remaining = tangent * leftover;

                    if (canGroundNow)
                    {
                        IsGrounded = true;
                        GroundNormal = n;
                    }

                    pos += n * (skin * 0.25f);
                    continue;
                }

                // --- Flat ground self-hit tolerance ---
                bool flatGround = angleFromUp < 5f;
                if ((IsGrounded && horizontalish && flatGround && hit.distance <= skin * 1.5f) ||
                    (_groundingPreventionTimer > 0f && goingUp && flatGround && hit.distance <= skin * 2f))
                {
                    pos += remaining;
                    remaining = Vector2.zero;
                    break;
                }

                // --- Step-up attempt (horizontal only) ---
                if (horizontalish && stepUpHeight > 0f)
                {
                    int steps = 3;
                    float step = stepUpHeight / steps;
                    bool stepped = false;

                    for (int s = 1; s <= steps; s++)
                    {
                        float raise = s * step;
                        Vector2 raisedPos = pos + Vector2.up * raise;

                        RaycastHit2D upBlock = Physics2D.CapsuleCast(
                            pos, size, CapsuleDirection2D.Vertical, 0f,
                            Vector2.up, raise, collisionMask
                        );
                        if (upBlock && upBlock.distance <= skin) break;

                        RaycastHit2D hit2 = Physics2D.CapsuleCast(
                            raisedPos, size, CapsuleDirection2D.Vertical, 0f,
                            dir, dist + skin, collisionMask
                        );

                        if (!hit2 || hit2.distance > skin + 1e-5f)
                        {
                            pos = raisedPos;

                            if (hit2)
                            {
                                float travel2 = Mathf.Max(0f, hit2.distance - skin);
                                pos += dir * travel2;

                                float leftover2 = Mathf.Max(0f, dist - travel2);
                                remaining = (dir * leftover2);
                            }
                            else
                            {
                                pos += dir * dist;
                                remaining = Vector2.zero;
                            }

                            if (canGroundNow)
                            {
                                IsGrounded = true;
                                pos += Vector2.up * (skin * 0.25f);
                            }
                            stepped = true;
                            break;
                        }
                    }

                    if (stepped) continue;
                }

                // --- Generic slide (walls / ceilings / too-steep when not horizontal) ---
                float travelGeneric = Mathf.Max(0f, hit.distance - skin);
                pos += dir * travelGeneric;

                Vector2 nn = hit.normal;
                Vector2 t = new Vector2(nn.y, -nn.x);
                float along = Vector2.Dot(remaining - dir * travelGeneric, t);
                remaining = t * along;

                // Ceiling: kill upward velocity
                if (goingUp && nn.y < -0.5f)
                    Velocity = new Vector2(Velocity.x, 0f);

                // Only allow grounding when we’re not rising and not in prevention window
                if (canGroundNow && Vector2.Angle(nn, Vector2.up) <= maxSlopeAngle)
                {
                    IsGrounded = true;
                    GroundNormal = nn;
                }
                else if (IsGrounded && Vector2.Angle(nn, Vector2.up) > maxSlopeAngle)
                {
                    IsGrounded = false;
                }

                pos += nn * (skin * 0.25f);
            }
            else
            {
                pos += remaining;
                remaining = Vector2.zero;
                break;
            }
        }

        // Final micro-unstick from side walls to avoid rare wedging
        UnstickFromWalls(ref pos, size);

        transform.position = pos;
    }

    // Nudge out from very close side-walls to avoid getting stuck after slope transitions.
    void UnstickFromWalls(ref Vector2 pos, Vector2 size)
    {
        // Only check left/right so we don't fight ground snapping.
        Vector2[] dirs = { Vector2.left, Vector2.right };
        float targetClearance = skin * 0.5f;

        foreach (var d in dirs)
        {
            RaycastHit2D h = Physics2D.CapsuleCast(
                pos, size, CapsuleDirection2D.Vertical, 0f,
                d, skin * 1.5f, collisionMask
            );

            if (h && h.distance < targetClearance)
            {
                float push = (targetClearance - h.distance);
                pos -= d * push; // move away from the nearby wall
            }
        }
    }

    static Vector2 Project2D(Vector2 a, Vector2 onto)
    {
        float denom = onto.sqrMagnitude;
        return denom > 1e-8f ? onto * (Vector2.Dot(a, onto) / denom) : Vector2.zero;
    }

    void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;
        Gizmos.color = Color.cyan;

        // Visualize capsule as a box stand-in (good enough for sizing)
        Vector2 size = new Vector2(radius * 2f, Mathf.Max(0.01f, height));
        Gizmos.DrawWireCube(transform.position, size);
    }

    // Call to completely freeze/unfreeze the controller.
    public void Freeze(bool zeroOutVelocity = true, bool clearInput = true)
    {
        _frozen = true;
        if (clearInput)
        {
            _moveInput = Vector2.zero;
            _jumpRequested = false;
        }
        if (zeroOutVelocity)
            Velocity = Vector2.zero;
    }

    public void Unfreeze()
    {
        _frozen = false;
        _groundingPreventionTimer = 0f;   // allow immediate grounding again
    }

    // Move the player to a new position and reset velocity.
    // Overload for Vector3 and Transform for convenience.
    public void TeleportTo(Vector3 worldPosition, bool recheckGroundNow = true)
    {
        transform.position = worldPosition;
        Velocity = Vector2.zero;
        IsGrounded = false;               // will be recomputed
        if (recheckGroundNow)             // optional immediate ground check
            DoGroundCheck();
    }

    public void TeleportTo(Transform target, bool recheckGroundNow = true)
    {
        if (target != null)
            TeleportTo(target.position, recheckGroundNow);
    }
}