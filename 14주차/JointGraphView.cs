using System.Collections.Generic;
using UnityEngine;

public class JointGraphView : MonoBehaviour
{
    public RectTransform graphArea;
    public UILineRenderer torqueLine;
    public UILineRenderer velocityLine;
    public RectTransform highlightMarker;

    private List<float> times;
    private List<float> torques;
    private List<float> velocities;

    private float minTime, maxTime;
    private float minValue, maxValue;

    // 그래프 데이터 세팅
    public void SetData(List<float> times,
                        List<float> torques,
                        List<float> velocities)
    {
        this.times = times;
        this.torques = torques;
        this.velocities = velocities;

        if (times == null || times.Count == 0)
        {
            if (torqueLine != null) torqueLine.SetPoints(null);
            if (velocityLine != null) velocityLine.SetPoints(null);
            return;
        }

        minTime = times[0];
        maxTime = times[times.Count - 1];

        minValue = float.MaxValue;
        maxValue = float.MinValue;

        foreach (var v in torques)
        {
            if (v < minValue) minValue = v;
            if (v > maxValue) maxValue = v;
        }

        foreach (var v in velocities)
        {
            if (v < minValue) minValue = v;
            if (v > maxValue) maxValue = v;
        }

        if (Mathf.Approximately(maxValue, minValue))
        {
            maxValue += 1f;
            minValue -= 1f;
        }

        if (torqueLine != null) DrawLine(torqueLine, this.torques);
        if (velocityLine != null) DrawLine(velocityLine, this.velocities);
    }

    // UILineRenderer로 라인 그리기
    private void DrawLine(UILineRenderer line, List<float> values)
    {
        int count = times.Count;
        if (count == 0)
        {
            line.SetPoints(null);
            return;
        }

        Vector2 size = graphArea.rect.size;
        float halfW = size.x * 0.5f;
        float halfH = size.y * 0.5f;

        List<Vector2> pts = new List<Vector2>(count);

        for (int i = 0; i < count; i++)
        {
            float tx = Mathf.InverseLerp(minTime, maxTime, times[i]);     // 0~1
            float ty = Mathf.InverseLerp(minValue, maxValue, values[i]);  // 0~1

            float x = Mathf.Lerp(-halfW, halfW, tx);
            float y = Mathf.Lerp(-halfH, halfH, ty);

            pts.Add(new Vector2(x, y));
        }

        line.SetPoints(pts);
    }

    // 슬라이더/시간에 맞춰 하이라이트 점 이동
    public void SetHighlightTime(float time)
    {
        if (times == null || times.Count == 0 || torques == null || torques.Count == 0)
            return;

        float t01 = Mathf.InverseLerp(minTime, maxTime, time);
        t01 = Mathf.Clamp01(t01);

        float targetTime = Mathf.Lerp(minTime, maxTime, t01);

        int idx = 0;
        float bestDiff = Mathf.Abs(times[0] - targetTime);
        for (int i = 1; i < times.Count; i++)
        {
            float d = Mathf.Abs(times[i] - targetTime);
            if (d < bestDiff)
            {
                bestDiff = d;
                idx = i;
            }
        }

        float tx = Mathf.InverseLerp(minTime, maxTime, times[idx]);
        float ty = Mathf.InverseLerp(minValue, maxValue, torques[idx]);

        Vector2 size = graphArea.rect.size;
        float halfW = size.x * 0.5f;
        float halfH = size.y * 0.5f;

        float x = Mathf.Lerp(-halfW, halfW, tx);
        float y = Mathf.Lerp(-halfH, halfH, ty);

        if (highlightMarker != null)
        {
            highlightMarker.anchoredPosition = new Vector2(x, y);
        }
    }
}
