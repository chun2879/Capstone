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
    public float velocityScale = 0.5f;      // 속도 벡터 길이 스케일
    public float accelerationScale = 0.2f;  // 가속도 벡터 길이 스케일
    public float fadeSpeed = 5f;            // 잔상 감쇠 속도 (실시간 모드에서만)

    private LineRenderer velocityLine;
    private LineRenderer accelerationLine;

    private Vector3 prevPos;
    private Vector3 prevVelocity;

    private Vector3 smoothedVelocity;
    private Vector3 smoothedAcceleration;

    private PseudoTorqueHeatmap torqueHeatmap;

    // Recorder가 읽게 할 공개 프로퍼티
    public Vector3 SmoothedVelocity => smoothedVelocity;
    public Vector3 SmoothedAcceleration => smoothedAcceleration;

    // REPLAY 모드 여부
    public bool IsReplaying = false;

    void Start()
    {
        if (root == null || endEffector == null)
        {
            Debug.LogError("[RobotMotionVisualizer] root와 endEffector를 지정하세요.");
            return;
        }

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
        // REPLAY 모드: 기록된 벡터만 그리기
        if (IsReplaying)
        {
            DrawVector(velocityLine, endEffector.position, smoothedVelocity * velocityScale);
            DrawVector(accelerationLine, endEffector.position, smoothedAcceleration * accelerationScale);
            return; // 실시간 계산/페이딩 금지
        }

        // 실시간 시뮬레이션 모드
        Vector3 currentPos = endEffector.position;

        // 위치 → 속도
        Vector3 velocity = (currentPos - prevPos) / Mathf.Max(Time.deltaTime, 1e-4f);
        // 속도 → 가속도
        Vector3 acceleration = (velocity - prevVelocity) / Mathf.Max(Time.deltaTime, 1e-4f);

        // 부드럽게 보간 (게임 중)
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

    // 슬라이더 되감기 시 기록된 벡터 적용
    public void ApplyRecordedVectors(Vector3 vel, Vector3 acc)
    {
        IsReplaying = true;

        smoothedVelocity = vel;
        smoothedAcceleration = acc;

        DrawVector(velocityLine, endEffector.position, smoothedVelocity * velocityScale);
        DrawVector(accelerationLine, endEffector.position, smoothedAcceleration * accelerationScale);
    }

    // 새 시뮬레이션 시작/리셋 시 호출
    public void ExitReplay()
    {
        IsReplaying = false;
    }
}