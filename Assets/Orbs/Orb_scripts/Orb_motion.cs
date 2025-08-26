using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Orb_motion : MonoBehaviour
{
    private float X;
    private float Y;

    private float time_offset;
    private float newX;
    private float newY;

    private float speed_offset;
    // Start is called before the first frame update
    void Start() {
        speed_offset = Random.Range(0.5f, 0.8f);
        time_offset = Random.Range(1, 360);
        X = transform.position.x;
        Y = transform.position.y;
    }

    // Update is called once per frame
    void Update() {
        newX = X + Mathf.Cos((2 * (Time.time + time_offset)) * speed_offset) / 20;
        newY = Y + Mathf.Sin((3 * (Time.time + time_offset)) * speed_offset) / 16;

        transform.position = new Vector3(newX, newY, transform.position.z);
    }
}
