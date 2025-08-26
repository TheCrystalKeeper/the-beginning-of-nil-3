using UnityEngine;

public class GrassAnimation : MonoBehaviour
{
    [Header("Sway Settings")]
    [Tooltip("How far the grass bends left and right (in degrees).")]
    public float swayAmount = 15f;
    [Tooltip("How fast the grass sways back and forth.")]
    public float swaySpeed = 1f;
    [Tooltip("Random time offset so multiple tufts don't sway in sync.")]
    public float randomPhaseOffset = 0f;

    private float _baseRotation;

    void Start()
    {
        _baseRotation = transform.eulerAngles.z;

        if (Mathf.Approximately(randomPhaseOffset, 0f))
            randomPhaseOffset = Random.Range(0f, Mathf.PI * 2f);
    }

    void Update()
    {
        float angle = _baseRotation + Mathf.Cos(Time.time * swaySpeed + randomPhaseOffset) * swayAmount;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }
}
