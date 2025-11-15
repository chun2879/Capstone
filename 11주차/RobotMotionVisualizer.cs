using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PseudoTorqueHeatmap))]
public class RobotMotionVisualizer : MonoBehaviour
{
    [Header("Articulation Root (ex: base)")]
    public Transform root; // base 오브젝트
    [Header("End Effector Reference")]
    public Transform endEffector; // 로봇 팔 끝단

    [Header("Line Visualization Settings")]
    public float velocityScale = 0.5f;   // 속도 벡터 길이 스케일
    public float accelerationScale = 0.2f; // 가속도 벡터 길이 스케일
    public float fadeSpeed = 5f;          // 잔상 감쇠 속도

    private LineRenderer velocityLine;
    private LineRenderer accelerationLine;

    private Vector3 prevPos;
    private Vector3 prevVelocity;
    private Vector3 smoothedVelocity;
    private Vector3 smoothedAcceleration;

    private PseudoTorqueHeatmap torqueHeatmap;

    void Start()
    {
        if (root == null || endEffector == null)
        {
            Debug.LogError("[RobotMotionVisualizer] root와 endEffector를 지정하세요.");
            return;
        }

        // PseudoTorqueHeatmap 참조 (색상 연동용)
        torqueHeatmap = GetComponent<PseudoTorqueHeatmap>();

        // 속도 벡터 라인
        GameObject velObj = new GameObject("VelocityVector");
        velObj.transform.SetParent(transform);
        velocityLine = velObj.AddComponent<LineRenderer>();
        SetupLineRenderer(velocityLine, Color.cyan);

        // 가속도 벡터 라인
        GameObject accObj = new GameObject("AccelerationVector");
        accObj.transform.SetParent(transform);
        accelerationLine = accObj.AddComponent<LineRenderer>();
        SetupLineRenderer(accelerationLine, Color.magenta);

        prevPos = endEffector.position;
    }

    void Update()
    {
        // 위치 → 속도 계산
        Vector3 currentPos = endEffector.position;
        Vector3 velocity = (currentPos - prevPos) / Mathf.Max(Time.deltaTime, 1e-4f);

        // 가속도 계산
        Vector3 acceleration = (velocity - prevVelocity) / Mathf.Max(Time.deltaTime, 1e-4f);

        // 부드럽게 보간
        smoothedVelocity = Vector3.Lerp(smoothedVelocity, velocity, Time.deltaTime * fadeSpeed);
        smoothedAcceleration = Vector3.Lerp(smoothedAcceleration, acceleration, Time.deltaTime * fadeSpeed);

        // 시각화 벡터 그리기
        DrawVector(velocityLine, endEffector.position, smoothedVelocity * velocityScale);
        DrawVector(accelerationLine, endEffector.position, smoothedAcceleration * accelerationScale);

        prevVelocity = velocity;
        prevPos = currentPos;
    }

    private void SetupLineRenderer(LineRenderer lr, Color color)
    {
        lr.positionCount = 2;
        lr.startWidth = 0.01f;
        lr.endWidth = 0.005f;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = color;
        lr.endColor = color;
        lr.numCapVertices = 2;
    }

    private void DrawVector(LineRenderer lr, Vector3 origin, Vector3 vec)
    {
        lr.SetPosition(0, origin);
        lr.SetPosition(1, origin + vec);
    }
}
