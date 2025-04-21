using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class NPCEvent
{
    public string eventID;
    public Transform triggerLocation;
    public float triggerRadius = 2f;
    public string globalEventName;
    public float gestureHoldTime = 2.0f;
    public float gestureTimeLimit = 5.0f;

    // 事件分支
    public EventBranch defaultBranch = new EventBranch();
    public List<GestureBranch> gestureBranches = new List<GestureBranch>();
}

[Serializable]
public class EventBranch
{
    public float scoreValue;
    // 其他分支属性
}

[Serializable]
public class GestureBranch
{
    public string gestureType;
    public float scoreValue;
    // 其他手势分支属性
}