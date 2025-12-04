using System.Collections.Generic;
using UnityEngine;

public class PseudoTorqueHeatmap : MonoBehaviour
{
    [Header("Articulation Root (ex: base)")]
    public Transform root; // base 오브젝트 지정

    [Header("Visualization Settings")]
    public Gradient colorGradient; // 의사 토크 색상 변화용
    public float maxPseudoTorque = 100f;
    public bool includeGrippers = false;

    [Header("Controller (for replay check)")]
    public PickAndPlaceController controller;

    // 부하(큐브) 질량에 따른 의사토크 스케일링
    [Header("Load for Pseudo Torque Scaling")]
    public Rigidbody loadRigidbody;
    public bool isHoldingLoad = false;

    // 튜닝용 파라미터
    [Tooltip("기준 하중 (이 값일 때 토크 스케일 = 1)")]
    public float referenceMass = 10f;      // 아래에서 설명
    [Tooltip("질량 비율에 대한 민감도 (0.5~0.8 추천)")]
    public float massInfluence = 0.6f;
    [Tooltip("스케일 상한 (너무 과하게 튀는 것 방지)")]
    public float maxLoadFactor = 3.5f;

    // UI/Recorder에서 접근해야 하므로 public 읽기 전용
    public List<ArticulationBody> joints { get; private set; } = new List<ArticulationBody>();
    private List<Renderer> renderers = new List<Renderer>();

    private Quaternion[] prevRotations;
    private float[] smoothedTorques;

    void Start()
    {
        if (root == null)
        {
            Debug.LogError("[PseudoTorqueHeatmap] Root Transform을 지정하세요.");
            return;
        }

        joints.AddRange(root.GetComponentsInChildren<ArticulationBody>());
        if (!includeGrippers)
            joints.RemoveAll(j => j.name.ToLower().Contains("gripper"));

        foreach (var j in joints)
        {
            var rend = j.GetComponentInChildren<Renderer>();
            renderers.Add(rend);
        }

        prevRotations = new Quaternion[joints.Count];
        smoothedTorques = new float[joints.Count];

        for (int i = 0; i < joints.Count; i++)
            prevRotations[i] = joints[i].transform.localRotation;

        Debug.Log($"[PseudoTorqueHeatmap] {joints.Count} joints initialized.");
    }

    void Update()
    {
        // REPLAY(슬라이더 되감기) 중에는 실시간 토크 재계산 금지
        if (controller != null && controller.IsReplaying)
        {
            for (int i = 0; i < joints.Count; i++)
            {
                if (joints[i] != null)
                    prevRotations[i] = joints[i].transform.localRotation;
            }
            return;
        }

        // 부하 스케일 미리 계산
        float loadFactor = 1f;
        if (isHoldingLoad && loadRigidbody != null && referenceMass > 0f)
        {
            float ratio = loadRigidbody.mass / referenceMass;
            if (ratio < 1f) ratio = 1f;  // 기준보다 가벼운 부하는 1로
            loadFactor = Mathf.Pow(ratio, massInfluence);
            loadFactor = Mathf.Min(loadFactor, maxLoadFactor); // 상한 제한
        }

        for (int i = 0; i < joints.Count; i++)
        {
            var joint = joints[i];
            var rend = renderers[i];
            if (joint == null || rend == null) continue;

            // 기본 의사토크
            float deltaAngle = Quaternion.Angle(prevRotations[i], joint.transform.localRotation);
            float pseudoTorque = deltaAngle / Mathf.Max(Time.deltaTime, 1e-4f);

            // 부하 스케일 적용 — 에러났던 부분은 여기!
            pseudoTorque *= loadFactor;

            // smoothing
            smoothedTorques[i] = Mathf.Lerp(smoothedTorques[i], pseudoTorque, Time.deltaTime * 5f);

            // 색상 적용
            float normalized = Mathf.Clamp01(smoothedTorques[i] / maxPseudoTorque);
            Color c = colorGradient.Evaluate(normalized);

            rend.material.color = c;
            rend.material.SetColor("_EmissionColor", c * 0.5f);

            prevRotations[i] = joint.transform.localRotation;
        }
    }


    // 특정 jointIndex의 스무딩된 의사 토크 반환
    public float GetSmoothedTorque(int index)
    {
        if (index < 0 || index >= smoothedTorques.Length)
            return 0f;

        return smoothedTorques[index];
    }

    // 특정 ArticulationBody가 joints 리스트에서 몇 번째인지 반환
    public int GetJointIndex(ArticulationBody joint)
    {
        return joints.IndexOf(joint);
    }

    // ===== 슬라이더 scrubbing 시 기록된 토크 스냅샷을 그대로 적용 =====
    public void ApplyTorqueSnapshot(List<float> torqueValues)
    {
        if (torqueValues == null) return;
        if (torqueValues.Count != joints.Count) return;

        for (int i = 0; i < joints.Count; i++)
        {
            var rend = renderers[i];
            if (rend == null) continue;

            float normalized = Mathf.Clamp01(torqueValues[i] / maxPseudoTorque);
            Color c = colorGradient.Evaluate(normalized);

            rend.material.color = c;
            rend.material.SetColor("_EmissionColor", c * 0.5f);

            // 스냅샷 기준으로 smoothedTorques도 동기화
            smoothedTorques[i] = torqueValues[i];
        }
    }
}
