using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(CanvasRenderer))]
public class UILineRenderer : MaskableGraphic
{
    [Range(1f, 20f)]
    public float thickness = 2f;

    // 로컬 좌표계(-rect.width/2 ~ +rect.width/2) 기준 포인트들
    public List<Vector2> points = new List<Vector2>();

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        if (points == null || points.Count < 2)
            return;

        float halfThickness = thickness * 0.5f;

        for (int i = 0; i < points.Count - 1; i++)
        {
            Vector2 p0 = points[i];
            Vector2 p1 = points[i + 1];

            Vector2 dir = (p1 - p0).normalized;
            Vector2 normal = new Vector2(-dir.y, dir.x); // 좌측 수직벡터

            Vector2 v0 = p0 + normal * halfThickness;
            Vector2 v1 = p0 - normal * halfThickness;
            Vector2 v2 = p1 - normal * halfThickness;
            Vector2 v3 = p1 + normal * halfThickness;

            int idx = vh.currentVertCount;

            vh.AddVert(v0, color, Vector2.zero);
            vh.AddVert(v1, color, Vector2.zero);
            vh.AddVert(v2, color, Vector2.zero);
            vh.AddVert(v3, color, Vector2.zero);

            vh.AddTriangle(idx, idx + 1, idx + 2);
            vh.AddTriangle(idx, idx + 2, idx + 3);
        }
    }

    public void SetPoints(List<Vector2> newPoints)
    {
        points.Clear();
        if (newPoints != null)
            points.AddRange(newPoints);

        SetVerticesDirty(); // 다시 그리기
    }
}
