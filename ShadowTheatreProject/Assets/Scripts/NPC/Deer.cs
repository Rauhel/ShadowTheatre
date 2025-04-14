using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Deer : MonoBehaviour
{
    public Vector3 pointA;
    public Vector3 pointB;
    public float normalSpeed = 2f;
    public float scaredSpeed = 4f;
    public float stayDuration = 3f;

    private float currentSpeed;
    private float stayTimer = 0f;
    private bool isStaying = false;
    private bool isScared = false;
    private float journeyLength;
    private float startTime;
    // Start is called before the first frame update
    void Start()
    {
        transform.position = pointA;
        currentSpeed = normalSpeed;
        startTime = Time.time;
        journeyLength = Vector3.Distance(pointA, pointB);

    }

    // Update is called once per frame
    void Update()
    {
        float currentTime = Time.time;

        if (currentTime > 30f) return; // 30ÃëºóÍ£Ö¹ÒÆ¶¯

        if (isStaying)
        {
            stayTimer += Time.deltaTime;
            if (stayTimer >= stayDuration)
            {
                isStaying = false;
                stayTimer = 0f;
            }
            return;
        }

        float distCovered = (Time.time - startTime) * currentSpeed;
        float fractionOfJourney = distCovered / journeyLength;
        transform.position = Vector3.Lerp(pointA, pointB, fractionOfJourney);
    }

    // ¼ì²âµ½Íæ¼Òshadow typeÎªÀÇ
    public void OnDetectWolfShadow()
    {
        isStaying = true;
        stayTimer = 0f;
    }

    // ¼ì²âµ½ÀÇNPC
    public void OnDetectWolfNPC()
    {
        isScared = true;
        currentSpeed = scaredSpeed;
    }

    public bool IsDeerStaying()
    {
        return isStaying;
    }

    public Vector3 GetCurrentPosition()
    {
        return transform.position;
    }

}

