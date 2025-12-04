using UnityEngine;

// 이 컴포넌트는 물리 관절의 제약 정보를 저장하는 역할만 합니다.
public class GhostJointInfo : MonoBehaviour
{
    public Quaternion anchorRotation; // ArticulationBody의 Anchor Rotation 값을 저장할 변수
    public Vector3 axis;              // 로컬 회전 축 (Revolute Joint는 항상 X축)
    public float lowerLimit;          // 최소 회전 각도 (degrees)
    public float upperLimit;          // 최대 회전 각도 (degrees)
}

