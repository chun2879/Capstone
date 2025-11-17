using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class RecordedFrame
{
    public float time;

    public Vector3 ikPosition;
    public List<Quaternion> ghostRotations = new List<Quaternion>();

    public Vector3 cubePosition;
    public Quaternion cubeRotation;

    public bool cubeHeld;

    // 의사 토크 스냅샷
    public List<float> pseudoTorques = new List<float>();

    // 엔드이펙터 속도 / 가속도 스냅샷
    public Vector3 recordedVelocity;
    public Vector3 recordedAcceleration;
}

public class StateRecorder : MonoBehaviour
{
    private List<RecordedFrame> frames = new List<RecordedFrame>();

    public bool HasData => frames.Count > 0;
    public float MaxTime => frames.Count > 0 ? frames[frames.Count - 1].time : 0f;

    public void Clear()
    {
        frames.Clear();
    }

    public void RecordFrame(
        float time,
        Transform ikTarget,
        List<Transform> ghostJoints,
        Transform cube,
        bool cubeHeld,
        PseudoTorqueHeatmap heatmap,
        Vector3 smoothedVelocity,       
        Vector3 smoothedAcceleration     
    )
    {
        RecordedFrame f = new RecordedFrame();
        f.time = time;

        // IK
        f.ikPosition = ikTarget.position;

        // 관절 회전
        foreach (var j in ghostJoints)
            f.ghostRotations.Add(j.localRotation);

        // 큐브
        if (cube != null)
        {
            f.cubePosition = cube.position;
            f.cubeRotation = cube.rotation;
        }

        f.cubeHeld = cubeHeld;

        // 히트맵 토크 기록
        if (heatmap != null && heatmap.joints != null)
        {
            for (int i = 0; i < heatmap.joints.Count; i++)
                f.pseudoTorques.Add(heatmap.GetSmoothedTorque(i));
        }

        // 엔드이펙터 속도/가속도 기록
        f.recordedVelocity = smoothedVelocity;
        f.recordedAcceleration = smoothedAcceleration;

        frames.Add(f);
    }

    /// t보다 같거나 작은 프레임 중 가장 마지막 것 (이전 프레임) 반환
    public RecordedFrame GetFrameAtTime(float t)
    {
        if (frames.Count == 0) return null;

        int left = 0;
        int right = frames.Count - 1;

        while (right - left > 1)
        {
            int mid = (left + right) / 2;
            if (frames[mid].time < t)
                left = mid;
            else
                right = mid;
        }

        return frames[left];
    }
}
