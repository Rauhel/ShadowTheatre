using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class PathDecision
{
    public string decisionPointID;
    public Transform decisionLocation;
    public float decisionRadius = 2f;
    public List<PathOption> pathOptions = new List<PathOption>();
}

[Serializable]
public class PathOption
{
    public Transform path;
    public float scoreThreshold;
}