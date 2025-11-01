using UnityEngine;

public class GhostSetupUtility : MonoBehaviour
{
    public Transform physicalRoot;
    public Transform ghostRoot;

    void Awake()
    {
        if (physicalRoot == null || ghostRoot == null)
        {
            Debug.LogError("Physical Root 또는 Ghost Root가 할당되지 않았습니다.");
            return;
        }

        CopySettingsRecursive(physicalRoot, ghostRoot);
        Debug.Log("✅ 고스트 리그에 물리 관절 정보(Anchor Rotation 포함) 복제를 완료했습니다.");
    }

    private void CopySettingsRecursive(Transform physical, Transform ghost)
    {
        var body = physical.GetComponent<ArticulationBody>();
        if (body != null && body.jointType == ArticulationJointType.RevoluteJoint)
        {
            var info = ghost.gameObject.GetComponent<GhostJointInfo>();
            if (info == null)
            {
                info = ghost.gameObject.AddComponent<GhostJointInfo>();
            }

            // ArticulationBody의 Revolute Joint는 항상 자신의 로컬 X축(Vector3.right)을 중심으로 회전
            info.axis = Vector3.right;

            // Anchor Rotation 값을 복사
            info.anchorRotation = body.anchorRotation;

            // 관절 한계(Limit) 값을 복사
            info.lowerLimit = body.xDrive.lowerLimit;
            info.upperLimit = body.xDrive.upperLimit;
        }

        // 모든 자식 관절에 대해 재귀적으로 반복
        for (int i = 0; i < physical.childCount; i++)
        {
            Transform physicalChild = physical.GetChild(i);
            Transform ghostChild = ghost.Find(physicalChild.name);

            if (ghostChild != null)
            {
                CopySettingsRecursive(physicalChild, ghostChild);
            }
        }
    }
}
