using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MainCamera : MonoBehaviour
{
    public Transform player_1;
    public float smoothSpeed = 0.125f;
    public Vector3 offset  ;
    public float minX;

    public float maxX;
    public float minY;
    public float maxY;




    // Update is called once per frame
    void LateUpdate()
    {
        Vector3 desiredPosition = player_1.position + offset;
        desiredPosition.x  = Mathf.Clamp(desiredPosition.y, minX, maxX);
        desiredPosition.y = Mathf.Clamp(desiredPosition.x, minY, maxY);
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
        transform.position = smoothedPosition;


    }
}
