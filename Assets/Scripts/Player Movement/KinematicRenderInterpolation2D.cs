using UnityEngine;

// ========================================================================
// Script to move the child character (Actual rendered player)
// towards the calculated position at the proper time.
// ========================================================================

public class KinematicRenderInterpolation2D : MonoBehaviour
{
    public Transform renderTransform;
    Vector3 _prevPos, _currPos;
    float _prevRotZ, _currRotZ;
    float _accum; // time since last fixed step

    void OnEnable()
    {
        _prevPos = _currPos = transform.position;
        _prevRotZ = _currRotZ = transform.eulerAngles.z;
        _accum = 0f;
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
        _accum += Time.deltaTime;
        float t = Mathf.Clamp01(Time.fixedDeltaTime > 0f ? _accum / Time.fixedDeltaTime : 1f);
        if (renderTransform)
        {
            renderTransform.position = Vector3.Lerp(_prevPos, _currPos, t);
            float z = Mathf.LerpAngle(_prevRotZ, _currRotZ, t);
            renderTransform.rotation = Quaternion.Euler(0, 0, z);
        }
    }
}