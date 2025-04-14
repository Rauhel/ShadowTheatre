using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bird : MonoBehaviour
{
    public Deer deer;
    public Vector3 pointC;
    public float followSpeed = 3f;
    public float moveSpeed = 3f;

    private bool isFollowingDeer = true;
    private bool isFollowingPlayer = false;
    private float startTime;
    private Vector3 startMovePosition;
    private float journeyLength;

    // Start is called before the first frame update
    void Start()
    {
        startTime = Time.time;

    }

    // Update is called once per frame
    void Update()
    {
        float currentTime = Time.time;

        if (currentTime > 30f) return; // 30���ֹͣ�ƶ�

        if (currentTime < 15f)
        {
            // 0-15�����¹
            if (isFollowingDeer && !isFollowingPlayer)
            {
                if (deer.IsDeerStaying())
                {
                    // ¹ͣ�£���Ҳͣ��
                    return;
                }
                else
                {
                    // ����¹�ƶ�
                    transform.position = Vector3.MoveTowards(transform.position, deer.GetCurrentPosition(), followSpeed * Time.deltaTime);
                }
            }
        }
        else
        {
            // 15-30���ƶ�����C
            if (!isFollowingPlayer)
            {
                if (journeyLength == 0f)
                {
                    startMovePosition = transform.position;
                    journeyLength = Vector3.Distance(startMovePosition, pointC);
                }

                float distCovered = (Time.time - (startTime + 15f)) * moveSpeed;
                float fractionOfJourney = distCovered / journeyLength;
                transform.position = Vector3.Lerp(startMovePosition, pointC, fractionOfJourney);
            }
        }
    }
    public void OnDetectDeerShadow()
    {
        isFollowingPlayer = true;
        isFollowingDeer = false;
    }
}
