using System.Collections.Generic;
using UnityEngine;

public class PseudoTorqueHeatmap : MonoBehaviour
{
    [Header("Articulation Root (ex: base)")]
    public Transform root; // base 오브젝트 지정

    [Header("Visualization Settings")]
    public Gradient colorGradient; // 색상 변화용
    public float maxPseudoTorque = 100f; // 최대 회전 속도 대비
    public bool includeGrippers = false; // 그리퍼 포함 여부

    private List<ArticulationBody> joints = new List<ArticulationBody>();
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
        for (int i = 0; i < joints.Count; i++)
        {
            var joint = joints[i];
            var rend = renderers[i];
            if (joint == null || rend == null) continue;

            // 이전 프레임 대비 회전 변화량 계산 (deg/sec)
            float deltaAngle = Quaternion.Angle(prevRotations[i], joint.transform.localRotation);
            float pseudoTorque = deltaAngle / Mathf.Max(Time.deltaTime, 1e-4f);

            // 부드럽게 보간
            smoothedTorques[i] = Mathf.Lerp(smoothedTorques[i], pseudoTorque, Time.deltaTime * 5f);

            // 정규화 및 색상 결정
            float normalized = Mathf.Clamp01(smoothedTorques[i] / maxPseudoTorque);
            Color c = colorGradient.Evaluate(normalized);

            // 색상 적용
            rend.material.color = c;
            rend.material.SetColor("_EmissionColor", c * 0.5f);

            // 현재 회전 저장
            prevRotations[i] = joint.transform.localRotation;
        }
    }
}
